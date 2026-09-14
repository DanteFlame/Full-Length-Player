namespace FullLengthPlayer;

internal sealed record ConsensusResult(AudioMatch? Match, int Checked, int Agreed, bool Conflict);
internal static class AudioConsensus
{
    internal static ConsensusResult Decide(IReadOnlyList<AudioMatch> matches, int count)
    {
        var good = matches.Where(x => x.Reliable).OrderBy(x => x.Offset).ToArray();
        bool conflict = good.Length > 1 && good[^1].Offset - good[0].Offset > 0.100001;
        if (good.Length == 0 || conflict) return new(null, count, good.Length, conflict);
        double median = good[good.Length / 2].Offset;
        return new(new(MasterTransport.RoundOffset(median), good.Average(x => x.Score), good.Min(x => x.Separation), true), count, good.Length, false);
    }
    // Spread the early checks across the available interval instead of spending
    // the budget on adjacent commentary. Do not count overlapping A clips twice.
    internal static double[] SampleAdvances(double remaining)
    {
        double horizon = Math.Floor(Math.Max(0, Math.Min(580, remaining - 20)) * 20) / 20;
        var starts = new List<double>();
        foreach (double fraction in new[] { 0.0, 0.5, 1.0, 0.25, 0.75, 0.125, 0.375, 0.625, 0.875 })
        {
            double start = Math.Round(horizon * fraction / 0.05) * 0.05;
            if (starts.All(previous => Math.Abs(previous - start) >= 20)) starts.Add(start);
        }
        return starts.ToArray();
    }
    internal static async Task<ConsensusResult> Run(AudioInput a, AudioInput b, double aTime, double bTime,
        double aDuration, double bDuration, IProgress<string> progress, CancellationToken token)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
        budget.CancelAfter(TimeSpan.FromSeconds(10));
        var matches = new List<AudioMatch>(); int count = 0;
        if (aDuration < 20 || bDuration < 20) return Decide(matches, count);
        double origin = Math.Max(0, Math.Min(aTime, aDuration - 20));
        foreach (double advance in SampleAdvances(aDuration - origin))
        {
            double aStart = origin + advance, expected = bTime + (aStart - aTime);
            if (aStart + 20 > aDuration) continue;
            double bStart = Math.Max(0, expected - 60), length = Math.Min(bDuration - bStart, expected + 80 - bStart);
            if (length < 20) continue;
            try
            {
                progress.Report($"Checking sample {count + 1} at A {TimeSpan.FromSeconds(aStart):hh\\:mm\\:ss}…");
                var readA = AudioAlignment.Decode(a, aStart, 20, budget.Token);
                var readB = AudioAlignment.Decode(b, bStart, length, budget.Token);
                await Task.WhenAll(readA, readB);
                var match = await Task.Run(() => AudioAlignment.Match(readA.Result, readB.Result, aStart, bStart, budget.Token), budget.Token);
                matches.Add(match); count++;
                var result = Decide(matches, count);
                if (result.Agreed >= 3 && result.Match != null) return result;
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { break; }
            catch (InvalidOperationException) when (count > 0)
            {
                // A later unavailable/short network segment must not erase evidence
                // already collected. Its absence is not agreement or disagreement.
                continue;
            }
        }
        token.ThrowIfCancellationRequested();
        return Decide(matches, count);
    }
}
