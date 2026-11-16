using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Sleeve;

/// <summary>Ícone perto do relógio, com o menu do player.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _mini;
    private readonly ToolStripMenuItem _topmost;
    private readonly ToolStripMenuItem _hiRes;
    private readonly ToolStripMenuItem _startup;
    private readonly ToolStripMenuItem _show, _play, _next, _exit;

    public event Action? ToggleWindow;
    public event Action? PlayPause;
    public event Action? Next;
    public event Action? ToggleMini;
    public event Action? ToggleTopmost;
    public event Action? ToggleHiRes;
    public event Action? Exit;

    public TrayIcon()
    {
        _mini = new ToolStripMenuItem(Strings.Get("tray.mini"), null, (_, _) => ToggleMini?.Invoke());
        _topmost = new ToolStripMenuItem(Strings.Get("tray.pin"), null, (_, _) => ToggleTopmost?.Invoke());
        _hiRes = new ToolStripMenuItem(Strings.Get("tray.hiRes"), null, (_, _) => ToggleHiRes?.Invoke());
        _startup = new ToolStripMenuItem(Strings.Get("tray.startup"), null, (_, _) =>
        {
            StartupManager.Set(!StartupManager.IsEnabled);
            _startup!.Checked = StartupManager.IsEnabled;
        });

        var menu = new ContextMenuStrip();
        _show = new ToolStripMenuItem(Strings.Get("tray.show"), null, (_, _) => ToggleWindow?.Invoke());
        _play = new ToolStripMenuItem(Strings.Get("tray.play"), null, (_, _) => PlayPause?.Invoke());
        _next = new ToolStripMenuItem(Strings.Get("tray.next"), null, (_, _) => Next?.Invoke());
        _exit = new ToolStripMenuItem(Strings.Get("tray.exit"), null, (_, _) => Exit?.Invoke());
        menu.Items.Add(_show);
        menu.Items.Add(_play);
        menu.Items.Add(_next);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_mini);
        menu.Items.Add(_topmost);
        menu.Items.Add(_hiRes);
        menu.Items.Add(_startup);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exit);
        menu.Opening += (_, _) => _startup.Checked = StartupManager.IsEnabled;

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Sleeve",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ToggleWindow?.Invoke();
        };
    }

    public void Sync(bool mini, bool topmost, bool hiRes)
    {
        _mini.Checked = mini;
        _topmost.Checked = topmost;
        _hiRes.Checked = hiRes;
    }

    /// <summary>Reescreve o menu no idioma atual.</summary>
    public void Relabel()
    {
        _show.Text = Strings.Get("tray.show");
        _play.Text = Strings.Get("tray.play");
        _next.Text = Strings.Get("tray.next");
        _mini.Text = Strings.Get("tray.mini");
        _topmost.Text = Strings.Get("tray.pin");
        _hiRes.Text = Strings.Get("tray.hiRes");
        _startup.Text = Strings.Get("tray.startup");
        _exit.Text = Strings.Get("tray.exit");
    }

    public void Hint(string text) =>
        _icon.ShowBalloonTip(4000, "Sleeve", text, ToolTipIcon.None);

    public void SetText(string text) =>
        _icon.Text = text.Length > 63 ? text[..60] + "..." : text;

    private static Icon LoadIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (path is not null && Icon.ExtractAssociatedIcon(path) is Icon icon) return icon;
        }
        catch
        {
            // usa o ícone padrão
        }
        return SystemIcons.Application;
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _icon.Visible = false;
        _icon.Dispose();
    }
}

/// <summary>Liga/desliga a abertura automática junto com o Windows (só para o seu usuário).</summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Sleeve";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(ValueName) is string;
            }
            catch
            {
                return false;
            }
        }
    }

    public static void Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled && Environment.ProcessPath is string exe)
                key.SetValue(ValueName, $"\"{exe}\"");
            else
                key.DeleteValue(ValueName, false);
        }
        catch
        {
            // sem permissão no registro: a opção só não fica marcada
        }
    }
}
