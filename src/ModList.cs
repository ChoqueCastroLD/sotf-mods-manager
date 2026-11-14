// The mod list: loads mods from sotf-mods.com, then filters, sorts and pages them, and draws each row with its thumbnail.
using System; using System.Collections; using System.Collections.Generic; using System.Data; using System.Diagnostics; using System.Drawing; using System.IO; using System.Linq;
using System.Text.RegularExpressions; using System.Threading.Tasks; using System.Web.Script.Serialization; using System.Windows.Forms; using System.Windows.Media.Imaging;

partial class SotfModsManager {
    const string Installed = "Installed"; const int ActionCol = 6;
    static readonly string[][] Views = { new[] { "All", "", "No mods match your search" }, new[] { "Not installed", "Action = 'Install'", "Every mod is installed" },
        new[] { "Installed", "Action <> 'Install'", "No mods installed yet" }, new[] { "Updates", "Action = 'Update'", "All your mods are up to date" } }; // tab, filter, empty message
    static readonly Dictionary<string, string> SecondLine = new Dictionary<string, string> { { "Name", "Info" }, { "Author", "Category" }, { "Latest", "Installed" } };
    static readonly string ThumbDir = Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"SotfModsManager\thumbs")).FullName;
    DataTable mods = new DataTable(); Dictionary<string, Image> thumbs = new Dictionary<string, Image>();
    DataGridView grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToResizeRows = false, AllowUserToResizeColumns = false, RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, BorderStyle = BorderStyle.None, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, ScrollBars = ScrollBars.None,
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None, EnableHeadersVisualStyles = false, ColumnHeadersHeight = 40, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Bg, GridColor = Line };
    Label showing = new Label { Dock = DockStyle.Fill, ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft }, pageLabel = new Label { Width = 120, Height = 40, BackColor = Surface, ForeColor = Muted, TextAlign = ContentAlignment.MiddleCenter };
    Button[] tabs; Button prev, next; int view, first; string empty = "Loading mods";

    Control[] SetupList() { // returns the list, its pager and its toolbar, docked bottom to top
        foreach (var c in "Name Author Category Downloads Updated Installed Latest Action Id User Slug Deps Info Image".Split(' '))
            mods.Columns.Add(c, c == "Downloads" ? typeof(int) : c == "Updated" ? typeof(DateTime) : typeof(string));
        mods.DefaultView.Sort = "Downloads DESC";
        typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(grid, true, null); // no flicker on slow PCs
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Bg, ForeColor = Muted, SelectionBackColor = Bg, Font = F(9f, true), Padding = new Padding(8, 0, 8, 0) };
        grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Bg, ForeColor = Fg, SelectionBackColor = Surface, SelectionForeColor = Fg, Padding = new Padding(8, 0, 8, 0) }; grid.RowTemplate.Height = 64;
        foreach (var c in new[] { "Thumb::0", "Name:Mod:38", "Author:Author:16", "Downloads:Downloads:13:N0", "Updated:Updated:12:yyyy-MM-dd", "Latest:Version:13", "Action::0" }) { var p = c.Split(':'); var fix = p[2] == "0";
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = p[0], HeaderText = p[1], FillWeight = fix ? 1 : int.Parse(p[2]), Width = p[0] == "Thumb" ? 124 : 130, DefaultCellStyle = { Format = p.Length > 3 ? p[3] : "" },
                AutoSizeMode = fix ? DataGridViewAutoSizeColumnMode.None : DataGridViewAutoSizeColumnMode.NotSet, SortMode = fix ? DataGridViewColumnSortMode.NotSortable : DataGridViewColumnSortMode.Programmatic }); }
        var downloads = grid.Columns["Downloads"]; downloads.DefaultCellStyle.Alignment = downloads.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        downloads.HeaderCell.SortGlyphDirection = SortOrder.Descending; grid.Columns["Name"].DefaultCellStyle.Font = F(10f, true);
        grid.CellPainting += PaintCell; grid.Resize += (s, e) => Render(); grid.MouseWheel += (s, e) => Go(e.Delta < 0 ? 1 : -1);
        grid.Paint += (s, e) => { if (grid.Rows.Count == 0) TextRenderer.DrawText(e.Graphics, mods.Rows.Count == 0 ? empty : Views[view][2], Font, grid.ClientRectangle, Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); };
        grid.CellMouseEnter += (s, e) => { if (e.RowIndex >= 0) grid.Rows[e.RowIndex].Selected = true; grid.Cursor = e.ColumnIndex == ActionCol && Pending(e.RowIndex) ? Cursors.Hand : Cursors.Default; }; // hover highlight
        grid.MouseLeave += (s, e) => grid.ClearSelection();
        grid.ColumnHeaderMouseClick += (s, e) => Sort(grid.Columns[e.ColumnIndex]);
        grid.CellToolTipTextNeeded += (s, e) => { if (e.RowIndex >= 0 && e.ColumnIndex < 2) e.ToolTipText = "Double-click to open the mod page."; };
        grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Process.Start(SiteUrl + "/mods/" + Row(e.RowIndex)["User"] + "/" + Row(e.RowIndex)["Slug"]); };
        grid.CellClick += (s, e) => { if (e.ColumnIndex == ActionCol && Pending(e.RowIndex)) { var r = Row(e.RowIndex); Run("Installing " + r["Name"], t => InstallMod(r, t, new HashSet<string>())); } };
        tabs = Views.Select((v, i) => { var b = Btn('\0', v[0], () => { view = i; Filter(true); }, Surface, 0); b.AutoSize = true; b.Padding = new Padding(10, 0, 10, 0); return b; }).ToArray();
        prev = Btn(Prev, null, () => Go(-1), Surface, 40, "Previous page"); next = Btn(Next, null, () => Go(1), Surface, 40, "Next page");
        var reload = Btn(Reload, null, LoadMods, Raised, 40, "Reload mod list"); reload.Dock = DockStyle.Right;
        var searchField = Field(search, 280, SearchIcon); searchField.Dock = DockStyle.Left; search.TextChanged += (s, e) => Filter(true);
        SendMessage(search.Handle, 0x1501, (IntPtr)1, "Search mods or authors"); // EM_SETCUEBANNER placeholder
        var list = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 0, 24, 0) }; list.Controls.Add(grid);
        var pager = Strip(64, 40, Bg, showing, Group(DockStyle.Right, prev, pageLabel, next)); pager.Dock = DockStyle.Bottom;
        return new Control[] { list, pager, Strip(64, 40, Bg, Group(DockStyle.Right, tabs), new Panel { Dock = DockStyle.Right, Width = 8 }, reload, searchField) };
    }

    async void LoadMods() { // approved Sons of the Forest mods only (type Mod leaves out builds); every mod on sotf-mods.com runs on RedLoader
        empty = "Loading mods"; grid.Invalidate();
        try { using (var web = Web()) for (int page = 1, pages = 1; page <= pages; page++) { // the API serves at most 1000 mods per page
                var json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<Dictionary<string, object>>(await web.DownloadStringTaskAsync(SiteUrl + "/api/mods?type=Mod&approved=true&limit=1000&page=" + page));
                pages = Convert.ToInt32(((Dictionary<string, object>)json["meta"])["pages"]); if (page == 1) mods.Rows.Clear(); // cleared only after a good answer, so a failed reload keeps the list
                foreach (Dictionary<string, object> m in (IEnumerable)json["data"]) { var u = (Dictionary<string, object>)m["user"]; var cat = m["category"] as Dictionary<string, object>;
                    mods.Rows.Add(m["name"], u["name"], cat == null ? "" : cat["name"], Convert.ToInt32(m["downloads"]), DateTime.Parse((string)(m["lastReleasedAt"] ?? m["updatedAt"])), "", m["latestVersion"], "",
                        m["mod_id"], u["slug"], m["slug"], string.Join(",", ((IEnumerable)m["dependencies"]).Cast<object>()), m["shortDescription"],
                        Regex.Replace(m["imageUrl"] as string ?? "", @"original\.webp$", "320.webp")); } } // the site keeps a 320 px copy of every image
            RefreshState();
        } catch (Exception ex) { empty = "Could not load mods"; grid.Invalidate(); Toast(Fail, "Could not load mods: " + ex.Message, "Retry", LoadMods); }
    }

    void Filter(bool top = false) { // search, tab and tab counts; top goes back to the first page
        var q = Regex.Replace(search.Text.Trim().Replace("'", "''"), @"[\[\]\*%]", "[$0]");
        mods.DefaultView.RowFilter = "(Name LIKE '*" + q + "*' OR Author LIKE '*" + q + "*')" + (view > 0 ? " AND " + Views[view][1] : "");
        for (var i = 0; i < tabs.Length; i++) { tabs[i].Text = Views[i][0] + "  " + mods.Select(Views[i][1]).Length; Tint(tabs[i], i == view ? Raised : Surface); tabs[i].ForeColor = i == view ? Fg : Muted; }
        if (top) first = 0; Render();
    }
    void Sort(DataGridViewColumn c) { // header click: ascending first, then it toggles
        if (c.SortMode == DataGridViewColumnSortMode.NotSortable) return; var asc = c.HeaderCell.SortGlyphDirection != SortOrder.Ascending;
        foreach (DataGridViewColumn x in grid.Columns) x.HeaderCell.SortGlyphDirection = SortOrder.None;
        c.HeaderCell.SortGlyphDirection = asc ? SortOrder.Ascending : SortOrder.Descending; mods.DefaultView.Sort = c.Name + (asc ? "" : " DESC"); Filter(true);
    }
    int PageSize { get { return Math.Max(1, (grid.ClientSize.Height - grid.ColumnHeadersHeight) / grid.RowTemplate.Height); } } // as many rows as fit, so the list never scrolls
    void Go(int pages) { first += pages * PageSize; Render(); }
    void Render() { // only the visible page becomes grid rows, so drawing and thumbnails stay cheap
        var dv = mods.DefaultView; int size = PageSize, total = dv.Count, page = Math.Max(0, Math.Min(first / size, (total - 1) / size)); first = page * size;
        grid.Rows.Clear();
        for (var i = first; i < Math.Min(total, first + size); i++) { var r = dv[i].Row; grid.Rows[grid.Rows.Add(null, r["Name"], r["Author"], r["Downloads"], r["Updated"], r["Latest"], r["Action"])].Tag = r; }
        grid.ClearSelection(); prev.Enabled = page > 0; next.Enabled = first + size < total; pageLabel.Text = "Page " + (page + 1) + " of " + Math.Max(1, (total + size - 1) / size);
        showing.Text = total == 0 ? "" : "Showing " + (first + 1) + " to " + Math.Min(total, first + size) + " of " + Plural(total, "mod");
    }
    DataRow Row(int i) { return (DataRow)grid.Rows[i].Tag; }
    bool Pending(int i) { return i >= 0 && (string)Row(i)["Action"] != Installed; }

    void PaintCell(object s, DataGridViewCellPaintingEventArgs e) { // thumbnail, two-line text and a borderless action button
        if (e.RowIndex < 0) return; var r = Row(e.RowIndex); var col = grid.Columns[e.ColumnIndex].Name; string key, second = SecondLine.TryGetValue(col, out key) ? r[key] as string : null;
        if (col != "Thumb" && col != "Action" && string.IsNullOrEmpty(second)) return; // plain cells keep the default drawing
        e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border); var g = e.Graphics;
        if (col == "Thumb") { var box = Rectangle.Inflate(e.CellBounds, -12, -4); using (var b = new SolidBrush(Raised)) g.FillRectangle(b, box); var img = Thumb(r["Image"] as string); if (img != null) g.DrawImage(img, box); }
        else if (col == "Action") { var a = (string)e.Value; var box = Rectangle.Inflate(e.CellBounds, -10, -14); if (a != Installed) using (var b = new SolidBrush(a == "Update" ? Red : Raised)) g.FillRectangle(b, box);
            TextRenderer.DrawText(g, a, grid.Columns["Name"].DefaultCellStyle.Font, box, a == Installed ? Muted : Fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); }
        else { var box = Rectangle.Inflate(e.CellBounds, -8, -9); var h = box.Height / 2; const TextFormatFlags f = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter;
            TextRenderer.DrawText(g, (string)e.FormattedValue, e.CellStyle.Font, new Rectangle(box.X, box.Y, box.Width, h), Fg, f);
            TextRenderer.DrawText(g, second, Small, new Rectangle(box.X, box.Y + h, box.Width, h), Muted, f); }
        e.Handled = true;
    }
    Image Thumb(string url) { // lazy: a thumbnail is fetched the first time it is drawn, then kept in memory and on disk
        Image img = null; if (!string.IsNullOrEmpty(url) && !thumbs.TryGetValue(url, out img)) { thumbs[url] = null; LoadThumb(url); } return img; }
    async void LoadThumb(string url) { try { thumbs[url] = await Task.Run(() => Decode(url)); grid.Invalidate(); } catch { } } // a missing image just stays blank
    static Image Decode(string url) { // Windows decodes WebP itself (WIC), so no image library is needed
        var file = Path.Combine(ThumbDir, Regex.Replace(url, @"\W", "_"));
        if (!File.Exists(file)) using (var web = Web()) File.WriteAllBytes(file, web.DownloadData(url));
        try { var src = new BitmapImage(); src.BeginInit(); src.UriSource = new Uri(file); src.DecodePixelWidth = 100; src.CacheOption = BitmapCacheOption.OnLoad; src.EndInit();
            var bmp = new BmpBitmapEncoder(); bmp.Frames.Add(BitmapFrame.Create(src)); var ms = new MemoryStream(); bmp.Save(ms); return new Bitmap(ms);
        } catch { File.Delete(file); throw; } // a damaged cache file is fetched again next time
    }
}
