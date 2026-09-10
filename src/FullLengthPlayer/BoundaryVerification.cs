namespace FullLengthPlayer;
internal static class BoundaryVerification
{
    internal static async Task Run(MainForm form)
    {
        var a = form.Reaction.Player!; var b = form.Source.Player!;
        void Assert(bool value, string message) { if (!value) throw new Exception(message); }
        async Task Until(Func<bool> condition, string message)
        {
            var deadline = DateTime.UtcNow.AddSeconds(12);
            while (!condition() || form.Master.SeekingTogether) { if (DateTime.UtcNow > deadline) throw new Exception(message); await Task.Delay(50); }
        }
        form.Master.Unlock(); a.Set("pause", "yes"); b.Set("pause", "yes");
        form.SharedSpeed.Set(2);
        form.Master.SetOffset(-3); form.Master.SeekReaction(0);
        await Until(() => a.Number("time-pos") < 0.1 && b.Number("time-pos") < 0.1, "Preamble seek failed.");
        form.Master.TogglePause();
        await Until(() => a.Number("time-pos") > 1 && a.Number("time-pos") < 3, "Reaction did not play preamble.");
        Assert(b.Get("pause") == "yes" && b.Number("time-pos") < 0.1, "Source must wait at zero.");
        await Until(() => a.Number("time-pos") > 4 && b.Number("time-pos") > 0.5 && b.Get("pause") == "no", "Source did not join automatically.");
        Assert(Math.Abs(b.Number("time-pos") - a.Number("time-pos") + 3) < 0.15 && form.Master.Offset == -3, "Rejoining lost offset.");
        form.Master.SeekReaction(0.5); form.Master.TogglePause();
        await Until(() => a.Get("pause") == "yes" && b.Get("pause") == "yes", "Pause during preamble seek ignored.");
        form.SharedSpeed.Set(1.5);
        await Until(() => a.Get("pause") == "yes" && b.Get("pause") == "yes", "Speed edit resumed paused preamble.");
        form.Master.SetOffset(3); form.Master.SeekReaction(36);
        await Until(() => Math.Abs(a.Number("time-pos") - 36) < 0.1, "Discussion setup failed.");
        form.Master.TogglePause();
        await Until(() => a.Number("time-pos") > 38 && b.Number("time-pos") > 39.7, "Reaction failed to play after source end.");
        Assert(a.Get("pause") == "no" && b.Get("pause") == "yes", "Source EOF paused reaction.");
        form.Master.SeekReaction(38);
        await Until(() => a.Get("pause") == "no" && b.Get("pause") == "yes", "Discussion seek did not preserve play intent.");
        form.Master.SeekReaction(10);
        await Until(() => b.Get("pause") == "no" && Math.Abs(b.Number("time-pos") - a.Number("time-pos") - 3) < 0.15, "Seeking back did not restore source playback.");
        form.Master.TogglePause();
        form.Master.SeekReaction(39);
        await Until(() => a.Get("pause") == "yes" && b.Get("pause") == "yes" && Math.Abs(a.Number("time-pos") - 39) < 0.1, "Paused discussion seek failed.");
        string redacted = ResolverDiagnostics.Redact("ERROR: BaW_jenozKc https://example.org/stream?secret=abc\nCookie: secret-value\nAuthorization: Bearer secret-value", "https://www.youtube.com/watch?v=BaW_jenozKc");
        Assert(!redacted.Contains("secret") && !redacted.Contains("BaW_jenozKc") && !redacted.Contains("https://"), "Diagnostic redaction failed.");
    }
}
