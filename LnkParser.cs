using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GunAPP;

/// <summary>
/// 解析 .lnk 快捷方式，获取目标程序路径
/// </summary>
internal static class LnkParser
{
    public record ShortcutInfo(string TargetPath, string Description, bool Valid);

    public static ShortcutInfo Resolve(string lnkPath)
    {
        // 方法1：尝试 COM IShellLink
        var result = ResolveViaCom(lnkPath);
        if (result.Valid && !IsGarbled(result.TargetPath))
            return result;

        // 方法2：尝试 PowerShell WScript.Shell
        result = ResolveViaPowerShell(lnkPath);
        if (result.Valid && !IsGarbled(result.TargetPath))
            return result;

        return new ShortcutInfo("", "", false);
    }

    /// <summary>通过 COM IShellLink 解析</summary>
    private static ShortcutInfo ResolveViaCom(string lnkPath)
    {
        NativeMethods.IShellLinkW? shellLink = null;
        NativeMethods.IPersistFile? persistFile = null;
        try
        {
            shellLink = (NativeMethods.IShellLinkW)new NativeMethods.ShellLinkCom();
            persistFile = (NativeMethods.IPersistFile)shellLink;

            persistFile.Load(lnkPath, NativeMethods.STGM_READ);

            var sb = new StringBuilder(512);
            shellLink.GetPath(sb, sb.Capacity, IntPtr.Zero, NativeMethods.SLGP_RAWPATH);
            var targetPath = sb.ToString().Trim('\0');

            if (string.IsNullOrEmpty(targetPath))
                return new ShortcutInfo("", "", false);

            var descSb = new StringBuilder(1024);
            shellLink.GetDescription(descSb, descSb.Capacity);

            return new ShortcutInfo(targetPath, descSb.ToString().Trim('\0'), true);
        }
        catch
        {
            return new ShortcutInfo("", "", false);
        }
        finally
        {
            if (persistFile != null) Marshal.ReleaseComObject(persistFile);
            if (shellLink != null) Marshal.ReleaseComObject(shellLink);
        }
    }

    /// <summary>通过 PowerShell WScript.Shell 解析</summary>
    private static ShortcutInfo ResolveViaPowerShell(string lnkPath)
    {
        try
        {
            var escapedPath = lnkPath.Replace("'", "''");
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $s=(New-Object -ComObject WScript.Shell).CreateShortcut('{escapedPath}'); Write-Output $s.TargetPath\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null) return new ShortcutInfo("", "", false);

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(3000);

            if (string.IsNullOrEmpty(output) || output.Contains("异常") || output.Contains("error", StringComparison.OrdinalIgnoreCase))
                return new ShortcutInfo("", "", false);

            return new ShortcutInfo(output, "", true);
        }
        catch
        {
            return new ShortcutInfo("", "", false);
        }
    }

    /// <summary>检测字符串是否为乱码（非正常路径）</summary>
    private static bool IsGarbled(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        // 正常路径必须包含 \ 或 :
        bool hasPathChars = text.Contains('\\') || text.Contains(':');
        if (!hasPathChars) return true;

        // 检查替换字符 U+FFFD（真正乱码的标志）
        foreach (var c in text)
        {
            if (c == 0xFFFD) return true;
        }

        // 检查不可打印字符（控制字符，排除常见分隔符）
        foreach (var c in text)
        {
            if (c < 0x20 && c != '\t' && c != '\n' && c != '\r')
                return true;
        }

        return false;
    }

    /// <summary>从 exe 文件版本信息获取程序显示名</summary>
    public static string GetDisplayName(string exePath)
    {
        try
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
            if (!string.IsNullOrEmpty(versionInfo.ProductName))
                return versionInfo.ProductName;
            if (!string.IsNullOrEmpty(versionInfo.FileDescription))
                return versionInfo.FileDescription;
        }
        catch { }

        var name = Path.GetFileNameWithoutExtension(exePath);
        return string.IsNullOrEmpty(name) ? exePath : name;
    }
}
