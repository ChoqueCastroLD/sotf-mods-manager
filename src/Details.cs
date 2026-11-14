// The details panel: picture gallery, facts, install button and the full description of the selected mod.
using System; using System.Data; using System.Diagnostics; using System.IO; using System.Drawing; using System.Linq; using System.Net; using System.Text.RegularExpressions; using System.Windows.Forms;

partial class SotfModsManager {
    static readonly Font Heading = F(11.5f, true);
    Panel details = new Panel { Dock = DockStyle.Right, Width = 380, BackColor = Surface, Padding = new Padding(20) }, shotBar = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(0, 8, 0, 0) };
    Panel gallery = Buffered(new Panel { Dock = DockStyle.Top, Height = 191, Cursor = Cursors.Hand }); // 16:9
    Label counter = new Label { Dock = DockStyle.Fill, ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft }, title = new Label { Dock = DockStyle.Top, Height = 44, Font = F(13f, true), TextAlign = ContentAlignment.BottomLeft, AutoEllipsis = true },
        byline = new Label { Dock = DockStyle.Top, Height = 28, ForeColor = Muted, AutoEllipsis = true }, needs = new Label { Dock = DockStyle.Top, Height = 32, ForeColor = Muted, Font = Small, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
    Label[] facts = new Label[3];
    RichTextBox desc = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Surface, ForeColor = Fg, Font = Body, DetectUrls = false, TabStop = false, BulletIndent = 14 };
    Button modBtn, pageBtn; DataRow shown; string[] shots = { "" }; int shot;

    Control SetupDetails() {
        gallery.Paint += (s, e) => DrawPicture(e.Graphics, shots[shot], gallery.ClientRectangle, 640, 640, false);
        gallery.Click += (s, e) => { if (shots[shot] != "") Process.Start(shots[shot]); }; tip.SetToolTip(gallery, "Open the full size picture");
        shotBar.Controls.AddRange(new Control[] { counter, Group(DockStyle.Right, Btn(Prev, null, () => Shot(-1), Raised, 40, "Previous picture"), Btn(Next, null, () => Shot(1), Raised, 40, "Next picture")) });
        var table = new TableLayoutPanel { Dock = DockStyle.Top, Height = 52, ColumnCount = 3 }; var names = new[] { "Version", "Downloads", "Updated" };
        for (var i = 0; i < 3; i++) { table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            table.Controls.Add(new Label { Text = names[i], ForeColor = Muted, Font = Small, AutoSize = true, Margin = new Padding(0) }, i, 0); table.Controls.Add(facts[i] = new Label { AutoSize = true, Font = Bold, Margin = new Padding(0, 2, 0, 0) }, i, 1); }
        modBtn = Btn('\0', "Install", () => { if ((string)shown["Action"] == Manual) OpenLog(Path.Combine(Game, "Mods")); else Act(shown); }, Red, 170);
        var actions = new Panel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(0, 8, 0, 8) }; actions.Controls.Add(Group(DockStyle.Left, modBtn, pageBtn = Btn(OpenIcon, "Mod page", () => Process.Start(SiteUrl + ((string)shown["Kind"] == Build ? "/builds/" : "/mods/") + shown["User"] + "/" + shown["Slug"]), Raised, 140)));
        desc.HandleCreated += (s, e) => SetWindowTheme(desc.Handle, "DarkMode_Explorer", null); // dark scroll bar on Windows 10 1809 and later
        details.Controls.AddRange(new Control[] { desc, needs, actions, table, byline, title, shotBar, gallery }); // docked bottom to top
        return details;
    }
    void ShowMod(DataRow r) { // r is null when the page is empty
        foreach (Control c in details.Controls) c.Visible = r != null; if (r == null) { shown = null; return; } // the panel keeps its place so the list does not jump
        if (r != shown) { shown = r; shots = ((string)r["Gallery"]).Split('\n'); shot = 0; Shot(0); ForgetPictures(640, shots); Describe(r["Description"] as string);
            title.Text = (string)r["Name"]; byline.Text = (string)r["Byline"]; facts[1].Text = ((int)r["Downloads"]).ToString("N0"); facts[2].Text = ((DateTime)r["Updated"]).ToString("yyyy-MM-dd");
            needs.Text = "Needs RedLoader" + string.Concat(Ids(r["Deps"]).Select(id => ", " + (ById(id) == null ? id : ById(id)["Name"]))); tip.SetToolTip(needs, needs.Text); }
        var a = (string)r["Action"]; facts[0].Text = (string)r["Latest"]; modBtn.Text = a == Manual ? "Mods folder" : a == Installed ? "Reinstall" : a == "Update" ? "Update to " + r["Latest"] : "Install"; Tint(modBtn, Pending(r) ? Red : Line); pageBtn.Visible = a != Manual; pageBtn.Text = (string)r["Kind"] == Build ? "Build page" : "Mod page";
        if (a == Manual) facts[1].Text = "Not listed"; shotBar.Visible = shots.Length > 1;
    }
    void Shot(int step) { shot = (shot + step + shots.Length) % shots.Length; counter.Text = "Picture " + (shot + 1) + " of " + shots.Length; gallery.Invalidate(); }
    void Describe(string md) { // light Markdown: headings, bold and bullets keep their look; links keep their text; pictures and HTML are left out
        desc.Clear(); var blank = true;
        foreach (var raw in WebUtility.HtmlDecode(md ?? "").Split('\n')) {
            var head = Regex.Match(raw, @"^\s*#+\s*"); var item = Regex.Match(raw, @"^\s*[-*+]\s+"); desc.SelectionBullet = item.Success && !head.Success;
            var line = raw.Substring(head.Success ? head.Length : item.Success ? item.Length : 0).TrimEnd();
            line = Regex.Replace(Regex.Replace(line, @"!\[[^\]]*\]\([^)]*\)|<[^>]+>|`|(?<!\*)\*(?!\*)", ""), @"\[([^\]]*)\]\([^)]*\)", "$1");
            if (line.Trim() == "") { if (!blank) desc.AppendText("\n"); blank = true; continue; } blank = false;
            var parts = line.Split(new[] { "**" }, StringSplitOptions.None);
            for (var i = 0; i < parts.Length; i++) { desc.SelectionFont = head.Success ? Heading : i % 2 == 1 ? Bold : Body; desc.AppendText(parts[i]); }
            desc.AppendText("\n");
        }
        desc.Select(0, 0); desc.ScrollToCaret();
    }
}
