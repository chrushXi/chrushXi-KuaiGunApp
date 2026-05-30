using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace GunAPP;

/// <summary>
/// 响应速度设置弹窗
/// </summary>
internal class SpeedSettingsForm : Form
{
    private static readonly Color BgColor = Color.FromArgb(252, 252, 252);
    private static readonly Color TextMain = Color.FromArgb(40, 40, 40);
    private static readonly Color TextSub = Color.FromArgb(120, 120, 120);
    private static readonly Color GreenBtn = Color.FromArgb(76, 175, 80);
    private static readonly Color BlueBtn = Color.FromArgb(0, 122, 255);
    private static readonly Color CloseBtnHover = Color.FromArgb(232, 17, 35);
    private static readonly Color SelectedBg = Color.FromArgb(0, 122, 255);
    private static readonly Color HoverBg = Color.FromArgb(240, 240, 240);

    private Rectangle _closeBtnRect;
    private Rectangle _dragRect;
    private bool _hoverClose;
    private bool _dragging;
    private Point _dragStart;

    private RectangleF _btnOkRect;
    private bool _hoverOk;
    private int _hoverIndex = -1;

    private const int TitleBarH = 36;

    private static readonly SpeedOption[] SpeedOptions =
    [
        new("较慢", 800, "响应延迟约 0.8 秒\n资源占用极低，适合配置较低的电脑"),
        new("正常", 200, "响应延迟约 0.2 秒\n资源占用低，推荐日常使用"),
        new("快速", 50, "响应延迟约 0.05 秒\n资源占用中等，适合 SSD 用户"),
        new("极速", 20, "响应延迟约 0.02 秒\n资源占用较高，不建议机械硬盘使用"),
    ];

    private int _selectedIndex;

    public SpeedSettingsForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgColor;
        DoubleBuffered = true;
        Size = new Size(380, 380);

        // 选中当前速度
        for (int i = 0; i < SpeedOptions.Length; i++)
        {
            if (SpeedOptions[i].Interval == AppSettings.PollingInterval)
            {
                _selectedIndex = i;
                break;
            }
        }
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

    private RectangleF[] _optionRects = [];

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

        // 标题栏
        var logo = LoadLogo();
        if (logo != null)
        {
            g.DrawImage(logo, new Rectangle(12, 6, 24, 24));
            using var tf = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            using var tb = new SolidBrush(TextMain);
            g.DrawString("响应速度设置", tf, tb, 42, 10);
        }
        else
        {
            using var tf = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            using var tb = new SolidBrush(TextMain);
            g.DrawString("响应速度设置", tf, tb, 14, 10);
        }

        DrawCloseButton(g);

        float y = TitleBarH + 16;
        int padX = 20;
        int optionH = 56;

        // 选项列表
        _optionRects = new RectangleF[SpeedOptions.Length];
        for (int i = 0; i < SpeedOptions.Length; i++)
        {
            var opt = SpeedOptions[i];
            var optRect = new RectangleF(padX, y, Width - padX * 2, optionH);
            _optionRects[i] = optRect;

            bool isSelected = i == _selectedIndex;
            bool isHover = i == _hoverIndex;

            // 背景
            using var optPath = RoundedRect(new Rectangle((int)optRect.X, (int)optRect.Y, (int)optRect.Width, (int)optRect.Height), 10);
            if (isSelected)
            {
                using var selBrush = new SolidBrush(Color.FromArgb(20, 0, 122, 255));
                g.FillPath(selBrush, optPath);
                using var selPen = new Pen(SelectedBg, 2);
                g.DrawPath(selPen, optPath);
            }
            else if (isHover)
            {
                using var hoverBrush = new SolidBrush(HoverBg);
                g.FillPath(hoverBrush, optPath);
            }
            else
            {
                using var defBrush = new SolidBrush(Color.FromArgb(248, 248, 248));
                g.FillPath(defBrush, optPath);
            }

            // 选项名称
            using var nameFont = new Font("Microsoft YaHei UI", 10f, isSelected ? FontStyle.Bold : FontStyle.Regular);
            using var nameBrush = new SolidBrush(isSelected ? SelectedBg : TextMain);
            g.DrawString(opt.Name, nameFont, nameBrush, optRect.X + 16, optRect.Y + 8);

            // 间隔值
            using var intervalFont = new Font("Microsoft YaHei UI", 8f);
            using var intervalBrush = new SolidBrush(TextSub);
            g.DrawString($"{opt.Interval}ms", intervalFont, intervalBrush, optRect.X + 80, optRect.Y + 10);

            // 描述
            using var descFont = new Font("Microsoft YaHei UI", 8f);
            using var descBrush = new SolidBrush(TextSub);
            g.DrawString(opt.Description.Split('\n')[0], descFont, descBrush, optRect.X + 16, optRect.Y + 30);

            // 选中标记
            if (isSelected)
            {
                using var checkFont = new Font("Segoe UI", 12f, FontStyle.Bold);
                using var checkBrush = new SolidBrush(SelectedBg);
                g.DrawString("✓", checkFont, checkBrush, optRect.Right - 30, optRect.Y + 14);
            }

            y += optionH + 8;
        }

