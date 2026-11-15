using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sleeve;

/// <summary>
/// Busca a capa em 1000 px na busca pública do iTunes (sem login nem chave).
/// Tenta pelo álbum (loja BR, depois US) e, se não achar, pela música.
/// Só aceita o resultado se o artista bater; senão fica com a capa do Windows.
/// As capas encontradas ficam guardadas em %LocalAppData%\Sleeve\capas.
/// </summary>
public static class HiResCovers
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly ConcurrentDictionary<string, Task<byte[]?>> Requests = new();

    private static string CacheDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Sleeve", "capas");

    public static Task<byte[]?> GetAsync(string artist, string album, string title)
    {
        var key = $"{Normalize(PrimaryArtist(artist))}|{Normalize(album)}|{Normalize(title)}";
        return Requests.GetOrAdd(key, k => FetchAsync(k, artist, album, title));
    }

    private static async Task<byte[]?> FetchAsync(string key, string artist, string album, string title)
    {
        var file = Path.Combine(CacheDir, Hash(key) + ".jpg");
        try
        {
            if (File.Exists(file))
                return await File.ReadAllBytesAsync(file).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(artist))
            {
                CoverLog.Write($"[iTunes] sem artista, não dá para buscar: \"{title}\"");
                return null;
            }

            string? art = null;
            if (!string.IsNullOrWhiteSpace(album))
            {
                art = await SearchAsync(artist, album, "album", "collectionName", "BR").ConfigureAwait(false)
                   ?? await SearchAsync(artist, album, "album", "collectionName", "US").ConfigureAwait(false);
            }
            if (art is null && !string.IsNullOrWhiteSpace(title))
            {
                art = await SearchAsync(artist, title, "song", "trackName", "BR").ConfigureAwait(false)
                   ?? await SearchAsync(artist, title, "song", "trackName", "US").ConfigureAwait(false);
            }

            if (art is null)
            {
                CoverLog.Write($"[iTunes] não encontrada: {artist} — \"{album}\" / \"{title}\"");
                return null;
            }

            var bytes = await Http.GetByteArrayAsync(art.Replace("100x100bb", "1000x1000bb")).ConfigureAwait(false);
            Directory.CreateDirectory(CacheDir);
            await File.WriteAllBytesAsync(file, bytes).ConfigureAwait(false);
            CoverLog.Write($"[iTunes] ok: {artist} — \"{album}\"");
            return bytes;
        }
        catch (Exception ex)
        {
            // sem internet ou resposta estranha: tenta de novo na próxima vez que a faixa tocar
            CoverLog.Write($"[iTunes] erro em {artist} — \"{album}\": {ex.GetType().Name}: {ex.Message}");
            Requests.TryRemove(key, out _);
            return null;
        }
    }

    /// <summary>Procura e devolve o link da capa (100 px) do primeiro resultado que bater, ou null.</summary>
    private static async Task<string?> SearchAsync(string artist, string name, string entity, string nameField, string country)
    {
        var term = Uri.EscapeDataString($"{PrimaryArtist(artist)} {StripExtras(name)}");
        var url = $"https://itunes.apple.com/search?term={term}&entity={entity}&limit=25&country={country}";

        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url).ConfigureAwait(false));
        var wantArtist = Normalize(PrimaryArtist(artist));
        var wantName = Normalize(name);

        foreach (var r in doc.RootElement.GetProperty("results").EnumerateArray())
        {
            var gotArtist = Normalize(Str(r, "artistName"));
            var gotName = Normalize(Str(r, nameField));
            var artistOk = gotArtist.Length > 0 && (gotArtist.Contains(wantArtist) || wantArtist.Contains(gotArtist));
            var nameOk = gotName.Length > 0 && (gotName == wantName || gotName.StartsWith(wantName + " ") || wantName.StartsWith(gotName + " "));
            var art = Str(r, "artworkUrl100");
            if (artistOk && nameOk && art.Length > 0) return art;
        }
        return null;
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    /// <summary>"Artista A, Artista B" → "Artista A"</summary>
    private static string PrimaryArtist(string artist) =>
        artist.Split(new[] { ',', '&', ';' }, 2)[0].Trim();

    /// <summary>Tira "(Deluxe)", "[Remaster]" e "- Single" do texto da busca.</summary>
    private static string StripExtras(string s)
    {
        s = Regex.Replace(s, @"\(.*?\)|\[.*?\]", " ");
        s = Regex.Replace(s, @"\s-\s.*$", " ");
        return s.Trim();
    }

    /// <summary>Minúsculas, sem acento, sem extras e sem pontuação, para comparar nomes.</summary>
    private static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var decomposed = StripExtras(s).ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string Hash(string s) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
}

/// <summary>Registro curto do que aconteceu com as capas, em %AppData%\Sleeve\capas.log.</summary>
public static class CoverLog
{
    private static readonly object Gate = new();

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Sleeve", "capas.log");

    public static void Write(string line)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > 200_000) info.Delete(); // não deixa crescer pra sempre
                File.AppendAllText(FilePath, $"[{DateTime.Now:dd/MM HH:mm:ss}] {line}\n");
            }
        }
        catch
        {
            // registro é opcional
        }
    }
}
