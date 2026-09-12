namespace FullLengthPlayer;

internal static class AudioAlignmentVerification
{
    internal static async Task Run()
    {
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("Audio sync: " + message); }
        const int rate = AudioAlignment.Rate;
        var random = new Random(8271);
        // Changing multi-band tones: deterministic musical-like content, not a periodic sine.
        double[,] levels = new double[400, 6];
        for (int i = 0; i < 400; i++) for (int j = 0; j < 6; j++) levels[i, j] = random.NextDouble();
        short[] source = new short[45 * rate];
        double[] frequencies = { 250, 480, 850, 1400, 2150, 2600 };
        for (int i = 0; i < source.Length; i++)
        {
            double t = i / (double)rate, cell = t * 8;
            int n = (int)cell; double blend = cell - n, value = 0;
            for (int j = 0; j < 6; j++) value += (levels[n, j] * (1 - blend) + levels[n + 1, j] * blend) * Math.Sin(2 * Math.PI * frequencies[j] * t);
            source[i] = (short)(value * 3500);
        }
        short[] reaction = new short[24 * rate];
        int delay = (int)(7.35 * rate);
        for (int i = 0; i < reaction.Length; i++)
        {
            // Attenuated shared audio plus room echo and an unrelated voice-like tone/noise.
            double ambient = source[i + delay] * 0.6 + source[Math.Max(0, i + delay - 173)] * 0.15;
            double commentary = 250 * Math.Sin(i * 0.14) + (random.NextDouble() - 0.5) * 350;
            reaction[i] = (short)(ambient + commentary);
        }
        var direct = AudioAlignment.Match(reaction[..(20 * rate)], source, 0, 0, CancellationToken.None);
        Check(direct.Reliable && Math.Abs(direct.Offset - 7.35) < 0.051, "Failed room/noise/volume match.");
        var negative = AudioAlignment.Match(reaction[..(20 * rate)], source, 100, 50, CancellationToken.None);
        Check(negative.Reliable && Math.Abs(negative.Offset + 42.65) < 0.051, "Wrong negative offset sign.");
        Check(!AudioAlignment.Match(new short[20 * rate], new short[40 * rate], 0, 0, CancellationToken.None).Reliable, "Silence must not suggest alignment.");
        var unrelated = new short[40 * rate];
        for (int i = 0; i < unrelated.Length; i++) unrelated[i] = (short)random.Next(-6000, 6000);
        Check(!AudioAlignment.Match(reaction[..(20 * rate)], unrelated, 0, 0, CancellationToken.None).Reliable, "Unrelated audio must be rejected.");
        var repeated = Enumerable.Range(0, 3).SelectMany(_ => reaction[..(12 * rate)]).ToArray();
        Check(!AudioAlignment.Match(reaction[..(10 * rate)], repeated, 0, 0, CancellationToken.None).Reliable, "Repeated matches must be rejected.");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        try { AudioAlignment.Match(reaction, source, 0, 0, canceled.Token); throw new InvalidOperationException("Cancellation ignored."); }
        catch (OperationCanceledException) { }
        string directory = Path.Combine(Path.GetTempPath(), "flp audio test " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            void Wave(string name, short[] samples)
            {
                using var writer = new BinaryWriter(File.Create(Path.Combine(directory, name)));
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
                foreach (short sample in samples) writer.Write(sample);
            }
            Wave("reaction.wav", reaction); Wave("source.wav", source);
            AudioInput Input(string name) => new(Path.Combine(directory, name), "", Array.Empty<string>(), null, "auto");
            var a = await AudioAlignment.Decode(Input("reaction.wav"), 2, 20, CancellationToken.None);
            var b = await AudioAlignment.Decode(Input("source.wav"), 0, 36, CancellationToken.None);
            Check(Math.Abs(a.Length - 20 * rate) < rate / 10, "Decoder sample rate/length incorrect.");
            var decoded = AudioAlignment.Match(a, b, 2, 0, CancellationToken.None);
            Check(decoded.Reliable && Math.Abs(decoded.Offset - 7.35) < 0.051, "Native decoded samples lost timestamp alignment.");
            try { await AudioAlignment.Decode(Input("source.wav"), 0, 20, canceled.Token); throw new InvalidOperationException("Decode cancellation ignored."); }
            catch (OperationCanceledException) { }
        }
        finally { Directory.Delete(directory, true); }
    }
}
