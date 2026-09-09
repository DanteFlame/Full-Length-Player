namespace FullLengthPlayer;

internal sealed class MainForm : Form
{
    internal PlayerPane Reaction { get; } = new("Reaction A");
    internal PlayerPane Source { get; } = new("Source B");
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 200 };
    private PlayerPane active;
    internal MasterTransport Master { get; }
    private readonly ToolStrip masterBar = new() { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
    private readonly TrackBar masterTimeline = new() { Dock = DockStyle.Top, Height = 32, Maximum = 10000, TickStyle = TickStyle.None };
    private readonly Label masterStatus = new() { Dock = DockStyle.Top, Height = 24, ForeColor = Color.White, AutoEllipsis = true };
    private bool masterDragging;
    private readonly FlowLayoutPanel syncBar = new() { Dock = DockStyle.Top, Height = 36, AutoScroll = true, WrapContents = false, BackColor = SystemColors.Control };
    private readonly NumericUpDown offsetInput = new() { DecimalPlaces = 3, Increment = 0.1m, Minimum = -604800, Maximum = 604800, Width = 110 };
    private readonly Label syncStatus = new() { AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
    private double displayedOffset = double.NaN;
    public MainForm()
    {
        Text = "Full-Length Player";
        BackColor = Color.Black;
        ClientSize = new Size(1280, 650);
        MinimumSize = new Size(320, 240);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        active = Reaction;
        Master = new MasterTransport(() => Reaction.Player, () => Source.Player);
        Reaction.ManualTransport += () => Master.Unlock("Unlocked by independent control — relock after aligning");
        Source.ManualTransport += () => Master.Unlock("Unlocked by independent control — relock after aligning");
        void SyncButton(string text, Action action)
        {
            var button = new Button { Text = text, AutoSize = true };
            button.Click += (_, _) => RunMaster(action);
            syncBar.Controls.Add(button);
        }
        SyncButton("Lock current alignment", Master.CaptureAlignment);
        SyncButton("Unlock", () => Master.Unlock());
        syncBar.Controls.Add(new Label { Text = "Offset B−A (s)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        syncBar.Controls.Add(offsetInput);
        // Apply explicit edits only; timer updates must never change the stored offset.
        SyncButton("Apply offset", () => Master.SetOffset((double)offsetInput.Value));
        SyncButton("−0.1 s", () => Master.Nudge(-0.1));
        SyncButton("+0.1 s", () => Master.Nudge(0.1));
        syncBar.Controls.Add(syncStatus);
        masterBar.Items.Add(new ToolStripLabel("BOTH PLAYERS"));
        void MasterButton(string text, Action action)
        {
            var button = new ToolStripButton(text);
            button.Click += (_, _) => RunMaster(action);
            masterBar.Items.Add(button);
        }
        MasterButton("Play / Pause both", Master.TogglePause);
        MasterButton("−10 s both", () => Master.Jump(-10));
        MasterButton("+10 s both", () => Master.Jump(10));
        masterTimeline.MouseDown += (_, _) => masterDragging = true;
        masterTimeline.MouseUp += (_, _) => { masterDragging = false; SeekMasterTimeline(); };
        masterTimeline.KeyUp += (_, e) => { if (e.KeyCode is Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown) SeekMasterTimeline(); };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(Reaction, 0, 0);
        layout.Controls.Add(Source, 1, 0);
        var info = new Label { Dock = DockStyle.Bottom, Height = 25, ForeColor = Color.White, Text = "Space / arrows: BOTH • F1 / F2: select A / B • Shift+Space / Shift+arrows: selected player • Ctrl+O: open", AutoEllipsis = true };
        Controls.Add(layout);
        Controls.Add(info);
        Controls.Add(masterTimeline);
        Controls.Add(masterStatus);
        Controls.Add(syncBar);
        Controls.Add(masterBar);
        UpdateMaster();
        Reaction.Activated += SelectPane;
        Source.Activated += SelectPane;
        SelectPane(Reaction);
        timer.Tick += (_, _) => { Reaction.UpdatePlayback(); Source.UpdatePlayback(); try { Master.Tick(); } catch (Exception e) { Master.Unlock("Sync stopped: " + e.Message); } UpdateMaster(); };
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
    private void RunMaster(Action action)
    {
        try { action(); } catch (Exception e) { MessageBox.Show(this, e.Message, "Shared playback error"); }
        UpdateMaster();
    }
    private void SeekMasterTimeline()
    {
        var position = Master.Snapshot();
        if (position != null) RunMaster(() => Master.SeekReaction(position.ADuration * masterTimeline.Value / 10000.0));
    }
    private void UpdateMaster()
    {
        var position = Master.Snapshot();
        masterBar.Enabled = masterTimeline.Enabled = syncBar.Enabled = position != null;
        syncStatus.Text = $"{Master.SyncStatus}" + (Master.Locked ? $" • Fixed offset {Master.Offset:+0.000;-0.000;0.000} s • Drift {Master.Drift.GetValueOrDefault():+0.000;-0.000;0.000} s" : "");
        if (displayedOffset != Master.Offset)
        {
            displayedOffset = Master.Offset;
            offsetInput.Value = Math.Clamp((decimal)Master.Offset, offsetInput.Minimum, offsetInput.Maximum);

        }
        if (position == null) { masterStatus.Text = "Load both videos to use shared controls"; masterTimeline.Value = 0; return; }
        if (!masterDragging) masterTimeline.Value = (int)Math.Clamp(position.ATime / position.ADuration * 10000, 0, 10000);
        masterStatus.Text = $"Shared timeline (Reaction A): {TimeSpan.FromSeconds(position.ATime):hh\\:mm\\:ss} / {TimeSpan.FromSeconds(position.ADuration):hh\\:mm\\:ss} • Seeks move both equally, stopping at either file’s boundary";
    }
    private void SelectPane(PlayerPane pane)
    {
        active = pane;
        Reaction.SetActive(pane == Reaction);
        Source.SetActive(pane == Source);
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (offsetInput.ContainsFocus || Reaction.TrackMenuOpen || Source.TrackMenuOpen) return base.ProcessCmdKey(ref msg, keyData);
        Action? action = keyData switch
        {
            Keys.Oemcomma => () => RunMaster(() => Master.Nudge(-0.1)),
            Keys.OemPeriod => () => RunMaster(() => Master.Nudge(0.1)),
            Keys.F1 => () => SelectPane(Reaction),
            Keys.F2 => () => SelectPane(Source),
            Keys.Control | Keys.O => () => active.Open(),
            Keys.Space => Master.TogglePause,
            Keys.Left => () => Master.Jump(-5),
            Keys.Right => () => Master.Jump(5),
            Keys.Shift | Keys.Space => () => active.TogglePause(),
            Keys.Shift | Keys.Left => () => active.Seek(-5),
            Keys.Shift | Keys.Right => () => active.Seek(5),
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
