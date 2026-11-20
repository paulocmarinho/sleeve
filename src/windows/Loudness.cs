using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace Sleeve;

/// <summary>
/// Mede o volume de uma faixa sem ReplayGain, do jeito da norma EBU R128 (a mesma do ReplayGain 2):
/// filtro de ponderação K, blocos de 400 ms e as duas comportas (-70 LUFS e -10 LU).
/// Devolve o ganho no padrão ReplayGain (alvo -18 LUFS). Cada arquivo é medido uma vez;
/// o resultado fica em %LocalAppData%\Sleeve\volume.json.
/// O Windows decodifica MP3, AAC/M4A, FLAC e WAV; OGG e Opus ficam sem medida.
/// </summary>
public static class Loudness
{
    private static string CacheFile => Path.Combine(LibraryService.DataDir, "volume.json");
    private static readonly SemaphoreSlim OneAtATime = new(1, 1);
    private static Dictionary<string, double?>? _cache;
    private static readonly object Gate = new();

    public static async Task<double?> GainAsync(string path)
    {
        var info = new FileInfo(path);
        var key = $"{path.ToLowerInvariant()}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
        lock (Gate)
        {
            _cache ??= Load();
            if (_cache.TryGetValue(key, out var known)) return known;
        }

        await OneAtATime.WaitAsync();
        try
        {
            lock (Gate) if (_cache!.TryGetValue(key, out var known)) return known;
            var gain = await Task.Run(() => Measure(path));
            lock (Gate)
            {
                _cache![key] = gain;
                Save();
            }
            return gain;
        }
        finally
        {
            OneAtATime.Release();
        }
    }

    private static double? Measure(string path)
    {
        try
        {
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal; // não atrapalha o resto do PC
            using var reader = new MediaFoundationReader(path);
            var samples = reader.ToSampleProvider();
            int channels = samples.WaveFormat.Channels, rate = samples.WaveFormat.SampleRate;
            if (channels < 1 || rate < 8000) return null;

            var k = new KFilter[channels];
            for (var c = 0; c < channels; c++) k[c] = new KFilter(rate);

            // Soma dos quadrados em pedaços de 100 ms; um bloco de 400 ms = 4 pedaços seguidos
            var step = rate / 10;
            var chunks = new List<double>();
            double acc = 0;
            var inChunk = 0;
            var buffer = new float[step * channels];
            int read;
            while ((read = samples.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (var i = 0; i + channels <= read; i += channels)
                {
                    for (var c = 0; c < channels; c++)
                    {
                        var y = k[c].Process(buffer[i + c]);
                        // Em 5.1: o LFE (canal 4) não conta e os dois traseiros pesam 1,41
                        var weight = channels >= 6 ? (c == 3 ? 0 : c is 4 or 5 ? 1.41 : 1.0) : 1.0;
                        acc += weight * y * y;
                    }
                    if (++inChunk == step)
                    {
                        chunks.Add(acc / step);
                        acc = 0;
                        inChunk = 0;
                    }
                }
            }

            var blocks = new List<double>();
            for (var i = 3; i < chunks.Count; i++) blocks.Add((chunks[i] + chunks[i - 1] + chunks[i - 2] + chunks[i - 3]) / 4);
            if (blocks.Count == 0) return null;

            static double Lufs(double z) => -0.691 + 10 * Math.Log10(z);
            var abs = blocks.FindAll(z => z > 0 && Lufs(z) > -70);
            if (abs.Count == 0) return null;
            var mean = 0.0;
            foreach (var z in abs) mean += z;
            var relGate = Lufs(mean / abs.Count) - 10;
            var gated = abs.FindAll(z => Lufs(z) > relGate);
            mean = 0;
            foreach (var z in gated) mean += z;
            var integrated = Lufs(mean / gated.Count);
            return Math.Round(-18 - integrated, 2);
        }
        catch
        {
            return null; // formato que o Windows não abre, ou arquivo estragado
        }
    }

    /// <summary>Filtro de ponderação K (prateleira de agudos + passa-altas), coeficientes para qualquer taxa.</summary>
    private sealed class KFilter
    {
        private readonly double b0, b1, b2, a1, a2, c0, c1, c2, d1, d2;
        private double x1, x2, y1, y2, u1, u2, v1, v2;

        public KFilter(int rate)
        {
            // Estágio 1: prateleira de agudos (+4 dB)
            double f0 = 1681.974450955533, g = 3.999843853973347, q = 0.7071752369554196;
            var k = Math.Tan(Math.PI * f0 / rate);
            var vh = Math.Pow(10, g / 20);
            var vb = Math.Pow(vh, 0.4996667741545416);
            var a0 = 1 + k / q + k * k;
            b0 = (vh + vb * k / q + k * k) / a0;
            b1 = 2 * (k * k - vh) / a0;
            b2 = (vh - vb * k / q + k * k) / a0;
            a1 = 2 * (k * k - 1) / a0;
            a2 = (1 - k / q + k * k) / a0;
            // Estágio 2: passa-altas
            f0 = 38.13547087602444;
            q = 0.5003270373238773;
            k = Math.Tan(Math.PI * f0 / rate);
            var n = 1 + k / q + k * k;
            c0 = 1; c1 = -2; c2 = 1;
            d1 = 2 * (k * k - 1) / n;
            d2 = (1 - k / q + k * k) / n;
        }

        public double Process(double x)
        {
            var y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x; y2 = y1; y1 = y;
            var z = c0 * y + c1 * u1 + c2 * u2 - d1 * v1 - d2 * v2;
            u2 = u1; u1 = y; v2 = v1; v1 = z;
            return z;
        }
    }

    private static Dictionary<string, double?> Load()
    {
        try
        {
            if (File.Exists(CacheFile))
                return JsonSerializer.Deserialize<Dictionary<string, double?>>(File.ReadAllText(CacheFile)) ?? new();
        }
        catch { /* estragado: mede de novo */ }
        return new();
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(LibraryService.DataDir);
            File.WriteAllText(CacheFile, JsonSerializer.Serialize(_cache));
        }
        catch { /* sem cache, mede de novo depois */ }
    }
}
