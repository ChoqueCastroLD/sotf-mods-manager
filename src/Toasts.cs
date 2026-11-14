// Notifications in the bottom right corner: stacked, deduplicated, timed by text length. Errors stay until closed.
using System; using System.Collections.Generic; using System.Drawing; using System.Linq; using System.Windows.Forms;

partial class SotfModsManager {
    List<Panel> toasts = new List<Panel>();

    Panel Toast(char icon, string text, string action = null, Action onAction = null, Action expire = null, Panel existing = null) { // existing turns a loading toast into its result
        var t = existing ?? toasts.FirstOrDefault(x => x.AccessibleName == text);
        if (t == null) { t = new Panel { Size = new Size(380, 60), BackColor = Raised, Padding = new Padding(14, 0, 6, 0) }; toasts.Add(t); Controls.Add(t); }
        var old = t.Tag as Timer; if (old != null) old.Dispose(); t.Tag = null; t.AccessibleName = text; t.Controls.Clear(); t.BringToFront();
        var msg = new Label { Name = "msg", Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Padding = new Padding(6, 0, 0, 0) }; tip.SetToolTip(msg, text);
        t.Controls.Add(msg);
        if (action != null) { var a = Btn('\0', action, () => { CloseToast(t); onAction(); }, Raised, 80); a.ForeColor = RedHover; t.Controls.Add(a); }
        if (icon != Busy) t.Controls.Add(Btn(CloseIcon, null, () => CloseToast(t), Raised, 36));
        t.Controls.Add(new Label { Text = icon.ToString(), Dock = DockStyle.Left, Width = 26, Font = new Font(IconFont, 12f), ForeColor = icon == Fail ? RedHover : icon == Busy ? Muted : Fg, TextAlign = ContentAlignment.MiddleLeft });
        if (icon != Busy && icon != Fail) { var tm = new Timer { Interval = 3000 + 60 * text.Length }; tm.Tick += (s, e) => { CloseToast(t); if (expire != null) expire(); }; t.Tag = tm; tm.Start(); }
        foreach (Control c in t.Controls) if (c is Button) c.Dock = DockStyle.Right;
        LayoutToasts(); return t;
    }
    void Status(Panel t, string text) { t.Controls["msg"].Text = text; }
    void CloseToast(Panel t) { var tm = t.Tag as Timer; if (tm != null) tm.Dispose(); toasts.Remove(t); Controls.Remove(t); t.Dispose(); LayoutToasts(); }
    void LayoutToasts() { var y = ClientSize.Height - 72; for (var i = toasts.Count - 1; i >= 0; i--) { y -= toasts[i].Height; toasts[i].Location = new Point(ClientSize.Width - toasts[i].Width - 24, y); y -= 8; } } // above the pager
}
