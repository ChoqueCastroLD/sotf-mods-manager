// SotF Mods Manager: installs RedLoader and Sons of the Forest mods from sotf-mods.com. https://sotf-mods.com
// This file builds the window and keeps its state. The other files in src hold the rest.
using System; using System.Data; using System.Diagnostics; using System.Drawing; using System.Linq; using System.Net; using System.Runtime.InteropServices; using System.Windows.Forms;

partial class SotfModsManager : Form {
    const string AppVersion = "0.6.0", SiteUrl = "https://sotf-mods.com", RepoUrl = "https://github.com/ChoqueCastroLD/sotf-mods-manager";
    Label loaderState = new Label { Dock = DockStyle.Fill, ForeColor = Muted, Font = F(9.5f) }, deps = new Label { Dock = DockStyle.Right, Width = 330, ForeColor = Muted, Font = F(9f), TextAlign = ContentAlignment.MiddleLeft };
    TextBox gamePath = Box(), version = Box(), search = Box();
    FlowLayoutPanel settings = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 250, BackColor = Surface, Padding = new Padding(21, 0, 24, 14), Visible = false };
    Button play, loaderBtn; string latest; static string opened; // the file or link Windows started the app with

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, string lParam);
    [STAThread] static void Main(string[] args) {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; ServicePointManager.DefaultConnectionLimit = 6; // thumbnails download in parallel
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Register(); opened = args.FirstOrDefault(); Application.Run(new SotfModsManager());
    }

    SotfModsManager() {
        Text = "SotF Mods Manager " + AppVersion; var area = Screen.PrimaryScreen.WorkingArea; Size = new Size(Math.Min(1280, area.Width), Math.Min(820, area.Height)); MinimumSize = new Size(1000, 600); StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg; ForeColor = Fg; Font = Body; Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); var dark = 1; DwmSetWindowAttribute(Handle, 20, ref dark, 4); // dark title bar
        var logo = new Label { Image = Image.FromStream(typeof(SotfModsManager).Assembly.GetManifestResourceStream("logo.png")), ImageAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Left, Width = 160, Cursor = Cursors.Hand };
        logo.Click += (s, e) => Process.Start(SiteUrl); tip.SetToolTip(logo, "Open sotf-mods.com");
        play = Btn(Play, "Play", PlayGame, Red, 120); play.Dock = DockStyle.Right;
        loaderBtn = Btn(Reload, "Install", () => Run("Installing RedLoader", InstallLoader), Red, 170);
        var header = Strip(72, 40, Surface, loaderState, new Label { Text = "RedLoader", Dock = DockStyle.Top, Height = 22, Font = F(10.5f, true) }, deps, // one bar: logo, RedLoader state, its actions, Play
            Group(DockStyle.Right, loaderBtn, Btn(Gear, null, () => settings.Visible = !settings.Visible, Raised, 46, "Settings")), new Panel { Dock = DockStyle.Right, Width = 12 }, play, logo);
        var defenderInfo = new Label { Text = InfoIcon.ToString(), Font = new Font(IconFont, 11f), ForeColor = Muted, AutoSize = true, Margin = new Padding(6, 24, 0, 0), Cursor = Cursors.Help };
        tip.SetToolTip(defenderInfo, "Microsoft Defender scans every RedLoader file the first time the game starts,\nwhich can take minutes. Excluding the game folder skips that scan. Asks for admin rights.");
        var browse = Btn(Folder, null, Browse, Raised, 40, "Choose folder"); browse.Margin = new Padding(3, 4, 3, 3); var versionField = Field(version, 140);
        settings.Controls.AddRange(new Control[] { Caption("Game folder"), Field(gamePath, 560), browse, Caption("RedLoader version: a release tag such as 0.8.6, or latest"), versionField,
            Group(DockStyle.None, Btn(Remove, "Uninstall RedLoader", UninstallLoader, Raised, 190), Btn(Shield, "Defender exclusion", () => Run("Adding Defender exclusion", DefenderExclusion), Raised, 180),
                Btn(Folder, "Game folder", () => Open(""), Raised, 140), Btn(Folder, "Mods folder", () => Open("Mods"), Raised, 140)), defenderInfo });
        foreach (var c in settings.Controls.OfType<Label>().Concat(new Control[] { browse, versionField })) settings.SetFlowBreak(c, true); // captions sit above their fields
        version.Text = "latest";
        Controls.AddRange(SetupList().Concat(new Control[] { settings, header }).ToArray()); // docked bottom to top
        gamePath.TextChanged += (s, e) => RefreshState(); Resize += (s, e) => LayoutToasts();
        gamePath.Text = FindGame(); RefreshState(); if (Valid) TryDelete(Undo); // leftover from an uninstall that was not undone
        Load += async (s, e) => { LoadMods(); latest = await LatestTag("ToniMacaroni/RedLoader"); RefreshState(); var me = await LatestTag("ChoqueCastroLD/sotf-mods-manager");
            if (Newer(me, AppVersion)) Toast(InfoIcon, "SotF Mods Manager " + me + " is available", "Download", () => Process.Start(RepoUrl + "/releases/latest")); };
    }

    void RefreshState() { // reads what is installed and updates every label, button and row
        var v = LoaderVersion; var old = Newer(latest, v);
        loaderState.Text = !Valid ? "Sons of the Forest was not found. Choose the game folder in settings." : v == null ? "Not installed"
            : old ? "Version " + v + " installed, version " + latest + " is available" : "Version " + v + (latest == v ? ", up to date" : " installed");
        deps.Text = (VcInstalled() ? "Visual C++ runtime installed" : "Visual C++ runtime missing, installed with RedLoader") + "\n.NET runtime bundled with RedLoader";
        loaderBtn.Text = v == null ? "Install" : old ? "Update to " + latest : "Repair"; Tint(loaderBtn, v == null || old ? Red : Raised); loaderBtn.Enabled = play.Enabled = Valid; profileBtn.Text = Valid ? Active : "Profiles";
        foreach (DataRow r in mods.Rows) { var id = (string)r["Id"]; var have = Valid ? JsonVersion(@"Mods\" + id + @"\manifest.json", @"Mods\" + id + ".dll") : null; var upd = have != null && Newer((string)r["Latest"], have);
            r["Installed"] = upd ? "Installed " + have : ""; r["Action"] = have == null ? "Install" : upd ? "Update" : Installed; }
        if (!Valid) settings.Visible = true; Filter();
    }
}
