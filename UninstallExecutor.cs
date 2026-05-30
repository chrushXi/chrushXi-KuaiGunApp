using System.Diagnostics;

namespace GunAPP;

/// <summary>
/// 执行卸载命令
/// </summary>
internal static class UninstallExecutor
{
    public enum Result { Success, Failed, Cancelled, NotFound }

    public static Result Execute(string uninstallString)
    {
        if (string.IsNullOrWhiteSpace(uninstallString)) return Result.NotFound;

        try
        {
            // 分离 exe 路径和参数
            var (exePath, args) = SplitCommand(uninstallString);

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                UseShellExecute = true,
                Verb = "runas", // UAC 提权
            };

            Process.Start(psi);
            return Result.Success;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // ERROR_CANCELLED：用户拒绝 UAC
            return Result.Cancelled;
        }
        catch
        {
            return Result.Failed;
        }
    }

    /// <summary>分离 exe 路径和参数</summary>
    private static (string exe, string args) SplitCommand(string cmdLine)
    {
        cmdLine = cmdLine.Trim();

        if (cmdLine.StartsWith('"'))
        {
            var endQuote = cmdLine.IndexOf('"', 1);
            if (endQuote >= 0)
                return (cmdLine[1..endQuote], cmdLine[(endQuote + 1)..].Trim());
        }

        // 无引号：找到第一个空格分隔
        var spaceIdx = cmdLine.IndexOf(' ');
        if (spaceIdx >= 0)
            return (cmdLine[..spaceIdx], cmdLine[(spaceIdx + 1)..]);

        return (cmdLine, "");
    }
}
