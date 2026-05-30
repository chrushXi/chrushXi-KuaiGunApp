using System.Runtime.InteropServices;

namespace GunAPP;

/// <summary>
/// 通过优化轮询监控回收站目录新增的 .lnk 文件
/// 核心优化：先检查目录时间戳，无变化则跳过全量扫描
/// </summary>
internal static class ShellMonitor
{
    private static System.Windows.Forms.Timer? _timer;
    private static Action<string, string>? _callback;
    private static bool _running;
    private static bool _paused;

    // 复用集合，减少 GC 压力
    private static HashSet<string> _knownLnkFiles = new(StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> _currentScan = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> _newFiles = new();
    private static string _recycleBinPath = "";

    // 时间戳缓存：只在目录发生变化时才做全量扫描
    private static DateTime _lastWriteTimeUtc;

    public static bool Start(IntPtr hwnd, Action<string, string> callback)
    {
        if (_running) return false;
        _callback = callback;

        _recycleBinPath = GetRecycleBinPath();
        if (string.IsNullOrEmpty(_recycleBinPath)) return false;

        ScanRecycleBinLnks(_knownLnkFiles);
        RecordCurrentState();

        _timer = new System.Windows.Forms.Timer { Interval = AppSettings.PollingInterval };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        _running = true;
        return true;
    }

    public static void Stop()
    {
        if (!_running) return;
        _paused = true;
        _timer?.Stop();
        ScanRecycleBinLnks(_knownLnkFiles);
        RecordCurrentState();
    }

    public static void Resume()
    {
        if (!_running) return;
        _paused = false;
        ScanRecycleBinLnks(_knownLnkFiles);
        RecordCurrentState();
        _timer!.Interval = AppSettings.PollingInterval;
        _timer.Start();
    }

    public static void UpdateInterval()
    {
        if (_timer != null)
        {
            _timer.Interval = AppSettings.PollingInterval;
            var trayIcon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.TrayIcon;
            trayIcon?.UpdateTooltip();
        }
    }

    public static void Shutdown()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        _running = false;
        _paused = false;
    }

    private static void OnTimerTick(object? sender, EventArgs e)
    {
        if (!_running || _paused || _callback == null) return;

        try
        {
            // ★ 关键优化：时间戳没变则跳过（零分配，只读一个 struct）
            if (!HasDirectoryChanged())
                return;

            // 有变化才做全量扫描
            _currentScan.Clear();
            ScanRecycleBinLnks(_currentScan);

            _newFiles.Clear();
            foreach (var file in _currentScan)
            {
                if (!_knownLnkFiles.Contains(file))
                    _newFiles.Add(file);
            }

            if (_newFiles.Count > 0)
            {
                var temp = _knownLnkFiles;
                _knownLnkFiles = _currentScan;
                _currentScan = temp;

                foreach (var lnkPath in _newFiles)
                {
                    _callback.Invoke(lnkPath, "");
                }
            }
            else
            {
                // 没有新增文件但目录变了（文件被删除了），同步更新
                _knownLnkFiles.Clear();
                foreach (var f in _currentScan)
                    _knownLnkFiles.Add(f);
            }

            // 更新时间戳缓存
            RecordCurrentState();
        }
        catch (UnauthorizedAccessException) { /* 权限不足，跳过本次扫描 */ }
        catch (IOException) { /* IO 异常，跳过本次扫描 */ }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GunAPP] ShellMonitor 异常: {ex.Message}");
        }
    }

    /// <summary>仅用时间戳判断回收站是否有变化（零堆分配）</summary>
    private static bool HasDirectoryChanged()
    {
        try
        {
            var currentWriteTime = Directory.GetLastWriteTimeUtc(_recycleBinPath);
            return currentWriteTime != _lastWriteTimeUtc;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>记录当前回收站的时间戳</summary>
    private static void RecordCurrentState()
    {
        try
        {
            _lastWriteTimeUtc = Directory.GetLastWriteTimeUtc(_recycleBinPath);
        }
        catch { }
    }

    private static void ScanRecycleBinLnks(HashSet<string> result)
    {
        result.Clear();

        try
        {
            var files = Directory.GetFiles(_recycleBinPath, "$R*.lnk", SearchOption.AllDirectories);
            foreach (var f in files)
            {
                result.Add(f);
            }
        }
        catch (UnauthorizedAccessException)
        {
            try
            {
                var files = Directory.GetFiles(_recycleBinPath, "$R*.lnk");
                foreach (var f in files) result.Add(f);
            }
            catch { }

            foreach (var dir in Directory.GetDirectories(_recycleBinPath))
            {
                try
                {
                    var files = Directory.GetFiles(dir, "$R*.lnk");
                    foreach (var f in files) result.Add(f);
                }
                catch { }
            }
        }
        catch { }
    }

    private static string GetRecycleBinPath()
    {
        var sid = GetCurrentUserIdentitySid();
        if (string.IsNullOrEmpty(sid)) return "";

        var path = Path.Combine(@"C:\$Recycle.Bin", sid);
        return Directory.Exists(path) ? path : "";
    }

    private static string GetCurrentUserIdentitySid()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            return identity.User?.Value ?? "";
        }
        catch
        {
            return "";
        }
    }
}