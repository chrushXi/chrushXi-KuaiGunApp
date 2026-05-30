using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace GunAPP;

/// <summary>
/// 白名单管理界面
/// </summary>
internal class WhitelistForm : Form
{
    private static readonly Color BgColor = Color.FromArgb(252, 252, 252);
    private static readonly Color TitleBarColor = Color.FromArgb(245, 245, 245);
    private static readonly Color TextMain = Color.FromArgb(40, 40, 40);
    private static readonly Color TextSub = Color.FromArgb(120, 120, 120);
    private static readonly Color RedBtn = Color.FromArgb(239, 83, 80);
    private static readonly Color GreenBtn = Color.FromArgb(76, 175, 80);
    private static readonly Color ItemBg = Color.FromArgb(248, 248, 248);
    private static readonly Color ItemHover = Color.FromArgb(240, 240, 240);
    private static readonly Color CloseBtnHover = Color.FromArgb(232, 17, 35);

    private Rectangle _closeBtnRect;
    private Rectangle _dragRect;
    private bool _hoverClose;
    private bool _dragging;
    private Point _dragStart;

    private List<WhitelistItem> _items = [];
    private int _hoverIndex = -1;
    private RectangleF _btnClearAllRect;
    private bool _hoverClearAll;

    private const int TitleBarH = 36;
    private const int ItemH = 48;
    private const int PadX = 16;

    public WhitelistForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgColor;
        DoubleBuffered = true;
        Size = new Size(420, 400);

