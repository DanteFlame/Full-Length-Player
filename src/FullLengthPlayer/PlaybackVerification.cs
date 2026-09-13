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
        async Task Until(Func<bool> condition, string failure, bool waitTransport = true)
        {
            var deadline = DateTime.UtcNow.AddSeconds(25);
            while (!condition() || (waitTransport && form.Master.SeekingTogether))
            {
                if (DateTime.UtcNow > deadline) throw new Exception(failure);
                await Task.Delay(100);
            }
        }
        void Assert(bool condition, string failure) { if (!condition) throw new Exception(failure); }
        var second = Path.Combine(Path.GetDirectoryName(media)!, "source video.mkv");
        Assert(form.Master.Snapshot() == null, "Shared transport must wait for both media files.");
        form.Master.TogglePause(); form.Master.Jump(10); // empty pair is safe
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
        // Shared commands must converge mixed pause states, retain alignment and
        // leave volume/track choices alone. Exercise the same controller as the UI.
        a.Set("pause", "yes"); b.Set("pause", "no");
        form.Master.TogglePause();
        Assert(a.Get("pause") == "yes" && b.Get("pause") == "yes", "Master pause did not converge mixed states.");
        a.Command("seek", "8", "absolute+exact"); b.Command("seek", "12", "absolute+exact");
        await Until(() => Math.Abs(a.Number("time-pos") - 8) < 0.2 && Math.Abs(b.Number("time-pos") - 12) < 0.2, "Cannot prepare alignment test.");
        form.Master.Jump(10);
        await Until(() => Math.Abs(a.Number("time-pos") - 18) < 0.2 && Math.Abs(b.Number("time-pos") - 22) < 0.2, "Shared forward skip failed.");
        form.Master.Jump(-10);
        await Until(() => Math.Abs(a.Number("time-pos") - 8) < 0.2 && Math.Abs(b.Number("time-pos") - 12) < 0.2, "Shared backward skip failed.");
        form.Master.SeekReaction(5);
        await Until(() => Math.Abs(a.Number("time-pos") - 5) < 0.2 && Math.Abs(b.Number("time-pos") - 9) < 0.2, "Shared timeline lost manual alignment.");
        Assert(a.Get("pause") == "yes" && b.Get("pause") == "yes", "Shared seek changed paused state.");
        form.Master.Jump(-100);
        await Until(() => a.Number("time-pos") < 0.2 && Math.Abs(b.Number("time-pos") - 4) < 0.2, "Start boundary must clamp both equally.");
        form.Master.Jump(100);
        await Until(() => Math.Abs(a.Number("time-pos") - 36) < 0.3 && b.Number("time-pos") > 39.7, "End boundary must clamp both equally.");
        form.Master.SeekReaction(8);
        await Until(() => Math.Abs(a.Number("time-pos") - 8) < 0.3 && Math.Abs(b.Number("time-pos") - 12) < 0.3, "Seek away from end failed.");
        a.Set("pause", "yes"); b.Set("pause", "yes");
        form.Master.TogglePause();
        await Until(() => a.Number("time-pos") > 8.5 && b.Number("time-pos") > 12.5, "Master play failed.");
        form.Master.TogglePause();
        await Task.Delay(250);
        var holdA = a.Number("time-pos"); var holdB = b.Number("time-pos");
        await Task.Delay(500);
        Assert(Math.Abs(a.Number("time-pos") - holdA) < 0.15 && Math.Abs(b.Number("time-pos") - holdB) < 0.15, "Master pause failed.");
        Assert(a.Number("volume") == 35 && b.Number("volume") == 70, "Shared transport changed volume choices.");
        // Fixed offset must survive injected drift and repeated master seeks.
        a.Command("seek", "8", "absolute+exact"); b.Command("seek", "12", "absolute+exact");
        await Until(() => Math.Abs(a.Number("time-pos") - 8) < 0.1 && Math.Abs(b.Number("time-pos") - 12) < 0.1 && a.Get("seeking") == "no" && b.Get("seeking") == "no", "Cannot prepare sync lock.");
        form.Master.CaptureAlignment();
        var fixedOffset = form.Master.Offset;
        Assert(form.Master.Locked && Math.Abs(fixedOffset - 4) < 0.1, "Capture did not store B−A.");
        b.Command("seek", "13", "absolute+exact"); // simulate decoder/seek drift, not a manual UI edit
        await Until(() => Math.Abs(b.Number("time-pos") - 13) < 0.1, "Cannot inject drift.");
        await Until(() => Math.Abs(b.Number("time-pos") - a.Number("time-pos") - fixedOffset) < 0.08 && form.Master.CorrectionCount > 0, "Periodic correction failed while paused.");
        var corrections = form.Master.CorrectionCount;
        await Task.Delay(2400);
        Assert(form.Master.CorrectionCount == corrections, "Correction should ignore an aligned pair.");
        foreach (var target in new[] { 20.0, 5.0, 24.0, 9.0 })
        {
            form.Master.SeekReaction(target);
            await Until(() => Math.Abs(a.Number("time-pos") - target) < 0.1 && Math.Abs(b.Number("time-pos") - target - fixedOffset) < 0.1, "Locked seeking accumulated drift.");
            Assert(form.Master.Offset == fixedOffset, "Master seek changed the stored offset.");
        }
        form.Master.Nudge(0.05);
        await Until(() => Math.Abs(b.Number("time-pos") - a.Number("time-pos") - form.Master.Offset) < 0.08, "Positive nudge failed.");
        Assert(Math.Abs(form.Master.Offset - fixedOffset - 0.05) < 0.001, "Nudge value wrong.");
        form.Master.Nudge(-0.05);
        await Until(() => Math.Abs(b.Number("time-pos") - a.Number("time-pos") - fixedOffset) < 0.08, "Negative nudge failed.");
        form.Master.SetOffset(-3);
        form.Master.SeekReaction(0);
        await Until(() => a.Number("time-pos") < 0.1 && b.Number("time-pos") < 0.1, "Full reaction start must remain accessible.");
        form.Master.SeekReaction(100);
        await Until(() => a.Number("time-pos") > 39.7 && Math.Abs(b.Number("time-pos") - 37) < 0.1, "Negative-offset end boundary failed.");
        form.Master.SeekReaction(10);
        await Until(() => Math.Abs(a.Number("time-pos") - 10) < 0.1 && Math.Abs(b.Number("time-pos") - 7) < 0.1, "Negative-offset seek back failed.");
        bool invalidRejected = false;
        try { form.Master.SetOffset(1000); } catch (InvalidOperationException) { invalidRejected = true; }
        Assert(invalidRejected && form.Master.Offset == -3, "Invalid offset corrupted alignment.");
        // A real independently controlled seek must release the lock, not snap back.
        form.Source.Seek(5);
        Assert(!form.Master.Locked, "Independent seeking must unlock sync.");
        await Until(() => Math.Abs(b.Number("time-pos") - 12) < 0.1 && b.Get("seeking") == "no", "Independent seek failed.");
        await Task.Delay(2400);
        Assert(Math.Abs(b.Number("time-pos") - 12) < 0.1, "Unlocked correction fought manual adjustment.");
        form.Master.SetOffset(4);
        await Until(() => Math.Abs(b.Number("time-pos") - 14) < 0.1 && b.Get("seeking") == "no", "Relock failed.");
        a.Set("pause", "yes"); b.Set("pause", "yes");
        form.Master.TogglePause();
        await Until(() => a.Number("time-pos") > 10.5 && b.Number("time-pos") > 14.5, "Locked playback failed.");
        b.Command("seek", "1", "relative+exact");
        corrections = form.Master.CorrectionCount;
        await Until(() => form.Master.CorrectionCount > corrections && Math.Abs(b.Number("time-pos") - a.Number("time-pos") - 4) < 0.1, "Periodic correction failed during playback.");
        Assert(form.Master.Offset == 4 && a.Number("volume") == 35 && b.Number("volume") == 70, "Correction changed offset or volume.");
        form.Master.TogglePause();
        a.Command("seek", "38", "absolute+exact"); b.Command("seek", "39", "absolute+exact");
        await Until(() => Math.Abs(a.Number("time-pos") - 38) < 0.1 && b.Number("time-pos") > 39.7 && a.Get("pause") == "yes" && b.Get("pause") == "yes", "Source boundary must not pull reaction backward.");
        form.Reaction.LoadVideo(media);
        Assert(!form.Master.Locked, "Replacing media must invalidate the lock.");
        // Milestone 5: exercise the real keyboard dispatcher and native speed properties.
        await Until(() => a.Number("time-pos") > 0.3 && a.Get("seeking") == "no", "Reload not ready for speed test.");
        a.Set("pause", "yes"); b.Set("pause", "yes");
        a.Command("seek", "8", "absolute+exact"); b.Command("seek", "12", "absolute+exact");
        await Until(() => Math.Abs(a.Number("time-pos") - 8) < 0.1 && Math.Abs(b.Number("time-pos") - 12) < 0.1 && a.Get("seeking") == "no" && b.Get("seeking") == "no", "Speed test alignment failed.");
        form.Master.CaptureAlignment();
        var pointerA = form.Reaction.PointToScreen(new Point(20, 20));
        var pointerB = form.Source.PointToScreen(new Point(20, 20));
        void Key(Keys key) => Assert(form.HandleShortcut(key, pointerA), "Unrecognized shortcut " + key);
        async Task Speed(double expected)
        {
            await Until(() => Math.Abs(a.Number("speed") - expected) < 0.001 && Math.Abs(b.Number("speed") - expected) < 0.001, "Shared speed incorrect.");
            Assert(form.Master.Locked && Math.Abs(form.Master.Offset - 4) < 0.001, "Speed change broke stored sync.");
        }
        Key(Keys.D); await Speed(1.25);
        Key(Keys.D); await Speed(1.5);
        Key(Keys.A); await Speed(1);
        Key(Keys.A); await Speed(1.5);
        Key(Keys.G); await Speed(2);
        Key(Keys.G); await Speed(1.5);
        Key(Keys.A); await Speed(1);
        Key(Keys.G); await Speed(2); // entering a different toggle remembers current 1x
        Key(Keys.G); await Speed(1);
        Key(Keys.D); await Speed(1.25); // explicit adjustment ends the previous toggle
        Key(Keys.A); await Speed(1);
        Key(Keys.A); await Speed(1.25);
        Key(Keys.S); await Speed(1);
        Key(Keys.S); await Speed(0.75);
        form.SharedSpeed.Set(0); await Speed(0.25);
        form.SharedSpeed.Set(10); await Speed(4);
        form.SharedSpeed.Set(1.5); await Speed(1.5);
        Assert(a.Get("audio-pitch-correction") == "yes" && b.Get("audio-pitch-correction") == "yes", "Pitch correction unavailable.");
        Key(Keys.L);
        await Until(() => Math.Abs(a.Number("time-pos") - 13) < 0.1 && Math.Abs(b.Number("time-pos") - 17) < 0.1, "L shared seek failed.");
        Key(Keys.J);
        await Until(() => Math.Abs(a.Number("time-pos") - 8) < 0.1 && Math.Abs(b.Number("time-pos") - 12) < 0.1, "J shared seek failed.");
        // Rapid queued jumps use requested positions, not stale decoder positions.
        form.Master.Jump(5); form.Master.Jump(5); form.Master.Jump(-5);
        await Until(() => Math.Abs(a.Number("time-pos") - 13) < 0.1 && Math.Abs(b.Number("time-pos") - 17) < 0.1, "Rapid skips lost their intended target.");
        Key(Keys.K);
        await Until(() => a.Number("time-pos") > 13.5 && b.Number("time-pos") > 17.5, "K shared play failed.");
        form.Master.SeekReaction(5);
        Assert(form.Master.SeekingTogether && a.Get("pause") == "yes" && b.Get("pause") == "yes", "Shared seek must hold both decoders.");
        await Until(() => !form.Master.SeekingTogether && a.Get("pause") == "no" && b.Get("pause") == "no", "Both videos did not resume after seeking.");
        Assert(Math.Abs(b.Number("time-pos") - a.Number("time-pos") - 4) < 0.12, "Resume did not preserve sync.");
        form.Master.Jump(5);
        Key(Keys.K); // pause requested while seeking must cancel automatic resume
        await Until(() => a.Get("pause") == "yes" && b.Get("pause") == "yes", "Pause during seek was ignored.");
        // A decoder pause mismatch while locked must not suspend correction forever.
        b.Set("pause", "no");
        await Until(() => a.Get("pause") == "yes" && b.Get("pause") == "yes" && Math.Abs(b.Number("time-pos") - a.Number("time-pos") - 4) < 0.12, "Native pause mismatch did not recover.");
        // Hover wins for modified shortcuts; unmodified actions remain shared.
        form.HandleShortcut(Keys.Shift | Keys.D, pointerB);
        await Speed(1.75); // Shift must never make speed independent
        form.HandleShortcut(Keys.F1, pointerB);
        Assert(form.HoverTarget(pointerB) == form.Source && form.HoverTarget(new Point(-10000, -10000)) == form.Reaction, "Hover/fallback targeting failed.");
        form.SharedSpeed.Set(1.5);
        await Until(() => a.Number("speed") == 1.5 && b.Number("speed") == 1.5, "Shared speed did not restore equal rates.");
        // Composition exercises real native surfaces and full-window transitions.
        var ah = form.Reaction.Surface.Handle; var bh = form.Source.Surface.Handle;
        var composition = form.Composition;
        form.WindowState = FormWindowState.Normal;
        form.Bounds = Rectangle.Inflate(Screen.FromControl(form).WorkingArea, -20, -20);
        foreach (double aspect in new[] { 16.0 / 9, 4.0 / 3, 16.0 / 10 })
        foreach (bool topAnchor in new[] { false, true })
        foreach (double fraction in new[] { 0.25, 0.7, 1.0 })
        {
            composition.CanvasAspect = aspect; composition.TopAnchor = topAnchor; composition.SourceFraction = fraction;
            composition.CropTop = 0.25; composition.CropBottom = 0.1;
            composition.Zoom = 1.2; composition.PanX = 0.05; composition.PanY = -0.05;
            composition.Arrange();
            var c = composition.CanvasBounds; var r = composition.SourceBounds;
            Assert(Math.Abs(c.Width / (double)c.Height - aspect) < 0.02, "Canvas aspect incorrect.");
            Assert(Math.Abs(r.Left * 2 + r.Width - c.Width) <= 1, "Source must stay centered.");
            Assert(topAnchor ? r.Top == 0 : r.Bottom == c.Height, "Source lost its anchor.");
            Assert(r.Width <= c.Width && r.Height <= c.Height, "Source escaped canvas.");
            Assert(composition.ReactionBounds.Y < 0 && composition.MaskBounds.Height < composition.ReactionBounds.Height, "Reaction crop/pan not applied.");
            Assert(form.HoverTarget(form.Source.Surface.PointToScreen(new Point(r.Width / 2, r.Height / 2))) == form.Source, "Foreground hover must win.");
        }
        composition.CanvasAspect = 16.0 / 9; composition.TopAnchor = false;
        composition.SourceFraction = 0.7; composition.Zoom = 1; composition.PanX = composition.PanY = 0;
        composition.Arrange();
        var bounds = form.Bounds;
        Key(Keys.F);
        Assert(form.Fullscreen && form.FormBorderStyle == FormBorderStyle.None, "F fullscreen failed.");
        Assert(composition.Width == form.ClientSize.Width && composition.Height == form.ClientSize.Height, "Fullscreen did not hide controls.");
        Key(Keys.Escape);
        Assert(!form.Fullscreen && form.Bounds == bounds, "Fullscreen did not restore window bounds.");
        Key(Keys.F11); Key(Keys.F11);
        Assert(!form.Fullscreen && form.Reaction.Surface.Handle == ah && form.Source.Surface.Handle == bh, "Layout recreated native surfaces.");
        Assert(form.Master.Locked && Math.Abs(form.Master.Offset - 4) < 0.001, "Layout changed sync lock.");
        // Capture the actual composed desktop, in addition to decoded-frame evidence.
        await Task.Delay(1000);
        a.Command("screenshot-to-file", report + ".final-a.png", "subtitles");
        b.Command("screenshot-to-file", report + ".final-b.png", "subtitles");
        using (var screen = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
        {
            using var graphics = Graphics.FromImage(screen);
            graphics.CopyFromScreen(form.PointToScreen(Point.Empty), Point.Empty, screen.Size);
            screen.Save(report + ".composition.png");
        }
        form.HandleShortcut(Keys.Shift | Keys.J, pointerB);
        Assert(!form.Master.Locked, "Independent seek must still unlock sync.");
        var preferencesFile = report + ".preferences.tmp";
        var preferences = new SpeedPreferences(preferencesFile);
        Assert(preferences.Favorite == 2, "Default favorite incorrect.");
        preferences.Save(2.25);
        Assert(new SpeedPreferences(preferencesFile).Favorite == 2.25, "Favorite preference was not persisted.");
        File.WriteAllText(preferencesFile, "invalid json");
        Assert(new SpeedPreferences(preferencesFile).Favorite == 2, "Malformed preferences should fall back safely.");
        File.Delete(preferencesFile);

        Assert(a.Number("volume") == 35 && b.Number("volume") == 70, "Speed shortcuts changed volumes.");
        await NetworkVerification.Run(form, media);
        await YouTubeVerification.Run(form, media);
        await BoundaryVerification.Run(form);
        await AudioAlignmentVerification.Run();
        await ConvenienceVerification.Run(form, media);
        File.WriteAllText(report, JsonSerializer.Serialize(new { passed = true, milestone = 10, commentaryReplay = true, hoverVolumeWheel = true, oppositeReactionAnchor = true, audioAlignment = true, reactionPreambleAndDiscussion = true, youtubeDiagnostics = true, youtubeResolver = true, separateYouTubeAudio = true, offsetGrid005 = true, hlsPlayback = true, httpHeaderIsolation = true, hlsSharedSeek = true, httpFailureRecovery = true, canvas16x10 = true, composition = true, fullscreen = true, sharedShiftSpeed = true, nativeSurfaceRetention = true, sharedSpeed = true, speedToggles = true, hoverTargeting = true, coordinatedSeekResume = true, rapidSkips = true, favoritePreferences = true, fixedOffset = true, driftCorrection = true, offsetNudges = true, negativeOffset = true, manualUnlock = true, sharedPlayPause = true, sharedSeek = true, boundaryClamping = true, mpv = a.Get("mpv-version"), simultaneousVideo = true, simultaneousAudioDecode = true, audioOutput = "null (CI only)", independentPause = true, independentSeek = true, independentVolume = true, namedTrackMenus = true, trackIsolation = true, replacementBothPlayers = true }));
        form.Close();
    }
}
