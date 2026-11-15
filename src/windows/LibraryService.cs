using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Sleeve;

// O que a interface recebe (os nomes em minúsculo viram o JSON)
public sealed record TrackDto(string id, string title, string artist, int number, int disc, double duration, string url);

public sealed class AlbumDto
{
    public string id { get; set; } = "";
    public string title { get; set; } = "";
    public string artist { get; set; } = "";
    public int? year { get; set; }
    public string? cover { get; set; }
    public string color { get; set; } = "#7C3AED";
    public long added { get; set; }
    public List<TrackDto> tracks { get; set; } = new();
}

/// <summary>
/// Lê as pastas de música: título, artista, álbum, número da faixa e capa de cada arquivo.
/// Guarda o que já leu (por arquivo e data de modificação) para a próxima abertura ser rápida.
/// </summary>
public sealed class LibraryService
{
    public static readonly string[] Extensions = { ".mp3", ".flac", ".m4a", ".aac", ".ogg", ".oga", ".opus", ".wav" };

    public static string DataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sleeve");
    public static string CoverDir => Path.Combine(DataDir, "capas");
    private static string CacheFile => Path.Combine(DataDir, "biblioteca.json");

    /// <summary>Nome do endereço interno de cada pasta: a pasta 0 vira https://musica0.sleeve/…</summary>
    public static string HostFor(int folderIndex) => $"musica{folderIndex}.sleeve";

    // O que foi lido de cada arquivo
    private sealed class FileEntry
    {
        public string Path { get; set; } = "";
        public long Modified { get; set; }
        public long Size { get; set; }
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string AlbumArtist { get; set; } = "";
        public string Album { get; set; } = "";
        public int Number { get; set; }
        public int Disc { get; set; }
        public int Year { get; set; }
        public double Duration { get; set; }
    }

    private Dictionary<string, FileEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public List<AlbumDto> Albums { get; private set; } = new();

    /// <summary>Quantos arquivos já leu (avisado de tempos em tempos durante a leitura).</summary>
    public event Action<int>? Progress;

    public LibraryService()
    {
        try
        {
            if (File.Exists(CacheFile))
            {
                var list = JsonSerializer.Deserialize<List<FileEntry>>(File.ReadAllText(CacheFile)) ?? new();
                _entries = list.ToDictionary(e => e.Path, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch
        {
            // cache estragado: lê tudo de novo
        }
    }

    /// <summary>Monta os álbuns só com o que já estava guardado (para abrir na hora).</summary>
    public List<AlbumDto> BuildFromCache(IReadOnlyList<string> folders)
    {
        lock (_gate)
        {
            var existing = _entries.Values.Where(e => FolderIndex(folders, e.Path) >= 0).ToList();
            Albums = Build(existing, folders);
            return Albums;
        }
    }

    public Task<List<AlbumDto>> ScanAsync(IReadOnlyList<string> folders, CancellationToken token) =>
        Task.Run(() => Scan(folders, token), token);

    private List<AlbumDto> Scan(IReadOnlyList<string> folders, CancellationToken token)
    {
        var found = new List<FileEntry>();
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
        int count = 0;

        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder)) continue;
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(folder, "*", options); }
            catch { continue; }

            foreach (var path in files)
            {
                token.ThrowIfCancellationRequested();
                if (!Extensions.Contains(Path.GetExtension(path).ToLowerInvariant())) continue;

                FileInfo info;
                try { info = new FileInfo(path); } catch { continue; }
                var modified = info.LastWriteTimeUtc.Ticks;

                FileEntry? entry;
                lock (_gate) _entries.TryGetValue(path, out entry);
                if (entry is null || entry.Modified != modified || entry.Size != info.Length)
                    entry = ReadTags(path, modified, info.Length);

                found.Add(entry);
                if (++count % 50 == 0) Progress?.Invoke(count);
            }
        }

        lock (_gate)
        {
            _entries = found.GroupBy(e => e.Path, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            SaveCache();
            Albums = Build(found, folders);
            return Albums;
        }
    }

