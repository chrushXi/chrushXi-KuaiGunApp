using System.Runtime.InteropServices;
using System.Text;

namespace GunAPP;

/// <summary>
/// Win32 P/Invoke 和 COM 接口声明
/// </summary>
internal static class NativeMethods
{
    // ═══ 自定义消息 ═══
    public const int WM_APP = 0x8000;
    public const int WM_SHELL_DECODED = WM_APP + 1;
    public const int WM_TRAY_ICON = WM_APP + 2;
    // ═══ Shell 通知事件 ═══
    public const int SHCNE_CREATE = 0x00000002;
    public const int SHCNE_RENAMEITEM = 0x00000001;
    public const int SHCNE_UPDATEITEM = 0x00002000;

    // ═══ SHChangeNotifyRegister 参数 ═══
    public const int SHCNRF_ShellLevel = 0x0002;
    public const int SHCNRF_NewDelivery = 0x0008;

    // ═══ CSIDL ═══
    public const int CSIDL_BITBUCKET = 0x0a;

    // ═══ SHGetKnownFolderIDList ═══
    public static readonly Guid FOLDERID_RecycleBinFolder = new("B7534046-3ECB-4C18-BE4E-64CD4CB7D6AC");

    // ═══ SLGP flags ═══
    public const uint SLGP_RAWPATH = 0x00000004;

    // ═══ STGM ═══
    public const uint STGM_READ = 0x00000000;

    // ═══ P/Invoke: Shell ═══

    [DllImport("shell32.dll", SetLastError = true)]
    public static extern uint SHChangeNotifyRegister(
        IntPtr hwnd, int fSources, int fEvents, uint wMsg,
        int cItems, ref SHChangeNotifyEntry pfsne);

    [DllImport("shell32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SHChangeNotifyDeregister(uint hNotify);

    [DllImport("shell32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SHGetPathFromIDListW(IntPtr pidl,
        [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszPath);

    [DllImport("shell32.dll")]
    public static extern int SHGetSpecialFolderLocation(IntPtr hwnd, int csidl, out IntPtr ppidl);

    [DllImport("shell32.dll")]
    public static extern void ILFree(IntPtr pidl);

    [DllImport("shell32.dll")]
    public static extern int SHGetKnownFolderIDList(ref Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppidl);

    // ═══ P/Invoke: Window ═══

    [DllImport("user32.dll")]
    public static extern uint RegisterWindowMessageW([MarshalAs(UnmanagedType.LPWStr)] string lpString);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool PostMessageW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    // ═══ 结构体 ═══

    [StructLayout(LayoutKind.Sequential)]
    public struct SHChangeNotifyEntry
    {
        public IntPtr pidl;
        [MarshalAs(UnmanagedType.Bool)] public bool fRecursive;
    }

    // ═══ COM: IShellLinkW ═══

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    public class ShellLinkCom { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
     Guid("000214EE-0000-0000-C000-000000000046")]
    public interface IShellLinkW
    {
        [PreserveSig] int GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile,
            int cch, IntPtr pfd, uint fFlags);

        [PreserveSig] int GetIDList(out IntPtr ppidl);
        [PreserveSig] int SetIDList(IntPtr pidl);

        [PreserveSig] int GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        [PreserveSig] int SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);

        [PreserveSig] int GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        [PreserveSig] int SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);

        [PreserveSig] int GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        [PreserveSig] int SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);

        [PreserveSig] int GetHotkey(out ushort pwHotkey);
        [PreserveSig] int SetHotkey(ushort wHotkey);

        [PreserveSig] int GetShowCmd(out int piShowCmd);
        [PreserveSig] int SetShowCmd(int iShowCmd);

        [PreserveSig] int GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath,
            int cch, out int piIcon);

        [PreserveSig] int SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);

        [PreserveSig] int SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);

        [PreserveSig] int Resolve(IntPtr hwnd, uint fFlags);

        [PreserveSig] int SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    // ═══ COM: IPersistFile ═══

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
     Guid("0000010b-0000-0000-C000-000000000046")]
    public interface IPersistFile
    {
        void GetClassID(out Guid pClassID);

        [PreserveSig] int IsDirty();

        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);

        void Save([MarshalAs(UnmanagedType.LPWStr)] string? pszFileName,
            [MarshalAs(UnmanagedType.Bool)] bool fRemember);

        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string? pszFileName);

        void GetCurFileName([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

}
