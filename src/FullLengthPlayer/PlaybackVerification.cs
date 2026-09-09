using System.Text.Json;

namespace FullLengthPlayer;

internal static class PlaybackVerification
{
    // Same two embedded instances and track menus as normal playback.
    // CI decodes both audio streams with null outputs; audible mixing is tested by Gonz.
    public static async Task Run(MainForm form, string media, string report)
    {
        var a = form.Reaction.Player!;
        var b = form.Source.Player!;
        async Task Until(Func<bool> condition, string failure)
        {
            var deadline = DateTime.UtcNow.AddSeconds(25);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline) throw new Exception(failure);
                await Task.Delay(100);
            }
        }
        void Assert(bool condition, string failure) { if (!condition) throw new Exception(failure); }
        var second = Path.Combine(Path.GetDirectoryName(media)!, "source video.mkv");
        form.Reaction.LoadVideo(media);
        form.Source.LoadVideo(second);
        await Until(() => a.Number("time-pos") > 0.5 && b.Number("time-pos") > 0.5 && a.Number("video-params/w") > 0 && b.Number("video-params/w") > 0, "Both videos must decode concurrently.");
        Assert(a.Number("audio-params/samplerate") > 0 && b.Number("audio-params/samplerate") > 0, "Both audio streams must decode.");
        form.Reaction.TogglePause();
        await Task.Delay(300);
        double paused = a.Number("time-pos"), running = b.Number("time-pos");
        await Task.Delay(700);
        Assert(Math.Abs(a.Number("time-pos") - paused) < 0.15 && b.Number("time-pos") > running + 0.3, "Pausing A must leave B playing.");
        form.Source.TogglePause();
        await Task.Delay(300);
        double held = b.Number("time-pos");
        a.Command("seek", "6", "absolute+exact");
        await Until(() => Math.Abs(a.Number("time-pos") - 6) < 0.3, "A seek failed.");
        Assert(Math.Abs(b.Number("time-pos") - held) < 0.15, "A seek changed B.");
        a.Set("volume", "35"); b.Set("volume", "70");
        Assert(a.Number("volume") == 35 && b.Number("volume") == 70, "Volumes must be independent.");
        var menu = form.Reaction.RefreshTrackMenu(false);
        var named = menu.DropDownItems.OfType<ToolStripMenuItem>().Single(x => x.Text.Contains("Japanese alternate"));
        Assert(named.Text.Contains("jpn"), "Track menu must show language metadata.");
        named.PerformClick();
        await Until(() => a.Get("aid") == "2", "Named audio selection failed.");
        Assert(b.Get("aid") == "1", "A audio selection changed B.");
        Assert(form.Reaction.RefreshTrackMenu(false).DropDownItems.OfType<ToolStripMenuItem>().Single(x => (string?)x.Tag == "2").Checked, "Current track is not marked.");
        a.Command("sub-add", Path.ChangeExtension(media, ".srt"), "select");
        await Until(() => a.Get("sid") is not (null or "no" or "auto"), "Subtitle loading failed.");
        var subMenu = form.Reaction.RefreshTrackMenu(true);
        Assert(subMenu.DropDownItems.OfType<ToolStripMenuItem>().Any(x => x.Text.Contains("external")), "External subtitle missing from menu.");
        subMenu.DropDownItems.OfType<ToolStripMenuItem>().Single(x => (string?)x.Tag == "no").PerformClick();
        await Until(() => a.Get("sid") == "no", "Subtitles off failed.");
        var audioB = form.Source.RefreshTrackMenu(false);
        Assert(!audioB.DropDownItems.OfType<ToolStripMenuItem>().Any(x => x.Text.Contains("Japanese alternate")), "Track menus leaked between players.");
        form.ClientSize = new Size(900, 550);
        foreach (var pair in new[] { (a, "reaction"), (b, "source") })
        {
            string image = report + "." + pair.Item2 + ".png";
            pair.Item1.Command("screenshot-to-file", image, "subtitles");
            await Until(() => File.Exists(image), "Missing decoded frame for " + pair.Item2);
            using var frame = new Bitmap(image);
            Assert(frame.Width >= 100 && frame.Height >= 100, "Invalid frame.");
        }
        form.Source.TogglePause();
        await Until(() => b.Number("time-pos") > held + 0.5, "B resume failed.");
        running = b.Number("time-pos");
        form.Reaction.LoadVideo(second);
        await Until(() => a.Number("time-pos") < 2, "A replacement did not reset position.");
        await Until(() => a.Number("time-pos") > 0.5, "A replacement did not play.");
        Assert(b.Number("time-pos") > running, "Replacing A interrupted B.");
        Assert(!form.Reaction.RefreshTrackMenu(false).DropDownItems.OfType<ToolStripMenuItem>().Any(x => x.Text.Contains("Japanese alternate")), "Stale track names after replacement.");
        form.Source.LoadVideo(media);
        await Until(() => b.Number("time-pos") < 2, "B replacement failed.");
        await Until(() => b.Number("time-pos") > 0.5, "B replacement did not play.");
        File.WriteAllText(report, JsonSerializer.Serialize(new { passed = true, milestone = 2, mpv = a.Get("mpv-version"), simultaneousVideo = true, simultaneousAudioDecode = true, audioOutput = "null (CI only)", independentPause = true, independentSeek = true, independentVolume = true, namedTrackMenus = true, trackIsolation = true, replacementBothPlayers = true }));
        form.Close();
    }
}
