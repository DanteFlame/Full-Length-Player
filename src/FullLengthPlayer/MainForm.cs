using System.Globalization;

namespace FullLengthPlayer;

internal sealed class MainForm : Form
{
    private readonly Panel video = new() { Dock = DockStyle.Fill, BackColor = Color.Black };
    private readonly Label status = new() { Text = "Open a local video to begin", Dock = DockStyle.Bottom, Height = 26, ForeColor = Color.White, AutoEllipsis = true };
    private readonly TrackBar timeline = new() { Dock = DockStyle.Bottom, Height = 32, Maximum = 10000, TickStyle = TickStyle.None };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 200 };
    private MpvPlayer? player;
    private bool dragging;
    private string? playbackError;
    public MainForm()
    {
        Text = "Full-Length Player";
        BackColor = Color.Black;
        ClientSize = new Size(960, 540);
        MinimumSize = new Size(320, 240);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 72, AutoScroll = true, BackColor = SystemColors.Control };
        void Button(string text, Action action)
        {
            var button = new Button { Text = text, AutoSize = true };
            button.Click += (_, _) => Run(action);
            toolbar.Controls.Add(button);
        }
        Button("Open video", Open);
        Button("Play / Pause", () => player?.Command("cycle", "pause"));
        Button("−5 s", () => Seek(-5));
        Button("+5 s", () => Seek(5));
        Button("Audio track", () => player?.Command("cycle", "aid"));
        Button("Subtitle track", () => player?.Command("cycle", "sid"));
        Button("Add subtitles", AddSubtitles);
        toolbar.Controls.Add(new Label { Text = "Volume", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        var volume = new TrackBar { Minimum = 0, Maximum = 100, Value = 100, Width = 120, Height = 28, TickStyle = TickStyle.None };
        volume.ValueChanged += (_, _) => Run(() => player?.Set("volume", volume.Value.ToString(CultureInfo.InvariantCulture)));
        toolbar.Controls.Add(volume);
        Controls.Add(video);
        Controls.Add(timeline);
        Controls.Add(status);
        Controls.Add(toolbar);
        timeline.MouseDown += (_, _) => dragging = true;
        timeline.MouseUp += (_, _) => { dragging = false; Run(SeekTimeline); };
        timeline.KeyUp += (_, _) => Run(SeekTimeline);
        timer.Tick += (_, _) => UpdatePlayback();
        AllowDrop = true;
        DragEnter += (_, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
        DragDrop += (_, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0) Run(() => LoadVideo(files[0])); };
        Shown += async (_, _) =>
        {
            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            bool verify = args.Length == 3 && args[0] == "--verify-playback";
            try
            {
                player = new MpvPlayer(video.Handle, verify);
                timer.Start();
                if (verify) await PlaybackVerification.Run(this, player, args[1], args[2]);
                else if (args.Length == 1) LoadVideo(args[0]);
            }
            catch (Exception error)
            {
                if (verify) { File.WriteAllText(args[2] + ".error.txt", error.ToString()); Environment.ExitCode = 1; Close(); }
                else
                {
                    var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FullLengthPlayer", "logs");
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory, "mpv-error.log"), error.ToString());
                    status.Text = "MPV initialization failed";
                    MessageBox.Show(this, error.ToString(), "Playback error");
                }
            }
        };
    }
    private void Run(Action action) { try { action(); } catch (Exception e) { MessageBox.Show(this, e.Message, "Playback error"); } }
    private void Open()
    {
        using var dialog = new OpenFileDialog { Title = "Open local video", Filter = "Video files|*.mkv;*.mp4;*.webm;*.avi;*.mov;*.m4v;*.ts|All files|*.*" };
        if (dialog.ShowDialog(this) == DialogResult.OK) LoadVideo(dialog.FileName);
    }
    internal void LoadVideo(string path)
    {
        if (player == null) throw new InvalidOperationException("MPV is unavailable. Restart the app with all downloaded files together.");
        if (!File.Exists(path)) throw new FileNotFoundException("Local video not found.", path);
        playbackError = null;
        player.Command("loadfile", Path.GetFullPath(path), "replace");
        player.Set("pause", "no");
    }
    private void AddSubtitles()
    {
        using var dialog = new OpenFileDialog { Filter = "Subtitles|*.srt;*.ass;*.ssa;*.vtt|All files|*.*" };
        if (dialog.ShowDialog(this) == DialogResult.OK) player?.Command("sub-add", dialog.FileName, "select");
    }
    private void Seek(int seconds) => player?.Command("seek", seconds.ToString(CultureInfo.InvariantCulture), "relative+exact");
    private void SeekTimeline() => player?.Command("seek", (timeline.Value / 100.0).ToString(CultureInfo.InvariantCulture), "absolute-percent+exact");
    private void UpdatePlayback()
    {
        if (player == null) return;
        playbackError = player.PollError() ?? playbackError;
        double time = player.Number("time-pos"), duration = player.Number("duration");
        if (!dragging) timeline.Value = duration > 0 ? (int)Math.Clamp(time / duration * 10000, 0, 10000) : 0;
        status.Text = playbackError != null ? $"Could not play video: {playbackError}" : duration > 0
            ? $"{(player.Get("pause") == "yes" ? "Paused" : "Playing")}  {TimeSpan.FromSeconds(time):hh\\:mm\\:ss} / {TimeSpan.FromSeconds(duration):hh\\:mm\\:ss}   Audio: {player.Get("aid")}   Sub: {player.Get("sid")}"
            : "Open a local video to begin";
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.O: Run(Open); return true;
            case Keys.Space: Run(() => player?.Command("cycle", "pause")); return true;
            case Keys.Left: Run(() => Seek(-5)); return true;
            case Keys.Right: Run(() => Seek(5)); return true;
            default: return base.ProcessCmdKey(ref msg, keyData);
        }
    }
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        timer.Stop(); timer.Dispose(); player?.Dispose(); player = null;
        base.OnFormClosed(e);
    }
}
