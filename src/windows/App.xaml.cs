using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

namespace Sleeve;

public partial class App : Application
{
    private const string MutexName = "Sleeve.SingleInstance";
    private const string ShowSignalName = "Sleeve.Show";

    private Mutex? _singleInstance;
    private EventWaitHandle? _showSignal;
    private RegisteredWaitHandle? _showWait;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Só um player por vez. Abrir de novo traz a janela que já está rodando (mesmo escondida na bandeja).
        _singleInstance = new Mutex(true, MutexName, out bool isFirst);

        // Acabou de atualizar: a versão antiga ainda está fechando, então espera ela sair
        if (!isFirst && Array.IndexOf(e.Args, "--atualizado") >= 0)
        {
            try { isFirst = _singleInstance.WaitOne(TimeSpan.FromSeconds(15)); }
            catch (AbandonedMutexException) { isFirst = true; }
        }

        // Músicas pedidas pelo Windows (duplo clique num .mp3, "Abrir com › Sleeve")
        var files = e.Args.Where(a => !a.StartsWith("--", StringComparison.Ordinal) && File.Exists(a)).ToArray();

        if (!isFirst)
        {
            // Já tem um Sleeve aberto: entrega as músicas para ele e sai
            OpenRequest.Write(files);
            try { EventWaitHandle.OpenExisting(ShowSignalName).Set(); }
            catch { /* a outra instância está fechando */ }
            Shutdown();
            return;
        }

        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showSignal, (_, _) =>
        {
            // Vários arquivos abertos de uma vez chegam quase juntos: espera um instante e pega todos
            Thread.Sleep(350);
            var pending = OpenRequest.ReadAndClear();
            Dispatcher.InvokeAsync(() =>
            {
                var window = MainWindow as Sleeve.MainWindow;
                window?.ShowFromTray();
                if (pending.Length > 0) window?.OpenFiles(pending);
            });
        }, null, Timeout.Infinite, false);

        // Primeira abertura depois da troca de nome: traz as configurações antigas
        LegacyMigration.Run();

        // Aparece em "Abrir com" para MP3, FLAC etc. (só para este usuário, sem virar o padrão sozinho)
        try { FileAssociations.Register(); }
        catch (Exception ex) { Log(ex); }

        // Um erro inesperado não derruba o player; fica registrado num log
        DispatcherUnhandledException += (_, args) =>
        {
            Log(args.Exception);
            args.Handled = true;
        };

        // A janela pode ficar escondida na bandeja; o app só fecha pelo "Sair"
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        base.OnStartup(e);
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        if (files.Length > 0) window.OpenFiles(files);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showWait?.Unregister(null);
        _showSignal?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    public static void Log(Exception ex)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sleeve");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "erros.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch
        {
            // sem log, sem drama
        }
    }
}
