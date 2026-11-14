// Profiles: each profile is its own set of mods. The active one lives in Mods; the others wait in .sotfmm-profiles in the game folder,
// so switching only moves folders on the same disk: instant, no downloads. Exported profiles are a small list of sotf-mods.com mods, not files.
using System; using System.Collections; using System.Collections.Generic; using System.Data; using System.Diagnostics; using System.Drawing; using System.IO; using System.Linq;
using System.Text.RegularExpressions; using System.Threading.Tasks; using System.Web.Script.Serialization; using System.Windows.Forms;

partial class SotfModsManager {
    const char ProfileIcon = ''; const string ProfileFilter = "SotF mod profile (*.sotfprofile)|*.sotfprofile";
    ContextMenuStrip profileMenu = NewMenu();
    Button profileBtn;
    string Profiles { get { return Path.Combine(Game, ".sotfmm-profiles"); } }
    string Active { get { var f = Path.Combine(Profiles, "active.txt"); return File.Exists(f) ? File.ReadAllText(f).Trim() : "Default"; } }
    string[] ProfileNames { get { return new[] { Active }.Concat(Directory.Exists(Profiles) ? Directory.GetDirectories(Profiles).Select(Path.GetFileName) : new string[0]).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToArray(); } }

    Control SetupProfiles() {
        profileBtn = Dropdown(ProfileIcon, 180, "Mod profiles: switch, create, import, export or install a modpack", profileMenu);
        profileMenu.Opening += (s, e) => { if (!Valid) { e.Cancel = true; Toast(Fail, "Choose the game folder in settings first"); return; } var active = Active; profileMenu.Items.Clear();
            foreach (var n in ProfileNames) { var name = n; profileMenu.Items.Add(new ToolStripMenuItem(n, null, (a, b) => Run("Switching to " + name, t => Task.FromResult(Switch(name)))) { Checked = n == active }); }
            profileMenu.Items.Add(new ToolStripSeparator()); profileMenu.Items.Add("New profile", null, (a, b) => NewProfile()); profileMenu.Items.Add("Import profile", null, (a, b) => ImportProfile());
            profileMenu.Items.Add("Install modpack from link", null, (a, b) => InstallModpack()); profileMenu.Items.Add("Browse modpacks on sotf-mods.com", null, (a, b) => Process.Start(SiteUrl + "/modpacks"));
            profileMenu.Items.Add("Export " + active, null, (a, b) => ExportProfile()); if (ProfileNames.Length > 1) profileMenu.Items.Add("Delete " + active, null, (a, b) => DeleteProfile()); };
        return profileBtn;
    }
    string Switch(string to) { // the active profile's mods move out of Mods, the chosen profile's mods move in
        EnsureClosed(); string mods = Path.Combine(Game, "Mods"), from = Active; if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return "Already using " + to;
        MoveAll(mods, Path.Combine(Profiles, from)); MoveAll(Path.Combine(Profiles, to), mods); File.WriteAllText(Path.Combine(Profiles, "active.txt"), to); RefreshState();
        return "Switched to " + to + ", " + Plural(Directory.GetFiles(mods, "*.dll").Length, "mod");
    }
    static void MoveAll(string from, string to) { Directory.CreateDirectory(from); Directory.CreateDirectory(to); foreach (var p in Directory.GetFileSystemEntries(from)) MoveItem(p, Path.Combine(to, Path.GetFileName(p))); }
    string NewName(string wanted) { // a safe folder name that no profile uses yet
        var name = Regex.Replace(wanted ?? "", @"[^\w .-]", "").Trim(' ', '.'); if (name.Length > 40) name = name.Substring(0, 40); if (name == "") name = "Profile";
        for (int i = 2, n = name.Length; ProfileNames.Contains(name, StringComparer.OrdinalIgnoreCase); i++) name = name.Substring(0, n) + " " + i; return name;
    }
    void NewProfile() {
        var box = Box(); SendMessage(box.Handle, 0x1501, (IntPtr)1, "Profile name, for example Coop"); if (Ask("New profile", "A new profile starts with no mods. The mods you have now stay in " + Active + ", ready to switch back.", new[] { "Create and switch", "Cancel" }, box) != "Create and switch") return;
        var name = NewName(box.Text); Directory.CreateDirectory(Path.Combine(Profiles, name)); Run("Switching to " + name, t => Task.FromResult(Switch(name)));
    }
    void DeleteProfile() { // the active profile can be deleted after switching to another one
        string name = Active, next = ProfileNames.First(n => n != name);
        if (Ask("Delete " + name, "Delete the profile " + name + " and its mods? You will switch to " + next + ".", new[] { "Delete", "Cancel" }) != "Delete") return;
        Run("Deleting " + name, t => { var msg = Switch(next); TryDelete(Path.Combine(Profiles, name)); return Task.FromResult(name + " deleted. " + msg); });
    }
    void ExportProfile() {
        var list = mods.Rows.Cast<DataRow>().Where(r => (string)r["Action"] != "Install" && (string)r["Action"] != Manual).Select(r => new Dictionary<string, object> { { "id", r["Id"] }, { "name", r["Name"] }, { "user", r["User"] }, { "slug", r["Slug"] } }).ToList();
        using (var d = new SaveFileDialog { Filter = ProfileFilter, FileName = Active + ".sotfprofile" }) if (d.ShowDialog(this) == DialogResult.OK) {
            File.WriteAllText(d.FileName, new JavaScriptSerializer().Serialize(new Dictionary<string, object> { { "profile", Active }, { "game", "Sons of the Forest" }, { "mods", list } }));
            Toast(Done, "Exported " + Active + " with " + Plural(list.Count, "mod") + ". Only mods from sotf-mods.com are included."); }
    }
    void ImportProfile() { using (var d = new OpenFileDialog { Filter = ProfileFilter }) if (d.ShowDialog(this) == DialogResult.OK) Import(File.ReadAllText(d.FileName)); }
    void InstallModpack() {
        var box = Box(); SendMessage(box.Handle, 0x1501, (IntPtr)1, SiteUrl + "/modpacks/user/modpack");
        if (Ask("Install modpack", "Paste the link of a modpack from sotf-mods.com. It becomes a new profile with its mods.", new[] { "Install", "Cancel" }, box) == "Install") OpenModpack(box.Text);
    }
    void OpenShared(string arg) { // what Windows passes when a .sotfprofile file or a sotfmm://modpack/user/slug link is opened
        var one = Regex.Match(arg ?? "", @"^sotfmm://(mod|build)/([\w.-]+)/([\w.-]+)"); // one-click install from a mod or build page
        if (!Valid) Toast(Fail, "Choose the game folder in settings first"); else if (File.Exists(arg)) Import(File.ReadAllText(arg)); else if (!one.Success) OpenModpack(arg);
        else { var r = mods.Rows.Cast<DataRow>().FirstOrDefault(x => (string)x["User"] == one.Groups[2].Value && (string)x["Slug"] == one.Groups[3].Value);
            if (r == null) Toast(Fail, "That " + one.Groups[1].Value + " is not on sotf-mods.com"); else { view = (string)r["Kind"] == Build ? 4 : 0; search.Text = (string)r["Name"]; Filter(true); if (Pending(r)) Act(r); else Toast(Done, r["Name"] + " is already installed"); } } }
    async void OpenModpack(string link) { // only the user and slug of the link are used, and only to call sotf-mods.com
        var m = Regex.Match(link ?? "", @"modpacks?/([\w.-]+)/([\w.-]+)"); if (!m.Success) { Toast(Fail, "That is not a modpack link from sotf-mods.com"); return; }
        try { string json; using (var web = Web()) json = await web.DownloadStringTaskAsync(SiteUrl + "/api/modpacks/slug/" + m.Groups[1].Value + "/" + m.Groups[2].Value + "/download"); Import(json); }
        catch (Exception ex) { Toast(Fail, "Could not get the modpack: " + ex.Message, "Retry", () => OpenModpack(link)); }
    }
    void Import(string profile) { // a profile file or a modpack: a new profile with the latest version of each mod
        List<string> ids; string name;
        try { var json = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(profile); name = NewName(json["profile"] as string);
            ids = ((IEnumerable)json["mods"]).Cast<Dictionary<string, object>>().Select(m => m["id"] as string).Where(id => id != null).Distinct().ToList();
        } catch (Exception ex) { Toast(Fail, "Not a valid profile file: " + ex.Message); return; }
        var found = ids.Select(ById).Where(r => r != null).ToList(); var missing = ids.Count - found.Count;
        var text = "Import " + name + " as a new profile and download its " + Plural(found.Count, "mod") + " from sotf-mods.com: " + Names(found) + "." + (missing > 0 ? " " + Plural(missing, "mod") + (missing == 1 ? " is" : " are") + " no longer on the site and will be skipped." : "");
        if (Ask("Import profile", text, new[] { "Import and switch", "Cancel" }) != "Import and switch") return;
        Run("Importing " + name, async t => { // dependencies first, each mod once, always the latest version
            EnsureClosed(); if (LoaderVersion == null) throw new Exception("install RedLoader first.");
            Directory.CreateDirectory(Path.Combine(Profiles, name)); Switch(name); var seen = new HashSet<DataRow>();
            foreach (var r in found) foreach (var m in Deps(r, seen).Concat(seen.Add(r) ? new[] { r } : new DataRow[0]).ToList()) await InstallMod(m, t);
            return name + " imported with " + Plural(seen.Count, "mod"); });
    }
}
