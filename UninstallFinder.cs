using Microsoft.Win32;

namespace GunAPP;

/// <summary>
/// 在注册表中查找程序的卸载信息
/// </summary>
internal static class UninstallFinder
{
    public record UninstallInfo(
        string DisplayName,
        string UninstallString,
        string QuietUninstallString,
        string InstallLocation,
        string DisplayIcon,
        string Publisher,
        int MatchScore)
    {
        public bool Found => MatchScore > 0;
    }

    /// <summary>根据目标 exe 路径查找卸载信息</summary>
    public static UninstallInfo Find(string targetExePath)
    {
        var best = new UninstallInfo("", "", "", "", "", "", 0);

        // 搜索多个注册表根键
        string[] roots =
        [
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        ];

        foreach (var root in roots)
        {
            var info = SearchRoot(RegistryHive.LocalMachine, root, targetExePath);
            if (info.MatchScore > best.MatchScore) best = info;
        }

        // HKCU
        var cuInfo = SearchRoot(RegistryHive.CurrentUser, roots[0], targetExePath);
        if (cuInfo.MatchScore > best.MatchScore) best = cuInfo;

        // 兜底：如果注册表找不到，检查程序目录里有没有卸载程序
        if (!best.Found)
        {
            var fallback = FindUninstallerInDirectory(targetExePath);
            if (fallback.Found) return fallback;
        }

        return best;
    }

    private static UninstallInfo SearchRoot(RegistryHive hive, string subKey, string targetExePath)
    {
        var best = new UninstallInfo("", "", "", "", "", "", 0);
        var targetLower = NormalizePath(targetExePath);

        try
        {
            using var rootKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var uninstallKey = rootKey.OpenSubKey(subKey);
            if (uninstallKey == null) return best;

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                using var subKey2 = uninstallKey.OpenSubKey(subKeyName);
                if (subKey2 == null) continue;

                var displayName = subKey2.GetValue("DisplayName") as string;
                var uninstallString = subKey2.GetValue("UninstallString") as string;
                if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(uninstallString))
                    continue;

                var quietUninstall = subKey2.GetValue("QuietUninstallString") as string ?? "";
                var installLocation = subKey2.GetValue("InstallLocation") as string ?? "";
                var displayIcon = subKey2.GetValue("DisplayIcon") as string ?? "";
                var publisher = subKey2.GetValue("Publisher") as string ?? "";

                int score = 0;

                // 统一路径格式
                var locNorm = NormalizePath(installLocation);
                var iconNorm = NormalizePath(displayIcon);

                // 策略1：InstallLocation 前缀匹配（最高权重）
                if (!string.IsNullOrEmpty(locNorm))
                {
                    var locLower = locNorm.TrimEnd('\\') + "\\";
                    if (targetLower.StartsWith(locLower, StringComparison.Ordinal))
                        score += 100;
                }

                // 策略2：DisplayIcon 与目标路径比对
                if (!string.IsNullOrEmpty(iconNorm))
                {
                    var iconLower = iconNorm;
                    var commaIdx = iconLower.IndexOf(',');
                    if (commaIdx >= 0) iconLower = iconLower[..commaIdx];
                    iconLower = iconLower.Trim('"');

                    if (targetLower == iconLower)
                        score += 80;
                    else if (targetLower.Contains(iconLower) || iconLower.Contains(targetLower))
                        score += 40;
                }

                // 策略3：UninstallString 中包含目标路径的目录部分
                if (!string.IsNullOrEmpty(installLocation))
                {
                    var locLower = installLocation.ToLowerInvariant();
                    if (targetLower.Contains(locLower))
                        score += 20;
                }
                else
                {
                    var dirName = Path.GetFileName(Path.GetDirectoryName(targetExePath));
                    if (!string.IsNullOrEmpty(dirName))
                    {
                        var uninstLower = uninstallString.ToLowerInvariant();
                        if (uninstLower.Contains(dirName.ToLowerInvariant()))
                            score += 15;
                    }
                }

                // 策略4：DisplayName 中包含 exe 文件名关键词
                var exeName = Path.GetFileNameWithoutExtension(targetExePath).ToLowerInvariant();
                if (!string.IsNullOrEmpty(exeName) &&
                    displayName.ToLowerInvariant().Contains(exeName))
                    score += 10;

                if (score > best.MatchScore)
                {
                    best = new UninstallInfo(
                        displayName, uninstallString, quietUninstall,
                        installLocation, displayIcon, publisher, score);
                }
            }
        }
        catch
        {
            // 忽略访问权限不足的键
        }

        return best;
    }

    /// <summary>在程序目录中查找卸载程序</summary>
    private static UninstallInfo FindUninstallerInDirectory(string exePath)
    {
        var dir = Path.GetDirectoryName(exePath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            return new UninstallInfo("", "", "", "", "", "", 0);

        // 常见的卸载程序名称
        string[] uninstallNames =
        [
            "uninstall.exe", "uninst.exe", "Uninstall.exe", "Uninst.exe",
            "unins000.exe", "unins001.exe",
            "卸载.exe", "卸载程序.exe",
        ];

        foreach (var name in uninstallNames)
        {
            var path = Path.Combine(dir, name);
            if (File.Exists(path))
            {
                var programName = Path.GetFileNameWithoutExtension(exePath);
                return new UninstallInfo(
                    programName,
                    $"\"{path}\"",
                    "",
                    dir,
                    "",
                    "",
                    5);  // 低分，表示是兜底方案
            }
        }

        // 递归搜索一层子目录
        try
        {
            foreach (var subDir in Directory.GetDirectories(dir))
            {
                foreach (var name in uninstallNames)
                {
                    var path = Path.Combine(subDir, name);
                    if (File.Exists(path))
                    {
                        var programName = Path.GetFileNameWithoutExtension(exePath);
                        return new UninstallInfo(
                            programName,
                            $"\"{path}\"",
                            "",
                            dir,
                            "",
                            "",
                            5);
                    }
                }
            }
        }
        catch { }

        return new UninstallInfo("", "", "", "", "", "", 0);
    }

    /// <summary>统一路径格式：小写 + 单反斜杠</summary>
    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        // 去掉引号
        path = path.Trim('"');
        // 双反斜杠替换为单反斜杠
        path = path.Replace("\\\\", "\\");
        return path.ToLowerInvariant();
    }
}
