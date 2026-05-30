using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GunAPP;

/// <summary>
/// 内存修剪工具：弹窗关闭后让任务管理器数字回落
/// </summary>
internal static class MemoryTrim
{
    /// <summary>修剪内存：先 GC，再让 OS 回收空闲页面</summary>
    public static void Trim()
    {
        GC.Collect(2, GCCollectionMode.Optimized, false, false);
        SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
    }

    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, int dwMinimumWorkingSetSize, int dwMaximumWorkingSetSize);
}