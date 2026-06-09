using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using MagicBattery.Hid;
using MagicBattery.Tray.Core;

namespace MagicBattery.Tray;

/// <summary>
/// 托盘程序入口。无主窗口(<c>ShutdownMode=OnExplicitShutdown</c>),
/// 维持一个托盘图标 + 后台 <see cref="BatteryCoordinator"/>(多设备)+ 低电量告警。
/// </summary>
public partial class App : Application
{
    private const int WM_DEVICECHANGE = 0x0219;
    private const int DBT_DEVNODES_CHANGED = 0x0007;

    private Mutex? _singleInstance;
    private TaskbarIcon? _tray;
    private BatteryCoordinator? _coordinator;
    private AutostartService? _autostart;
    private ConfigService? _configService;
    private AppConfig _config = AppConfig.Default;
    private LowBatteryAlerter? _alerter;
    private INotifier? _notifier;

    private ContextMenu? _menu;
    private Separator? _deviceSeparator;
    private MenuItem? _autostartItem;
    private MenuItem? _alertsItem;

    private IDisposable? _currentIcon; // System.Drawing.Icon,持有 GDI 句柄,换图标时需 Dispose

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 单实例:已在运行则退出,避免两个托盘抢同一个设备控制管道
        _singleInstance = new Mutex(initiallyOwned: true, @"Local\MagicBattery.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        _configService = new ConfigService();
        _config = _configService.Load();

        _autostart = new AutostartService(new RegistryAutostartStore());
        _alerter = new LowBatteryAlerter(_config.AlertThresholds, () => _config.LowBatteryAlertsEnabled);

        int minutes = Math.Max(1, _config.PollIntervalMinutes);
        _coordinator = new BatteryCoordinator(
            openAll: () => MagicBatteryReaderFactory.CreateAll(),
            interval: TimeSpan.FromMinutes(minutes));
        _coordinator.SnapshotChanged += OnSnapshot;

        _tray = new TaskbarIcon
        {
            ToolTipText = "Magic 设备",
            ContextMenu = BuildContextMenu(),
        };
        _tray.ForceCreate();
        _notifier = new TrayNotifier(_tray);

        ApplySnapshot(_coordinator.Devices); // 初始置灰图标
        SetupDeviceWatcher();

        // 首轮读取放后台线程,避免阻塞 UI(HidD_GetInputReport 是同步控制管道调用)
        _ = Task.Run(() => _coordinator.StartAsync());
    }

    private ContextMenu BuildContextMenu()
    {
        _deviceSeparator = new Separator();

        _alertsItem = new MenuItem
        {
            Header = "低电量告警",
            IsCheckable = true,
            IsChecked = _config.LowBatteryAlertsEnabled,
        };
        _alertsItem.Click += (_, _) =>
        {
            _config = _config with { LowBatteryAlertsEnabled = _alertsItem!.IsChecked };
            _configService?.Save(_config);
        };

        var refreshItem = new MenuItem { Header = "立即刷新" };
        refreshItem.Click += (_, _) => _coordinator?.RequestRefresh();

        _autostartItem = new MenuItem
        {
            Header = "开机自启",
            IsCheckable = true,
            IsChecked = _autostart?.IsEnabled ?? false,
        };
        // IsCheckable 的 MenuItem 点击后框架已翻转 IsChecked,这里把注册表同步到新状态
        _autostartItem.Click += (_, _) =>
        {
            if (_autostartItem!.IsChecked)
            {
                _autostart?.Enable();
            }
            else
            {
                _autostart?.Disable();
            }
        };

        var exitItem = new MenuItem { Header = "退出" };
        exitItem.Click += (_, _) => Shutdown();

        _menu = new ContextMenu();
        // 顶部动态设备区由 RebuildDeviceRows 填充(插在 _deviceSeparator 之前)
        _menu.Items.Add(_deviceSeparator);
        _menu.Items.Add(_alertsItem);
        _menu.Items.Add(refreshItem);
        _menu.Items.Add(_autostartItem);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(exitItem);
        return _menu;
    }

    private void OnSnapshot(IReadOnlyList<DeviceBattery> devices) =>
        Dispatcher.BeginInvoke(() => ApplySnapshot(devices));

