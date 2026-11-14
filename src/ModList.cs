// The mod list: loads mods from sotf-mods.com, then filters, sorts and pages them, and draws every row itself.
using System; using System.Collections; using System.Collections.Generic; using System.Data; using System.Drawing; using System.Linq;
using System.Text.RegularExpressions; using System.Web.Script.Serialization; using System.Windows.Forms;

partial class SotfModsManager {
    const string Installed = "Installed"; const int ActionCol = 5;
    static readonly string[][] Views = { new[] { "All", "", "No mods match your search" }, new[] { "Not installed", "Action = 'Install'", "Every mod is installed" },
        new[] { "Installed", "Action <> 'Install'", "No mods installed yet" }, new[] { "Updates", "Action = 'Update'", "All your mods are up to date" } }; // tab, filter, empty message
    static readonly Dictionary<string, string> SecondLine = new Dictionary<string, string> { { "Name", "Byline" }, { "Latest", "Installed" } };
    DataTable mods = new DataTable();
    DataGridView grid = Buffered(new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToResizeRows = false, AllowUserToResizeColumns = false, RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, BorderStyle = BorderStyle.None, CellBorderStyle = DataGridViewCellBorderStyle.None, ScrollBars = ScrollBars.None,
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None, EnableHeadersVisualStyles = false, ColumnHeadersHeight = 40, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Bg, StandardTab = true });
    Label showing = new Label { Dock = DockStyle.Fill, ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft };
    FlowLayoutPanel pages; Button[] tabs; Button prev, next; int view, first, hoverRow = -1, hoverCol = -1; bool rendering; string empty = "Loading mods";

    Control[] SetupList() { // returns the list with its details panel, and the toolbar above them
        foreach (var c in "Name Author Byline Downloads Updated Installed Latest Action Id User Slug Deps Clashes Gallery Description".Split(' '))
            mods.Columns.Add(c, c == "Downloads" ? typeof(int) : c == "Updated" ? typeof(DateTime) : typeof(string));
        mods.DefaultView.Sort = "Downloads DESC";
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Bg, ForeColor = Muted, SelectionBackColor = Bg, Font = F(9f, true), Padding = new Padding(8, 0, 8, 0) };
        grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Bg, ForeColor = Fg, Font = Body }; grid.RowTemplate.Height = 68;
        foreach (var c in new[] { "Thumb::0", "Name:Mod:40", "Downloads:Downloads:15:N0", "Updated:Updated:15:yyyy-MM-dd", "Latest:Version:15", "Action::0" }) { var p = c.Split(':'); var fix = p[2] == "0";
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = p[0], HeaderText = p[1], FillWeight = fix ? 1 : int.Parse(p[2]), Width = fix ? 124 : 100, DefaultCellStyle = { Format = p.Length > 3 ? p[3] : "" },
                AutoSizeMode = fix ? DataGridViewAutoSizeColumnMode.None : DataGridViewAutoSizeColumnMode.NotSet, SortMode = fix ? DataGridViewColumnSortMode.NotSortable : DataGridViewColumnSortMode.Programmatic }); }
        grid.Columns["Downloads"].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight; grid.Columns["Downloads"].HeaderCell.SortGlyphDirection = SortOrder.Descending; grid.Columns["Name"].DefaultCellStyle.Font = Bold;
        grid.CellPainting += PaintCell; grid.Resize += (s, e) => Render(); grid.MouseWheel += (s, e) => Go(Page + (e.Delta < 0 ? 1 : -1));
        grid.Paint += (s, e) => { if (grid.Rows.Count == 0) TextRenderer.DrawText(e.Graphics, mods.Rows.Count == 0 ? empty : Views[search.Text.Trim() == "" ? view : 0][2], Body, grid.ClientRectangle, Muted, Center); };
        grid.CellMouseMove += (s, e) => Hover(e.RowIndex, e.ColumnIndex); grid.MouseLeave += (s, e) => Hover(-1, -1);
        grid.KeyDown += (s, e) => { if (e.KeyCode == Keys.PageDown || e.KeyCode == Keys.PageUp) { Go(Page + (e.KeyCode == Keys.PageDown ? 1 : -1)); e.Handled = true; } };
        grid.ColumnHeaderMouseClick += (s, e) => Sort(grid.Columns[e.ColumnIndex]); grid.CurrentCellChanged += (s, e) => { if (!rendering) ShowMod(Current); };
        grid.CellClick += (s, e) => { if (e.ColumnIndex == ActionCol && Pending(e.RowIndex)) Act(Row(e.RowIndex)); };
        tabs = Views.Select((v, i) => { var b = Btn('\0', v[0], () => { view = i; Filter(true); }, Surface, 0); b.AutoSize = true; b.Padding = new Padding(10, 0, 10, 0); return b; }).ToArray();
        prev = Btn(Prev, null, () => Go(Page - 1), Surface, 40, "Previous page (Page Up)"); next = Btn(Next, null, () => Go(Page + 1), Surface, 40, "Next page (Page Down)"); pages = Group(DockStyle.Right);
        var reload = Btn(Reload, null, LoadMods, Raised, 40, "Reload mod list"); reload.Dock = DockStyle.Right;
        var searchField = Field(search, 300, SearchIcon); searchField.Dock = DockStyle.Left; search.TextChanged += (s, e) => Filter(true);
        SendMessage(search.Handle, 0x1501, (IntPtr)1, "Search mods or authors"); // EM_SETCUEBANNER placeholder
        var list = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 0, 16, 0) }; list.Controls.AddRange(new Control[] { grid, Strip(64, 40, Bg, showing, pages) }); list.Controls[1].Dock = DockStyle.Bottom;
        var body = new Panel { Dock = DockStyle.Fill }; body.Controls.AddRange(new Control[] { list, SetupDetails() });
        return new Control[] { body, Strip(64, 40, Bg, Group(DockStyle.Right, tabs), new Panel { Dock = DockStyle.Right, Width = 8 }, reload, searchField, new Panel { Dock = DockStyle.Left, Width = 8 }, SetupProfiles()) };
    }

    async void LoadMods() { // approved Sons of the Forest mods only (type Mod leaves out builds); every mod on sotf-mods.com runs on RedLoader
        empty = "Loading mods"; grid.Invalidate();
        try { using (var web = Web()) for (int page = 1, count = 1; page <= count; page++) { // the API serves at most 1000 mods per page
                var json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<Dictionary<string, object>>(await web.DownloadStringTaskAsync(SiteUrl + "/api/mods?type=Mod&approved=true&limit=1000&page=" + page));
                count = Convert.ToInt32(((Dictionary<string, object>)json["meta"])["pages"]); if (page == 1) mods.Rows.Clear(); // cleared only after a good answer, so a failed reload keeps the list
                foreach (Dictionary<string, object> m in (IEnumerable)json["data"]) { var u = (Dictionary<string, object>)m["user"]; var cat = m["category"] as Dictionary<string, object>;
                    var shots = new[] { m["imageUrl"] as string }.Concat(((IEnumerable)m["images"] ?? new object[0]).Cast<Dictionary<string, object>>().Select(i => i["url"] as string)).Where(x => !string.IsNullOrEmpty(x)).Distinct();
                    mods.Rows.Add(m["name"], u["name"], "by " + u["name"] + (cat == null ? "" : " in " + cat["name"]), Convert.ToInt32(m["downloads"]), DateTime.Parse((string)(m["lastReleasedAt"] ?? m["updatedAt"])), "", m["latestVersion"], "",
                        m["mod_id"], u["slug"], m["slug"], IdList(m, "dependencies"), IdList(m, "incompatibilities"), string.Join("\n", shots), m["description"] ?? m["shortDescription"]); } }
            RefreshState();
        } catch (Exception ex) { empty = "Could not load mods"; grid.Invalidate(); Toast(Fail, "Could not load mods: " + ex.Message, "Retry", LoadMods); }
    }

    void Filter(bool top = false) { // search, tab and tab counts; top goes back to the first page
        var q = Regex.Replace(search.Text.Trim().Replace("'", "''"), @"[\[\]\*%]", "[$0]");
        mods.DefaultView.RowFilter = "(Name LIKE '*" + q + "*' OR Author LIKE '*" + q + "*')" + (view > 0 ? " AND " + Views[view][1] : "");
        for (var i = 0; i < tabs.Length; i++) { tabs[i].Text = Views[i][0] + "  " + mods.Select(Views[i][1]).Length; Tint(tabs[i], i == view ? Line : Surface); tabs[i].ForeColor = i == view ? Fg : Muted; }
        if (top) first = 0; Render();
    }
    void Sort(DataGridViewColumn c) { // header click: ascending first, then it toggles
        if (c.SortMode == DataGridViewColumnSortMode.NotSortable) return; var asc = c.HeaderCell.SortGlyphDirection != SortOrder.Ascending;
        foreach (DataGridViewColumn x in grid.Columns) x.HeaderCell.SortGlyphDirection = SortOrder.None;
        c.HeaderCell.SortGlyphDirection = asc ? SortOrder.Ascending : SortOrder.Descending; mods.DefaultView.Sort = c.Name + (asc ? "" : " DESC"); Filter(true);
    }
    int PageSize { get { return Math.Max(1, (grid.ClientSize.Height - grid.ColumnHeadersHeight) / grid.RowTemplate.Height); } } // as many rows as fit, so the list never scrolls
    int Page { get { return first / PageSize; } }
    void Go(int page) { first = page * PageSize; Render(); }
    void Render() { // only the visible page becomes grid rows, so drawing and pictures stay cheap
        var dv = mods.DefaultView; int size = PageSize, total = dv.Count, count = Math.Max(1, (total + size - 1) / size), page = Math.Max(0, Math.Min(first / size, count - 1)); first = page * size;
        rendering = true; grid.Rows.Clear(); hoverRow = -1;
        for (var i = first; i < Math.Min(total, first + size); i++) { var r = dv[i].Row; grid.Rows[grid.Rows.Add(null, r["Name"], r["Downloads"], r["Updated"], r["Latest"], r["Action"])].Tag = r; }
        var at = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(x => x.Tag == shown) ?? (grid.Rows.Count > 0 ? grid.Rows[0] : null); // the shown mod stays selected while it is on the page
        if (at != null) grid.CurrentCell = at.Cells[1]; rendering = false; ShowMod(Current);
        showing.Text = total == 0 ? "" : "Showing " + (first + 1) + " to " + Math.Min(total, first + size) + " of " + Plural(total, "mod");
        var items = new List<Control> { prev }; for (int p = 0, last = -1; p < count; p++) if (p == 0 || p == count - 1 || Math.Abs(p - page) <= 2) { // first, last and the pages around the current one
            if (p > last + 1) items.Add(new Label { Text = "...", Size = new Size(32, 40), BackColor = Surface, ForeColor = Muted, TextAlign = ContentAlignment.MiddleCenter });
            var n = p; items.Add(Btn('\0', (p + 1).ToString(), () => Go(n), p == page ? Red : Surface, 40)); last = p; }
        items.Add(next); foreach (var c in pages.Controls.Cast<Control>().Except(new[] { prev, next }).ToList()) c.Dispose(); pages.Controls.Clear(); Join(pages, items.ToArray());
        prev.Enabled = page > 0; next.Enabled = page < count - 1;
    }
    static string IdList(Dictionary<string, object> m, string key) { object v; return m.TryGetValue(key, out v) && v is IEnumerable && !(v is string) ? string.Join(",", ((IEnumerable)v).Cast<object>()) : ""; } // mod ids
    DataRow ById(string id) { return mods.Rows.Cast<DataRow>().FirstOrDefault(x => (string)x["Id"] == id); }
    DataRow Row(int i) { return (DataRow)grid.Rows[i].Tag; }
    DataRow Current { get { return grid.CurrentRow == null ? null : Row(grid.CurrentRow.Index); } }
    bool Pending(int i) { return i >= 0 && Pending(Row(i)); }
    static bool Pending(DataRow r) { return (string)r["Action"] != Installed; }
    void Hover(int row, int col) { // hover is drawn by PaintCell, so it covers the whole row
        if (row == hoverRow && col == hoverCol) return; var old = hoverRow; hoverRow = row; hoverCol = col;
        foreach (var i in new[] { old, row }) if (i >= 0 && i < grid.Rows.Count) grid.InvalidateRow(i); grid.Cursor = col == ActionCol && Pending(row) ? Cursors.Hand : Cursors.Default;
    }

    void PaintCell(object s, DataGridViewCellPaintingEventArgs e) { // one drawing path for every cell: row state, picture, two-line text or the action button
        if (e.RowIndex < 0) return; var r = Row(e.RowIndex); var col = grid.Columns[e.ColumnIndex].Name; var g = e.Graphics; var b = e.CellBounds;
        Fill(g, grid.Rows[e.RowIndex].Selected ? Raised : e.RowIndex == hoverRow ? Surface : Bg, b); Fill(g, Line, new Rectangle(b.X, b.Bottom - 1, b.Width, 1));
        if (col == "Thumb") DrawPicture(g, ((string)r["Gallery"]).Split('\n')[0], new Rectangle(b.X + 8, b.Y + 7, 96, 54), 320, 192, true);
        else if (col == "Action") { var a = (string)e.Value; var box = Rectangle.Inflate(b, -8, -14); var hot = e.RowIndex == hoverRow && e.ColumnIndex == hoverCol;
            if (a != Installed) Fill(g, a == "Update" ? (hot ? RedHover : Red) : hot ? Hot : Line, box); TextRenderer.DrawText(g, a, Bold, box, a == Installed ? Muted : Fg, Center); }
        else { string key, second = SecondLine.TryGetValue(col, out key) ? r[key] as string : null; var box = Rectangle.Inflate(b, -8, -12);
            var f = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | (col == "Downloads" ? TextFormatFlags.Right : 0);
            if (string.IsNullOrEmpty(second)) TextRenderer.DrawText(g, (string)e.FormattedValue, e.CellStyle.Font, box, Fg, f);
            else { var h = box.Height / 2; TextRenderer.DrawText(g, (string)e.FormattedValue, e.CellStyle.Font, new Rectangle(box.X, box.Y, box.Width, h), Fg, f);
                TextRenderer.DrawText(g, second, Small, new Rectangle(box.X, box.Y + h, box.Width, box.Height - h), Muted, f); } }
        e.Handled = true;
    }
}
