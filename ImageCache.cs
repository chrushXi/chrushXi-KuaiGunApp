using System.Drawing;

namespace GunAPP;

/// <summary>
/// 全局共享的图片/图标缓存，避免 6 个窗体类各自重复加载同一张 logo.png
/// </summary>
internal static class ImageCache
{
    private static Image? _logo;
    private static Icon? _appIcon;
    private static bool _logoLoaded;
    private static bool _iconLoaded;

    private static readonly string[] LogoPaths =
    [
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "public", "logo.png"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png"),
    ];

    /// <summary>获取 Logo 图片（全局唯一实例，不会重复加载）</summary>
    public static Image? Logo
    {
        get
        {
            if (!_logoLoaded)
            {
                _logoLoaded = true;
                foreach (var path in LogoPaths)
                {
                    if (File.Exists(path))
                    {
                        try
                        {
                            // 使用 FileStream + Image.FromStream 避免文件锁定
                            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                            _logo = Image.FromStream(fs);
                            break;
                        }
                        catch { }
                    }
                }
            }
            return _logo;
        }
    }

    /// <summary>获取应用图标（从 Logo 转换，全局唯一）</summary>
    public static Icon? AppIcon
    {
        get
        {
            if (!_iconLoaded)
            {
                _iconLoaded = true;
                var logo = Logo;
                if (logo != null)
                {
                    try
                    {
                        using var bmp = new Bitmap(logo);
                        var hIcon = bmp.GetHicon();
                        // Icon.FromHandle 依赖 GDI 句柄，Clone 后原始句柄仍被引用
                        // 正确做法：先 Clone，再 DestroyIcon（Clone 会创建独立句柄）
                        var icon = (Icon)Icon.FromHandle(hIcon).Clone();
                        DestroyIcon(hIcon);
                        _appIcon = icon;
                    }
                    catch { }
                }
            }
            return _appIcon;
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}