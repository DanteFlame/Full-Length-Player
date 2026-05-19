using System.Text.Json;

namespace FullLengthPlayer;

internal sealed class AppSettings
{
    public string? ReactionSource { get; set; }
    public string? ContentSource { get; set; }
    public double OffsetSeconds { get; set; }
    public int ReactionVolume { get; set; } = 100;
    public int ContentVolume { get; set; } = 100;
    public float PlaybackRate { get; set; } = 1.0f;
    public bool LockStep { get; set; } = true;
    public int OverlayPercent { get; set; } = 33;
    public bool OverlayVisible { get; set; } = true;

    private static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FullLengthPlayer");

    private static string SettingsFile => Path.Combine(SettingsDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return new AppSettings();
            var json = File.ReadAllText(SettingsFile);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFile, json);
    }
}
