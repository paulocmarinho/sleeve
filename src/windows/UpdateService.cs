using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sleeve;

/// <summary>
/// Atualização automática pelo GitHub: confere a última versão publicada em
/// Releases, baixa o Sleeve.exe novo ao lado do atual e troca na hora de reiniciar.
/// </summary>
public sealed class UpdateService
{
    /// <summary>Repositório de onde vêm as versões.</summary>
    public const string Repository = "paulocmarinho/sleeve";
    public static string ReleasesPage => $"https://github.com/{Repository}/releases/latest";

    private static readonly HttpClient Http = CreateClient();

    public static Version Current { get; } =
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

    public static string CurrentText => $"{Current.Major}.{Current.Minor}.{Math.Max(0, Current.Build)}";

    private static string? ExePath => Environment.ProcessPath;
    private static string? StagedPath => ExePath is null ? null : ExePath + ".novo";
    private static string? OldPath => ExePath is null ? null : ExePath + ".antigo";

    /// <summary>Versão nova já baixada e pronta para trocar.</summary>
    public string? ReadyVersion { get; private set; }

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Sleeve-Atualizador/" + CurrentText);
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }

    /// <summary>Apaga o .exe antigo que ficou da última atualização.</summary>
    public static void CleanUp()
    {
        try { if (OldPath is not null && File.Exists(OldPath)) File.Delete(OldPath); }
        catch { /* ainda em uso: fica para a próxima */ }
    }

    /// <summary>Procura uma versão mais nova. Devolve (versão, link do Sleeve.exe) ou null se já está em dia.</summary>
    public async Task<(string version, string url)?> CheckAsync()
    {
        using var doc = JsonDocument.Parse(await Http.GetStringAsync($"https://api.github.com/repos/{Repository}/releases/latest"));
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest)) return null;
        if (Normalize(latest) <= Normalize(Current)) return null;

        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (string.Equals(asset.GetProperty("name").GetString(), "Sleeve.exe", StringComparison.OrdinalIgnoreCase))
                return (tag.TrimStart('v', 'V'), asset.GetProperty("browser_download_url").GetString() ?? "");
        }
        return null;
    }

    /// <summary>Baixa o .exe novo para "Sleeve.exe.novo", ao lado do atual.</summary>
    public async Task DownloadAsync(string version, string url)
    {
        if (StagedPath is null) throw new InvalidOperationException("Não achei onde o Sleeve está.");
        var temp = StagedPath + ".parcial";
        using (var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync();
            await using var output = File.Create(temp);
            await input.CopyToAsync(output);
        }

        // Confere que é mesmo um programa do Windows, e não uma página de erro
        var head = new byte[2];
        await using (var check = File.OpenRead(temp))
        {
            if (check.Length < 1_000_000 || await check.ReadAsync(head) != 2 || head[0] != 'M' || head[1] != 'Z')
                throw new InvalidDataException("O arquivo baixado não é o Sleeve.");
        }

        if (File.Exists(StagedPath)) File.Delete(StagedPath);
        File.Move(temp, StagedPath);
        ReadyVersion = version;
    }

    /// <summary>
    /// Troca o .exe e abre a versão nova. O Windows deixa renomear um programa aberto,
    /// então o atual vira "Sleeve.exe.antigo" e o novo assume o nome original.
    /// </summary>
    public bool ApplyAndRestart(bool restart = true)
    {
        if (ExePath is null || StagedPath is null || OldPath is null || !File.Exists(StagedPath)) return false;
        try
        {
            if (File.Exists(OldPath)) File.Delete(OldPath);
            File.Move(ExePath, OldPath);
            File.Move(StagedPath, ExePath);
        }
        catch
        {
            // Sem permissão de escrita na pasta: desfaz o que der
            try { if (!File.Exists(ExePath) && File.Exists(OldPath)) File.Move(OldPath, ExePath); } catch { }
            return false;
        }
        if (restart) Process.Start(new ProcessStartInfo(ExePath, "--atualizado") { UseShellExecute = false });
        return true;
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));
}
