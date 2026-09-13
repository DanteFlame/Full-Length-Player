using System.Text.RegularExpressions;

namespace FullLengthPlayer;

internal static class ResolverDiagnostics
{
    internal static string LogPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FullLengthPlayer", "logs", "youtube-error.log");
    internal static string Redact(string message, string input)
    {
        if (input.Length > 0) message = message.Replace(input, "[video URL]");
        if (YouTubeResolver.IsYouTube(input))
        {
            try { message = message.Replace(YouTubeResolver.CanonicalUrl(input).Split("v=")[1], "[video ID]"); } catch (ArgumentException) { }
        }
        message = Regex.Replace(message, @"https?://\S+", "[URL]", RegexOptions.IgnoreCase);
        message = Regex.Replace(message, @"(?im)^.*(?:cookie|authorization|password|bearer).*$", "[credential-related line omitted]");
        message = Regex.Replace(message, @"[A-Za-z0-9_\-+/=]{32,}", "[opaque value]");
        foreach (var path in new[] { AppContext.BaseDirectory, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) })
            if (path.Length > 0) message = message.Replace(path, "[local folder]", StringComparison.OrdinalIgnoreCase);
        return message.Length > 6000 ? message[..6000] : message;
    }
    internal static string Save(string error, string url, string kind)
    {
        string cleaned = Redact(error, url);
        string reason = error.Contains("not a bot", StringComparison.OrdinalIgnoreCase) ? "YouTube requested a sign-in/bot check."
            : error.Contains("format is not available", StringComparison.OrdinalIgnoreCase) ? "YouTube did not offer the requested video/audio formats."
            : error.Contains("certificate", StringComparison.OrdinalIgnoreCase) ? "The resolver reported a certificate error."
            : error.Contains("JavaScript", StringComparison.OrdinalIgnoreCase) || error.Contains("deno", StringComparison.OrdinalIgnoreCase) ? "The resolver reported a JavaScript runtime/challenge error."
            : kind == "timeout" ? "YouTube lookup timed out." : "YouTube lookup failed.";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.WriteAllText(LogPath, $"UTC: {DateTime.UtcNow:O}\nStage: {kind}\nReason: {reason}\nApp: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}\nyt-dlp: 2026.08.19; Deno: 2.9.6\n64-bit OS: {Environment.Is64BitOperatingSystem}\nyt-dlp exists: {File.Exists(Path.Combine(AppContext.BaseDirectory, "youtube", "yt-dlp.exe"))}\nDeno exists: {File.Exists(Path.Combine(AppContext.BaseDirectory, "youtube", "deno.exe"))}\n{cleaned}\n");
            return reason + "\n\nA redacted diagnostic was saved to:\n" + LogPath + "\n\nPlease share that file if retrying fails.";
        }
        catch (IOException) { return reason + " The diagnostic file could not be written."; }
        catch (UnauthorizedAccessException) { return reason + " The diagnostic folder is not writable."; }
    }
}
