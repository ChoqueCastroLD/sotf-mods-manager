// Profiles: each profile is its own set of mods. The active one lives in Mods; the others wait in .sotfmm-profiles in the game folder,
// so switching only moves folders on the same disk: instant, no downloads. Exported profiles are a small list of sotf-mods.com mods, not files.
using System; using System.Collections; using System.Collections.Generic; using System.Data; using System.Diagnostics; using System.Drawing; using System.IO; using System.IO.Compression; using System.Linq; using System.Security.Cryptography;
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
        Run("Deleting " + name, t => { var msg = Switch(next); TryDelete(Path.Combine(Profiles, name)); File.Delete(NotesFile(name)); return Task.FromResult(name + " deleted. " + msg); });
    }
    void ExportProfile() {
        var list = mods.Rows.Cast<DataRow>().Where(r => (string)r["Action"] != "Install" && (string)r["User"] != "").Select(r => new Dictionary<string, object> { { "id", r["Id"] }, { "name", r["Name"] }, { "user", r["User"] }, { "slug", r["Slug"] } }).ToList();
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
        var one = Regex.Match(Uri.UnescapeDataString(arg ?? ""), @"^sotfmm://(mod|build)/([^/?#]+)/([^/?#]+)"); // slugs can hold any character but a slash, for example axel's-mod-menu // one-click install from a mod or build page
        if (!Valid) Toast(Fail, "Choose the game folder in settings first"); else if (File.Exists(arg)) Import(File.ReadAllText(arg)); else if (!one.Success) OpenModpack(arg);
        else { var r = mods.Rows.Cast<DataRow>().FirstOrDefault(x => (string)x["User"] == one.Groups[2].Value && (string)x["Slug"] == one.Groups[3].Value);
            if (r == null) Toast(Fail, "That " + one.Groups[1].Value + " is not on sotf-mods.com"); else { view = (string)r["Kind"] == Build ? 4 : 0; search.Text = (string)r["Name"]; Filter(true); if (Pending(r)) Act(r); else Toast(Done, r["Name"] + " is already installed"); } } }
    async void OpenModpack(string link) { // only the user and slug of the link are used, and only to call sotf-mods.com
        var m = Regex.Match(Uri.UnescapeDataString(link ?? ""), @"modpacks?/([^/?#\s]+)/([^/?#\s]+)"); if (!m.Success) { Toast(Fail, "That is not a modpack link from sotf-mods.com"); return; }
        try { string json; using (var web = Web()) json = await web.DownloadStringTaskAsync(SiteUrl + "/api/modpacks/slug/" + Esc(m.Groups[1].Value) + "/" + Esc(m.Groups[2].Value) + "/download"); Import(json); }
        catch (Exception ex) { Toast(Fail, "Could not get the modpack: " + ex.Message, "Retry", () => OpenModpack(link)); }
    }
    void Import(string profile) { // a profile file or a modpack: a new profile with the latest version of each mod, the author's notes and, if the user agrees, the mod settings
        List<string> ids; string name; var notes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); Dictionary<string, object> configs;
        try { var json = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(profile); name = NewName(json["profile"] as string); object c, n;
            var list = ((IEnumerable)json["mods"]).Cast<Dictionary<string, object>>().ToList(); ids = list.Select(m => m["id"] as string).Where(id => id != null).Distinct().ToList();
            foreach (var m in list) if (m["id"] is string && m.TryGetValue("note", out n) && n is string && Note((string)n) != "") notes[(string)m["id"]] = Note((string)n);
            configs = json.TryGetValue("configs", out c) ? c as Dictionary<string, object> : null;
        } catch (Exception ex) { Toast(Fail, "Not a valid profile file: " + ex.Message); return; }
        var found = ids.Select(ById).Where(r => r != null).ToList(); var missing = ids.Count - found.Count; var noted = found.Where(r => notes.ContainsKey((string)r["Id"])).Select(r => r["Name"] + ": " + notes[(string)r["Id"]]).ToList();
        var text = "Import " + name + " as a new profile and download its " + Plural(found.Count, "mod") + " from sotf-mods.com: " + Names(found) + "." + (missing > 0 ? " " + Plural(missing, "mod") + (missing == 1 ? " is" : " are") + " no longer on the site and will be skipped." : "")
            + (noted.Count > 0 ? "\n\nNotes from the modpack author\n" + string.Join("\n", noted) : "") + (configs != null ? "\n\nIt also includes mod settings. You choose whether to apply them after the mods are installed." : "");
        if (Ask("Import profile", text, new[] { "Import and switch", "Cancel" }) != "Import and switch") return;
        Run("Importing " + name, async t => { // dependencies first, each mod once, always the latest version
            EnsureClosed(); if (LoaderVersion == null) throw new Exception("install RedLoader first.");
            Directory.CreateDirectory(Path.Combine(Profiles, name)); Switch(name); var seen = new HashSet<DataRow>(); var nf = NotesFile(name);
            if (notes.Count > 0) File.WriteAllText(nf, new JavaScriptSerializer().Serialize(notes)); else File.Delete(nf);
            foreach (var r in found) foreach (var m in Deps(r, seen).Concat(seen.Add(r) ? new[] { r } : new DataRow[0]).ToList()) await InstallMod(m, t);
            if (configs != null) BeginInvoke(new Action(() => ApplyConfigs(configs))); // runs once this task is done
            return name + " imported with " + Plural(seen.Count, "mod"); });
    }
    // Author notes: plain text per mod, kept next to the profile folder (not inside it, since that folder's content moves into Mods when the profile is active)
    Dictionary<string, string> authorNotes = new Dictionary<string, string>();
    string NotesFile(string profile) { return Path.Combine(Profiles, profile + ".notes.json"); }
    Dictionary<string, string> Notes(string profile) { try { return new Dictionary<string, string>(new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(File.ReadAllText(NotesFile(profile))), StringComparer.OrdinalIgnoreCase); } catch { return new Dictionary<string, string>(); } }
    static string Note(string s) { s = Regex.Replace(s.Replace('\t', ' '), @"[\p{Cc}-[\n]]", "").Trim(); return s.Length > 280 ? s.Substring(0, 280) : s; }

    // Mod settings of a modpack: a zip from sotf-mods.com, checked against its hash and every path before anything is written, all or nothing, with Undo
    static readonly string[] ConfigTypes = { ".cfg", ".toml", ".json", ".ini", ".txt", ".xml", ".yml", ".yaml" }, ConfigRoots = { "UserData", "Mods" };
    const int MaxConfigZip = 1 << 20, MaxConfigFile = 256 << 10, MaxConfigFiles = 100;
    async void ApplyConfigs(Dictionary<string, object> c) {
        if (busy) { Toast(InfoIcon, "Wait for the current task to finish"); return; }
        busy = true; var t = Toast(Busy, "Getting the modpack settings"); Func<string, string> get = k => { object v; return c.TryGetValue(k, out v) ? v as string : null; };
        try { var url = new Uri(get("url") ?? ""); if (url.Scheme != "https" || url.Host != "sotf-mods.com" && !url.Host.EndsWith(".sotf-mods.com")) throw new Exception("the settings are not on sotf-mods.com.");
            byte[] zip; using (var web = Web()) using (var s = await web.OpenReadTaskAsync(url)) zip = await Task.Run(() => ReadMax(s, MaxConfigZip, "the settings download"));
            using (var sha = SHA256.Create()) if (!string.Equals(BitConverter.ToString(sha.ComputeHash(zip)).Replace("-", ""), get("sha256"), StringComparison.OrdinalIgnoreCase)) throw new Exception("the download does not match the modpack.");
            var files = ConfigFiles(zip, Game); var shared = files.Keys.Any(k => k.StartsWith(@"UserData\", StringComparison.OrdinalIgnoreCase));
            if (Ask("Modpack settings", "This modpack includes " + Plural(files.Count, "config file") + " (mod settings). Apply them? Files with the same name are replaced; you can undo it."
                + (shared ? " Files in UserData are shared by all your profiles." : ""), new[] { "Apply settings", "Skip" }) != "Apply settings") { CloseToast(t); return; }
            EnsureClosed(); var undo = WriteConfigs(files);
            Toast(Done, "Applied " + Plural(files.Count, "config file") + " from the modpack", "Undo", () => { try { EnsureClosed(); undo.Item1(); Toast(Done, "Modpack settings undone"); } catch (Exception ex) { Toast(Fail, "Undo failed: " + ex.Message); } }, undo.Item2, t);
        } catch (Exception ex) { Toast(Fail, "Could not apply the modpack settings: " + ex.Message, "Retry", () => ApplyConfigs(c), existing: t); }
        finally { busy = false; }
    }
    static byte[] ReadMax(Stream s, int max, string what) { var m = new MemoryStream(); var b = new byte[65536]; int n;
        while ((n = s.Read(b, 0, b.Length)) > 0) { m.Write(b, 0, n); if (m.Length > max) throw new Exception(what + " is larger than " + (max < 1 << 20 ? max / 1024 + " KB." : (max >> 20) + " MB.")); } return m.ToArray(); }
    static Dictionary<string, byte[]> ConfigFiles(byte[] zip, string game) { // relative path to content; one entry that is not allowed rejects the whole set
        var root = Path.GetFullPath(game).TrimEnd('\\') + "\\"; var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        using (var z = new ZipArchive(new MemoryStream(zip))) foreach (var e in z.Entries) { var p = e.FullName.Replace('\\', '/'); if (p.EndsWith("/")) continue; var parts = p.Split('/');
            if (files.Count >= MaxConfigFiles) throw new Exception("the settings have more than " + MaxConfigFiles + " files.");
            var ok = parts.Length > 1 && !p.Contains(":") && !parts.Any(x => x == "" || x == "." || x == "..") && ConfigRoots.Contains(parts[0], StringComparer.OrdinalIgnoreCase) && ConfigTypes.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase);
            var target = ok ? Path.GetFullPath(Path.Combine(root, p.Replace('/', '\\'))) : "";
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase) || files.ContainsKey(target.Substring(root.Length))) throw new Exception("the settings contain a file that is not allowed: " + p);
            byte[] data; using (var s = e.Open()) data = ReadMax(s, MaxConfigFile, p); if (Array.IndexOf(data, (byte)0) >= 0) throw new Exception(p + " is not a text file.");
            files[target.Substring(root.Length)] = data; }
        return files;
    }
    Tuple<Action, Action> WriteConfigs(Dictionary<string, byte[]> files) { // replaced files are copied to the undo folder first; returns undo and forget
        string game = Game, backup = Path.Combine(Undo, "configs", DateTime.Now.ToString("yyyyMMdd-HHmmss")); var added = files.Keys.Select(k => Path.Combine(game, k)).Where(f => !File.Exists(f)).ToList();
        foreach (var k in files.Keys.Where(k => File.Exists(Path.Combine(game, k)))) { var b = Path.Combine(backup, k); Directory.CreateDirectory(Path.GetDirectoryName(b)); File.Copy(Path.Combine(game, k), b, true); }
        Action undo = () => { foreach (var k in files.Keys) { var b = Path.Combine(backup, k); if (File.Exists(b)) File.Copy(b, Path.Combine(game, k), true); } foreach (var f in added) File.Delete(f); TryDelete(backup); };
        try { foreach (var x in files) { var f = Path.Combine(game, x.Key); Directory.CreateDirectory(Path.GetDirectoryName(f)); File.WriteAllBytes(f, x.Value); } }
        catch { undo(); throw; } // a failed write leaves nothing half applied
        return Tuple.Create(undo, (Action)(() => TryDelete(backup)));
    }
}
