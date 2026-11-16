using System.Collections.Generic;
using System.Globalization;

namespace Sleeve;

/// <summary>
/// Textos que o Windows mostra fora da interface: menu da bandeja, avisos e janelas.
/// A interface avisa o idioma escolhido; antes disso vale o salvo ou o do sistema.
/// </summary>
public static class Strings
{
    public static string Lang { get; private set; } = FromSystem();

    private static readonly Dictionary<string, Dictionary<string, string>> All = new()
    {
        ["pt-BR"] = new()
        {
            ["tray.show"] = "Mostrar / ocultar", ["tray.play"] = "Tocar / pausar", ["tray.next"] = "Próxima",
            ["tray.mini"] = "Modo mini", ["tray.pin"] = "Sempre no topo", ["tray.hiRes"] = "Capa em alta resolução",
            ["tray.startup"] = "Iniciar com o Windows", ["tray.exit"] = "Sair",
            ["hint.tray"] = "O Sleeve continua tocando aqui na bandeja. Use \"Sair\" no menu para fechar de vez.",
            ["folder.pick"] = "Escolha a pasta das suas músicas",
            ["folder.nested"] = "Essa pasta já faz parte da biblioteca, porque está dentro de:\n{0}\n\nO Sleeve lê todas as pastas dentro das que você adicionou.",
            ["webview.missing"] = "O Sleeve precisa do Microsoft Edge WebView2, que já vem no Windows 10 e 11 atualizados.\n\nSe ele não estiver instalado, baixe em: {0}\n\n{1}",
        },
        ["en"] = new()
        {
            ["tray.show"] = "Show / hide", ["tray.play"] = "Play / pause", ["tray.next"] = "Next",
            ["tray.mini"] = "Mini player", ["tray.pin"] = "Always on top", ["tray.hiRes"] = "High-resolution covers",
            ["tray.startup"] = "Start with Windows", ["tray.exit"] = "Quit",
            ["hint.tray"] = "Sleeve keeps playing here in the tray. Use \"Quit\" in the menu to close it for good.",
            ["folder.pick"] = "Choose your music folder",
            ["folder.nested"] = "This folder is already in your library, because it is inside:\n{0}\n\nSleeve reads every folder inside the ones you added.",
            ["webview.missing"] = "Sleeve needs Microsoft Edge WebView2, which comes with up-to-date Windows 10 and 11.\n\nIf it is not installed, download it from: {0}\n\n{1}",
        },
        ["es"] = new()
        {
            ["tray.show"] = "Mostrar / ocultar", ["tray.play"] = "Reproducir / pausar", ["tray.next"] = "Siguiente",
            ["tray.mini"] = "Minirreproductor", ["tray.pin"] = "Siempre visible", ["tray.hiRes"] = "Portadas en alta resolución",
            ["tray.startup"] = "Iniciar con Windows", ["tray.exit"] = "Salir",
            ["hint.tray"] = "Sleeve sigue sonando aquí en la bandeja. Usa \"Salir\" en el menú para cerrarlo del todo.",
            ["folder.pick"] = "Elige la carpeta de tu música",
            ["folder.nested"] = "Esta carpeta ya está en tu biblioteca, porque está dentro de:\n{0}\n\nSleeve lee todas las carpetas dentro de las que añadiste.",
            ["webview.missing"] = "Sleeve necesita Microsoft Edge WebView2, que viene con Windows 10 y 11 actualizados.\n\nSi no está instalado, descárgalo en: {0}\n\n{1}",
        },
    };

    /// <summary>Troca o idioma; devolve true se mudou.</summary>
    public static bool Set(string? lang)
    {
        var next = lang is not null && All.ContainsKey(lang) ? lang : FromSystem();
        if (next == Lang) return false;
        Lang = next;
        return true;
    }

    public static string Get(string key, params object[] args)
    {
        var text = All[Lang].TryGetValue(key, out var s) ? s : All["pt-BR"][key];
        return args.Length > 0 ? string.Format(text, args) : text;
    }

    private static string FromSystem()
    {
        var name = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return name == "pt" ? "pt-BR" : name == "es" ? "es" : "en";
    }
}