    private static FileEntry ReadTags(string path, long modified, long size)
    {
        var e = new FileEntry { Path = path, Modified = modified, Size = size };
        try
        {
            using var f = TagLib.File.Create(path);
            var t = f.Tag;
            e.Title = t.Title ?? "";
            e.Artist = t.JoinedPerformers ?? "";
            e.AlbumArtist = t.FirstAlbumArtist ?? "";
            e.Album = t.Album ?? "";
            e.Number = (int)t.Track;
            e.Disc = (int)t.Disc;
            e.Year = (int)t.Year;
            e.Duration = f.Properties?.Duration.TotalSeconds ?? 0;
        }
        catch
        {
            // arquivo sem etiquetas ou estragado: fica com o nome do arquivo
        }
        if (string.IsNullOrWhiteSpace(e.Title)) e.Title = Path.GetFileNameWithoutExtension(path);
        return e;
    }

    private List<AlbumDto> Build(List<FileEntry> files, IReadOnlyList<string> folders)
    {
        Directory.CreateDirectory(CoverDir);
        var albums = new List<AlbumDto>();

        // Agrupa por artista do álbum + nome do álbum. Sem nome de álbum: a pasta vira o álbum.
        foreach (var group in files.GroupBy(AlbumKey))
        {
            var list = group.OrderBy(e => e.Disc).ThenBy(e => e.Number).ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
            var first = list[0];
            var albumName = !string.IsNullOrWhiteSpace(first.Album) ? first.Album : new DirectoryInfo(Path.GetDirectoryName(first.Path) ?? "").Name;
            var artist = !string.IsNullOrWhiteSpace(first.AlbumArtist) ? first.AlbumArtist
                       : list.Select(e => e.Artist).Where(a => a.Length > 0).GroupBy(a => a).Count() == 1 ? first.Artist
                       : list.Any(e => e.Artist.Length > 0) ? "Vários artistas" : "";
            var id = Hash(group.Key);
            int firstYear = list.Select(e => e.Year).FirstOrDefault(v => v > 0);

            var album = new AlbumDto
            {
                id = id,
                title = albumName,
                artist = artist,
                year = firstYear > 0 ? firstYear : null,
                added = list.Max(e => e.Modified),
                tracks = list.Select((e, i) => new TrackDto(
                    Hash(e.Path), e.Title, e.Artist, e.Number > 0 ? e.Number : i + 1, Math.Max(1, e.Disc), Math.Round(e.Duration, 1), UrlFor(e.Path, folders))).ToList(),
            };
            album.tracks.RemoveAll(t => t.url.Length == 0);
            if (album.tracks.Count == 0) continue;

            (album.cover, album.color) = EnsureCover(id, list);
            albums.Add(album);
        }
        return albums;
    }

    private static string AlbumKey(FileEntry e)
    {
        if (string.IsNullOrWhiteSpace(e.Album)) return "pasta|" + (Path.GetDirectoryName(e.Path) ?? "").ToLowerInvariant();
        var artist = !string.IsNullOrWhiteSpace(e.AlbumArtist) ? e.AlbumArtist : "";
        // Sem "artista do álbum", junta pelo nome do álbum dentro da mesma pasta (coletâneas)
        var where = artist.Length > 0 ? artist.ToLowerInvariant() : (Path.GetDirectoryName(e.Path) ?? "").ToLowerInvariant();
        return where + "|" + e.Album.Trim().ToLowerInvariant();
    }

