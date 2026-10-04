namespace FullLengthPlayer;

internal static class BufferHoldVerification
{
    // Real decoded HTTP/local media with controlled cache-state signals. This
    // exercises lock behavior reproducibly without depending on YouTube stalls.
    internal static async Task Run(MainForm form)
    {
        var a = form.Reaction.Player!; var b = form.Source.Player!;
        form.Master.Unlock();
        bool bufferA = false, bufferB = false;
        var master = new MasterTransport(() => a, () => b,
            p => (p == a ? bufferA : bufferB) || p.Get("paused-for-cache") == "yes");
        void Check(bool ok, string message) { if (!ok) throw new Exception("Buffer lock: " + message); }
        async Task Until(Func<bool> ready)
        {
            long deadline = Environment.TickCount64 + 25000;
            while (true)
            {
                master.Tick();
                if (ready()) return;
                if (Environment.TickCount64 > deadline) throw new Exception("Buffer lock fixture timed out: " + master.SyncStatus);
                await Task.Delay(50);
            }
        }
        try
        {
            a.Set("pause", "yes"); b.Set("pause", "yes");
            master.SetOffset(3, 8);
            await Until(() => !master.SeekingTogether);
            master.TogglePause();
            await Until(() => a.Number("time-pos") > 8.2);
            bufferA = true; master.Tick();
            Check(master.SeekingTogether && master.ResumeAfterSeek && a.Get("pause") == "yes" && b.Get("pause") == "yes", "Reaction stall did not hold both");
            double at = a.Number("time-pos"), bt = b.Number("time-pos");
            await Task.Delay(300); master.Tick();
            Check(Math.Abs(a.Number("time-pos") - at) < .12 && Math.Abs(b.Number("time-pos") - bt) < .12, "Player ran ahead during hold");
            // Inject accumulated drift while held to exercise refill -> realign.
            b.Command("seek", (at + 3.5).ToString(System.Globalization.CultureInfo.InvariantCulture), "absolute+exact");
            await Until(() => b.Get("seeking") == "no" && Math.Abs(b.Number("time-pos") - at - 3.5) < .12);
            bufferA = false;
            await Until(() => !master.SeekingTogether && a.Get("pause") == "no" && b.Get("pause") == "no");
            Check(Math.Abs(b.Number("time-pos") - a.Number("time-pos") - 3) < .15, "Refill did not restore offset");

            bufferB = true; master.Tick();
            Check(master.SeekingTogether && a.Get("pause") == "yes" && b.Get("pause") == "yes", "Source stall did not hold reaction");
            master.TogglePause();
            bufferB = false;
            await Until(() => !master.SeekingTogether);
            Check(a.Get("pause") == "yes" && b.Get("pause") == "yes", "Manual pause lost during refill");

            master.TogglePause(); bufferA = bufferB = true; master.Tick();
            master.CancelSeek(); bufferA = bufferB = false;
            await Task.Delay(200); master.Tick();
            Check(master.SeekSuspended && a.Get("pause") == "yes" && b.Get("pause") == "yes", "Cancelled buffer wait resumed");

            master.SeekReaction(10);
            await Until(() => !master.SeekingTogether);
            master.TogglePause(); bufferA = true; master.Tick();
            master.SeekReaction(6); // A new explicit seek replaces the held target.
            bufferA = false;
            await Until(() => !master.SeekingTogether && a.Get("pause") == "no");
            Check(a.Number("time-pos") >= 5.9 && a.Number("time-pos") < 7 && Math.Abs(b.Number("time-pos") - a.Number("time-pos") - 3) < .15, "Seek during hold lost target or resume intent");

            master.Unlock(); bufferA = true; master.Tick();
            Check(!master.SeekingTogether && a.Get("pause") == "no" && b.Get("pause") == "no", "Unlocked playback was held");
            Check(master.BufferHoldCount >= 4, "Buffer holds not recorded");
        }
        finally { bufferA = bufferB = false; master.Unlock(); a.Set("pause", "yes"); b.Set("pause", "yes"); }
    }
}
