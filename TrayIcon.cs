using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace GunAPP;

/// <summary>
/// 系统托盘图标管理
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private bool _paused;
    private bool _disposed;

    public event Action? OnExit;

    public TrayIcon()
    {
        var icon = LoadIconFromPng();

        _notifyIcon = new NotifyIcon
        {
            Icon = icon ?? SystemIcons.Application,
            Text = $"快滚!APP! - {AppSettings.SpeedName}",
            Visible = true,
        };

        _notifyIcon.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Right)
            {
                ShowCustomMenu();
            }
            else if (e.Button == MouseButtons.Left)
            {
                NotifyForm.Show("快滚!APP!",
                    _paused ? "运行已暂停。\n\n右键托盘图标恢复监控。"
                            : "正在运行中。\n\n将任意快捷方式拖入回收站即可触发。");
            }
        };
    }

    public bool IsPaused => _paused;

    public void SetPaused(bool paused)
    {
        _paused = paused;
        UpdateTooltip();

        if (paused)
            ShellMonitor.Stop();
        else
            ShellMonitor.Resume();
    }

    public void UpdateTooltip()
    {
        _notifyIcon.Text = _paused 
            ? "快滚!APP! - 已暂停" 
            : $"快滚!APP! - {AppSettings.SpeedName}";
    }

    private void ShowCustomMenu()
    {
        var menu = new CustomMenu(_paused, (pause) =>
        {
            SetPaused(pause);
        }, () => OnExit?.Invoke(), () =>
        {
            using var form = new WhitelistForm();
            form.ShowDialog();
            MemoryTrim.Trim();
        }, () =>
        {
            AutoStart.Toggle();
        }, () =>
        {
            using var form = new AboutForm();
            form.ShowDialog();
            MemoryTrim.Trim();
        }, () =>
        {
            // 赞赏
            var donateImg = DonateImageCache.Image;
            if (donateImg != null)
            {
                using var form = new DonateForm(donateImg);
                form.ShowDialog();
                MemoryTrim.Trim();
            }
            else
            {
                NotifyForm.Show("快滚!APP!", "赞赏码加载中，请稍后再试。");
            }
        }, () =>
        {
            // 响应速度
            using var form = new SpeedSettingsForm();
            form.ShowDialog();
            MemoryTrim.Trim();
        });

        var cursorPos = Cursor.Position;
        menu.ShowAt(cursorPos);
    }

    private static Icon? LoadIconFromPng()
    {
        try
        {
            return ImageCache.AppIcon;
        }
        catch { }
        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Icon?.Dispose();
        _notifyIcon.Dispose();
    }
}

/// <summary>
/// 赞赏码图片缓存
/// </summary>
internal static class DonateImageCache
{
    private static Image? _image;
    private static bool _loading;
    private const string DonateUrl = "http://111.228.60.167/pay.jpg";

    public static Image? Image => _image;

    public static Image? LoadSync()
    {
        if (_image != null) return _image;
        try
        {
            using var client = new System.Net.Http.HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var bytes = client.GetByteArrayAsync(DonateUrl).GetAwaiter().GetResult();
            using var ms = new MemoryStream(bytes);
            _image = System.Drawing.Image.FromStream(ms);
        }
        catch { }
        return _image;
    }

    public static async void Preload()
    {
        if (_image != null || _loading) return;
        _loading = true;
        try
        {
            using var client = new System.Net.Http.HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var bytes = await client.GetByteArrayAsync(DonateUrl);
            using var ms = new MemoryStream(bytes);
            _image = System.Drawing.Image.FromStream(ms);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GunAPP] 赞赏码预加载失败: {ex.Message}");
        }
        finally { _loading = false; }
    }
}

/// <summary>
/// 赞赏码弹窗
/// </summary>
internal class DonateForm : Form
{
    private static readonly Color BgColor = Color.FromArgb(252, 252, 252);
    private static readonly Color TextMain = Color.FromArgb(40, 40, 40);
    private static readonly Color TextSub = Color.FromArgb(120, 120, 120);
    private static readonly Color CloseBtnHover = Color.FromArgb(232, 17, 35);

    private Rectangle _closeBtnRect;
    private Rectangle _dragRect;
    private bool _hoverClose;
    private bool _dragging;
    private Point _dragStart;
    private readonly Image _donateImage;

    private const int TitleBarH = 36;

