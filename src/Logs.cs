// Logs: a menu that opens RedLoader's and the game's logs, and a watch that offers them when the game crashes.
// Unity writes "Crash!!!" into Player.log and a crash report folder in %TEMP% when the game crashes.
using System; using System.Diagnostics; using System.IO; using System.Linq; using System.Windows.Forms;

partial class SotfModsManager {
    static readonly string GameData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"AppData\LocalLow\Endnight\SonsOfTheForest"),
        Crashes = Path.Combine(Path.GetTempPath(), @"Endnight\SonsOfTheForest\Crashes");
    string[][] Logs { get { return new[] { new[] { "RedLoader log", Path.Combine(Game, @"_Redloader\Latest.log") }, new[] { "RedLoader errors", Path.Combine(Game, @"_Redloader\ErrorLog.log") },
        new[] { "Game log", Path.Combine(GameData, "Player.log") }, new[] { "Crash reports folder", Crashes } }; } } // label, path
    DateTime? started; // when the game was first seen running

    Button SetupLogs() {
        var menu = NewMenu(); Button b = null; b = Btn(LogIcon, "Logs", () => menu.Show(b, 0, b.Height), Raised, 100, "Open RedLoader and game logs");
        menu.Opening += (s, e) => { menu.Items.Clear(); foreach (var l in Logs) { var p = l[1]; menu.Items.Add(l[0], null, (x, y) => OpenLog(p)).Enabled = File.Exists(p) || Directory.Exists(p); } };
        var watch = new Timer { Interval = 3000 }; watch.Tick += (s, e) => Watch(); watch.Start(); return b;
    }
    static void OpenLog(string path) { try { Process.Start(path); } catch (Exception) { Process.Start("explorer.exe", "/select,\"" + path + "\""); } } // the default text editor, else Explorer
    void Watch() { // checks for a crash each time the game closes, whether it started from here or from Steam
        if (Running) { if (started == null) started = DateTime.Now; return; } if (started == null) return; var since = started.Value; started = null; if (!Crashed(since)) return;
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal; Activate();
        var c = Ask("Sons of the Forest crashed", "The game closed unexpectedly. The logs usually name the mod that caused it. To report it, send the log to the mod's author.", new[] { "Open RedLoader log", "Open game log", "Close" });
        if (c != null && c.StartsWith("Open")) OpenLog(Logs[c == "Open RedLoader log" ? 0 : 2][1]);
    }
    static bool Crashed(DateTime since) {
        var log = Path.Combine(GameData, "Player.log"); if (Directory.Exists(Crashes) && Directory.GetDirectories(Crashes).Any(d => Directory.GetCreationTime(d) > since)) return true;
        try { if (File.GetLastWriteTime(log) > since) using (var r = new StreamReader(new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))) return r.ReadToEnd().Contains("Crash!!!"); } catch (IOException) { }
        return false;
    }
}