    private void ApplySnapshot(IReadOnlyList<DeviceBattery> devices)
    {
        if (_tray is null)
        {
            return;
        }

        // 主图标 = 最低电量设备;不走 IconSource(其转换器对运行期位图抛 NotImplemented)
        DeviceBattery primary = BatterySnapshot.Primary(devices);
        System.Drawing.Icon icon = TrayIconRenderer.Render(TrayIconModel.FromState(primary));
        _tray.Icon = icon;
        _currentIcon?.Dispose();
        _currentIcon = icon;

        _tray.ToolTipText = TooltipFormatter.Tooltip(devices);
        RebuildDeviceRows(devices);
        RunAlerts(devices);
    }

    // 重建菜单顶部的逐设备只读行(插在设备分隔符之前)
    private void RebuildDeviceRows(IReadOnlyList<DeviceBattery> devices)
    {
        if (_menu is null || _deviceSeparator is null)
        {
            return;
        }

        int sepIndex = _menu.Items.IndexOf(_deviceSeparator);
        for (int i = sepIndex - 1; i >= 0; i--)
        {
            _menu.Items.RemoveAt(i);
        }

        if (devices.Count == 0)
        {
            _menu.Items.Insert(0, new MenuItem { Header = "未检测到设备", IsEnabled = false });
            return;
        }

        int at = 0;
        foreach (DeviceBattery d in devices)
        {
            _menu.Items.Insert(at++, new MenuItem { Header = TooltipFormatter.Device(d), IsEnabled = false });
        }
    }

    private void RunAlerts(IReadOnlyList<DeviceBattery> devices)
    {
        if (_alerter is null || _notifier is null)
        {
            return;
        }

        foreach (DeviceBattery d in devices)
        {
            if (d.Availability != BatteryAvailability.Live)
            {
                continue;
            }

            AlertDecision? decision = _alerter.Evaluate(d.DeviceKey, d.Kind, d.Level, d.Percentage, d.IsCharging);
            if (decision is not null)
            {
                string name = DeviceKindNames.Of(decision.Kind);
                string amount = decision.Percentage is int p ? $"{p}%" : $"{BatteryLevelNames.Of(decision.Level)}档";
                _notifier.Notify("电量不足", $"{name}电量 {amount},请及时充电");
            }
        }
    }

    // 监听设备插拔(WM_DEVICECHANGE / DBT_DEVNODES_CHANGED),去抖后触发立即刷新,
    // 让 USB↔蓝牙切换、设备增减在数秒内生效。广播消息只发给顶级窗口,故建一个不可见的顶级窗口接收。
    private void SetupDeviceWatcher()
    {
        var parameters = new HwndSourceParameters("MagicBattery.DeviceWatcher")
        {
            Width = 0,
            Height = 0,
            ParentWindow = IntPtr.Zero,
            WindowStyle = 0, // 无 WS_VISIBLE
        };
        _deviceWatcher = new HwndSource(parameters);
        _deviceWatcher.AddHook(DeviceChangeHook);

        _deviceDebounce = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _deviceDebounce.Tick += (_, _) =>
        {
            _deviceDebounce!.Stop();
            _coordinator?.RequestRefresh();
        };
    }

    private HwndSource? _deviceWatcher;
    private DispatcherTimer? _deviceDebounce;

    private IntPtr DeviceChangeHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_DEVICECHANGE && wParam.ToInt32() == DBT_DEVNODES_CHANGED)
        {
            _deviceDebounce?.Stop();
            _deviceDebounce?.Start();
        }

        return IntPtr.Zero;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _deviceDebounce?.Stop();
        _deviceWatcher?.RemoveHook(DeviceChangeHook);
        _deviceWatcher?.Dispose();
        _coordinator?.Dispose();
        _tray?.Dispose();
        _currentIcon?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    /// <summary>包住 <c>TaskbarIcon.ShowNotification</c> 的通知实现(Shell_NotifyIcon,无新依赖)。</summary>
    private sealed class TrayNotifier : INotifier
    {
        private readonly TaskbarIcon _tray;

        public TrayNotifier(TaskbarIcon tray) => _tray = tray;

        public void Notify(string title, string message) =>
            _tray.ShowNotification(title, message, NotificationIcon.Warning);
    }
}
