// Theme and reusable UI building blocks. Only fonts and icons that ship with Windows, so the look adds no weight.
using System; using System.Drawing; using System.Linq; using System.Runtime.InteropServices; using System.Windows.Forms;

partial class SotfModsManager {
    const char Play = '', Gear = '', Folder = '', Reload = '', Remove = '', Shield = '', SearchIcon = '', Prev = '', Next = '',
        CloseIcon = '', InfoIcon = '', Busy = '', Done = '', Fail = '', OpenIcon = '', SortIcon = '';
    // Black and blood red, like the game's own menus. Hot is the hover color of anything drawn on Line.
    static readonly Color Bg = C(0x0D0B0B), Surface = C(0x171313), Raised = C(0x241D1D), Line = C(0x342A2A), Hot = C(0x4A3B3B), Fg = C(0xEFE8E5), Muted = C(0x9A8E8B), Red = C(0xB3231A), RedHover = C(0xCF3324);
    // Segoe UI is hinted for ClearType, so it stays sharp at every size (Segoe UI Variable looks soft in WinForms)
    static readonly string IconFont = HasFont("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets"; // Windows 11, else Windows 10
    static readonly Font Small = F(9f), SmallBold = F(9f, true), Body = F(10f), Bold = F(10f, true), Arrow = new Font(IconFont, 8f);
    const TextFormatFlags Center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
    ToolTip tip = new ToolTip();

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);
    static ContextMenuStrip NewMenu() { return new ContextMenuStrip { Renderer = new ToolStripProfessionalRenderer(new DarkMenu()), ShowImageMargin = false, ShowCheckMargin = true, ForeColor = Fg, Font = Body }; }
    Button Dropdown(char icon, int w, string hint, ContextMenuStrip menu) { // a button that opens a menu below it; its text shows the current choice
        Button b = null; b = Btn(icon, "", () => menu.Show(b, 0, b.Height), Raised, w, hint); b.Dock = DockStyle.Left; b.TextAlign = b.ImageAlign = ContentAlignment.MiddleLeft; b.Padding = new Padding(10, 0, 0, 0); b.AutoEllipsis = true; return b; }
    class DarkMenu : ProfessionalColorTable { // dark menus with the app colors
        public override Color ToolStripDropDownBackground { get { return Raised; } } public override Color MenuBorder { get { return Line; } }
        public override Color MenuItemSelected { get { return Line; } } public override Color MenuItemBorder { get { return Line; } }
        public override Color ImageMarginGradientBegin { get { return Raised; } } public override Color ImageMarginGradientMiddle { get { return Raised; } } public override Color ImageMarginGradientEnd { get { return Raised; } }
        public override Color SeparatorDark { get { return Line; } } public override Color SeparatorLight { get { return Raised; } }
        public override Color CheckBackground { get { return Raised; } } public override Color CheckSelectedBackground { get { return Line; } } public override Color CheckPressedBackground { get { return Line; } }
    }
    static Color C(int rgb) { return Color.FromArgb(rgb >> 16, (rgb >> 8) & 255, rgb & 255); }
    static bool HasFont(string name) { return new System.Drawing.Text.InstalledFontCollection().Families.Any(f => f.Name == name); }
    static Font F(float size, bool bold = false) { return new Font(bold ? "Segoe UI Semibold" : "Segoe UI", size); } // type scale: 16 title, 13 mod, 10.5 section, 10 body, 9 meta
    static T Buffered<T>(T c) where T : Control { // double buffering: no flicker on slow PCs
        typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(c, true, null); return c; }
    static void Fill(Graphics g, Color color, Rectangle r) { using (var b = new SolidBrush(color)) g.FillRectangle(b, r); }
    static TextBox Box() { return new TextBox { BackColor = Raised, ForeColor = Fg, BorderStyle = BorderStyle.None, Font = F(10.5f), Dock = DockStyle.Fill }; }
    static Panel Field(TextBox box, int w, char icon = '\0') { // borderless 40 px input, the same height as buttons, with an optional leading icon
        var p = new Panel { Width = w, Height = 40, BackColor = Raised, Padding = new Padding(12, 10, 10, 0), Margin = new Padding(3, 4, 3, 3), Cursor = Cursors.IBeam }; p.Controls.Add(box);
        if (icon != '\0') p.Controls.Add(new Label { Text = icon.ToString(), Font = new Font(IconFont, 10f), ForeColor = Muted, Dock = DockStyle.Left, Width = 26, Padding = new Padding(0, 2, 0, 0) });
        p.Click += (s, e) => box.Focus(); return p;
    }
    static Label Caption(string text) { return new Label { Text = text, AutoSize = true, ForeColor = Muted, Font = Small, Margin = new Padding(3, 14, 3, 0) }; }
    static Panel Strip(int height, int inner, Color back, params Control[] items) { // full-width band; children fill the centered inner height
        var p = new Panel { Dock = DockStyle.Top, Height = height, BackColor = back, Padding = new Padding(24, (height - inner) / 2, 24, (height - inner + 1) / 2) }; p.Controls.AddRange(items); return p; }
    static FlowLayoutPanel Group(DockStyle dock, params Control[] items) { // segmented button group: items joined by 1 px lines
        var g = new FlowLayoutPanel { Dock = dock, AutoSize = true, WrapContents = false, BackColor = Line, Margin = new Padding(3, 12, 0, 0) }; Join(g, items); return g; }
    static void Join(FlowLayoutPanel g, Control[] items) { g.Controls.AddRange(items); foreach (var c in items) c.Margin = new Padding(c == items[0] ? 0 : 1, 0, 0, 0); }
    Button Btn(char icon, string text, Action a, Color back, int w, string hint = null) { // hint: tooltip for icon-only buttons
        var b = new Button { Text = text, Width = w, Height = 40, FlatStyle = FlatStyle.Flat, ForeColor = Fg, Cursor = Cursors.Hand, Margin = new Padding(0), Font = Bold, TabStop = false };
        b.FlatAppearance.BorderSize = 0; Tint(b, back); b.Click += (s, e) => a(); if (hint != null) tip.SetToolTip(b, hint);
        if (icon != '\0') { b.Image = Glyph(icon); b.TextImageRelation = TextImageRelation.ImageBeforeText; b.ImageAlign = text == null ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleRight; }
        return b;
    }
    static void Tint(Button b, Color back) { b.BackColor = back; b.FlatAppearance.MouseOverBackColor = back == Red ? RedHover : back == Line ? Hot : Line; b.FlatAppearance.MouseDownBackColor = Hot; }
    static Bitmap Glyph(char icon) { // icons drawn from the Windows icon font, so they cost no bytes
        var b = new Bitmap(18, 18); using (var g = Graphics.FromImage(b)) using (var f = new Font(IconFont, 12f, GraphicsUnit.Pixel)) using (var br = new SolidBrush(Fg)) {
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; g.DrawString(icon.ToString(), f, br, new RectangleF(0, 0, 18, 18), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }); }
        return b;
    }
    static string Plural(int n, string word) { return n.ToString("N0") + " " + word + (n == 1 ? "" : "s"); }
}
