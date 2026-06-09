using System.Globalization;
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
    private Localizer _localizer = Localizer.Chinese;
    private LowBatteryAlerter? _alerter;
    private INotifier? _notifier;
    private DebugLogger _log = DebugLogger.Disabled;

    private ContextMenu? _menu;
    private Separator? _deviceSeparator;
    private MenuItem? _autostartItem;
    private MenuItem? _alertsItem;

    private IDisposable? _currentIcon; // System.Drawing.Icon,持有 GDI 句柄,换图标时需 Dispose

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 调试日志(opt-in)与全局异常兜底最先就位,后续任何环节出问题都有痕迹
        bool logEnabled = e.Args.Contains("--log")
            || Environment.GetEnvironmentVariable("MAGICBATTERY_LOG") == "1";
        _log = logEnabled ? new DebugLogger(DebugLogger.DefaultPath()) : DebugLogger.Disabled;
        SetupExceptionGuards();

        // 单实例:已在运行则退出,避免两个托盘抢同一个设备控制管道
        _singleInstance = new Mutex(initiallyOwned: true, @"Local\MagicBattery.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        _configService = new ConfigService();
        _config = _configService.Load();
        _localizer = Localizer.For(LanguageResolver.Resolve(_config.Language, CultureInfo.CurrentUICulture));
        _log.Write($"启动 v{typeof(App).Assembly.GetName().Version} " +
            $"poll={_config.PollIntervalMinutes}min alerts={_config.LowBatteryAlertsEnabled} lang={_config.Language}");

        _autostart = new AutostartService(new RegistryAutostartStore());
        _alerter = new LowBatteryAlerter(_config.AlertThresholds, () => _config.LowBatteryAlertsEnabled);

        int minutes = Math.Max(1, _config.PollIntervalMinutes);
        _coordinator = new BatteryCoordinator(
            openAll: () => MagicBatteryReaderFactory.CreateAll(),
            interval: TimeSpan.FromMinutes(minutes),
            log: _log.Write);
        _coordinator.SnapshotChanged += OnSnapshot;

        _tray = new TaskbarIcon
        {
            ToolTipText = _localizer.DefaultTooltip,
            ContextMenu = BuildContextMenu(),
        };
        _tray.ForceCreate();
        _notifier = new TrayNotifier(_tray);

        ApplySnapshot(_coordinator.Devices); // 初始置灰图标
        SetupDeviceWatcher();

        // 首轮读取放后台线程,避免阻塞 UI(HidD_GetInputReport 是同步控制管道调用)
        _ = Task.Run(() => _coordinator.StartAsync());
    }

    /// <summary>
    /// 全局异常兜底:常驻托盘工具一次未捕获异常就会让进程无声消失,用户只看到「图标没了」。
    /// 这里统一记日志并吞掉,保住进程;开了 --log 时事后可查。
    /// </summary>
    private void SetupExceptionGuards()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            _log.Write("Dispatcher 未处理异常", args.Exception);
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _log.Write("Task 未观察异常", args.Exception);
            args.SetObserved();
        };
        // 非 UI 线程抛出时进程必死(无法标记 Handled),至少留下最后一条日志
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            _log.Write($"AppDomain 未处理异常(进程即将退出): {args.ExceptionObject}");
    }

    // 配置写盘失败(磁盘满、AppData 权限)只记日志,不让点击菜单崩掉进程
    private void SaveConfig()
    {
        try
        {
            _configService?.Save(_config);
        }
        catch (Exception ex)
        {
            _log.Write("保存配置失败", ex);
        }
    }

    private ContextMenu BuildContextMenu()
    {
        _deviceSeparator = new Separator();

        _alertsItem = new MenuItem
        {
            Header = _localizer.MenuAlerts,
            IsCheckable = true,
            IsChecked = _config.LowBatteryAlertsEnabled,
        };
        _alertsItem.Click += (_, _) =>
        {
            _config = _config with { LowBatteryAlertsEnabled = _alertsItem!.IsChecked };
            SaveConfig();
        };

        var refreshItem = new MenuItem { Header = _localizer.MenuRefresh };
        refreshItem.Click += (_, _) => _coordinator?.RequestRefresh();

        MenuItem languageItem = BuildLanguageMenu();

        _autostartItem = new MenuItem
        {
            Header = _localizer.MenuAutostart,
            IsCheckable = true,
            IsChecked = _autostart?.IsEnabled ?? false,
        };
        // IsCheckable 的 MenuItem 点击后框架已翻转 IsChecked,这里把注册表同步到新状态;
        // 注册表写失败时回滚勾选,保持菜单与实际状态一致
        _autostartItem.Click += (_, _) =>
        {
            bool wanted = _autostartItem!.IsChecked;
            try
            {
                if (wanted)
                {
                    _autostart?.Enable();
                }
                else
                {
                    _autostart?.Disable();
                }
            }
            catch (Exception ex)
            {
                _log.Write("开机自启切换失败", ex);
                _autostartItem.IsChecked = !wanted;
            }
        };

        var exitItem = new MenuItem { Header = _localizer.MenuExit };
        exitItem.Click += (_, _) => Shutdown();

        _menu = new ContextMenu();
        // 顶部动态设备区由 RebuildDeviceRows 填充(插在 _deviceSeparator 之前)
        _menu.Items.Add(_deviceSeparator);
        _menu.Items.Add(_alertsItem);
        _menu.Items.Add(refreshItem);
        _menu.Items.Add(_autostartItem);
        _menu.Items.Add(languageItem);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(exitItem);
        return _menu;
    }

    // 语言子菜单:跟随系统 / 中文 / English(勾当前设置)。语言名用本族文字恒定显示。
    private MenuItem BuildLanguageMenu()
    {
        var parent = new MenuItem { Header = _localizer.MenuLanguage };

        (LanguagePreference Pref, string Label)[] options =
        {
            (LanguagePreference.System, _localizer.MenuFollowSystem),
            (LanguagePreference.Chinese, "中文"),
            (LanguagePreference.English, "English"),
        };

        foreach ((LanguagePreference pref, string label) in options)
        {
            var item = new MenuItem
            {
                Header = label,
                IsCheckable = true,
                IsChecked = _config.Language == pref,
            };
            LanguagePreference captured = pref;
            item.Click += (_, _) => SetLanguage(captured);
            parent.Items.Add(item);
        }

        return parent;
    }

    // 切换语言:存盘 → 重解析 Localizer → 重建菜单 + 刷新 tooltip/图标/设备行(即时生效)
    private void SetLanguage(LanguagePreference preference)
    {
        _config = _config with { Language = preference };
        SaveConfig();
        _localizer = Localizer.For(LanguageResolver.Resolve(preference, CultureInfo.CurrentUICulture));

        if (_tray is not null)
        {
            _tray.ContextMenu = BuildContextMenu();
            ApplySnapshot(_coordinator?.Devices ?? Array.Empty<DeviceBattery>());
        }
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

        _tray.ToolTipText = _localizer.Tooltip(devices);
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
            _menu.Items.Insert(0, new MenuItem { Header = _localizer.MenuNoDevice, IsEnabled = false });
            return;
        }

        int at = 0;
        foreach (DeviceBattery d in devices)
        {
            _menu.Items.Insert(at++, new MenuItem { Header = _localizer.Device(d), IsEnabled = false });
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
                _notifier.Notify(_localizer.AlertTitle,
                    _localizer.AlertBody(decision.Kind, decision.Level, decision.Percentage));
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
            _log.Write("设备变更(WM_DEVICECHANGE),触发刷新");
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
        _log.Write("退出");
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
