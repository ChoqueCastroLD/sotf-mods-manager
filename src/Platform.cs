// The game folder, installed versions, files and network.
using System; using System.Collections.Generic; using System.Diagnostics; using System.IO; using System.Linq; using System.Net; using System.Text.RegularExpressions; using System.Threading.Tasks; using System.Windows.Forms; using System.Web.Script.Serialization; using Microsoft.Win32;

partial class SotfModsManager {
    string Game { get { return gamePath.Text.Trim(); } } string Undo { get { return Path.Combine(Game, ".sotfmm-undo"); } }
    bool Valid { get { return File.Exists(Path.Combine(Game, "SonsOfTheForest.exe")); } }
    string LoaderVersion { get { return Valid ? JsonVersion(@"_Redloader\info.json", "version.dll") : null; } }
    static bool Running { get { return Process.GetProcessesByName("SonsOfTheForest").Length > 0; } }
    static void EnsureClosed() { if (Running) throw new Exception("close Sons of the Forest first."); }
    string JsonVersion(string json, string dll) { // version from a manifest; "unknown" if only the dll is there; null if not installed
        json = Path.Combine(Game, json); if (!File.Exists(json)) return File.Exists(Path.Combine(Game, dll)) ? "unknown" : null;
        var m = Regex.Match(File.ReadAllText(json), "\"version\"\\s*:\\s*\"([^\"]+)\""); return m.Success ? m.Groups[1].Value : "unknown";
    }
    Dictionary<string, object> Manifest(string id) { try { return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(Game, @"Mods\" + id + @"\manifest.json"))) ?? new Dictionary<string, object>(); } catch (Exception) { return new Dictionary<string, object>(); } }
    static Version Ver(string s) { Version v; return Version.TryParse(Regex.Match(s ?? "", @"\d+(\.\d+){1,3}").Value, out v) ? v : null; }
    static bool Newer(string a, string b) { var x = Ver(a); return x != null && b != null && (Ver(b) == null || x > Ver(b)); } // an unreadable installed version counts as outdated
    static string Esc(object s) { return Uri.EscapeDataString((string)s); }
    static void MoveItem(string from, string to) { if (File.Exists(from)) File.Move(from, to); else if (Directory.Exists(from)) Directory.Move(from, to); }
    static void TryDelete(string dir) { try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { } }
    static void Register() { // per user, no admin rights: double-clicking a .sotfprofile file or an "Open in launcher" link (sotfmm://modpack/user/slug) opens this app
        var open = "\"" + Application.ExecutablePath + "\" \"%1\"";
        try { foreach (var k in new[] { new[] { ".sotfprofile", "", "SotfModsManager.Profile" }, new[] { @"SotfModsManager.Profile\shell\open\command", "", open },
                new[] { "sotfmm", "", "URL:SotF Mods Manager" }, new[] { "sotfmm", "URL Protocol", "" }, new[] { @"sotfmm\shell\open\command", "", open } })
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Classes\" + k[0], k[1], k[2]); } catch { }
    }
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
