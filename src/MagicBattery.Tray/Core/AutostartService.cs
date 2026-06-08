using System.Runtime.Versioning;
using Microsoft.Win32;

namespace MagicBattery.Tray.Core;

/// <summary>开机自启的存储抽象,便于把注册表读写隔离出来做单测。</summary>
public interface IAutostartStore
{
    string? Get(string name);
    void Set(string name, string value);
    void Remove(string name);
}

/// <summary>
/// 开机自启服务。走 HKCU Run 键 —— 无需管理员、绿色版友好(对齐硬约束)。
/// 逻辑(值名 / 路径加引号)在此层,可用假 store 单测;真实注册表读写在
/// <see cref="RegistryAutostartStore"/>。
/// </summary>
public sealed class AutostartService
{
    /// <summary>Run 键下的值名。</summary>
    public const string ValueName = "MagicBattery";

    private readonly IAutostartStore _store;
    private readonly Func<string> _exePath;

    public AutostartService(IAutostartStore store, Func<string>? exePath = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        // 单文件发布下 Environment.ProcessPath 指向真正的 exe
        _exePath = exePath ?? (() => Environment.ProcessPath ?? "");
    }

    public bool IsEnabled => !string.IsNullOrEmpty(_store.Get(ValueName));

    public void Enable() => _store.Set(ValueName, Quote(_exePath()));

    public void Disable() => _store.Remove(ValueName);

    /// <summary>切换并返回切换后的状态。</summary>
    public bool Toggle()
    {
        if (IsEnabled)
        {
            Disable();
            return false;
        }

        Enable();
        return true;
    }

    // 路径可能含空格,写注册表 Run 值时加引号
    private static string Quote(string path) => $"\"{path}\"";
}

/// <summary>HKCU\...\CurrentVersion\Run 的真实读写实现。</summary>
[SupportedOSPlatform("windows")]
public sealed class RegistryAutostartStore : IAutostartStore
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public string? Get(string name)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(name) as string;
    }

    public void Set(string name, string value)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    public void Remove(string name)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}
