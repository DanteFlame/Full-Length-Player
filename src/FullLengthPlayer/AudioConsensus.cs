namespace FullLengthPlayer;

internal sealed record ConsensusResult(AudioMatch? Match, int Checked, int Agreed, bool Conflict);
internal static class AudioConsensus
{
    internal static ConsensusResult Decide(IReadOnlyList<AudioMatch> matches, int count)
    {
        var good = matches.Where(x => x.Reliable).OrderBy(x => x.Offset).ToArray();
        bool conflict = good.Length > 1 && good[^1].Offset - good[0].Offset > 0.100001;
        if (good.Length < 3 || conflict) return new(null, count, good.Length, conflict);
        double median = good[good.Length / 2].Offset;
        return new(new(MasterTransport.RoundOffset(median), good.Average(x => x.Score), good.Min(x => x.Separation), true), count, good.Length, false);
    }
    internal static async Task<ConsensusResult> Run(AudioInput a, AudioInput b, double aTime, double bTime,
        double aDuration, double bDuration, IProgress<string> progress, CancellationToken token)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
        budget.CancelAfter(TimeSpan.FromSeconds(10));
        var matches = new List<AudioMatch>(); int count = 0;
        foreach (double advance in new double[] { 0, 20, 60, 120, 180, 240, 300, 420, 480, 580 })
        {
            double aStart = aTime + advance, expected = bTime + advance;
            if (aStart + 20 > aDuration) break;
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
                if (result.Match != null || result.Conflict) return result;
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { break; }
        }
        token.ThrowIfCancellationRequested();
        return Decide(matches, count);
    }
}
