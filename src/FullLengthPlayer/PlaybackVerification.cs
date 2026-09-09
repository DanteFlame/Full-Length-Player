using System.Text.Json;

namespace FullLengthPlayer;

internal static class PlaybackVerification
{
    // Exercises the same embedded instance used by the UI. CI audio output is null;
    // decoded audio is checked, but audible sound still requires a human test.
    public static async Task Run(MainForm form, MpvPlayer player, string media, string report)
    {
        async Task Until(Func<bool> condition, string failure)
        {
            var deadline = DateTime.UtcNow.AddSeconds(25);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline) throw new Exception(failure);
                await Task.Delay(100);
            }
        }
        form.LoadVideo(media);
        await Until(() => player.Number("time-pos") > 0.5 && player.Number("video-params/w") > 0, "Video did not start decoding.");
        if (player.Number("audio-params/samplerate") <= 0) throw new Exception("No decoded audio.");
        player.Set("pause", "yes");
        await Task.Delay(300);
        var paused = player.Number("time-pos");
        await Task.Delay(500);
        if (Math.Abs(player.Number("time-pos") - paused) > 0.15) throw new Exception("Pause did not hold position.");
        player.Command("seek", "4", "absolute+exact");
        await Until(() => Math.Abs(player.Number("time-pos") - 4) < 0.3, "Seek failed.");
        player.Set("volume", "35");
        if (Math.Abs(player.Number("volume") - 35) > 0.1) throw new Exception("Volume failed.");
        player.Command("sub-add", Path.ChangeExtension(media, ".srt"), "select");
        await Until(() => player.Get("sid") is not (null or "no" or "auto"), "Subtitle selection failed.");
        player.Command("screenshot-to-file", report + ".png", "subtitles");
        await Until(() => File.Exists(report + ".png"), "No decoded screenshot.");
        using (var frame = new Bitmap(report + ".png"))
        {
            if (frame.Width < 100 || frame.Height < 100) throw new Exception("Invalid decoded frame.");
        }
        player.Set("pause", "no");
        await Until(() => player.Number("time-pos") > 4.5, "Resume failed.");
        form.LoadVideo(media); // replacement/reopen on same native instance
        await Until(() => player.Number("time-pos") < 2, "Reopen failed.");
        await Until(() => player.Number("time-pos") > 0.5, "Reopened video did not advance.");
        File.WriteAllText(report, JsonSerializer.Serialize(new { passed = true, mpv = player.Get("mpv-version"), video = player.Get("video-codec"), audio = player.Get("audio-codec-name"), audioOutput = "null (CI only)", pause = true, seek = true, resume = true, reopen = true, subtitles = true }));
        form.Close();
    }
}
