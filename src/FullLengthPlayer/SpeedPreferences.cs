using System.Text.Json;

namespace FullLengthPlayer;

internal sealed class SpeedPreferences
{
    private readonly string path;
    internal double Favorite { get; private set; } = 2;
    internal SpeedPreferences(string? file = null)
    {
        path = file ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FullLengthPlayer", "preferences.json");
        try
        {
            if (!File.Exists(path)) return;
            using var data = JsonDocument.Parse(File.ReadAllText(path));
            if (data.RootElement.TryGetProperty("favoriteSpeed", out var property) && property.TryGetDouble(out var value)
                && double.IsFinite(value) && value >= SpeedControl.Minimum && value <= SpeedControl.Maximum) Favorite = value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { }
    }
    internal void Save(double speed)
    {
        if (!double.IsFinite(speed) || speed < SpeedControl.Minimum || speed > SpeedControl.Maximum) throw new ArgumentOutOfRangeException(nameof(speed));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(new { favoriteSpeed = speed }));
        File.Move(temp, path, true);
        Favorite = speed;
    }
}
