using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Sleeve;

/// <summary>
/// Alguns FLAC tocam no player do Windows, mas o motor de áudio do Sleeve (o do Edge) recusa o arquivo
/// inteiro por um defeito nas etiquetas: capa gravada sem o tipo da imagem, ou etiquetas ID3 no começo.
/// Aqui sai uma cópia sem esses pedaços, em %LocalAppData%\Sleeve\reparados. O som é o mesmo; o original fica intacto.
/// </summary>
public static class FlacRepair
{
    public const string Host = "reparados.sleeve";

    public static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sleeve", "reparados");

    /// <summary>Nome do arquivo consertado dentro de <see cref="Dir"/>, ou null se não havia o que consertar.</summary>
    public static string? Repair(string path)
    {
        if (!path.EndsWith(".flac", StringComparison.OrdinalIgnoreCase)) return null;
        var info = new FileInfo(path);
        var name = Hash($"{path.ToLowerInvariant()}|{info.Length}|{info.LastWriteTimeUtc.Ticks}") + ".flac";
        var dest = Path.Combine(Dir, name);
        if (File.Exists(dest)) return name;

        using var src = File.OpenRead(path);
        var changed = false;
        var head = ReadExactly(src, 10);
        if (head[0] == 'I' && head[1] == 'D' && head[2] == '3')
        {
            // Etiquetas ID3 antes do FLAC: pula (tamanho em "syncsafe", 7 bits por byte)
            long size = (head[6] << 21) | (head[7] << 14) | (head[8] << 7) | head[9];
            if ((head[5] & 0x10) != 0) size += 10;
            src.Position = 10 + size;
            head = ReadExactly(src, 4);
            changed = true;
        }
        else
        {
            src.Position = 4;
        }
        if (head[0] != 'f' || head[1] != 'L' || head[2] != 'a' || head[3] != 'C') return null;

        var blocks = new List<(int Type, byte[] Data)>();
        var last = false;
        while (!last)
        {
            var h = ReadExactly(src, 4);
            last = (h[0] & 0x80) != 0;
            var type = h[0] & 0x7F;
            var data = ReadExactly(src, (h[1] << 16) | (h[2] << 8) | h[3]);
            if (type == 6 && !PictureOk(data)) { changed = true; continue; }
            blocks.Add((type, data));
        }
        if (!changed || blocks.Count == 0 || blocks[0].Type != 0) return null;

        Directory.CreateDirectory(Dir);
        var temp = dest + ".tmp";
        using (var outFile = File.Create(temp))
        {
            outFile.Write("fLaC"u8);
            for (var i = 0; i < blocks.Count; i++)
            {
                var (type, data) = blocks[i];
                outFile.WriteByte((byte)(type | (i == blocks.Count - 1 ? 0x80 : 0)));
                outFile.WriteByte((byte)(data.Length >> 16));
                outFile.WriteByte((byte)(data.Length >> 8));
                outFile.WriteByte((byte)data.Length);
                outFile.Write(data);
            }
            src.CopyTo(outFile);
        }
        File.Move(temp, dest, overwrite: true);
        return name;
    }

    /// <summary>Apaga cópias que não são usadas há mais de 30 dias.</summary>
    public static void CleanUp()
    {
        try
        {
            if (!Directory.Exists(Dir)) return;
            foreach (var f in Directory.EnumerateFiles(Dir))
                if (File.GetLastAccessTimeUtc(f) < DateTime.UtcNow.AddDays(-30) || f.EndsWith(".tmp")) File.Delete(f);
        }
        catch { /* fica para a próxima */ }
    }

    // Bloco de imagem: tipo (4 bytes), tamanho do tipo MIME (4 bytes) e o MIME, que não pode faltar
    private static bool PictureOk(byte[] d)
    {
        if (d.Length < 32) return false;
        var mimeLength = (d[4] << 24) | (d[5] << 16) | (d[6] << 8) | d[7];
        return mimeLength > 0 && 8 + mimeLength <= d.Length;
    }

    private static byte[] ReadExactly(Stream s, int count)
    {
        var buffer = new byte[count];
        s.ReadExactly(buffer);
        return buffer;
    }

    private static string Hash(string text) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();
}