    public DonateForm(Image donateImage)
    {
        _donateImage = donateImage;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgColor;
        DoubleBuffered = true;
        Size = new Size(400, 460);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x00020000;
            return cp;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            if (Environment.OSVersion.Version.Build >= 22000)
            {
                int corner = 2;
                DwmSetWindowAttribute(Handle, 33, ref corner, sizeof(int));
            }
            else
            {
                using var p = RoundedRect(new Rectangle(0, 0, Width, Height), 14);
                Region = new Region(p);
            }
        }
        catch
        {
            using var p = RoundedRect(new Rectangle(0, 0, Width, Height), 14);
            Region = new Region(p);
        }

        _dragRect = new Rectangle(0, 0, Width - 40, TitleBarH);
        _closeBtnRect = new Rectangle(Width - 36, 4, 28, 28);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // 背景
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var bgPath = RoundedRect(rect, 14);
        using var bgBrush = new SolidBrush(BgColor);
        g.FillPath(bgBrush, bgPath);

        // 标题栏
        using var titleBrush = new SolidBrush(Color.FromArgb(245, 245, 245));
        g.FillRectangle(titleBrush, 0, 0, Width, TitleBarH);
        using var sepPen = new Pen(Color.FromArgb(230, 230, 230));
        g.DrawLine(sepPen, 0, TitleBarH, Width, TitleBarH);

        // 标题栏 Logo + 文字
        var logo = LoadLogo();
        if (logo != null)
        {
            g.DrawImage(logo, new Rectangle(12, 6, 24, 24));
            using var tf = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            using var tb = new SolidBrush(TextMain);
            g.DrawString("赞赏支持", tf, tb, 42, 10);
        }
        else
        {
            using var tf = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            using var tb = new SolidBrush(TextMain);
            g.DrawString("赞赏支持", tf, tb, 14, 10);
        }

        // 关闭按钮
        DrawCloseButton(g);

        // 赞赏码图片（铺满）
        float y = TitleBarH + 12;
        int imgSize = Width - 40;
        int imgX = 20;
        g.DrawImage(_donateImage, new Rectangle(imgX, (int)y, imgSize, imgSize));
        y += imgSize + 12;

        // 提示文字
        using var tipFont = new Font("Microsoft YaHei UI", 9f);
        using var tipBrush = new SolidBrush(TextSub);
        var tipSf = new StringFormat { Alignment = StringAlignment.Center };
        g.DrawString("如果觉得好用，请作者喝杯咖啡", tipFont, tipBrush, new RectangleF(0, y, Width, 20), tipSf);
    }

    private void DrawCloseButton(Graphics g)
    {
        var r = _closeBtnRect;
        using var path = RoundedRect(r, 6);
        if (_hoverClose) { using var b = new SolidBrush(CloseBtnHover); g.FillPath(b, path); }
        using var pen = new Pen(_hoverClose ? Color.White : Color.FromArgb(180, 180, 180), 1.5f);
        g.DrawLine(pen, r.X + 8, r.Y + 8, r.Right - 8, r.Bottom - 8);
        g.DrawLine(pen, r.Right - 8, r.Y + 8, r.X + 8, r.Bottom - 8);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging) { Location += (Size)(e.Location - (Size)_dragStart); return; }

        bool c = _closeBtnRect.Contains(e.Location);
        if (c != _hoverClose) { _hoverClose = c; Invalidate(); }

        Cursor = _dragRect.Contains(e.Location) ? Cursors.SizeAll : Cursors.Default;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && _dragRect.Contains(e.Location))
        {
            _dragging = true;
            _dragStart = e.Location;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left || _dragging) return;
        if (_closeBtnRect.Contains(e.Location)) Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) Close();
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        int d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private static Image? LoadLogo() => ImageCache.Logo;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
}

/// <summary>
/// 自定义圆角菜单 (真·Layered Window 完美抗锯齿版)
/// </summary>
internal class CustomMenu : Form
{
    private readonly bool _isPaused;
    private readonly Action<bool> _onPauseResume;
    private readonly Action _onExit;
    private readonly Action _onWhitelist;
    private readonly Action _onAutoStart;
    private readonly Action _onAbout;
    private readonly Action _onDonate;
    private readonly Action _onSpeed;
    private int _hoverIndex = -1;
    private RectangleF[] _itemRects = [];
    private bool _closed;