        // 警告提示
        y += 4;
        using var warnFont = new Font("Microsoft YaHei UI", 8f);
        using var warnBrush = new SolidBrush(Color.FromArgb(255, 149, 0));
        var warnSf = new StringFormat { Alignment = StringAlignment.Center };
        g.DrawString("* 响应越快，CPU 和磁盘占用越高", warnFont, warnBrush, new RectangleF(0, y, Width, 18), warnSf);
        y += 24;

        // 确定按钮
        _btnOkRect = new RectangleF((Width - 140) / 2, y, 140, 36);
        DrawButton(g, _btnOkRect, "确定", GreenBtn, _hoverOk);
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

    private void DrawButton(Graphics g, RectangleF rect, string text, Color color, bool hover)
    {
        using var path = RoundedRect(new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height), 8);
        using var brush = new SolidBrush(hover ? Darken(color, 0.08f) : color);
        g.FillPath(brush, path);
        using var font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
        using var tb = new SolidBrush(Color.White);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, tb, rect, sf);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging) { Location += (Size)(e.Location - (Size)_dragStart); return; }

        bool c = _closeBtnRect.Contains(e.Location);
        bool o = _btnOkRect.Contains(e.Location);
        int newHover = -1;

        for (int i = 0; i < _optionRects.Length; i++)
        {
            if (_optionRects[i].Contains(e.Location))
            {
                newHover = i;
                break;
            }
        }

        if (c != _hoverClose || o != _hoverOk || newHover != _hoverIndex)
        {
            _hoverClose = c;
            _hoverOk = o;
            _hoverIndex = newHover;
            Invalidate();
        }

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

        if (_closeBtnRect.Contains(e.Location)) { Close(); return; }

        // 点击选项
        for (int i = 0; i < _optionRects.Length; i++)
        {
            if (_optionRects[i].Contains(e.Location))
            {
                _selectedIndex = i;
                Invalidate();
                return;
            }
        }

        // 确定
        if (_btnOkRect.Contains(e.Location))
        {
            AppSettings.PollingInterval = SpeedOptions[_selectedIndex].Interval;
            ShellMonitor.UpdateInterval();
            
            // 显示提示
            var trayIcon = Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.TrayIcon;
            trayIcon?.UpdateTooltip();
            
            Close();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) Close();
        else if (e.KeyCode == Keys.Enter)
        {
            AppSettings.PollingInterval = SpeedOptions[_selectedIndex].Interval;
            ShellMonitor.UpdateInterval();
            Close();
        }
    }

    private static Color Darken(Color c, float f) => Color.FromArgb((int)(c.R * (1 - f)), (int)(c.G * (1 - f)), (int)(c.B * (1 - f)));

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

internal record SpeedOption(string Name, int Interval, string Description);
