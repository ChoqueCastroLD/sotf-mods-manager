// SotfModsManager - one-click RedLoader installer for Sons of the Forest. https://sotf-mods.com
using System; using System.Diagnostics; using System.Drawing; using System.IO; using System.IO.Compression; using System.Linq; using System.Net;
using System.Runtime.InteropServices; using System.Text.RegularExpressions; using System.Threading.Tasks; using System.Windows.Forms; using Microsoft.Win32;

class SotfModsManager : Form {
    const string AppVersion = "0.3.0", Repo = "https://github.com/ToniMacaroni/RedLoader/releases/", VcUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe";
    static readonly Color Bg = Color.FromArgb(19, 23, 20), Card = Color.FromArgb(34, 40, 35), Hover = Color.FromArgb(48, 56, 49), Accent = Color.FromArgb(67, 128, 76), Dim = Color.FromArgb(135, 145, 133);
    Label status = new Label { Dock = DockStyle.Top, Height = 42, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Dim };
    Label deps = new Label { Dock = DockStyle.Top, Height = 44, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Dim, Font = new Font("Segoe UI", 8.5f) };
    Label advanced = new Label { Dock = DockStyle.Top, Height = 26, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Dim, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 8.5f, FontStyle.Underline) };
    Button install = new Button { Dock = DockStyle.Top, Height = 52, FlatStyle = FlatStyle.Flat, BackColor = Accent, Font = new Font("Segoe UI Semibold", 12.5f), Cursor = Cursors.Hand };
    Panel track = new Panel { Dock = DockStyle.Top, Height = 3 }, fill = new Panel { Dock = DockStyle.Left, Width = 0, BackColor = Color.FromArgb(120, 190, 110) };
    TextBox gamePath = Box("", 338), version = Box("latest", 120);
    FlowLayoutPanel panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Visible = false, Padding = new Padding(0, 6, 0, 0) }; string latest; // newest RedLoader release on GitHub (null if offline)
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [STAThread] static void Main() { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; Application.EnableVisualStyles(); Application.Run(new SotfModsManager()); }
    SotfModsManager() {
        Text = "SotF Mods Manager " + AppVersion; Padding = new Padding(24, 6, 24, 4); BackColor = Bg; ForeColor = Color.FromArgb(236, 238, 232);
        Font = new Font("Segoe UI", 9.5f); FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; ShowIcon = false; StartPosition = FormStartPosition.CenterScreen;
        var dark = 1; DwmSetWindowAttribute(Handle, 20, ref dark, 4); // dark title bar on Windows 10/11
        var title = new Label { Dock = DockStyle.Top, Height = 50, Text = "SotF Mods Manager", Font = new Font("Segoe UI Semibold", 18f), TextAlign = ContentAlignment.BottomCenter };
        var footer = new Label { Dock = DockStyle.Bottom, Height = 22, Text = "sotf-mods.com", ForeColor = Accent, TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand, Font = deps.Font };
        install.FlatAppearance.BorderSize = 0; install.FlatAppearance.MouseOverBackColor = Color.FromArgb(82, 150, 92); track.Controls.Add(fill);
        panel.Controls.AddRange(new Control[] { Caption("Game folder"), gamePath, Btn("…", Browse, 42), Caption("RedLoader version  (a release tag, or latest)"), version,
            Btn("Repair RedLoader", async () => await Install()), Btn("Uninstall RedLoader", Uninstall), Btn("Defender exclusion", DefenderExclusion),
            Btn("Open game folder", () => Open("")), Btn("Open Mods folder", () => Open("Mods")), Btn("Open RedLoader folder", () => Open("_Redloader")) });
        panel.SetFlowBreak(version, true); Controls.AddRange(new Control[] { panel, advanced, deps, install, track, status, title, footer });
        advanced.Click += (s, e) => ShowAdvanced(!panel.Visible); footer.Click += (s, e) => Process.Start("https://sotf-mods.com"); ShowAdvanced(false);
        gamePath.TextChanged += (s, e) => Refresh2(); gamePath.Text = FindGame();
        install.Click += async (s, e) => { if (install.Tag != null) Process.Start("steam://rungameid/1326470"); else await Install(); }; Refresh2();
        Load += async (s, e) => { latest = await LatestTag("ToniMacaroni/RedLoader"); Refresh2(); var me = await LatestTag("ChoqueCastroLD/sotf-mods-manager");
            if (Newer(me, AppVersion) && MessageBox.Show("SotF Mods Manager " + me + " is available. Open download page?", Text, MessageBoxButtons.YesNo) == DialogResult.Yes)
                Process.Start("https://github.com/ChoqueCastroLD/sotf-mods-manager/releases/latest"); };
    }
    void ShowAdvanced(bool on) { panel.Visible = on; advanced.Text = (on ? "Hide" : "Show") + " advanced options"; ClientSize = new Size(440, on ? 512 : 256); }
    static TextBox Box(string text, int w) { return new TextBox { Text = text, Width = w, BackColor = Color.FromArgb(34, 40, 35), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(3, 6, 3, 3) }; }
    static Label Caption(string text) { return new Label { Text = text, Width = 392, Height = 18, ForeColor = Color.FromArgb(135, 145, 133), Font = new Font("Segoe UI", 8.5f), Margin = new Padding(3, 8, 3, 0) }; }
    Button Btn(string text, Action a, int w = 190) { var b = new Button { Text = text, Width = w, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Card, Cursor = Cursors.Hand };
        b.FlatAppearance.BorderSize = 0; b.FlatAppearance.MouseOverBackColor = Hover; b.Click += (s, e) => a(); return b; }
    static async Task<string> LatestTag(string repo) { // returns null when offline or rate-limited
        try { using (var web = new WebClient()) { web.Headers["User-Agent"] = "SotfModsManager";
            return Regex.Match(await web.DownloadStringTaskAsync("https://api.github.com/repos/" + repo + "/releases/latest"), "\"tag_name\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value; } } catch { return null; } }
    static bool Newer(string a, string b) { Version x, y; return a != null && b != null && Version.TryParse(a.TrimStart('v'), out x) && Version.TryParse(b.TrimStart('v'), out y) && x > y; }
    string Game { get { return gamePath.Text.Trim(); } } bool Valid { get { return File.Exists(Path.Combine(Game, "SonsOfTheForest.exe")); } }
    static bool VcInstalled() { // Visual C++ 2015-2022 x64, needed by RedLoader's native splash screen
        using (var k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64")) return k != null && Equals(k.GetValue("Installed"), 1); }
    static string FindGame() { // Steam uninstall key first, then every Steam library listed in libraryfolders.vdf
        var loc = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1326470", "InstallLocation", null) as string;
        if (loc != null && Directory.Exists(loc)) return loc;
        var vdf = Path.Combine(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", "") as string ?? "", @"steamapps\libraryfolders.vdf");
        var libs = File.Exists(vdf) ? Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"(.+?)\"").Cast<Match>().Select(m => m.Groups[1].Value.Replace(@"\\", @"\")) : new string[0];
        return libs.Select(l => Path.Combine(l, @"steamapps\common\Sons Of The Forest")).FirstOrDefault(Directory.Exists) ?? "";
    }
    string InstalledVersion() {
        var info = Path.Combine(Game, @"_Redloader\info.json"); if (!File.Exists(info)) return File.Exists(Path.Combine(Game, "version.dll")) ? "unknown" : null;
        var m = Regex.Match(File.ReadAllText(info), "\"version\"\\s*:\\s*\"(.+?)\""); return m.Success ? m.Groups[1].Value : "unknown";
    }
    void Refresh2() {
        var v = Valid ? InstalledVersion() : null; var upd = Newer(latest, v);
        status.Text = !Valid ? "Sons of the Forest not found.\nPick the game folder in advanced options." : v == null ? "Ready to install RedLoader"
            : upd ? "RedLoader " + v + "  ·  update available: " + latest : "RedLoader " + v + (latest == v ? "  ·  up to date ✔" : "  ·  installed ✔");
        deps.Text = "Visual C++ runtime  " + (VcInstalled() ? "✔" : "✖ (installed automatically)") + "     .NET runtime  ✔ bundled\nNo SDK, no manual file copying needed";
        install.Tag = v != null && !upd ? "play" : null; install.Enabled = Valid;
        install.Text = v == null ? "Install RedLoader" : upd ? "Update to RedLoader " + latest : "▶   Play Sons of the Forest";
        if (!Valid) ShowAdvanced(true);
    }
    async Task Download(string url, string file, string what) {
        using (var web = new WebClient()) { web.DownloadProgressChanged += (s, e) => { fill.Width = track.Width * e.ProgressPercentage / 100;
            status.Text = string.Format("Downloading {0}…  {1:0.0} / {2:0.0} MB", what, e.BytesReceived / 1048576.0, e.TotalBytesToReceive / 1048576.0); };
            await web.DownloadFileTaskAsync(url, file); } }
    async Task InstallVc() { // official Microsoft installer; Windows asks for admin permission
        var exe = Path.Combine(Path.GetTempPath(), "vc_redist.x64.exe"); await Download(VcUrl, exe, "Visual C++ runtime");
        status.Text = "Installing Visual C++ runtime…"; var p = Process.Start(exe, "/install /passive /norestart"); await Task.Run(() => p.WaitForExit()); }
    async Task Install() {
        if (Process.GetProcessesByName("SonsOfTheForest").Length > 0) { MessageBox.Show("Please close Sons of the Forest first."); return; }
        var v = version.Text.Trim(); var tmp = Path.GetTempFileName(); var ok = false; install.Enabled = false;
        try { if (!VcInstalled()) await InstallVc();
            await Download(v == "" || v == "latest" ? Repo + "latest/download/Redloader.zip" : Repo + "download/" + v + "/Redloader.zip", tmp, "RedLoader");
            status.Text = "Installing RedLoader…"; var root = Path.GetFullPath(Game) + "\\";
            await Task.Run(() => { using (var zip = ZipFile.OpenRead(tmp)) foreach (var e in zip.Entries) {
                var dest = Path.GetFullPath(Path.Combine(root, e.FullName));
                if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue; // block zip-slip paths
                if (e.Name == "") { Directory.CreateDirectory(dest); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(dest)); e.ExtractToFile(dest, true); } }); ok = true;
        } catch (UnauthorizedAccessException) { MessageBox.Show("No permission to write to the game folder. Run as administrator.");
        } catch (Exception ex) { MessageBox.Show("Install failed: " + ex.Message);
        } finally { File.Delete(tmp); fill.Width = 0; Refresh2(); if (ok) status.Text = "✔ RedLoader installed! Press Play.\nThe first start can take a few minutes."; }
    }
    void Uninstall() {
        if (MessageBox.Show("Remove RedLoader? Your Mods folder is kept.", Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        try { foreach (var f in new[] { "version.dll", "doorstop_config.ini" }) File.Delete(Path.Combine(Game, f));
            var dir = Path.Combine(Game, "_Redloader"); if (Directory.Exists(dir)) Directory.Delete(dir, true);
        } catch (Exception ex) { MessageBox.Show("Uninstall failed (is the game running?): " + ex.Message); } Refresh2();
    }
    void DefenderExclusion() { // Defender scans ~235 RedLoader DLLs on first start (RedLoader issue #49); excluding the game folder avoids it. Asks for admin.
        try { Process.Start(new ProcessStartInfo("powershell", "-NoProfile -Command Add-MpPreference -ExclusionPath '" + Game.Replace("'", "''") + "'") { Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden }); } catch { } }
    void Browse() { using (var d = new FolderBrowserDialog()) if (d.ShowDialog() == DialogResult.OK) gamePath.Text = d.SelectedPath; }
    void Open(string sub) { var p = Path.Combine(Game, sub); if (File.Exists(p) || Directory.Exists(p)) Process.Start(p); else MessageBox.Show("Not found: " + p); }
}
