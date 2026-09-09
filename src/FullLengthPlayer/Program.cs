namespace FullLengthPlayer;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
            return 0;
        }
        catch (Exception error)
        {
            var message = "Full-Length Player could not run.\n\n" + error;
            try
            {
                var directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FullLengthPlayer", "logs");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "startup-error.log");
                File.WriteAllText(path, message);
                message += "\n\nLog saved to: " + path;
            }
            catch
            {
                message += "\n\nThe error log could not be saved.";
            }
            MessageBox.Show(message, "Full-Length Player — error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
