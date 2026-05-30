using Microsoft.Win32;

namespace GunAPP;

/// <summary>
/// 开机自启动管理（通过注册表 HKCU\...\Run）
/// </summary>
internal static class AutoStart
{
    private const string KeyName = "KuaiGunAPP";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>当前是否已设置开机自启</summary>
    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                return key?.GetValue(KeyName) != null;
            }
            catch { return false; }
        }
    }

    /// <summary>启用开机自启</summary>
    public static void Enable()
    {
        try
        {
            var exePath = Environment.ProcessPath ?? "";
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            key?.SetValue(KeyName, $"\"{exePath}\" --silent");
        }
        catch { }
    }

    /// <summary>禁用开机自启</summary>
    public static void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            key?.DeleteValue(KeyName, false);
        }
        catch { }
    }

    /// <summary>切换开机自启状态</summary>
    public static void Toggle()
    {
        if (IsEnabled) Disable();
        else Enable();
    }
}
