namespace FullLengthPlayer;

// Milestone 0: the window must launch without a media engine or saved settings.
internal sealed class MainForm : Form
{
    public MainForm()
    {
        Text = "Full-Length Player";
        BackColor = Color.Black;
        ClientSize = new Size(960, 540);
        MinimumSize = new Size(320, 240);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
    }
}
