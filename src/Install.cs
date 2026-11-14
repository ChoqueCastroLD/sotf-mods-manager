// Installing and removing things: RedLoader, mods and their dependencies, the Defender exclusion, and starting the game.
using System; using System.Collections.Generic; using System.Data; using System.Diagnostics; using System.IO; using System.IO.Compression; using System.Linq;
using System.Text.RegularExpressions; using System.Threading.Tasks; using System.Windows.Forms;

partial class SotfModsManager {
    const string LoaderRepo = "https://github.com/ToniMacaroni/RedLoader/releases/";
    static readonly string[] LoaderFiles = { "version.dll", "doorstop_config.ini", "_Redloader" };
    bool busy;

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
    void Act(DataRow r) { // asks first when the mod brings dependencies or clashes with installed mods
        var deps = Deps(r, new HashSet<DataRow> { r }).Where(Pending).ToList(); var adding = deps.Concat(new[] { r }).ToList();
        var clashes = mods.Rows.Cast<DataRow>().Where(x => !Pending(x) && !adding.Contains(x) && adding.Any(a => Ids(a["Clashes"]).Contains((string)x["Id"]) || Ids(x["Clashes"]).Contains((string)a["Id"]))).ToList();
        string text = deps.Count == 0 ? "" : r["Name"] + " needs " + Names(deps) + ". " + (deps.Count == 1 ? "It" : "They") + " will be installed too.";
        if (clashes.Count > 0) text += (text == "" ? "" : "\n\n") + r["Name"] + " does not work with " + Names(clashes) + ", which " + (clashes.Count == 1 ? "is" : "are") + " installed.";
        var choice = text == "" ? "" : Ask("Install " + r["Name"], text, clashes.Count > 0 ? new[] { "Remove " + Names(clashes) + " and install", "Install anyway", "Cancel" } : new[] { "Install all", "Cancel" });
        if (choice == null || choice == "Cancel") return; var remove = choice.StartsWith("Remove") ? clashes : new List<DataRow>();
        Run((Pending(r) ? "Installing " : "Reinstalling ") + r["Name"], async t => {
            EnsureClosed(); if (LoaderVersion == null) throw new Exception("install RedLoader first.");
            foreach (var x in remove) RemoveMod(x); foreach (var d in adding) await InstallMod(d, t); // dependencies first
            return r["Name"] + " " + r["Latest"] + " installed" + (deps.Count > 0 ? " with " + Names(deps) : "") + (remove.Count > 0 ? ", " + Names(remove) + " removed" : ""); });
    }
    IEnumerable<DataRow> Deps(DataRow r, HashSet<DataRow> seen) { // every dependency on sotf-mods.com, deepest first
        foreach (var d in Ids(r["Deps"]).Select(ById).Where(d => d != null && seen.Add(d)).ToList()) { foreach (var x in Deps(d, seen)) yield return x; yield return d; } }
    static string[] Ids(object list) { return ((string)list).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries); }
    static string Names(IEnumerable<DataRow> rows) { return string.Join(", ", rows.Select(x => (string)x["Name"])); }
    string ModPath(DataRow r) { var id = (string)r["Id"]; if (!Regex.IsMatch(id, @"^[\w.-]+$")) throw new Exception("invalid mod id."); return Path.Combine(Game, "Mods", id); }
    void RemoveMod(DataRow r) { var p = ModPath(r); File.Delete(p + ".dll"); TryDelete(p); }
    async Task InstallMod(DataRow r, Panel t) {
        string game = Game, dll = ModPath(r) + ".dll";
        await Fetch(SiteUrl + "/api/mods/slug/" + Esc(r["User"]) + "/" + Esc(r["Slug"]) + "/download/" + Esc(r["Latest"]), (string)r["Name"], t, f => {
            bool zip; using (var s = File.OpenRead(f)) zip = s.ReadByte() == 'P' && s.ReadByte() == 'K'; // a mod is a zip or a bare dll
            if (zip) Extract(f, game); else { Directory.CreateDirectory(Path.GetDirectoryName(dll)); File.Copy(f, dll, true); } });
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
}
