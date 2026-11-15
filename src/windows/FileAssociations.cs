using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace Sleeve;

/// <summary>
/// Faz o Sleeve aparecer em "Abrir com" para arquivos de música, só para o usuário atual.
/// Não vira o programa padrão sozinho: isso o Windows deixa só a pessoa escolher.
/// O caminho do .exe é regravado a cada abertura, então mover o Sleeve de pasta não quebra nada.
/// </summary>
public static class FileAssociations
{
    private const string ProgId = "Sleeve.Musica";

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

    public static void Register()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return;
        var command = $"\"{exe}\" \"%1\"";

        using var classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes");

        // Já está tudo apontando para este .exe: nada a fazer
        using (var current = classes.OpenSubKey($@"{ProgId}\shell\open\command"))
            if (current?.GetValue("") as string == command) return;

        using (var prog = classes.CreateSubKey(ProgId))
        {
            prog.SetValue("", "Música");
            using (var icon = prog.CreateSubKey("DefaultIcon")) icon.SetValue("", $"\"{exe}\",0");
            using (var cmd = prog.CreateSubKey(@"shell\open\command")) cmd.SetValue("", command);
        }

        using (var app = classes.CreateSubKey(@"Applications\Sleeve.exe"))
        {
            app.SetValue("FriendlyAppName", "Sleeve");
            using (var cmd = app.CreateSubKey(@"shell\open\command")) cmd.SetValue("", command);
            using var types = app.CreateSubKey("SupportedTypes");
            foreach (var ext in LibraryService.Extensions) types.SetValue(ext, "");
        }

        foreach (var ext in LibraryService.Extensions)
        {
            using var key = classes.CreateSubKey($@"{ext}\OpenWithProgids");
            key.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
        }

        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); // avisa o Explorer
    }
}

/// <summary>
/// Recado entre duas cópias do Sleeve: a que o Windows abriu com o arquivo
/// anota o caminho aqui, e a que já estava aberta lê e toca.
/// </summary>
public static class OpenRequest
{
    private static string FilePath => Path.Combine(LibraryService.DataDir, "abrir.txt");

    public static void Write(string[] files)
    {
        if (files.Length == 0) return;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                Directory.CreateDirectory(LibraryService.DataDir);
                File.AppendAllLines(FilePath, files);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(50); // outra cópia escrevendo ao mesmo tempo
            }
        }
    }

    public static string[] ReadAndClear()
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                if (!File.Exists(FilePath)) return Array.Empty<string>();
                var lines = File.ReadAllLines(FilePath);
                File.Delete(FilePath);
                return lines.Where(l => l.Length > 0 && File.Exists(l)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
        }
        return Array.Empty<string>();
    }
}
