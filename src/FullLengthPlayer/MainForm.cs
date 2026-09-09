namespace FullLengthPlayer;

internal sealed class MainForm : Form
{
    internal PlayerPane Reaction { get; } = new("Reaction A");
    internal PlayerPane Source { get; } = new("Source B");
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 200 };
    private PlayerPane active;
    public MainForm()
    {
        Text = "Full-Length Player";
        BackColor = Color.Black;
        ClientSize = new Size(1280, 650);
        MinimumSize = new Size(320, 240);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        active = Reaction;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(Reaction, 0, 0);
        layout.Controls.Add(Source, 1, 0);
        var info = new Label { Dock = DockStyle.Bottom, Height = 25, ForeColor = Color.White, Text = "Independent playback • F1: Reaction A • F2: Source B • Space / arrows / Ctrl+O: selected player", AutoEllipsis = true };
        Controls.Add(layout);
        Controls.Add(info);
        Reaction.Activated += SelectPane;
        Source.Activated += SelectPane;
        SelectPane(Reaction);
        timer.Tick += (_, _) => { Reaction.UpdatePlayback(); Source.UpdatePlayback(); };
        Shown += async (_, _) =>
        {
            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            bool verify = args.Length == 3 && args[0] == "--verify-playback";
            try
            {
                Reaction.Initialize(verify);
                Source.Initialize(verify);
                timer.Start();
                if (verify) await PlaybackVerification.Run(this, args[1], args[2]);
                else
                {
                    if (args.Length >= 1) Reaction.LoadVideo(args[0]);
                    if (args.Length >= 2) Source.LoadVideo(args[1]);
                }
            }
            catch (Exception error)
            {
                if (verify) { File.WriteAllText(args[2] + ".error.txt", error.ToString()); Environment.ExitCode = 1; Close(); }
                else
                {
                    var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FullLengthPlayer", "logs");
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory, "mpv-error.log"), error.ToString());
                    MessageBox.Show(this, error.ToString(), "MPV initialization error");
                }
            }
        };
    }
    private void SelectPane(PlayerPane pane)
    {
        active = pane;
        Reaction.SetActive(pane == Reaction);
        Source.SetActive(pane == Source);
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (Reaction.TrackMenuOpen || Source.TrackMenuOpen) return base.ProcessCmdKey(ref msg, keyData);
        Action? action = keyData switch
        {
            Keys.F1 => () => SelectPane(Reaction),
            Keys.F2 => () => SelectPane(Source),
            Keys.Control | Keys.O => () => active.Open(),
            Keys.Space => () => active.TogglePause(),
            Keys.Left => () => active.Seek(-5),
            Keys.Right => () => active.Seek(5),
            _ => null
        };
        if (action == null) return base.ProcessCmdKey(ref msg, keyData);
        try { action(); } catch (Exception e) { MessageBox.Show(this, e.Message, "Playback error"); }
        return true;
    }
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        timer.Stop(); timer.Dispose();
        Reaction.Shutdown(); Source.Shutdown();
        base.OnFormClosed(e);
    }
}
