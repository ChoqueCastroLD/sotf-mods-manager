// Mod pictures: downloaded the first time they are drawn, cached on disk, decoded by Windows (WIC reads WebP), drawn with a loading or missing indicator.
using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Drawing2D; using System.IO; using System.Linq; using System.Net;
using System.Text.RegularExpressions; using System.Threading.Tasks; using System.Windows.Forms; using System.Windows.Media.Imaging;

partial class SotfModsManager {
    static readonly string PicDir = Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"SotfModsManager\thumbs")).FullName;
    static readonly Image Missing = new Bitmap(1, 1); // marks a picture that could not be loaded
    Dictionary<string, Image> pics = new Dictionary<string, Image>(); // a null value means it is loading

    // size picks the site's resized copy (320 or 640 px wide); width is the decoded width kept in memory
    void DrawPicture(Graphics g, string url, Rectangle box, int size, int width, bool cover) { // cover crops to fill the box, otherwise the whole picture fits inside
        Fill(g, Raised, box); Image img = Missing; var key = size + url;
        if (url != "" && !pics.TryGetValue(key, out img)) { pics[key] = null; LoadPicture(key, url, size, width); }
        if (img == null || img == Missing) { TextRenderer.DrawText(g, img == null ? "Loading picture" : "No picture", Small, box, Muted, Center | TextFormatFlags.WordBreak); return; }
        var s = cover ? Math.Max(box.Width / (float)img.Width, box.Height / (float)img.Height) : Math.Min(box.Width / (float)img.Width, box.Height / (float)img.Height);
        if (!cover) Fill(g, Bg, box); g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.SetClip(box);
        g.DrawImage(img, box.X + (box.Width - img.Width * s) / 2, box.Y + (box.Height - img.Height * s) / 2, img.Width * s, img.Height * s); g.ResetClip();
    }
    async void LoadPicture(string key, string url, int size, int width) { // downloads run async on the UI thread, only decoding uses a worker
        Image img = Missing; var file = Path.Combine(PicDir, size + "_" + Regex.Replace(url, @"\W", "_"));
        try { if (!File.Exists(file)) File.WriteAllBytes(file, await Download(url, size)); img = await Task.Run(() => Decode(file, width)); } catch { }
        pics[key] = img; grid.Invalidate(); gallery.Invalidate();
    }
    static async Task<byte[]> Download(string url, int size) { // the resized copy, or the original when the site has none
        var copy = Regex.Replace(url, @"original\.webp$", size + ".webp");
        using (var web = Web()) { try { return await web.DownloadDataTaskAsync(copy); } catch (WebException) { if (copy == url) throw; } return await web.DownloadDataTaskAsync(url); }
    }
    static Image Decode(string file, int width) {
        try { var src = new BitmapImage(); src.BeginInit(); src.UriSource = new Uri(file); src.DecodePixelWidth = width; src.CacheOption = BitmapCacheOption.OnLoad; src.EndInit();
            var bmp = new BmpBitmapEncoder(); bmp.Frames.Add(BitmapFrame.Create(src)); var ms = new MemoryStream(); bmp.Save(ms); return new Bitmap(ms);
        } catch { File.Delete(file); throw; } // a damaged cache file is fetched again next time
    }
    void ForgetPictures(int size, string[] keep) { // large gallery pictures are freed when another mod is shown, so memory stays low
        foreach (var k in pics.Keys.Where(k => k.StartsWith(size + "http") && !keep.Contains(k.Substring(3))).ToList()) { var i = pics[k]; pics.Remove(k); if (i != null && i != Missing) i.Dispose(); }
    }
}
