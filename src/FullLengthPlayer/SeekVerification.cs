namespace FullLengthPlayer;

internal static class SeekVerification
{
    // Real HTTP media/native players, with a virtual deadline clock so timeout
    // branches can be exercised without spending two minutes in each test.
    internal static async Task Run(MainForm form)
    {
        var a = form.Reaction.Player!; var b = form.Source.Player!;
        void Check(bool ok, string message) { if (!ok) throw new Exception("Network seek: " + message); }
        async Task Ready(double at, double bt)
        {
            long end = Environment.TickCount64 + 25000;
            while (a.Get("seeking") != "no" || b.Get("seeking") != "no"
                || a.Get("paused-for-cache") == "yes" || b.Get("paused-for-cache") == "yes"
                || Math.Abs(a.Number("time-pos") - at) > .12 || Math.Abs(b.Number("time-pos") - bt) > .12)
            {
                if (Environment.TickCount64 > end) throw new Exception("Network seek fixture did not settle.");
                await Task.Delay(50);
            }
        }
        form.Master.Unlock();
        Check(a.IsNetworkMedia, "HTTP source not identified");
        long now = 0;
        var seeks = new SeekCoordinator(() => now);
        a.Set("pause", "no"); b.Set("pause", "no");
        seeks.Begin(form.Master.Snapshot()!, 8, 11);
        Check(seeks.TimeoutMilliseconds == 120000 && seeks.WillResume, "Network deadline/resume intent incorrect");
        now = 20000;
        seeks.Tick();
        Check(seeks.Waiting && !seeks.Suspended, "Network seek expired at the old 15-second deadline");
        await Ready(8, 11);
        seeks.Tick(); seeks.Tick();
        Check(!seeks.Waiting && seeks.State == "Completed" && a.Get("pause") == "no" && b.Get("pause") == "no", "Delayed seek failed to resume both");

        seeks.Begin(form.Master.Snapshot()!, 6, 9);
        seeks.TogglePause(); // User cancels resume, not the seek itself.
        now += 20000;
        await Ready(6, 9); seeks.Tick(); seeks.Tick();
        Check(a.Get("pause") == "yes" && b.Get("pause") == "yes", "Pause during wait was ignored");

        a.Set("pause", "no"); b.Set("pause", "no");
        seeks.Begin(form.Master.Snapshot()!, 9, 12);
        now += 120001; seeks.Tick();
        Check(seeks.State == "Timed out" && seeks.Suspended && !seeks.Waiting, "Timeout not recorded");
        await Ready(9, 12); seeks.Tick();
        Check(a.Get("pause") == "yes" && b.Get("pause") == "yes", "Timed-out seek resumed later unexpectedly");

        seeks.Begin(form.Master.Snapshot()!, 10, 13);
        seeks.TogglePause(); seeks.StopWaiting();
        await Ready(10, 13); seeks.Tick();
        Check(seeks.State == "Cancelled" && !seeks.WillResume && a.Get("pause") == "yes" && b.Get("pause") == "yes", "Cancel wait resumed unexpectedly");

        bool networkA = a.IsNetworkMedia, networkB = b.IsNetworkMedia;
        try
        {
            a.IsNetworkMedia = b.IsNetworkMedia = false;
            seeks.Begin(form.Master.Snapshot()!, 10, 13);
            Check(seeks.TimeoutMilliseconds == 15000, "Local deadline changed");
            seeks.Cancel();
        }
        finally { a.IsNetworkMedia = networkA; b.IsNetworkMedia = networkB; }
        Console.WriteLine("PASS: network seek deadline, delayed resume, pause intent, timeout and cancellation.");
    }
}
