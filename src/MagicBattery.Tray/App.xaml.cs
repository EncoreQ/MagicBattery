using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using H.NotifyIcon;
using MagicBattery.Hid;
using MagicBattery.Tray.Core;

namespace MagicBattery.Tray;

/// <summary>
/// 托盘程序入口。无主窗口(<c>ShutdownMode=OnExplicitShutdown</c>),
/// 仅维持一个托盘图标 + 后台 <see cref="BatteryMonitor"/>。
/// </summary>
public partial class App : Application
{
    private const int WM_DEVICECHANGE = 0x0219;
    private const int DBT_DEVNODES_CHANGED = 0x0007;

    private Mutex? _singleInstance;
    private TaskbarIcon? _tray;
    private BatteryMonitor? _monitor;
    private AutostartService? _autostart;
    private MenuItem? _autostartItem;
    private HwndSource? _deviceWatcher;
    private DispatcherTimer? _deviceDebounce;
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

        _autostart = new AutostartService(new RegistryAutostartStore());

        _monitor = new BatteryMonitor(
            readerFactory: () => MagicBatteryReaderFactory.Create(),
            interval: TimeSpan.FromMinutes(15));
        _monitor.StateChanged += OnStateChanged;

        _tray = new TaskbarIcon
        {
            ToolTipText = "Magic Trackpad 2",
            ContextMenu = BuildContextMenu(),
        };
        _tray.ForceCreate();

        ApplyState(_monitor.State); // 初始置灰图标
        SetupDeviceWatcher();

        // 首轮读取放后台线程,避免阻塞 UI(HidD_GetInputReport 是同步控制管道调用)
        _ = Task.Run(() => _monitor.StartAsync());
    }

    private ContextMenu BuildContextMenu()
    {
        var refreshItem = new MenuItem { Header = "立即刷新" };
        refreshItem.Click += (_, _) => _monitor?.RequestRefresh();

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

        var menu = new ContextMenu();
        menu.Items.Add(refreshItem);
        menu.Items.Add(_autostartItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(exitItem);
        return menu;
    }

    private void OnStateChanged(BatteryViewState state) =>
        Dispatcher.BeginInvoke(() => ApplyState(state));

    private void ApplyState(BatteryViewState state)
    {
        if (_tray is null)
        {
            return;
        }

        TrayIconModel model = TrayIconModel.FromState(state);

        // 渲染器自行把位图包成 ICO 返回 System.Drawing.Icon,直接赋给 Icon 属性。
        // 不走 IconSource:其转换器只认 URI 背景的图片,会对运行期生成的位图抛 NotImplemented。
        System.Drawing.Icon icon = TrayIconRenderer.Render(model);
        _tray.Icon = icon;
        _currentIcon?.Dispose();
        _currentIcon = icon;

        _tray.ToolTipText = TooltipFormatter.Format(state);
    }

    // 监听设备插拔(WM_DEVICECHANGE / DBT_DEVNODES_CHANGED),去抖后触发立即刷新,
    // 让 USB↔蓝牙切换在数秒内生效。广播消息只发给顶级窗口,故建一个不可见的顶级窗口接收。
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
            _monitor?.RequestRefresh();
        };
    }

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
        _monitor?.Dispose();
        _tray?.Dispose();
        _currentIcon?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
