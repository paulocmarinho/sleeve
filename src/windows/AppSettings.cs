using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Sleeve;

/// <summary>Posição da janela, modo mini e "sempre no topo", salvos entre aberturas.</summary>
public sealed class AppSettings
{
    public double? Left { get; set; }
    public double? Top { get; set; }
    public bool Mini { get; set; }
    public bool Topmost { get; set; }
    public bool HiResCovers { get; set; } = true;
    public bool TrayHintShown { get; set; }
    public bool Dark { get; set; }
    public bool AutoUpdate { get; set; } = true;
    public string? Language { get; set; } // pt-BR, en ou es; vazio segue o Windows

    // Tamanho da janela grande (o mini tem tamanho fixo)
    public double? Width { get; set; }
    public double? Height { get; set; }
    public bool Maximized { get; set; }

    // Pastas de onde vem a biblioteca
    public List<string> MusicFolders { get; set; } = new();
    public bool FoldersConfigured { get; set; }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Sleeve", "config.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch
        {
            // arquivo corrompido: começa do zero
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // sem permissão de escrita: segue sem salvar
        }
    }
}

/// <summary>
/// Traz as configurações e capas do nome antigo (Tocando agora) na primeira vez que o Sleeve abre,
/// e troca a abertura automática do .exe antigo pela do novo.
/// </summary>
public static class LegacyMigration
{
    private const string OldName = "TocandoAgora";
    private const string NewName = "Sleeve";

    public static void Run()
    {
        TryMoveFolder(Environment.SpecialFolder.ApplicationData);
        TryMoveFolder(Environment.SpecialFolder.LocalApplicationData);
        TryMoveStartup();
    }

    private static void TryMoveFolder(Environment.SpecialFolder root)
    {
        try
        {
            var baseDir = Environment.GetFolderPath(root);
            var oldDir = Path.Combine(baseDir, OldName);
            var newDir = Path.Combine(baseDir, NewName);
            if (Directory.Exists(oldDir) && !Directory.Exists(newDir))
                Directory.Move(oldDir, newDir);
        }
        catch
        {
            // pasta em uso ou sem permissão: começa do zero
        }
    }

    private static void TryMoveStartup()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key?.GetValue(OldName) is null) return;

            key.DeleteValue(OldName, false);
            StartupManager.Set(true); // mesma escolha, agora apontando para o Sleeve
        }
        catch
        {
            // sem permissão no registro
        }
    }
}
