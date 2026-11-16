using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sleeve;

/// <summary>Letra de uma música: sincronizada (formato LRC, com tempos) ou só o texto.</summary>
public sealed record Lyrics(string? Synced, string? Plain);

/// <summary>
/// Procura a letra, nesta ordem: um .lrc com o mesmo nome do arquivo, a letra gravada no arquivo
/// e, se a pessoa deixou, o LRCLIB (lrclib.net, aberto e sem chave). As achadas na internet
/// ficam guardadas em %LocalAppData%\Sleeve\letras.
/// </summary>
public static class LyricsService
{
    private static readonly Lazy<HttpClient> Http = new(() =>
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Sleeve/{UpdateService.CurrentText} (+https://github.com/paulocmarinho/sleeve)");
        return http;
    });

    private static string CacheDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sleeve", "letras");

    public static async Task<Lyrics?> GetAsync(string? path, string title, string artist, string album, int duration, bool online)
    {
        if (path is not null && FromFile(path) is Lyrics local) return local;
        if (!online || string.IsNullOrWhiteSpace(title)) return null;

        var file = Path.Combine(CacheDir, Hash($"{artist}|{title}|{album}".ToLowerInvariant()) + ".json");
        try
        {
            if (File.Exists(file)) return JsonSerializer.Deserialize<Lyrics>(File.ReadAllText(file));
        }
        catch { /* cache estragado: busca de novo */ }

        var found = await FromLrclibAsync(title, artist, album, duration);
        if (found is not null)
        {
            try
            {
                Directory.CreateDirectory(CacheDir);
                File.WriteAllText(file, JsonSerializer.Serialize(found));
            }
            catch { /* sem cache, tudo bem */ }
        }
        return found;
    }

    private static Lyrics? FromFile(string path)
    {
        try
        {
            var lrc = Path.ChangeExtension(path, ".lrc");
            if (File.Exists(lrc)) return Wrap(File.ReadAllText(lrc));
            using var tags = TagLib.File.Create(path);
            return Wrap(tags.Tag.Lyrics);
        }
        catch
        {
            return null;
        }
    }

    private static Lyrics? Wrap(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return Regex.IsMatch(text, @"^\s*\[\d+:\d+", RegexOptions.Multiline) ? new Lyrics(text, null) : new Lyrics(null, text);
    }

    private static async Task<Lyrics?> FromLrclibAsync(string title, string artist, string album, int duration)
    {
        static string E(string s) => Uri.EscapeDataString(s);
        try
        {
            // 1. Busca exata (artista, música, álbum e duração)
            var q = $"track_name={E(title)}&artist_name={E(artist)}";
            if (album.Length > 0) q += $"&album_name={E(album)}";
            if (duration > 0) q += $"&duration={duration}";
            using (var r = await Http.Value.GetAsync("https://lrclib.net/api/get?" + q))
            {
                if (r.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
                    if (Read(doc.RootElement) is Lyrics exact) return exact;
                }
            }

            // 2. Busca mais solta, com o artista principal (sem "feat." e parceiros)
            var main = Regex.Split(artist, @",|&| feat\.? | ft\.? | x ", RegexOptions.IgnoreCase)[0].Trim();
            using var s = await Http.Value.GetAsync($"https://lrclib.net/api/search?track_name={E(title)}&artist_name={E(main)}");
            if (!s.IsSuccessStatusCode) return null;
            using var list = JsonDocument.Parse(await s.Content.ReadAsStringAsync());
            if (list.RootElement.ValueKind != JsonValueKind.Array) return null;
            var candidates = list.RootElement.EnumerateArray()
                .Where(x => duration <= 0 || !x.TryGetProperty("duration", out var d) || d.ValueKind != JsonValueKind.Number || Math.Abs(d.GetDouble() - duration) <= 4)
                .ToList();
            var best = candidates.FirstOrDefault(x => Has(x, "syncedLyrics"));
            if (best.ValueKind == JsonValueKind.Undefined) best = candidates.FirstOrDefault(x => Has(x, "plainLyrics"));
            return best.ValueKind == JsonValueKind.Undefined ? null : Read(best);
        }
        catch
        {
            return null; // sem internet ou o LRCLIB fora do ar
        }
    }

    private static bool Has(JsonElement x, string name) =>
        x.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString());

    private static Lyrics? Read(JsonElement x)
    {
        var synced = Has(x, "syncedLyrics") ? x.GetProperty("syncedLyrics").GetString() : null;
        var plain = Has(x, "plainLyrics") ? x.GetProperty("plainLyrics").GetString() : null;
        return synced is null && plain is null ? null : new Lyrics(synced, synced is null ? plain : null);
    }

    private static string Hash(string text) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();
}
