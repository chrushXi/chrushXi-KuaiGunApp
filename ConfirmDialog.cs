using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace GunAPP;

internal static class ConfirmDialog
{
    public enum UserChoice { Uninstall, JustDelete, WhitelistUninstall, WhitelistDelete }

    public static UserChoice Show(string programName, string iconPath)
    {
        using var form = new CustomConfirmForm(programName, iconPath);
        form.ShowDialog();
        return form.Choice;
    }
}

internal class CustomConfirmForm : Form
{
    public ConfirmDialog.UserChoice Choice { get; private set; } = ConfirmDialog.UserChoice.JustDelete;

    private readonly string _programName;
    private readonly string _iconPath;
    private bool _whitelistChecked;
    private Icon? _extractedIcon;

    // 颜色
    private static readonly Color BgColor = Color.FromArgb(252, 252, 252);
    private static readonly Color TitleBarColor = Color.FromArgb(245, 245, 245);
    private static readonly Color TextMain = Color.FromArgb(40, 40, 40);
    private static readonly Color TextSub = Color.FromArgb(120, 120, 120);
    private static readonly Color GreenBtn = Color.FromArgb(76, 175, 80);
    private static readonly Color GrayBtn = Color.FromArgb(230, 230, 230);
    private static readonly Color CheckColor = Color.FromArgb(76, 175, 80);
    private static readonly Color CloseBtnColor = Color.FromArgb(180, 180, 180);
    private static readonly Color CloseBtnHover = Color.FromArgb(232, 17, 35);

    // 区域
    private RectangleF _btnUninstall;
    private RectangleF _btnJustDelete;
    private RectangleF _checkboxRect;
    private Rectangle _closeBtnRect;
    private Rectangle _dragRect;
    private bool _hoverUninstall;
    private bool _hoverJustDelete;
    private bool _hoverClose;
    private bool _dragging;
    private Point _dragStart;

    private const int TitleBarH = 36;

    public CustomConfirmForm(string programName, string iconPath)
    {
        _programName = programName;
        _iconPath = iconPath;

        // 提取程序图标一次（不在 OnPaint 中反复提取，避免 GDI 泄漏）
        try
        {
            if (!string.IsNullOrEmpty(iconPath))
            {
                var p = iconPath.Split(',')[0].Trim('"');
                _extractedIcon = Icon.ExtractAssociatedIcon(p);
            }
        }
        catch { }

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgColor;
        DoubleBuffered = true;
        Size = new Size(400, 260);
        Icon = ImageCache.AppIcon;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _extractedIcon?.Dispose();
        base.Dispose(disposing);
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

        // 定义拖拽区域和关闭按钮
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

        // 标题栏背景
        using var titleBrush = new SolidBrush(TitleBarColor);
        g.FillRectangle(titleBrush, 0, 0, Width, TitleBarH);

        // 标题栏底部分隔线
        using var sepPen = new Pen(Color.FromArgb(230, 230, 230));
        g.DrawLine(sepPen, 0, TitleBarH, Width, TitleBarH);

        // 标题栏 Logo
        var logo = LoadLogo();
        if (logo != null)
        {
            g.DrawImage(logo, new Rectangle(12, 6, 24, 24));
            // 标题栏文字（Logo 右侧）
            using var titleFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            using var titleTextBrush = new SolidBrush(TextMain);
            g.DrawString("快滚！APP！", titleFont, titleTextBrush, 42, 10);
        }
        else
        {
            using var titleFont = new Font("Microsoft YaHei UI", 9f);
            using var titleTextBrush = new SolidBrush(TextSub);
            g.DrawString("快滚！APP！", titleFont, titleTextBrush, 14, 10);
        }

        // 关闭按钮
        DrawCloseButton(g);

        // 内容区域（标题栏下方）
        int contentY = TitleBarH + 16;

        // 图标
        int iconX = 28, iconY = contentY;
        DrawIcon(g, iconX, iconY);

        // 文字
        float tx = iconX + 52;
        using var headerFont = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
        using var headerBrush = new SolidBrush(TextMain);
        g.DrawString("检测到快捷方式被移入回收站", headerFont, headerBrush, tx, contentY);

        using var nameFont = new Font("Microsoft YaHei UI", 10f);
        g.DrawString(_programName, nameFont, headerBrush, tx, contentY + 26);

        using var descFont = new Font("Microsoft YaHei UI", 8.5f);
        using var subBrush = new SolidBrush(TextSub);
        g.DrawString("是否要彻底卸载此程序？", descFont, subBrush, tx, contentY + 50);

        // 复选框
        float cy = contentY + 88;
        _checkboxRect = new RectangleF(tx, cy, 200, 20);
        DrawCheckbox(g, tx, cy, _whitelistChecked);
        using var cf = new Font("Microsoft YaHei UI", 8.5f);
        g.DrawString("加入白名单，以后不再提示", cf, subBrush, tx + 22, cy + 2);

        // 按钮
        float by = contentY + 122;
        _btnUninstall = new RectangleF(tx, by, 110, 36);
        _btnJustDelete = new RectangleF(tx + 120, by, 130, 36);

        DrawButton(g, _btnUninstall, "一键卸载", GreenBtn, _hoverUninstall, true);
        DrawButton(g, _btnJustDelete, "取  消", GrayBtn, _hoverJustDelete, false);
    }