    // 缓存渲染结果，避免每次鼠标移动都重新创建所有 GDI+ 对象
    private Bitmap? _cachedBmp;
    private int _cachedHoverIndex = -1;

    private static readonly Color BgColorTop = Color.FromArgb(235, 252, 252, 252);
    private static readonly Color BgColorBottom = Color.FromArgb(235, 238, 238, 242);
    private static readonly Color TextMain = Color.FromArgb(255, 28, 28, 30);
    private static readonly Color GreenAccent = Color.FromArgb(255, 52, 199, 89);
    private static readonly Color OrangeAccent = Color.FromArgb(255, 255, 149, 0);
    private static readonly Color RedAccent = Color.FromArgb(255, 255, 59, 48);
    private static readonly Color BorderColor = Color.FromArgb(30, 0, 0, 0);
    private static readonly Color GlassHighlight = Color.FromArgb(150, 255, 255, 255);
    private static readonly Color SepColor = Color.FromArgb(20, 0, 0, 0);
    private static readonly Color HoverOverlay = Color.FromArgb(15, 0, 0, 0);

    private const int Radius = 16;
    private const int ItemH = 44;
    private const int SepH = 1;
    private const int PadX = 12;

    private new const int Margin = 16;
    private readonly int _contentW = 140;
    private readonly int _contentH;