        LoadItems();
        Icon = ImageCache.AppIcon;
    }

    private void LoadItems()
    {
        _items = Whitelist.GetAll().Select(path => new WhitelistItem
        {
            ExePath = path,
            ProgramName = Path.GetFileNameWithoutExtension(path),
        }).ToList();
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
        using var titleBrush = new SolidBrush(TitleBarColor);
        g.FillRectangle(titleBrush, 0, 0, Width, TitleBarH);
        using var sepPen = new Pen(Color.FromArgb(230, 230, 230));
        g.DrawLine(sepPen, 0, TitleBarH, Width, TitleBarH);

        // Logo + 标题
        var logo = LoadLogo();
        if (logo != null)
        {
            g.DrawImage(logo, new Rectangle(12, 6, 24, 24));
            using var tf = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            using var tb = new SolidBrush(TextMain);
            g.DrawString("白名单管理", tf, tb, 42, 10);
        }
        else
        {
            using var tf = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            using var tb = new SolidBrush(TextMain);
            g.DrawString("白名单管理", tf, tb, 14, 10);
        }

        // 关闭按钮
        DrawCloseButton(g);

        // 内容区域
        float y = TitleBarH + 12;

        if (_items.Count == 0)
        {
            // 空状态
            using var emptyFont = new Font("Microsoft YaHei UI", 10f);
            using var emptyBrush = new SolidBrush(TextSub);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("白名单为空", emptyFont, emptyBrush, new RectangleF(0, y, Width, 60), sf);
            y += 70;
        }
        else
        {
            // 列表
            var hoverRects = new List<RectangleF>();

            for (int i = 0; i < _items.Count; i++)
            {
                var itemRect = new RectangleF(PadX, y, Width - PadX * 2, ItemH - 4);
                hoverRects.Add(itemRect);

                // 悬停背景
                if (i == _hoverIndex)
                {
                    using var hoverPath = RoundedRect(new Rectangle((int)itemRect.X, (int)itemRect.Y, (int)itemRect.Width, (int)itemRect.Height), 8);
                    using var hoverBrush = new SolidBrush(ItemHover);
                    g.FillPath(hoverBrush, hoverPath);
                }

                // 程序名
                using var nameFont = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
                using var nameBrush = new SolidBrush(TextMain);
                g.DrawString(_items[i].ProgramName, nameFont, nameBrush, PadX + 12, y + 6);

                // 路径
                using var pathFont = new Font("Microsoft YaHei UI", 8f);
                using var pathBrush = new SolidBrush(TextSub);
                g.DrawString(_items[i].ExePath, pathFont, pathBrush, PadX + 12, y + 24);

                // 删除按钮
                var delRect = new RectangleF(Width - PadX - 28, y + 12, 24, 24);
                _items[i].DeleteRect = delRect;
                DrawDeleteButton(g, delRect, _items[i].HoverDelete);

                y += ItemH;
            }

            _itemHoverRects = hoverRects;
            y += 8;
        }

        // 底部按钮
        if (_items.Count > 0)
        {
            // 清空全部按钮：居中、大尺寸
            float btnW = Width - PadX * 2;
            _btnClearAllRect = new RectangleF(PadX, y, btnW, 40);
            DrawButton(g, _btnClearAllRect, "清空全部", RedBtn, _hoverClearAll);
        }
    }

    private List<RectangleF> _itemHoverRects = [];

    private void DrawCloseButton(Graphics g)
    {
        var r = _closeBtnRect;
        using var path = RoundedRect(r, 6);
        if (_hoverClose) { using var b = new SolidBrush(CloseBtnHover); g.FillPath(b, path); }
        using var pen = new Pen(_hoverClose ? Color.White : Color.FromArgb(180, 180, 180), 1.5f);
        g.DrawLine(pen, r.X + 8, r.Y + 8, r.Right - 8, r.Bottom - 8);
        g.DrawLine(pen, r.Right - 8, r.Y + 8, r.X + 8, r.Bottom - 8);
    }

    private void DrawDeleteButton(Graphics g, RectangleF rect, bool hover)
    {
        using var pen = new Pen(hover ? RedBtn : Color.FromArgb(180, 180, 180), 1.5f);
        float cx = rect.X + rect.Width / 2;
        float cy = rect.Y + rect.Height / 2;
        g.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5);
        g.DrawLine(pen, cx + 5, cy - 5, cx - 5, cy + 5);
    }

    private void DrawButton(Graphics g, RectangleF rect, string text, Color color, bool hover)
    {
        using var path = RoundedRect(new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height), 8);
        using var brush = new SolidBrush(hover ? Darken(color, 0.08f) : color);
        g.FillPath(brush, path);
        using var font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
        using var tb = new SolidBrush(Color.White);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, tb, rect, sf);
    }

    // ═══ 交互 ═══

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging) { Location += (Size)(e.Location - (Size)_dragStart); return; }

        bool c = _closeBtnRect.Contains(e.Location);
        bool ca = _btnClearAllRect.Contains(e.Location);

        int newHover = -1;
        for (int i = 0; i < _itemHoverRects.Count; i++)
        {
            if (_itemHoverRects[i].Contains(e.Location)) { newHover = i; break; }
        }

        // 删除按钮悬停
        for (int i = 0; i < _items.Count; i++)
        {
            _items[i].HoverDelete = _items[i].DeleteRect.Contains(e.Location);
        }

        if (c != _hoverClose || ca != _hoverClearAll || newHover != _hoverIndex)
        {
            _hoverClose = c;
            _hoverClearAll = ca;
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

        // 删除单个
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].DeleteRect.Contains(e.Location))
            {
                Whitelist.Remove(_items[i].ExePath);
                LoadItems();
                Invalidate();
                return;
            }
        }

        // 清空全部
        if (_btnClearAllRect.Contains(e.Location) && _items.Count > 0)
        {
            var confirmed = NotifyForm.ShowConfirm("白名单管理", "确定要清空白名单吗？", "清空", "取消");
            if (confirmed)
            {
                Whitelist.Clear();
                LoadItems();
                Invalidate();
            }
            return;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) Close();
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

internal class WhitelistItem
{
    public string ExePath { get; set; } = "";
    public string ProgramName { get; set; } = "";
    public RectangleF DeleteRect { get; set; }
    public bool HoverDelete { get; set; }
}