    /// <summary>Capa do álbum: a que vem dentro do arquivo, ou uma imagem na pasta. Devolve o endereço e a cor marcante.</summary>
    private static (string? url, string color) EnsureCover(string albumId, List<FileEntry> tracks)
    {
        var jpg = Path.Combine(CoverDir, albumId + ".jpg");
        var png = Path.Combine(CoverDir, albumId + ".png");
        var colorFile = Path.Combine(CoverDir, albumId + ".cor");
        string? existing = File.Exists(jpg) ? jpg : File.Exists(png) ? png : null;

        if (existing is null)
        {
            var bytes = EmbeddedPicture(tracks) ?? FolderPicture(tracks);
            if (bytes is null) return (null, "#7C3AED");
            existing = IsPng(bytes) ? png : jpg;
            try { File.WriteAllBytes(existing, bytes); } catch { return (null, "#7C3AED"); }
            try { File.Delete(colorFile); } catch { /* sem cor guardada */ }
        }

        string color;
        try
        {
            color = File.Exists(colorFile) ? File.ReadAllText(colorFile).Trim() : "";
            if (color.Length != 7)
            {
                var bmp = Palette.Decode(File.ReadAllBytes(existing));
                var c = bmp is null ? System.Windows.Media.Color.FromRgb(0x7C, 0x3A, 0xED) : Palette.Dominant(bmp);
                color = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                File.WriteAllText(colorFile, color);
            }
        }
        catch
        {
            color = "#7C3AED";
        }
        return ("https://app.sleeve/capa/" + Path.GetFileName(existing) + "?v=" + File.GetLastWriteTimeUtc(existing).Ticks, color);
    }

    /// <summary>Guarda uma capa vinda de fora (iTunes) para um álbum sem capa.</summary>
    public static (string url, string color)? SaveCover(string albumId, byte[] bytes)
    {
        try
        {
            Directory.CreateDirectory(CoverDir);
            var path = Path.Combine(CoverDir, albumId + (IsPng(bytes) ? ".png" : ".jpg"));
            File.WriteAllBytes(path, bytes);
            var bmp = Palette.Decode(bytes);
            var c = bmp is null ? System.Windows.Media.Color.FromRgb(0x7C, 0x3A, 0xED) : Palette.Dominant(bmp);
            var color = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            File.WriteAllText(Path.Combine(CoverDir, albumId + ".cor"), color);
            return ("https://app.sleeve/capa/" + Path.GetFileName(path) + "?v=" + DateTime.UtcNow.Ticks, color);
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? EmbeddedPicture(List<FileEntry> tracks)
    {
        foreach (var e in tracks.Take(4))
        {
            try
            {
                using var f = TagLib.File.Create(e.Path);
                var pics = f.Tag.Pictures;
                if (pics is { Length: > 0 })
                {
                    var front = pics.FirstOrDefault(p => p.Type == TagLib.PictureType.FrontCover) ?? pics[0];
                    if (front.Data?.Data is { Length: > 100 } data) return data;
                }
            }
            catch
            {
                // tenta o próximo arquivo
            }
        }
        return null;
    }

    private static readonly string[] FolderNames = { "cover", "folder", "front", "capa", "album", "albumart" };

    private static byte[]? FolderPicture(List<FileEntry> tracks)
    {
        var dir = Path.GetDirectoryName(tracks[0].Path);
        if (dir is null) return null;
        try
        {
            var images = Directory.EnumerateFiles(dir)
                .Where(p => p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var best = images.FirstOrDefault(p => FolderNames.Contains(Path.GetFileNameWithoutExtension(p).ToLowerInvariant()))
                       ?? (images.Count == 1 ? images[0] : null);
            return best is null ? null : File.ReadAllBytes(best);
        }
        catch
        {
            return null;
        }
    }

    private static string UrlFor(string path, IReadOnlyList<string> folders)
    {
        int i = FolderIndex(folders, path);
        if (i < 0) return "";
        var root = folders[i].TrimEnd('\\', '/');
        var relative = path.Substring(root.Length).TrimStart('\\', '/');
        var parts = relative.Split('\\', '/').Select(Uri.EscapeDataString);
        return $"https://{HostFor(i)}/" + string.Join('/', parts);
    }

    private static int FolderIndex(IReadOnlyList<string> folders, string path)
    {
        for (int i = 0; i < folders.Count; i++)
        {
            var root = folders[i].TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    private void SaveCache()
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(CacheFile, JsonSerializer.Serialize(_entries.Values.ToList()));
        }
        catch
        {
            // sem cache: só fica mais lento na próxima abertura
        }
    }

    private static bool IsPng(byte[] b) => b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47;

    public static string Hash(string s) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(s.ToLowerInvariant()))).Substring(0, 16).ToLowerInvariant();
}
