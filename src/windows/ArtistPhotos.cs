using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Sleeve;

/// <summary>
/// Foto do artista. Primeiro um artist.jpg (ou .png) na pasta do álbum ou na pasta acima dela;
/// se não tiver e a pessoa deixou, o Deezer (busca pública, sem chave, foto de 1000 px).
/// Para não pegar um homônimo, a foto do Deezer só vale se algum álbum ou música da biblioteca
/// aparecer na discografia daquele artista. Tudo fica guardado em %LocalAppData%\Sleeve\artistas.
/// </summary>
public static class ArtistPhotos
{
    public static string Dir => Path.Combine(LibraryService.DataDir, "artistas");

    private static readonly SemaphoreSlim OneAtATime = new(1, 1);
    private static readonly Lazy<HttpClient> Http = new(() =>
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Sleeve/{UpdateService.CurrentText} (+https://github.com/paulocmarinho/sleeve)");
        return http;
    });
    private static readonly string[] LocalNames = { "artist.jpg", "artist.jpeg", "artist.png" };

    /// <summary>Endereço da foto para a interface, ou null se não achou.</summary>
    public static async Task<string?> GetAsync(string name, IReadOnlyList<string> albums, IReadOnlyList<string> tracks,
        IReadOnlyList<string> albumFolders, bool online)
    {
        var key = Hash(name.ToLowerInvariant());

        // 1. Foto escolhida pela pessoa, na pasta do artista
        foreach (var folder in albumFolders)
        {
            foreach (var dir in new[] { folder, Path.GetDirectoryName(folder) })
            {
                if (string.IsNullOrEmpty(dir)) continue;
                foreach (var file in LocalNames)
                {
                    var path = Path.Combine(dir, file);
                    if (!File.Exists(path)) continue;
                    Directory.CreateDirectory(Dir);
                    var dest = Path.Combine(Dir, key + "-local" + Path.GetExtension(path).ToLowerInvariant());
                    if (!File.Exists(dest) || File.GetLastWriteTimeUtc(dest) < File.GetLastWriteTimeUtc(path)) File.Copy(path, dest, true);
                    return Url(dest);
                }
            }
        }

        // 2. Já baixada antes
        var saved = Path.Combine(Dir, key + ".jpg");
        if (File.Exists(saved)) return Url(saved);
        if (!online) return null;
        var none = Path.Combine(Dir, key + ".none");
        if (File.Exists(none) && File.GetLastWriteTimeUtc(none) > DateTime.UtcNow.AddDays(-14)) return null;

        // 3. Deezer (um artista por vez, para não passar do limite de pedidos deles)
        await OneAtATime.WaitAsync();
        try
        {
            var bytes = await FromDeezerAsync(name, albums, tracks);
            Directory.CreateDirectory(Dir);
            if (bytes is null)
            {
                File.WriteAllText(none, "");
                return null;
            }
            File.WriteAllBytes(saved, bytes);
            return Url(saved);
        }
        catch
        {
            return null; // sem internet: tenta de novo na próxima vez
        }
        finally
        {
            await Task.Delay(120);
            OneAtATime.Release();
        }
    }

    private static string Url(string file) =>
        "https://app.sleeve/artista/" + Path.GetFileName(file) + "?v=" + File.GetLastWriteTimeUtc(file).Ticks;

    private static async Task<byte[]?> FromDeezerAsync(string name, IReadOnlyList<string> albums, IReadOnlyList<string> tracks)
    {
        var wanted = Norm(name);
        using var search = await GetJsonAsync("https://api.deezer.com/search/artist?limit=8&q=" + Uri.EscapeDataString(name));
        if (search is null || !search.RootElement.TryGetProperty("data", out var data)) return null;

        var candidates = data.EnumerateArray()
            .Where(a => Norm(Str(a, "name")) == wanted)
            .Take(3)
            .Select(a => (Id: a.GetProperty("id").GetInt64(), Picture: Str(a, "picture_xl")))
            .ToList();

        var localAlbums = albums.Select(Norm).Where(x => x.Length > 0).ToHashSet();
        var localTracks = tracks.Select(Norm).Where(x => x.Length > 0).ToHashSet();

        foreach (var (id, picture) in candidates)
        {
            // Artista sem foto no Deezer vem com uma imagem genérica (endereço com "/artist//")
            if (picture.Length == 0 || picture.Contains("/artist//", StringComparison.Ordinal)) continue;
            if (!await MatchesAsync(id, localAlbums, localTracks)) continue;
            var bytes = await Http.Value.GetByteArrayAsync(picture);
            return bytes.Length > 1000 ? bytes : null;
        }
        return null;
    }

    /// <summary>Algum álbum ou música da biblioteca está na discografia desse artista do Deezer?</summary>
    private static async Task<bool> MatchesAsync(long id, HashSet<string> albums, HashSet<string> tracks)
    {
        using (var list = await GetJsonAsync($"https://api.deezer.com/artist/{id}/albums?limit=100"))
        {
            if (list is not null && list.RootElement.TryGetProperty("data", out var data)
                && data.EnumerateArray().Any(a => Same(Norm(Str(a, "title")), albums)))
                return true;
        }
        using var top = await GetJsonAsync($"https://api.deezer.com/artist/{id}/top?limit=50");
        return top is not null && top.RootElement.TryGetProperty("data", out var songs)
            && songs.EnumerateArray().Any(t => Same(Norm(Str(t, "title")), tracks));
    }

    private static bool Same(string remote, HashSet<string> local) =>
        remote.Length > 0 && (local.Contains(remote) || local.Any(l => l.Length >= 4 && remote.Length >= 4 && (l.Contains(remote) || remote.Contains(l))));

    private static async Task<JsonDocument?> GetJsonAsync(string url)
    {
        using var r = await Http.Value.GetAsync(url);
        if (!r.IsSuccessStatusCode) return null;
        var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("error", out _)) { doc.Dispose(); return null; }
        return doc;
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    /// <summary>Minúsculas, sem acento, sem "(Deluxe)", "[Remaster]", "feat." e pontuação.</summary>
    public static string Norm(string text)
    {
        var s = Regex.Replace(text.ToLowerInvariant(), @"\(.*?\)|\[.*?\]|\b(feat|ft)\.?\s.*$", " ");
        var plain = new StringBuilder();
        foreach (var c in s.Normalize(NormalizationForm.FormD))
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            plain.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }
        return Regex.Replace(plain.ToString(), @"\s+", " ").Trim();
    }

    private static string Hash(string text) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();
}
