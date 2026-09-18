using System.Runtime.InteropServices;
using System.Text.Json;

namespace FullLengthPlayer;
internal sealed record SavedMedia(string Kind, string Location, string Referer, string[] Headers, double Position, string Audio, string Subtitles);
internal sealed record SavedSettings(Dictionary<string, decimal> Numbers, int Anchor, int Canvas, double VolumeA, double VolumeB, string MuteA, string MuteB, double Speed, bool ReactionBottom = false);
internal sealed record SavedSession(int Version, SavedMedia A, SavedMedia B, SavedSettings Settings, bool Locked, double Offset, double Clock);
internal static class SessionStore
{
    internal static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FullLengthPlayer");
    internal static string LastPath => Path.Combine(DirectoryPath, "last-session.flpsession");
    internal static string SettingsPath => Path.Combine(DirectoryPath, "view-settings.json");
    internal static void AtomicWrite(string path, byte[] bytes)
    {
        path = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temporary, bytes); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    internal static void Save(string path, SavedSession session) => AtomicWrite(path, Protect(JsonSerializer.SerializeToUtf8Bytes(session), false));
    internal static SavedSession Read(string path)
    {
        if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidOperationException("Session file is too large.");
        try
        {
            var session = JsonSerializer.Deserialize<SavedSession>(Protect(File.ReadAllBytes(path), true));
            if (session == null || session.Version != 1 || session.A == null || session.B == null || session.Settings == null
                || !double.IsFinite(session.Offset) || Math.Abs(session.Offset) > 604800 || !double.IsFinite(session.Clock) || Math.Abs(session.Clock) > 604800) throw new InvalidOperationException();
            Validate(session.A); Validate(session.B); Validate(session.Settings);
            return session;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException)
        { throw new InvalidOperationException("Cannot read this session. Use a valid session saved by this Windows account on this PC."); }
    }
    internal static void Validate(SavedMedia media)
    {
        if (!double.IsFinite(media.Position) || media.Position < 0 || media.Position > 604800 || string.IsNullOrEmpty(media.Location)) throw new InvalidOperationException();
        if (media.Kind == "local") { if (!Path.IsPathFullyQualified(media.Location)) throw new InvalidOperationException(); }
        else if (media.Kind == "youtube") YouTubeResolver.CanonicalUrl(media.Location);
        else if (media.Kind == "network") NetworkSource.Parse(media.Location, media.Referer, string.Join("\n", media.Headers ?? Array.Empty<string>()));
        else throw new InvalidOperationException();
        static bool Track(string? value) => value is "no" or "auto" || int.TryParse(value, out int id) && id > 0;
        if (!Track(media.Audio) || !Track(media.Subtitles)) throw new InvalidOperationException();
    }
    internal static void Validate(SavedSettings settings)
    {
        if (settings.Numbers == null || settings.Anchor is < 0 or > 7 || settings.Canvas is < 0 or > 2
            || !double.IsFinite(settings.Speed) || settings.Speed is < 0.25 or > 4
            || !double.IsFinite(settings.VolumeA) || settings.VolumeA is < 0 or > 100
            || !double.IsFinite(settings.VolumeB) || settings.VolumeB is < 0 or > 100
            || settings.MuteA is not ("yes" or "no") || settings.MuteB is not ("yes" or "no")) throw new InvalidOperationException();
    }
    internal static void SaveSettings(SavedSettings settings) => AtomicWrite(SettingsPath, JsonSerializer.SerializeToUtf8Bytes(settings));
    internal static SavedSettings? ReadSettings()
    {
        try { if (new FileInfo(SettingsPath).Length > 1024 * 1024) return null; var s = JsonSerializer.Deserialize<SavedSettings>(File.ReadAllBytes(SettingsPath)); if (s != null) Validate(s); return s; }
        catch { return null; } // A bad optional settings file must not block startup.
    }
    [StructLayout(LayoutKind.Sequential)] private struct Blob { public int Size; public IntPtr Data; }
    private static byte[] Protect(byte[] bytes, bool decrypt)
    {
        Blob input = new() { Size = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) }, output = default;
        try
        {
            Marshal.Copy(bytes, 0, input.Data, bytes.Length);
            bool ok = decrypt ? CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok) throw new InvalidOperationException("Windows could not protect or open this session.");
            var result = new byte[output.Size]; Marshal.Copy(output.Data, result, 0, result.Length); return result;
        }
        finally { Marshal.FreeHGlobal(input.Data); if (output.Data != IntPtr.Zero) LocalFree(output.Data); }
    }
    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptProtectData(ref Blob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr value);
}
