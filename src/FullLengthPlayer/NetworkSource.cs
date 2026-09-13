using System.Text.RegularExpressions;

namespace FullLengthPlayer;

internal sealed class NetworkSource
{
    internal const string PatreonReferer = "https://www.patreon.com";
    internal string Url { get; }
    internal string Referer { get; }
    internal string[] Headers { get; }
    private NetworkSource(string url, string referer, string[] headers) { Url = url; Referer = referer; Headers = headers; }
    internal static NetworkSource Parse(string url, string referer, string fields)
    {
        url = url.Trim(); referer = referer.Trim();
        bool ValidUrl(string value) => !value.Any(char.IsControl) && Uri.TryCreate(value, UriKind.Absolute, out var u)
            && u.Scheme is "http" or "https" && string.IsNullOrEmpty(u.UserInfo);
        if (!ValidUrl(url)) throw new ArgumentException("Enter a direct HTTP or HTTPS media URL.");
        if (referer.Length > 0 && !ValidUrl(referer)) throw new ArgumentException("Referer must be an HTTP/HTTPS URL, or blank.");
        var headers = new List<string>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in fields.Replace("\r\n", "\n").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            int colon = raw.IndexOf(':');
            if (colon <= 0) throw new ArgumentException("Use one header per line: Name: value.");
            string name = raw[..colon].Trim(), value = raw[(colon + 1)..].Trim();
            if (!Regex.IsMatch(name, "^[!#$%&'*+.^_`|~0-9A-Za-z-]+$") || value.Any(char.IsControl))
                throw new ArgumentException("Invalid header name or control character in a header.");
            if (name.Equals("Referer", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Use the Referer field above for that header.");
            if (!names.Add(name)) throw new ArgumentException("Each header name may appear only once.");
            headers.Add(name + ": " + value);
        }
        return new NetworkSource(url, referer, headers.ToArray());
    }
}

internal sealed class NetworkSourceDialog : Form
{
    internal NetworkSource? Source { get; private set; }
    internal NetworkSourceDialog(bool reaction)
    {
        Text = "Open stream URL"; ClientSize = new Size(620, 360);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        var url = new TextBox { Left = 16, Top = 45, Width = 585 };
        var patreon = new CheckBox { Text = "Patreon Referer preset", Left = 16, Top = 78, Width = 230, Checked = reaction };
        var referer = new TextBox { Left = 16, Top = 128, Width = 585, Text = reaction ? NetworkSource.PatreonReferer : "" };
        patreon.CheckedChanged += (_, _) => referer.Text = patreon.Checked ? NetworkSource.PatreonReferer : "";
        var headers = new TextBox { Left = 16, Top = 184, Width = 585, Height = 90, Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true };
        var open = new Button { Text = "Open stream", Left = 390, Top = 320, Width = 115 };
        var cancel = new Button { Text = "Cancel", Left = 515, Top = 320, Width = 85, DialogResult = DialogResult.Cancel };
        void LabelAt(string text, int top) => Controls.Add(new Label { Text = text, Left = 16, Top = top, Width = 590, Height = 24 });
        LabelAt("YouTube video link or direct media / .m3u8 URL (not a Patreon post)", 18);
        LabelAt("Referer (editable; leave blank for streams that do not need it)", 106);
        LabelAt("Optional HTTP headers — one Name: value per line", 160);
        LabelAt("URLs and headers are used for this load only; they are not saved to disk.", 285);
        Controls.AddRange(new Control[] { url, patreon, referer, headers, open, cancel });
        url.TextChanged += (_, _) =>
        {
            bool youtube = YouTubeResolver.IsYouTube(url.Text);
            patreon.Enabled = referer.Enabled = headers.Enabled = !youtube;
            open.Text = youtube ? "Open YouTube" : "Open stream";
        };
        open.Click += (_, _) =>
        {
            try
            {
                bool youtube = YouTubeResolver.IsYouTube(url.Text);
                Source = NetworkSource.Parse(youtube ? YouTubeResolver.CanonicalUrl(url.Text) : url.Text, youtube ? "" : referer.Text, youtube ? "" : headers.Text);
                DialogResult = DialogResult.OK;
            }
            catch (ArgumentException e) { MessageBox.Show(this, e.Message, "Stream settings"); }
        };
        AcceptButton = open; CancelButton = cancel;
    }
}
