using System.Text;
using LibVLCSharp.Shared;

namespace FullLengthPlayer;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);
            Core.Initialize();
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            var message = BuildStartupErrorMessage(ex);
            TryWriteStartupLog(message);

            MessageBox.Show(
                message,
                "Full-Length Player failed to start",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static string BuildStartupErrorMessage(Exception ex)
    {
        var builder = new StringBuilder();
        builder.AppendLine("The app failed during startup.");
        builder.AppendLine();
        builder.AppendLine($"Executable directory: {AppContext.BaseDirectory}");
        builder.AppendLine($"Current directory: {Environment.CurrentDirectory}");
        builder.AppendLine();
        builder.AppendLine("Error details:");
        builder.AppendLine(ex.ToString());
        builder.AppendLine();
        builder.AppendLine("A copy of this message was written to startup-error.log next to the executable.");
        return builder.ToString();
    }

    private static void TryWriteStartupLog(string content)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "startup-error.log");
            File.WriteAllText(logPath, content);
        }
        catch
        {
            // Ignore file write failures; startup error dialog is still shown.
        }
    }
}
