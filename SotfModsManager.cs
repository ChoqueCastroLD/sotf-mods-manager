// SotfModsManager: installs RedLoader and Sons of the Forest mods from sotf-mods.com. https://sotf-mods.com
using System; using System.Collections; using System.Collections.Generic; using System.Data; using System.Diagnostics; using System.Drawing;
using System.IO; using System.IO.Compression; using System.Linq; using System.Net; using System.Runtime.InteropServices; using System.Text.RegularExpressions;
using System.Threading.Tasks; using System.Web.Script.Serialization; using System.Windows.Forms; using Microsoft.Win32;

class SotfModsManager : Form {
    const string AppVersion = "0.4.0", SiteUrl = "https://sotf-mods.com", LoaderRepo = "https://github.com/ToniMacaroni/RedLoader/releases/", Installed = "Installed";
    const char Play = '\uE768', Gear = '\uE713', Folder = '\uE8B7', Reload = '\uE72C', Remove = '\uE74D', Shield = '\uEA18', CloseIcon = '\uE711', InfoIcon = '\uE946', Busy = '\uE895', Done = '\uE73E', Fail = '\uEA39';
    const int ActionCol = 7;
    // Black and blood red, like the game's own menus. Only fonts that ship with Windows, so the app adds no weight.
    static readonly Color Bg = C(0x0D0B0B), Surface = C(0x171313), Raised = C(0x241D1D), Line = C(0x342A2A), Fg = C(0xEFE8E5), Muted = C(0x9A8E8B), Red = C(0xB3231A), RedHover = C(0xCF3324);
    static readonly string UiFont = HasFont("Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI", IconFont = HasFont("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets"; // Windows 11 faces, Windows 10 fallbacks
    static readonly string[] LoaderFiles = { "version.dll", "doorstop_config.ini", "_Redloader" };
    Label loaderState = new Label { Dock = DockStyle.Fill, ForeColor = Muted, Font = F(9.5f) }, deps = new Label { Dock = DockStyle.Right, Width = 330, ForeColor = Muted, Font = F(9f), TextAlign = ContentAlignment.MiddleLeft };
    Label count = new Label { Dock = DockStyle.Right, Width = 280, ForeColor = Muted, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 12, 0) };
    TextBox gamePath = Box(560), version = Box(140), search = Box(300);
    CheckBox hideInstalled = new CheckBox { Text = "Hide installed", Dock = DockStyle.Left, Width = 150, Padding = new Padding(18, 0, 0, 0), FlatStyle = FlatStyle.Flat };
    FlowLayoutPanel settings = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 224, BackColor = Surface, Padding = new Padding(21, 0, 24, 14), Visible = false };
    DataGridView grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true, AllowUserToAddRows = false, AllowUserToResizeRows = false, RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, BorderStyle = BorderStyle.None, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None, EnableHeadersVisualStyles = false, ColumnHeadersHeight = 40, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Bg, GridColor = Line };
    DataTable mods = new DataTable(); List<Panel> toasts = new List<Panel>(); ToolTip tip = new ToolTip(); Button play, loaderBtn; string latest; bool busy;

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, string lParam);
    [STAThread] static void Main() { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; Application.EnableVisualStyles(); Application.Run(new SotfModsManager()); }

    SotfModsManager() {
        Text = "SotF Mods Manager " + AppVersion; Size = new Size(1040, 700); MinimumSize = new Size(900, 520); StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg; ForeColor = Fg; Font = F(10f); ShowIcon = false; var dark = 1; DwmSetWindowAttribute(Handle, 20, ref dark, 4); // dark title bar
        var site = new LinkLabel { Text = "sotf-mods.com", Dock = DockStyle.Right, Width = 150, TextAlign = ContentAlignment.MiddleCenter, LinkColor = Muted, ActiveLinkColor = Fg, LinkBehavior = LinkBehavior.HoverUnderline };
        site.Click += (s, e) => Process.Start(SiteUrl);
        play = Btn(Play, "Play", PlayGame, Red, 120); play.Dock = DockStyle.Right;
        var header = Strip(80, 40, Bg, new Label { Text = "SotF Mods Manager", Dock = DockStyle.Fill, Font = F(18f, true), TextAlign = ContentAlignment.MiddleLeft }, site, play);
        var loaderGroup = Group(loaderBtn = Btn(Reload, "Install", () => Run("Installing RedLoader", InstallLoader), Red, 170), Btn(Gear, null, () => settings.Visible = !settings.Visible, Raised, 46, "Settings"));
        loaderGroup.Dock = DockStyle.Right;
        var bar = Strip(76, 40, Surface, loaderState, new Label { Text = "RedLoader", Dock = DockStyle.Top, Height = 22, Font = F(10.5f, true) }, deps, loaderGroup);
        var defenderInfo = new Label { Text = InfoIcon.ToString(), Font = new Font(IconFont, 11f), ForeColor = Muted, AutoSize = true, Margin = new Padding(6, 24, 0, 0), Cursor = Cursors.Help };
        tip.SetToolTip(defenderInfo, "Microsoft Defender scans every RedLoader file the first time the game starts,\nwhich can take minutes. Excluding the game folder skips that scan. Asks for admin rights.");
        var browse = Btn(Folder, null, Browse, Raised, 40, "Choose folder"); browse.Height = gamePath.Height; browse.Margin = gamePath.Margin;
        settings.Controls.AddRange(new Control[] { Caption("Game folder"), gamePath, browse, Caption("RedLoader version: a release tag such as 0.8.6, or latest"), version,
            Group(Btn(Remove, "Uninstall RedLoader", UninstallLoader, Raised, 190), Btn(Shield, "Defender exclusion", () => Run("Adding Defender exclusion", DefenderExclusion), Raised, 180),
                Btn(Folder, "Game folder", () => Open(""), Raised, 140), Btn(Folder, "Mods folder", () => Open("Mods"), Raised, 140)), defenderInfo });
        settings.SetFlowBreak(version, true); version.Text = "latest";
        var reload = Btn(Reload, null, LoadMods, Raised, 40, "Reload mod list"); reload.Dock = DockStyle.Right; search.Dock = DockStyle.Left;
        var toolbar = Strip(60, search.Height, Bg, count, reload, hideInstalled, search);
        SendMessage(search.Handle, 0x1501, (IntPtr)1, "Search mods or authors"); // EM_SETCUEBANNER placeholder
        hideInstalled.FlatAppearance.BorderColor = Line; hideInstalled.FlatAppearance.CheckedBackColor = Red;
        var list = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 0, 24, 20) }; list.Controls.Add(grid); SetupGrid();
        Controls.AddRange(new Control[] { list, toolbar, settings, bar, header });
        gamePath.TextChanged += (s, e) => RefreshState(); search.TextChanged += (s, e) => Filter(); hideInstalled.CheckedChanged += (s, e) => Filter(); Resize += (s, e) => LayoutToasts();
        gamePath.Text = FindGame(); RefreshState(); if (Valid) TryDelete(Undo); // leftover from an uninstall that was not undone
        Load += async (s, e) => { LoadMods(); latest = await LatestTag("ToniMacaroni/RedLoader"); RefreshState(); var me = await LatestTag("ChoqueCastroLD/sotf-mods-manager");
            if (Newer(me, AppVersion)) Toast(InfoIcon, "SotF Mods Manager " + me + " is available", "Download", () => Process.Start("https://github.com/ChoqueCastroLD/sotf-mods-manager/releases/latest")); };
    }

    void SetupGrid() {
        foreach (var c in "Name Author Category Downloads Updated Installed Latest Action Id User Slug Deps Info".Split(' '))
            mods.Columns.Add(c, c == "Downloads" ? typeof(int) : c == "Updated" ? typeof(DateTime) : typeof(string));
        typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(grid, true, null); // smooth scrolling on slow PCs
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Bg, ForeColor = Muted, SelectionBackColor = Bg, Font = F(9f, true), Padding = new Padding(8, 0, 8, 0) };
        grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Bg, ForeColor = Fg, SelectionBackColor = Surface, SelectionForeColor = Fg, Padding = new Padding(8, 0, 8, 0) }; grid.RowTemplate.Height = 44;
        foreach (var c in new[] { "Name:26", "Author:13", "Category:14", "Downloads:12:N0", "Updated:12:yyyy-MM-dd", "Installed:11", "Latest:10", "Action:1" }) { var p = c.Split(':');
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = p[0], HeaderText = p[0] == "Action" ? "" : p[0], FillWeight = int.Parse(p[1]), DefaultCellStyle = { Format = p.Length > 2 ? p[2] : "" } }); }
        grid.Columns[ActionCol].AutoSizeMode = DataGridViewAutoSizeColumnMode.None; grid.Columns[ActionCol].Width = 120;
        var bold = grid.Columns[0].DefaultCellStyle.Font = F(10f, true); grid.Columns[3].DefaultCellStyle.Alignment = grid.Columns[3].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        grid.DataSource = mods.DefaultView; mods.DefaultView.Sort = "Downloads DESC";
        grid.CellPainting += (s, e) => { if (e.ColumnIndex != ActionCol || e.RowIndex < 0) return; var a = e.Value as string; var done = a == Installed; // borderless button: red for an update, quiet when done
            e.PaintBackground(e.CellBounds, true); var r = Rectangle.Inflate(e.CellBounds, -8, -7); using (var b = new SolidBrush(done ? Surface : a == "Update" ? Red : Raised)) e.Graphics.FillRectangle(b, r);
            TextRenderer.DrawText(e.Graphics, a, bold, r, done ? Muted : Fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); e.Handled = true; };
        grid.CellMouseMove += (s, e) => grid.Cursor = e.ColumnIndex == ActionCol && Pending(e.RowIndex) ? Cursors.Hand : Cursors.Default;
        grid.CellToolTipTextNeeded += (s, e) => { if (e.RowIndex >= 0) e.ToolTipText = Row(e.RowIndex)["Info"] + "\n\nDouble-click to open the mod page."; };
        grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Process.Start(SiteUrl + "/mods/" + Row(e.RowIndex)["User"] + "/" + Row(e.RowIndex)["Slug"]); };
        grid.CellClick += (s, e) => { if (e.ColumnIndex == ActionCol && Pending(e.RowIndex)) { var r = Row(e.RowIndex); Run("Installing " + r["Name"], t => InstallMod(r, t, new HashSet<string>())); } };
    }
    DataRow Row(int i) { return ((DataRowView)grid.Rows[i].DataBoundItem).Row; }
    bool Pending(int i) { return i >= 0 && (string)Row(i)["Action"] != Installed; }

    async void LoadMods() { // only approved Sons of the Forest mods (type Mod excludes builds); every mod on sotf-mods.com targets RedLoader
        count.Text = "Loading mods";
        try { using (var web = Web()) for (int page = 1, pages = 1; page <= pages; page++) { // the API serves at most 1000 mods per page
                var json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<Dictionary<string, object>>(await web.DownloadStringTaskAsync(SiteUrl + "/api/mods?type=Mod&approved=true&limit=1000&page=" + page));
                pages = Convert.ToInt32(((Dictionary<string, object>)json["meta"])["pages"]); if (page == 1) mods.Rows.Clear(); // cleared only after a good answer, so a failed reload keeps the list
                foreach (Dictionary<string, object> m in (IEnumerable)json["data"]) { var u = (Dictionary<string, object>)m["user"]; var cat = m["category"] as Dictionary<string, object>;
                    mods.Rows.Add(m["name"], u["name"], cat == null ? "" : cat["name"], Convert.ToInt32(m["downloads"]), DateTime.Parse((string)(m["lastReleasedAt"] ?? m["updatedAt"])), "", m["latestVersion"], "",
                        m["mod_id"], u["slug"], m["slug"], string.Join(",", ((IEnumerable)m["dependencies"]).Cast<object>()), m["shortDescription"]); } }
            RefreshState(); grid.CurrentCell = null; if (grid.RowCount > 0) grid.FirstDisplayedScrollingRowIndex = 0; // start at the top of the list
        } catch (Exception ex) { count.Text = ""; Toast(Fail, "Could not load mods: " + ex.Message, "Retry", LoadMods); }
    }

    void RefreshState() {
        var v = LoaderVersion; var upd = Newer(latest, v);
        loaderState.Text = !Valid ? "Sons of the Forest was not found. Choose the game folder in settings." : v == null ? "Not installed"
            : upd ? "Version " + v + " installed, version " + latest + " is available" : "Version " + v + (latest == v ? ", up to date" : " installed");
        deps.Text = (VcInstalled() ? "Visual C++ runtime installed" : "Visual C++ runtime missing, installed with RedLoader") + "\n.NET runtime bundled with RedLoader";
        loaderBtn.Text = v == null ? "Install" : upd ? "Update to " + latest : "Repair"; Tint(loaderBtn, v == null || upd ? Red : Raised); loaderBtn.Enabled = play.Enabled = Valid;
        foreach (DataRow r in mods.Rows) { var id = (string)r["Id"]; var have = Valid ? JsonVersion(@"Mods\" + id + @"\manifest.json", @"Mods\" + id + ".dll") : null;
            r["Installed"] = have ?? ""; r["Action"] = have == null ? "Install" : Newer((string)r["Latest"], have) ? "Update" : Installed; }
        if (!Valid) settings.Visible = true; Filter();
    }
    void Filter() {
        var q = Regex.Replace(search.Text.Trim().Replace("'", "''"), @"[\[\]\*%]", "[$0]");
        mods.DefaultView.RowFilter = "(Name LIKE '*" + q + "*' OR Author LIKE '*" + q + "*')" + (hideInstalled.Checked ? " AND Action <> '" + Installed + "'" : "");
        int n = mods.DefaultView.Count, updates = mods.Select("Action = 'Update'").Length;
        if (mods.Rows.Count > 0) count.Text = Plural(n, "mod") + (updates > 0 ? ", " + Plural(updates, "update") + " available" : "");
    }
    static string Plural(int n, string word) { return n + " " + word + (n == 1 ? "" : "s"); }

    async void Run(string doing, Func<Panel, Task<string>> job) { // one task at a time, shown as a toast that goes from loading to success or error
        if (busy) { Toast(InfoIcon, "Wait for the current task to finish"); return; }
        busy = true; var t = Toast(Busy, doing);
        try { Toast(Done, await job(t), existing: t); }
        catch (Exception ex) { Toast(Fail, doing + " failed: " + (ex is UnauthorizedAccessException ? "no permission to write to the game folder. Run as administrator." : ex.Message), "Retry", () => Run(doing, job), existing: t); }
        finally { busy = false; RefreshState(); }
    }
    async Task<string> InstallLoader(Panel t) {
        EnsureClosed(); var v = version.Text.Trim(); var game = Game;
        if (!VcInstalled()) await Fetch("https://aka.ms/vs/17/release/vc_redist.x64.exe", "Visual C++ runtime", t, f => Process.Start(f, "/install /passive /norestart").WaitForExit(), ".exe"); // official installer, asks for admin rights
        await Fetch(LoaderRepo + (v == "" || v == "latest" ? "latest/download" : "download/" + Uri.EscapeDataString(v)) + "/Redloader.zip", "RedLoader", t, f => Extract(f, game));
        return "RedLoader installed. The first start can take a few minutes.";
    }
    async Task<string> InstallMod(DataRow r, Panel t, HashSet<string> seen) {
        EnsureClosed(); if (LoaderVersion == null) throw new Exception("install RedLoader first.");
        string id = (string)r["Id"], game = Game, dll = Path.Combine(game, "Mods", id + ".dll"); if (!Regex.IsMatch(id, @"^[\w.-]+$")) throw new Exception("invalid mod id.");
        foreach (var dep in ((string)r["Deps"]).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) { // dependencies first
            var d = mods.Rows.Cast<DataRow>().FirstOrDefault(x => (string)x["Id"] == dep); if (d != null && seen.Add(dep) && (string)d["Action"] != Installed) await InstallMod(d, t, seen); }
        await Fetch(SiteUrl + "/api/mods/slug/" + Esc(r["User"]) + "/" + Esc(r["Slug"]) + "/download/" + Esc(r["Latest"]), (string)r["Name"], t, f => {
            bool zip; using (var s = File.OpenRead(f)) zip = s.ReadByte() == 'P' && s.ReadByte() == 'K'; // a mod is a zip or a bare dll
            if (zip) Extract(f, game); else { Directory.CreateDirectory(Path.GetDirectoryName(dll)); File.Copy(f, dll, true); } });
        return r["Name"] + " " + r["Latest"] + " installed";
    }
    async Task Fetch(string url, string what, Panel t, Action<string> apply, string ext = "") { // download to a temp file, apply it off the UI thread, always clean up
        var tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ext);
        try { using (var web = Web()) {
                web.DownloadProgressChanged += (s, e) => Status(t, e.TotalBytesToReceive > 0 ? string.Format("Downloading {0}, {1:0.0} of {2:0.0} MB", what, e.BytesReceived / 1048576.0, e.TotalBytesToReceive / 1048576.0) : "Downloading " + what);
                await web.DownloadFileTaskAsync(url, tmp); }
            Status(t, "Installing " + what); await Task.Run(() => apply(tmp));
        } finally { File.Delete(tmp); }
    }
    static void Extract(string zipFile, string game) { // RedLoader zips and mod zips that already contain Mods/ go to the game root; bare mod zips go into Mods
        var root = Path.GetFullPath(game) + "\\";
        using (var zip = ZipFile.OpenRead(zipFile)) { var into = zip.Entries.Any(e => e.FullName.StartsWith("Mods/") || e.FullName.StartsWith("_Redloader/")) ? "" : "Mods\\";
            foreach (var e in zip.Entries) { var dest = Path.GetFullPath(Path.Combine(root, into + e.FullName));
                if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue; // block zip-slip paths
                if (e.Name == "") { Directory.CreateDirectory(dest); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(dest)); e.ExtractToFile(dest, true); } }
    }
    void UninstallLoader() { // moved aside first so the toast can offer undo instead of a confirmation dialog
        try { EnsureClosed(); var undo = Undo; Directory.CreateDirectory(undo);
            foreach (var n in LoaderFiles) MoveItem(Path.Combine(Game, n), Path.Combine(undo, n));
            Toast(Done, "RedLoader removed. Your mods were kept.", "Undo", () => { foreach (var n in LoaderFiles) MoveItem(Path.Combine(undo, n), Path.Combine(Game, n)); TryDelete(undo); RefreshState(); }, () => TryDelete(undo));
        } catch (Exception ex) { Toast(Fail, "Uninstall failed: " + ex.Message); } RefreshState();
    }
    async Task<string> DefenderExclusion(Panel t) {
        var p = Process.Start(new ProcessStartInfo("powershell", "-NoProfile -Command Add-MpPreference -ExclusionPath '" + Game.Replace("'", "''") + "'") { Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden });
        await Task.Run(() => p.WaitForExit()); if (p.ExitCode != 0) throw new Exception("Defender did not accept the exclusion."); return "Game folder excluded from Microsoft Defender";
    }
    void PlayGame() { if (Running) Toast(InfoIcon, "Sons of the Forest is already running"); else { Process.Start("steam://rungameid/1326470"); Toast(Play, "Starting Sons of the Forest through Steam"); } }

    Panel Toast(char icon, string text, string action = null, Action onAction = null, Action expire = null, Panel existing = null) { // bottom right, stacked, deduplicated
        var t = existing ?? toasts.FirstOrDefault(x => x.AccessibleName == text);
        if (t == null) { t = new Panel { Size = new Size(380, 60), BackColor = Raised, Padding = new Padding(14, 0, 6, 0) }; toasts.Add(t); Controls.Add(t); }
        var old = t.Tag as Timer; if (old != null) old.Dispose(); t.Tag = null; t.AccessibleName = text; t.Controls.Clear(); t.BringToFront();
        var msg = new Label { Name = "msg", Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Padding = new Padding(6, 0, 0, 0) }; tip.SetToolTip(msg, text);
        t.Controls.Add(msg);
        if (action != null) { var a = Btn('\0', action, () => { CloseToast(t); onAction(); }, Raised, 80); a.ForeColor = RedHover; t.Controls.Add(a); }
        if (icon != Busy) t.Controls.Add(Btn(CloseIcon, null, () => CloseToast(t), Raised, 36));
        t.Controls.Add(new Label { Text = icon.ToString(), Dock = DockStyle.Left, Width = 26, Font = new Font(IconFont, 12f), ForeColor = icon == Fail ? RedHover : icon == Busy ? Muted : Fg, TextAlign = ContentAlignment.MiddleLeft });
        if (icon != Busy && icon != Fail) { var tm = new Timer { Interval = 3000 + 60 * text.Length }; tm.Tick += (s, e) => { CloseToast(t); if (expire != null) expire(); }; t.Tag = tm; tm.Start(); } // errors stay until closed
        foreach (Control c in t.Controls) if (c is Button) c.Dock = DockStyle.Right;
        LayoutToasts(); return t;
    }
    void Status(Panel t, string text) { t.Controls["msg"].Text = text; }
    void CloseToast(Panel t) { var tm = t.Tag as Timer; if (tm != null) tm.Dispose(); toasts.Remove(t); Controls.Remove(t); t.Dispose(); LayoutToasts(); }
    void LayoutToasts() { var y = ClientSize.Height - 20; for (var i = toasts.Count - 1; i >= 0; i--) { y -= toasts[i].Height; toasts[i].Location = new Point(ClientSize.Width - toasts[i].Width - 24, y); y -= 8; } }

    // UI building blocks
    static Color C(int rgb) { return Color.FromArgb(rgb >> 16, (rgb >> 8) & 255, rgb & 255); }
    static bool HasFont(string name) { return new System.Drawing.Text.InstalledFontCollection().Families.Any(f => f.Name == name); }
    static Font F(float size, bool bold = false) { return new Font(UiFont + (bold ? " Semibold" : ""), size); } // type scale: 18 title, 10.5 section, 10 body, 9 meta
    static TextBox Box(int w) { return new TextBox { Width = w, BackColor = Raised, ForeColor = Fg, BorderStyle = BorderStyle.FixedSingle, Font = F(10.5f), Margin = new Padding(3, 4, 3, 3) }; }
    static Label Caption(string text) { return new Label { Text = text, AutoSize = true, ForeColor = Muted, Font = F(9f), Margin = new Padding(3, 14, 3, 0), MinimumSize = new Size(900, 0) }; }
    static Panel Strip(int height, int inner, Color back, params Control[] items) { // full-width band; children fill the centered inner height
        var p = new Panel { Dock = DockStyle.Top, Height = height, BackColor = back, Padding = new Padding(24, (height - inner) / 2, 24, (height - inner + 1) / 2) }; p.Controls.AddRange(items); return p; }
    static FlowLayoutPanel Group(params Control[] items) { // segmented button group: buttons joined by 1 px lines
        var g = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Line, Margin = new Padding(3, 12, 0, 0) }; g.Controls.AddRange(items);
        foreach (var c in items) c.Margin = new Padding(c == items[0] ? 0 : 1, 0, 0, 0); return g; }
    Button Btn(char icon, string text, Action a, Color back, int w, string hint = null) { // hint: tooltip for icon-only buttons
        var b = new Button { Text = text, Width = w, Height = 40, FlatStyle = FlatStyle.Flat, ForeColor = Fg, Cursor = Cursors.Hand, Margin = new Padding(0), Font = F(10f, true) };
        b.FlatAppearance.BorderSize = 0; Tint(b, back); b.Click += (s, e) => a(); if (hint != null) tip.SetToolTip(b, hint);
        if (icon != '\0') { b.Image = Glyph(icon); b.TextImageRelation = TextImageRelation.ImageBeforeText; b.ImageAlign = text == null ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleRight; }
        return b;
    }
    static void Tint(Button b, Color back) { b.BackColor = back; b.FlatAppearance.MouseOverBackColor = back == Red ? RedHover : Line; }
    static Bitmap Glyph(char icon) { // icons drawn from the Windows icon font, so they cost no bytes
        var b = new Bitmap(18, 18); using (var g = Graphics.FromImage(b)) using (var f = new Font(IconFont, 12f, GraphicsUnit.Pixel)) using (var br = new SolidBrush(Fg)) {
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; g.DrawString(icon.ToString(), f, br, new RectangleF(0, 0, 18, 18), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }); }
        return b;
    }

    // Game, files and network
    string Game { get { return gamePath.Text.Trim(); } } string Undo { get { return Path.Combine(Game, ".sotfmm-undo"); } }
    bool Valid { get { return File.Exists(Path.Combine(Game, "SonsOfTheForest.exe")); } }
    string LoaderVersion { get { return Valid ? JsonVersion(@"_Redloader\info.json", "version.dll") : null; } }
    static bool Running { get { return Process.GetProcessesByName("SonsOfTheForest").Length > 0; } }
    static void EnsureClosed() { if (Running) throw new Exception("close Sons of the Forest first."); }
    string JsonVersion(string json, string dll) { // version from a manifest; "unknown" if only the dll is there; null if not installed
        json = Path.Combine(Game, json); if (!File.Exists(json)) return File.Exists(Path.Combine(Game, dll)) ? "unknown" : null;
        var m = Regex.Match(File.ReadAllText(json), "\"version\"\\s*:\\s*\"([^\"]+)\""); return m.Success ? m.Groups[1].Value : "unknown";
    }
    static Version Ver(string s) { Version v; return Version.TryParse(Regex.Match(s ?? "", @"\d+(\.\d+){1,3}").Value, out v) ? v : null; }
    static bool Newer(string a, string b) { var x = Ver(a); return x != null && b != null && (Ver(b) == null || x > Ver(b)); } // an unreadable installed version counts as outdated
    static string Esc(object s) { return Uri.EscapeDataString((string)s); }
    static void MoveItem(string from, string to) { if (File.Exists(from)) File.Move(from, to); else if (Directory.Exists(from)) Directory.Move(from, to); }
    static void TryDelete(string dir) { try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { } }
    static WebClient Web() { var w = new WebClient { Encoding = System.Text.Encoding.UTF8 }; w.Headers["User-Agent"] = "SotfModsManager/" + AppVersion; return w; }
    static async Task<string> LatestTag(string repo) { // null when offline or rate limited
        try { using (var web = Web()) return Regex.Match(await web.DownloadStringTaskAsync("https://api.github.com/repos/" + repo + "/releases/latest"), "\"tag_name\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value; } catch { return null; } }
    static bool VcInstalled() { // Visual C++ 2015-2022 x64, needed by RedLoader's native splash screen
        using (var k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64")) return k != null && Equals(k.GetValue("Installed"), 1); }
    static string FindGame() { // Steam uninstall key first, then every Steam library listed in libraryfolders.vdf
        var loc = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1326470", "InstallLocation", null) as string;
        if (loc != null && Directory.Exists(loc)) return loc;
        var vdf = Path.Combine(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", "") as string ?? "", @"steamapps\libraryfolders.vdf");
        var libs = File.Exists(vdf) ? Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"(.+?)\"").Cast<Match>().Select(m => m.Groups[1].Value.Replace(@"\\", @"\")) : new string[0];
        return libs.Select(l => Path.Combine(l, @"steamapps\common\Sons Of The Forest")).FirstOrDefault(Directory.Exists) ?? "";
    }
    void Browse() { using (var d = new FolderBrowserDialog()) if (d.ShowDialog() == DialogResult.OK) gamePath.Text = d.SelectedPath; }
    void Open(string sub) { var p = Path.Combine(Game, sub); if (Valid && Directory.Exists(p)) Process.Start(p); else Toast(Fail, "Folder not found: " + p); }
}