    public CustomMenu(bool isPaused, Action<bool> onPauseResume, Action onExit, Action onWhitelist, Action onAutoStart, Action onAbout, Action onDonate, Action onSpeed)
    {
        _isPaused = isPaused;
        _onPauseResume = onPauseResume;
        _onExit = onExit;
        _onWhitelist = onWhitelist;
        _onAutoStart = onAutoStart;
        _onAbout = onAbout;
        _onDonate = onDonate;
        _onSpeed = onSpeed;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;

        // 8 个选项
        _contentH = 12 + ItemH + SepH + ItemH + SepH + ItemH + SepH + ItemH + SepH + ItemH + SepH + ItemH + SepH + ItemH + SepH + ItemH + 12;
        Size = new Size(_contentW + Margin * 2, _contentH + Margin * 2);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00080000;
            cp.ExStyle |= 0x00000080;
            return cp;
        }
    }

    public void ShowAt(Point screenPos)
    {
        var screen = Screen.FromPoint(screenPos);
        int targetX = screenPos.X - _contentW - Margin;
        int targetY = screenPos.Y - _contentH - Margin;

        if (targetX < screen.WorkingArea.Left) targetX = screen.WorkingArea.Left - Margin + 8;
        if (targetY < screen.WorkingArea.Top) targetY = screenPos.Y - Margin + 8;

        Location = new Point(targetX, targetY);

        RenderForm();
        Show();
        Activate();
    }

    private void RenderForm()
    {
        // 如果 hover 状态没变化且有缓存，直接复用
        if (_cachedBmp != null && _hoverIndex == _cachedHoverIndex)
            return;

        // 释放旧缓存
        _cachedBmp?.Dispose();
        _cachedBmp = null;

        var bmp = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias; 

        for (int i = 12; i >= 1; i--)
        {
            int alpha = (int)((13 - i) * 1.5); 
            var shadowRect = new Rectangle(Margin - i, Margin - i, _contentW + i * 2, _contentH + i * 2);
            using var shadowPath = RoundedRect(shadowRect, Radius + (i / 2));
            using var shadowBrush = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0));
            g.FillPath(shadowBrush, shadowPath);
        }

        var contentRect = new Rectangle(Margin, Margin, _contentW, _contentH);
        using var bgPath = RoundedRect(contentRect, Radius);

        using var bgBrush = new LinearGradientBrush(contentRect, BgColorTop, BgColorBottom, LinearGradientMode.Vertical);
        g.FillPath(bgBrush, bgPath);

        var innerRect = new Rectangle(Margin + 1, Margin + 1, _contentW - 3, _contentH - 3);
        using var innerPath = RoundedRect(innerRect, Radius - 1);
        using var highlightPen = new Pen(GlassHighlight, 1);
        g.DrawPath(highlightPen, innerPath);

        using var borderPen = new Pen(BorderColor, 1);
        g.DrawPath(borderPen, bgPath);

        float y = Margin + 12;
        var items = new List<RectangleF>();

        using var sepPen = new Pen(SepColor);

        // 8 个选项
        for (int i = 0; i < 8; i++)
        {
            items.Add(new RectangleF(Margin, y, _contentW, ItemH));
            y += ItemH;
            if (i < 7) { g.DrawLine(sepPen, Margin + PadX, y, Margin + _contentW - PadX, y); y += SepH; }
        }

        _itemRects = items.ToArray();

        for (int i = 0; i < items.Count; i++)
        {
            var r = items[i];
            if (i == _hoverIndex)
            {
                var hoverRect = new RectangleF(r.X + 6, r.Y + 2, r.Width - 12, r.Height - 4);
                using var hoverPath = RoundedRect(new Rectangle((int)hoverRect.X, (int)hoverRect.Y, (int)hoverRect.Width, (int)hoverRect.Height), 8);
                using var hoverBrush = new SolidBrush(HoverOverlay);
                g.FillPath(hoverBrush, hoverPath);
            }

            string text = i switch
            {
                0 => _isPaused ? "已暂停" : "运行中",
                1 => _isPaused ? "开始运行" : "暂停运行",
                2 => "白名单",
                3 => AutoStart.IsEnabled ? "关闭自启动" : "开机自启动",
                4 => $"响应: {AppSettings.SpeedName}",
                5 => "关于",
                6 => "赞赏",
                _ => "退出"
            };
            Color color = i switch
            {
                0 => _isPaused ? OrangeAccent : GreenAccent,
                4 => Color.FromArgb(255, 0, 122, 255),  // 响应速度用蓝色
                6 => Color.FromArgb(255, 255, 149, 0),  // 赞赏用橙色
                7 => RedAccent,
                _ => TextMain
            };
            bool bold = (i == 0);

            using var font = new Font("Microsoft YaHei UI", 10f, bold ? FontStyle.Bold : FontStyle.Regular);
            using var textBrush = new SolidBrush(color);
            var sf = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center };
            g.DrawString(text, font, textBrush, new RectangleF(r.X, r.Y, r.Width, r.Height), sf);
        }

        // 缓存当前渲染结果和对应的 hover 状态
        _cachedBmp = bmp;
        _cachedHoverIndex = _hoverIndex;

        SetLayeredWindow(bmp);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int newHover = -1;
        for (int i = 0; i < _itemRects.Length; i++)
        {
            if (_itemRects[i].Contains(e.Location))
            {
                newHover = i;
                break;
            }
        }
        // 只有 hover 状态真正变化时才重新渲染
        if (newHover != _hoverIndex)
        {
            _hoverIndex = newHover;
            RenderForm();
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left) return;

        for (int i = 0; i < _itemRects.Length; i++)
        {
            if (_itemRects[i].Contains(e.Location))
            {
                if (i == 1) _onPauseResume(!_isPaused);
                if (i == 2) _onWhitelist();
                if (i == 3) _onAutoStart();
                if (i == 4) _onSpeed();
                if (i == 5) _onAbout();
                if (i == 6) _onDonate();
                if (i == 7) _onExit();
                SafeClose();
                break;
            }
        }
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        SafeClose();
    }

    private void SafeClose()
    {
        if (_closed) return;
        _closed = true;
        _cachedBmp?.Dispose();
        _cachedBmp = null;
        Close();
    }

    private static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        if (d > 0)
        {
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        }
        path.CloseFigure();
        return path;
    }

    private void SetLayeredWindow(Bitmap bmp)
    {
        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memDc = CreateCompatibleDC(screenDc);
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr oldBitmap = IntPtr.Zero;

        try
        {
            hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
            oldBitmap = SelectObject(memDc, hBitmap);

            var newSize = new Size(bmp.Width, bmp.Height);
            var newLocation = this.Location;
            var sourceLocation = new Point(0, 0);
            var blend = new BLENDFUNCTION
            {
                BlendOp = 0,
                BlendFlags = 0,
                SourceConstantAlpha = 255, 
                AlphaFormat = 1
            };

            UpdateLayeredWindow(Handle, screenDc, ref newLocation, ref newSize,
                memDc, ref sourceLocation, 0, ref blend, 2);
        }
        finally
        {
            if (hBitmap != IntPtr.Zero)
            {
                SelectObject(memDc, oldBitmap);
                DeleteObject(hBitmap);
            }
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref Point pptDst, ref Size psize, IntPtr hdcSrc, ref Point pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern bool DeleteObject(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }
}
