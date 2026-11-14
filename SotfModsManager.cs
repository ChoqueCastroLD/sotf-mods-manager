// SotfModsManager - one-click RedLoader installer for Sons of the Forest. https://sotf-mods.com
using System; using System.Diagnostics; using System.Drawing; using System.IO; using System.IO.Compression;
using System.Linq; using System.Net; using System.Text.RegularExpressions; using System.Threading.Tasks;
using System.Windows.Forms; using Microsoft.Win32;

class SotfModsManager : Form {
    const string Repo = "https://github.com/ToniMacaroni/RedLoader/releases/", VcUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe";
    Label status = new Label { Dock = DockStyle.Top, Height = 50, TextAlign = ContentAlignment.MiddleCenter };
    Label deps = new Label { Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.DimGray };
    Button install = new Button { Dock = DockStyle.Top, Height = 50, Font = new Font("Segoe UI", 12f, FontStyle.Bold) };
    ProgressBar bar = new ProgressBar { Dock = DockStyle.Top, Height = 8 };
    CheckBox advanced = new CheckBox { Dock = DockStyle.Top, Text = "Advanced options", Padding = new Padding(4) };
    TextBox gamePath = new TextBox { Width = 300 }, version = new TextBox { Text = "latest", Width = 80 };
    FlowLayoutPanel panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Visible = false, Padding = new Padding(4) };

    [STAThread] static void Main() { Application.EnableVisualStyles(); Application.Run(new SotfModsManager()); }

    SotfModsManager() {
        Text = "SotF Mods Manager"; ClientSize = new Size(420, 176); Font = new Font("Segoe UI", 9f);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        panel.Controls.Add(new Label { Text = "Game folder:", AutoSize = true }); panel.Controls.Add(gamePath); panel.Controls.Add(Btn("Browse...", Browse));
        panel.Controls.Add(new Label { Text = "RedLoader version (tag or latest):", AutoSize = true }); panel.Controls.Add(version);
        panel.Controls.Add(Btn("Install VC++ runtime", async () => { await InstallVc(); Refresh2(); }));
        panel.Controls.Add(Btn("Add Defender exclusion (faster first start)", DefenderExclusion));
        panel.Controls.Add(Btn("Uninstall RedLoader", Uninstall)); panel.Controls.Add(Btn("Open game folder", () => Open("")));
        panel.Controls.Add(Btn("Open Mods", () => Open("Mods"))); panel.Controls.Add(Btn("Open RedLoader folder", () => Open("_Redloader")));
        Controls.AddRange(new Control[] { panel, advanced, bar, deps, install, status });
        advanced.CheckedChanged += (s, e) => { panel.Visible = advanced.Checked; ClientSize = new Size(420, advanced.Checked ? 350 : 176); };
        gamePath.TextChanged += (s, e) => Refresh2(); gamePath.Text = FindGame();
        install.Click += async (s, e) => await Install(); Refresh2();
    }
    Button Btn(string text, Action a) { var b = new Button { Text = text, AutoSize = true }; b.Click += (s, e) => a(); return b; }
    string Game { get { return gamePath.Text.Trim(); } } bool Valid { get { return File.Exists(Path.Combine(Game, "SonsOfTheForest.exe")); } }
    static bool VcInstalled() { // Visual C++ 2015-2022 x64, needed by RedLoader's native splash screen
        using (var k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64"))
            return k != null && Equals(k.GetValue("Installed"), 1);
    }
    static string FindGame() { // Steam uninstall key first, then every Steam library listed in libraryfolders.vdf
        var loc = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1326470", "InstallLocation", null) as string;
        if (loc != null && Directory.Exists(loc)) return loc;
        var vdf = Path.Combine(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", "") as string ?? "", @"steamapps\libraryfolders.vdf");
        var libs = File.Exists(vdf) ? Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"(.+?)\"").Cast<Match>().Select(m => m.Groups[1].Value.Replace(@"\\", @"\")) : new string[0];
        return libs.Select(l => Path.Combine(l, @"steamapps\common\Sons Of The Forest")).FirstOrDefault(Directory.Exists) ?? "";
    }
    string InstalledVersion() {
        var info = Path.Combine(Game, @"_Redloader\info.json");
        if (!File.Exists(info)) return File.Exists(Path.Combine(Game, "version.dll")) ? "unknown" : null;
        var m = Regex.Match(File.ReadAllText(info), "\"version\"\\s*:\\s*\"(.+?)\""); return m.Success ? m.Groups[1].Value : "unknown";
    }

    void Refresh2() {
        var v = Valid ? InstalledVersion() : null;
        status.Text = !Valid ? "Sons of the Forest not found.\nOpen 'Advanced options' and pick the game folder."
            : v == null ? "Game found. RedLoader is not installed." : "RedLoader " + v + " is installed. Ready to mod!";
        deps.Text = "Dependencies:  Visual C++ x64 runtime " + (VcInstalled() ? "✔ installed" : "✖ missing (installed automatically)")
            + "\n.NET runtime ✔ included with RedLoader (no SDK needed)";
        install.Text = v == null ? "Install RedLoader" : "Update / Repair RedLoader"; install.Enabled = Valid;
        if (!Valid) advanced.Checked = true;
    }
    async Task Download(string url, string file, string what) {
        status.Text = "Downloading " + what + "..."; ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        using (var web = new WebClient()) { web.DownloadProgressChanged += (s, e) => bar.Value = e.ProgressPercentage; await web.DownloadFileTaskAsync(url, file); }
    }
    async Task InstallVc() { // official Microsoft installer; Windows asks for admin permission
        var exe = Path.Combine(Path.GetTempPath(), "vc_redist.x64.exe"); await Download(VcUrl, exe, "Visual C++ runtime");
        status.Text = "Installing Visual C++ runtime..."; var p = Process.Start(exe, "/install /passive /norestart"); await Task.Run(() => p.WaitForExit());
    }

    async Task Install() {
        if (Process.GetProcessesByName("SonsOfTheForest").Length > 0) { MessageBox.Show("Please close Sons of the Forest first."); return; }
        var v = version.Text.Trim(); var tmp = Path.GetTempFileName(); install.Enabled = false;
        try {
            if (!VcInstalled()) await InstallVc();
            await Download(v == "" || v == "latest" ? Repo + "latest/download/Redloader.zip" : Repo + "download/" + v + "/Redloader.zip", tmp, "RedLoader");
            status.Text = "Installing RedLoader..."; var root = Path.GetFullPath(Game) + "\\";
            await Task.Run(() => { using (var zip = ZipFile.OpenRead(tmp)) foreach (var e in zip.Entries) {
                var dest = Path.GetFullPath(Path.Combine(root, e.FullName));
                if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue; // block zip-slip paths
                if (e.Name == "") { Directory.CreateDirectory(dest); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(dest)); e.ExtractToFile(dest, true); } });
            Refresh2(); MessageBox.Show("RedLoader installed! Launch the game from Steam.\nFirst start can take a few minutes.", Text);
        } catch (UnauthorizedAccessException) { MessageBox.Show("No permission to write to the game folder. Run as administrator.");
        } catch (Exception ex) { MessageBox.Show("Install failed: " + ex.Message);
        } finally { File.Delete(tmp); bar.Value = 0; Refresh2(); }
    }
    void Uninstall() {
        if (MessageBox.Show("Remove RedLoader? Your Mods folder is kept.", Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        try { foreach (var f in new[] { "version.dll", "doorstop_config.ini" }) File.Delete(Path.Combine(Game, f));
            var dir = Path.Combine(Game, "_Redloader"); if (Directory.Exists(dir)) Directory.Delete(dir, true);
        } catch (Exception ex) { MessageBox.Show("Uninstall failed (is the game running?): " + ex.Message); } Refresh2();
    }

    void DefenderExclusion() { // Defender scans ~235 RedLoader DLLs on first start (RedLoader issue #49); excluding the game folder avoids it
        try { Process.Start(new ProcessStartInfo("powershell", "-NoProfile -Command Add-MpPreference -ExclusionPath '" + Game + "'") { Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden }); } catch { }
    }
    void Browse() { using (var d = new FolderBrowserDialog()) if (d.ShowDialog() == DialogResult.OK) gamePath.Text = d.SelectedPath; }
    void Open(string sub) { var p = Path.Combine(Game, sub); if (File.Exists(p) || Directory.Exists(p)) Process.Start(p); else MessageBox.Show("Not found: " + p); }
}
