using System.Buffers.Binary;
using System.Numerics;

namespace FullLengthPlayer;

// Sources live only in memory. Never include signed URLs or headers in error text.
internal sealed record AudioInput(string Path, string Referer, string[] Headers, string? ExternalAudio, string Track);
internal sealed record AudioMatch(double Offset, double Score, double Separation, bool Reliable);

internal static class AudioAlignment
{
    internal const int Rate = 8000, Hop = 400, Window = 800;
    private const int Bands = 12;
    private static readonly int[] BandEdges = { 140, 210, 300, 410, 560, 740, 960, 1230, 1560, 1950, 2380, 2850, 3500 };

    internal static async Task<short[]> Decode(AudioInput input, double start, double seconds, CancellationToken cancellation)
    {
        string file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "flp-audio-" + Guid.NewGuid().ToString("N") + ".pcm");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        try
        {
            return await Task.Run(async () =>
            {
                // This extra MPV instance owns its worker calls and writes audio, never speakers/video.
                using (var decoder = new MpvPlayer(IntPtr.Zero, pcmFile: file, start: start, length: seconds))
                {
                    timeout.Token.ThrowIfCancellationRequested();
                    decoder.Set("referrer", input.Referer);
                    decoder.SetStringList("http-header-fields", input.Headers);
                    if (input.ExternalAudio != null) decoder.SetStringList("audio-files", new[] { input.ExternalAudio });
                    decoder.Set("aid", input.Track);
                    decoder.Command("loadfile", input.Path, "replace");
                    while (!decoder.Ended)
                    {
                        timeout.Token.ThrowIfCancellationRequested();
                        if (decoder.PollError() != null) throw new InvalidOperationException("Audio sample could not be decoded. For a stream, reopen its original URL and retry.");
                        await Task.Delay(25, timeout.Token).ConfigureAwait(false);
                    }
                } // Close PCM output before reading it.
                byte[] bytes = await File.ReadAllBytesAsync(file, timeout.Token).ConfigureAwait(false);
                if (bytes.Length < Rate * 2 * 10 || bytes.Length > Rate * 2 * (seconds + 3))
                    throw new InvalidOperationException("Not enough usable audio. Choose a scene with at least 20 seconds of shared sound remaining.");
                var samples = new short[bytes.Length / 2];
                for (int i = 0; i < samples.Length; i++) samples[i] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(i * 2, 2));
                return samples;
            }, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { throw new InvalidOperationException("Audio analysis timed out. Try a buffered section or local files."); }
        finally { try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    // Log-energy changes in independent frequency bands tolerate volume/EQ differences
    // better than raw waveform correlation. No claim of a calibrated probability.
    internal static double[][] Features(short[] samples, CancellationToken token)
    {
        int count = Math.Max(0, (samples.Length - Window) / Hop + 1);
        var energy = new double[count][];
        var hann = Enumerable.Range(0, Window).Select(i => 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (Window - 1))).ToArray();
        var peak = new double[Bands];
        for (int frame = 0; frame < count; frame++)
        {
            token.ThrowIfCancellationRequested();
            var spectrum = new Complex[1024];
            for (int i = 0; i < Window; i++) spectrum[i] = samples[frame * Hop + i] / 32768.0 * hann[i];
            Fft(spectrum);
            energy[frame] = new double[Bands];
            for (int band = 0; band < Bands; band++)
            {
                double power = 0;
                for (int bin = BandEdges[band] * 1024 / Rate; bin < BandEdges[band + 1] * 1024 / Rate; bin++)
                    power += spectrum[bin].Real * spectrum[bin].Real + spectrum[bin].Imaginary * spectrum[bin].Imaginary;
                energy[frame][band] = power;
                peak[band] = Math.Max(peak[band], power);
            }
        }
        // Relative floor keeps quiet/noisy bands from dominating the derivative.
        for (int f = 0; f < count; f++)
            for (int band = 0; band < Bands; band++)
                energy[f][band] = Math.Log(energy[f][band] + Math.Max(1e-5, peak[band] * 0.01));
        var result = new double[Math.Max(0, count - 1)][];
        for (int f = 0; f < result.Length; f++)
            result[f] = Enumerable.Range(0, Bands).Select(b => Math.Clamp(energy[f + 1][b] - energy[f][b], -3, 3)).ToArray();
        return result;
    }

    private static void Fft(Complex[] data)
    {
        for (int i = 1, j = 0; i < data.Length; i++)
        {
            int bit = data.Length >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (data[i], data[j]) = (data[j], data[i]);
        }
        for (int size = 2; size <= data.Length; size <<= 1)
        {
            var step = Complex.FromPolarCoordinates(1, -2 * Math.PI / size);
            for (int start = 0; start < data.Length; start += size)
            {
                Complex phase = Complex.One;
                for (int j = 0; j < size / 2; j++)
                {
                    var even = data[start + j]; var odd = data[start + j + size / 2] * phase;
                    data[start + j] = even + odd; data[start + j + size / 2] = even - odd;
                    phase *= step;
                }
            }
        }
    }

    internal static AudioMatch Match(short[] reaction, short[] source, double aStart, double bStart, CancellationToken token)
    {
        var a = Features(reaction, token); var b = Features(source, token);
        if (a.Length < 190 || b.Length < a.Length) throw new InvalidOperationException("Audio samples are too short to compare.");
        double aPower = a.Sum(row => row.Sum(v => v * v));
        var scores = new double[b.Length - a.Length + 1];
        for (int lag = 0; lag < scores.Length; lag++)
        {
            token.ThrowIfCancellationRequested();
            double dot = 0, bPower = 0;
            for (int f = 0; f < a.Length; f++)
            for (int band = 0; band < Bands; band++)
            {
                double x = a[f][band], y = b[f + lag][band];
                dot += x * y; bPower += y * y;
            }
            scores[lag] = aPower > 1 && bPower > 1 ? dot / Math.Sqrt(aPower * bPower) : 0;
        }
        int best = Array.IndexOf(scores, scores.Max());
        double other = scores.Where((_, i) => Math.Abs(i - best) > 10).DefaultIfEmpty(0).Max();
        double score = scores[best], separation = score - other;
        // A peak at a truncated search edge requires repositioning, not guessing beyond it.
        bool edge = best == 0 || best == scores.Length - 1;
        return new(MasterTransport.RoundOffset(bStart - aStart + best * Hop / (double)Rate), score, separation,
            !edge && score >= 0.35 && separation >= 0.08);
    }
}
