using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace GunAPP;

/// <summary>
/// 自定义通知弹窗（替代 MessageBox）
/// </summary>
internal class NotifyForm : Form
{
    private static readonly Color BgColor = Color.FromArgb(252, 252, 252);
    private static readonly Color TextMain = Color.FromArgb(40, 40, 40);
    private static readonly Color TextSub = Color.FromArgb(120, 120, 120);
    private static readonly Color GreenBtn = Color.FromArgb(76, 175, 80);
    private static readonly Color CloseBtnHover = Color.FromArgb(232, 17, 35);

    private Rectangle _closeBtnRect;
    private Rectangle _dragRect;
    private bool _hoverClose;
    private bool _dragging;
    private Point _dragStart;
    private RectangleF _btnOkRect;
    private RectangleF _btnCancelRect;
    private bool _hoverOk;
    private bool _hoverCancel;

    private const int TitleBarH = 36;

    private readonly string _title;
    private readonly string _message;
    private readonly Image? _donateImage;
    private readonly bool _showDonate;
    private readonly string _confirmText;
    private readonly string _cancelText;
    public bool Confirmed { get; private set; }

    public NotifyForm(string title, string message, Image? donateImage = null, string confirmText = "确定", string cancelText = "取消")
    {
        _title = title;
        _message = message;
        _donateImage = donateImage;
        _showDonate = donateImage != null;
        _confirmText = confirmText;
        _cancelText = cancelText;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgColor;
        DoubleBuffered = true;
        Size = _showDonate ? new Size(500, 760) : new Size(360, 220);
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
            g.DrawString("快滚!APP!", tf, tb, 42, 10);
        }
        else
        {
            using var tf = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            using var tb = new SolidBrush(TextMain);
            g.DrawString("快滚!APP!", tf, tb, 14, 10);
        }

        // 关闭按钮
        DrawCloseButton(g);

        float y = TitleBarH + 16;

        // 消息内容
        using var msgFont = new Font("Microsoft YaHei UI", 9.5f);
        using var msgBrush = new SolidBrush(TextMain);
        var msgSf = new StringFormat { Alignment = StringAlignment.Center };
        var msgRect = new RectangleF(20, y, Width - 40, 100);
        g.DrawString(_message, msgFont, msgBrush, msgRect, msgSf);
        y += 110;

        // 赞赏码
        if (_showDonate && _donateImage != null)
        {
            // 分隔线
            g.DrawLine(sepPen, 30, y, Width - 30, y);
            y += 12;

            // 标题
            using var donateFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            using var donateBrush = new SolidBrush(TextMain);
            var donateSf = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString("赞赏支持", donateFont, donateBrush, new RectangleF(0, y, Width, 20), donateSf);
            y += 24;

            // 图片
            int imgSize = 350;
            int imgX = (Width - imgSize) / 2;
            g.DrawImage(_donateImage, new Rectangle(imgX, (int)y, imgSize, imgSize));
            y += imgSize + 6;

            // 提示
            using var tipFont = new Font("Microsoft YaHei UI", 8f);
            using var tipBrush = new SolidBrush(Color.FromArgb(160, 160, 160));
            var tipSf = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString("如果觉得好用，请作者喝杯咖啡", tipFont, tipBrush, new RectangleF(0, y, Width, 16), tipSf);
        }

        // 按钮
        float btnY = Height - 52;
        if (_showDonate)
        {
            // 确认对话框：两个按钮
            _btnOkRect = new RectangleF(Width / 2 - 130, btnY, 120, 34);
            _btnCancelRect = new RectangleF(Width / 2 + 10, btnY, 120, 34);
            DrawButton(g, _btnOkRect, _confirmText, GreenBtn, _hoverOk);
            DrawButton(g, _btnCancelRect, _cancelText, Color.FromArgb(230, 230, 230), _hoverCancel, false);
        }
        else
        {
            // 普通通知：单个确定按钮
            _btnOkRect = new RectangleF((Width - 120) / 2, btnY, 120, 34);
            DrawButton(g, _btnOkRect, _confirmText, GreenBtn, _hoverOk);
        }
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

    private void DrawButton(Graphics g, RectangleF rect, string text, Color color, bool hover, bool primary = true)
    {
        using var path = RoundedRect(new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height), 8);
        using var brush = new SolidBrush(hover ? Darken(color, 0.08f) : color);
        g.FillPath(brush, path);
        using var font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
        using var tb = new SolidBrush(primary ? Color.White : TextMain);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, tb, rect, sf);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging) { Location += (Size)(e.Location - (Size)_dragStart); return; }

        bool c = _closeBtnRect.Contains(e.Location);
        bool o = _btnOkRect.Contains(e.Location);
        bool ca = _btnCancelRect.Contains(e.Location);

        if (c != _hoverClose || o != _hoverOk || ca != _hoverCancel)
        {
            _hoverClose = c;
            _hoverOk = o;
            _hoverCancel = ca;
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
        if (_btnOkRect.Contains(e.Location)) { Confirmed = true; Close(); return; }
        if (_btnCancelRect.Contains(e.Location)) { Confirmed = false; Close(); return; }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Escape) Close();
    }

    // ═══ 静态方法 ═══

    public static void Show(string title, string message)
    {
        using var form = new NotifyForm(title, message);
        form.ShowDialog();
    }

    public static bool ShowConfirm(string title, string message, string confirmText, string cancelText)
    {
        using var form = new NotifyForm(title, message, null, confirmText, cancelText);
        form.ShowDialog();
        return form.Confirmed;
    }

    public static void ShowWithDonate(string title, string message, Image? donateImage)
    {
        using var form = new NotifyForm(title, message, donateImage);
        form.ShowDialog();
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

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
}