    private void DrawCloseButton(Graphics g)
    {
        var r = _closeBtnRect;
        using var path = RoundedRect(r, 6);

        if (_hoverClose)
        {
            using var brush = new SolidBrush(CloseBtnHover);
            g.FillPath(brush, path);
        }

        // X 符号
        using var pen = new Pen(_hoverClose ? Color.White : CloseBtnColor, 1.5f);
        int pad = 8;
        g.DrawLine(pen, r.X + pad, r.Y + pad, r.Right - pad, r.Bottom - pad);
        g.DrawLine(pen, r.Right - pad, r.Y + pad, r.X + pad, r.Bottom - pad);
    }

    private void DrawIcon(Graphics g, int x, int y)
    {
        // 使用构造时缓存的图标（OnPaint 不再每次创建新 Icon）
        if (_extractedIcon != null)
        {
            g.DrawIcon(_extractedIcon, new Rectangle(x, y, 40, 40));
            return;
        }

        using var pen = new Pen(Color.FromArgb(255, 152, 0), 2.5f);
        g.DrawEllipse(pen, x + 8, y + 8, 24, 24);
        using var font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold);
        using var brush = new SolidBrush(Color.FromArgb(255, 152, 0));
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("!", font, brush, new RectangleF(x + 8, y + 8, 24, 24), sf);
    }

    private void DrawButton(Graphics g, RectangleF rect, string text, Color color, bool hover, bool primary)
    {
        using var path = RoundedRect(new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height), 8);
        using var brush = new SolidBrush(hover ? Darken(color, 0.08f) : color);
        g.FillPath(brush, path);

        using var font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
        using var tb = new SolidBrush(primary ? Color.White : TextMain);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, tb, rect, sf);
    }

    private void DrawCheckbox(Graphics g, float x, float y, bool on)
    {
        var box = new RectangleF(x, y + 2, 14, 14);
        using var path = RoundedRect(new Rectangle((int)box.X, (int)box.Y, 14, 14), 3);

        if (on)
        {
            using var brush = new SolidBrush(CheckColor);
            g.FillPath(brush, path);
            using var pen = new Pen(Color.White, 2);
            g.DrawLines(pen, new PointF[] { new(x + 3, y + 9), new(x + 6, y + 12), new(x + 11, y + 5) });
        }
        else
        {
            using var pen = new Pen(Color.FromArgb(200, 200, 200), 1);
            g.DrawPath(pen, path);
        }
    }

    // ═══ 鼠标交互 ═══

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        // 拖拽
        if (_dragging)
        {
            var diff = e.Location - (Size)_dragStart;
            Location += (Size)diff;
            return;
        }

        // 悬停检测
        bool u = _btnUninstall.Contains(e.Location);
        bool d = _btnJustDelete.Contains(e.Location);
        bool c = _closeBtnRect.Contains(e.Location);

        if (u != _hoverUninstall || d != _hoverJustDelete || c != _hoverClose)
        {
            _hoverUninstall = u;
            _hoverJustDelete = d;
            _hoverClose = c;
            Invalidate();
        }

        // 拖拽区域光标
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

        if (_closeBtnRect.Contains(e.Location)) { Choice = ConfirmDialog.UserChoice.JustDelete; Close(); return; }
        if (_checkboxRect.Contains(e.Location)) { _whitelistChecked = !_whitelistChecked; Invalidate(); return; }
        if (_btnUninstall.Contains(e.Location)) { Choice = _whitelistChecked ? ConfirmDialog.UserChoice.WhitelistUninstall : ConfirmDialog.UserChoice.Uninstall; Close(); return; }
        if (_btnJustDelete.Contains(e.Location)) { Choice = _whitelistChecked ? ConfirmDialog.UserChoice.WhitelistDelete : ConfirmDialog.UserChoice.JustDelete; Close(); }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) { Choice = ConfirmDialog.UserChoice.JustDelete; Close(); }
        else if (e.KeyCode == Keys.Enter) { Choice = _whitelistChecked ? ConfirmDialog.UserChoice.WhitelistUninstall : ConfirmDialog.UserChoice.Uninstall; Close(); }
    }

    // ═══ 工具 ═══

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

    private static Icon? LoadAppIcon() => ImageCache.AppIcon;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
}
