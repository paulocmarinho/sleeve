using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace Sleeve;

/// <summary>
/// A janela do Sleeve no Windows: mostra a interface (pasta wwwroot, dentro do .exe)
/// num WebView2 e faz por ela o que só o Windows sabe: ler as pastas de música,
/// conversar com o Spotify, volume por app, bandeja e a própria janela.
/// </summary>
public partial class MainWindow : Window
{
    private const double MiniWidth = 320, MiniHeight = 64;
    private const double DefaultWidth = 1200, DefaultHeight = 760;

    private readonly AppSettings _settings = AppSettings.Load();
    private readonly MediaService _media = new();
    private readonly VolumeService _volume = new();
    private readonly LibraryService _library = new();
    private readonly UpdateService _updater = new();
    private readonly TrayIcon _tray;
    private readonly DispatcherTimer _spotifyDebounce = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private readonly HashSet<string> _hiResTried = new();
    private readonly Dictionary<string, string> _coverColors = new();

    private CoreWebView2? _core;
    private CancellationTokenSource? _scanCts;
    private bool _ready, _exiting, _mini;
    private string _localText = "", _spotifyText = "";
    private bool _localPlaying, _spotifyPlaying;
    private bool _updateBusy;
    private string _updateState = "idle";
    private string? _updateVersion;

    public MainWindow()
    {
        InitializeComponent();
        ApplyTheme(_settings.Dark); // abre já na cor certa, sem piscar branco

        Strings.Set(_settings.Language);
        _tray = new TrayIcon();
        SetupTaskbar();
        _tray.ToggleWindow += ToggleFromTray;
        _tray.PlayPause += () => Post(new { type = "media-key", key = "toggle" });
        _tray.Next += () => Post(new { type = "media-key", key = "next" });
        _tray.ToggleMini += () => { ShowFromTray(); SetMini(!_mini); };
        _tray.ToggleTopmost += () => SetTopmost(!Topmost);
        _tray.ToggleHiRes += () => SetHiRes(!_settings.HiResCovers);
        _tray.Exit += ExitApp;

        // Desligar ou sair da conta do Windows fecha de verdade, sem esconder na bandeja
        Application.Current.SessionEnding += (_, _) =>
        {
            _exiting = true;
            _tray.Dispose();
        };

        _spotifyDebounce.Tick += async (_, _) =>
        {
            _spotifyDebounce.Stop();
            await SendSpotifyAsync();
        };
        _library.Progress += n => Dispatcher.InvokeAsync(() => Post(new { type = "scan", count = n }));

        // Primeira vez: usa a pasta Música do Windows
        if (!_settings.FoldersConfigured)
        {
            var music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            if (Directory.Exists(music)) _settings.MusicFolders.Add(music);
            _settings.FoldersConfigured = true;
            _settings.Save();
        }

        RestorePlacement();
        SourceInitialized += (_, _) => RoundCorners();
        Activated += (_, _) => Web.Focus();
        Loaded += OnLoaded;
    }

    private IReadOnlyList<string> Folders => _settings.MusicFolders;

    // ---------- Início ----------

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(LibraryService.DataDir, "WebView2"));
            await Web.EnsureCoreWebView2Async(env);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Strings.Get("webview.missing", "https://go.microsoft.com/fwlink/p/?LinkId=2124703", ex.Message),
                "Sleeve", MessageBoxButton.OK, MessageBoxImage.Warning);
            ExitApp();
            return;
        }

        _core = Web.CoreWebView2;
        var s = _core.Settings;
        s.AreDefaultContextMenusEnabled = false;
        s.IsStatusBarEnabled = false;
        s.IsZoomControlEnabled = false;
        s.AreBrowserAcceleratorKeysEnabled = false;
        s.IsPasswordAutosaveEnabled = false;
        s.IsGeneralAutofillEnabled = false;
        s.IsSwipeNavigationEnabled = false;
#if !DEBUG
        s.AreDevToolsEnabled = false;
#endif

        _core.AddWebResourceRequestedFilter("https://app.sleeve/*", CoreWebView2WebResourceContext.All);
        _core.WebResourceRequested += OnResource;
        _core.WebMessageReceived += OnMessage;
        _core.NewWindowRequested += (_, a) => a.Handled = true; // nunca abre janelas de navegador
        MapFolders();
        try
        {
            // Cópias de FLAC consertadas (veja FlacRepair)
            Directory.CreateDirectory(FlacRepair.Dir);
            _core.SetVirtualHostNameToFolderMapping(FlacRepair.Host, FlacRepair.Dir, CoreWebView2HostResourceAccessKind.Allow);
            _ = Task.Run(FlacRepair.CleanUp);
        }
        catch (Exception ex) { App.Log(ex); }
        _core.Navigate("https://app.sleeve/index.html");

        _media.Changed += () => Dispatcher.InvokeAsync(() =>
        {
            _spotifyDebounce.Stop();
            _spotifyDebounce.Start();
        });
        try { await _media.InitAsync(); }
        catch { /* sem controle de mídia do Windows: segue só com os arquivos */ }

        if (_settings.Maximized && !_mini) WindowState = WindowState.Maximized;
    }

    /// <summary>Cada pasta de música vira um endereço interno (https://musica0.sleeve/…) que o player toca direto.</summary>
    private void MapFolders()
    {
        if (_core is null) return;
        for (int i = 0; i < 32; i++)
        {
            try { _core.ClearVirtualHostNameToFolderMapping(LibraryService.HostFor(i)); }
            catch { /* não estava mapeada */ }
        }
        for (int i = 0; i < Folders.Count; i++)
        {
            if (!Directory.Exists(Folders[i])) continue;
            try { _core.SetVirtualHostNameToFolderMapping(LibraryService.HostFor(i), Folders[i], CoreWebView2HostResourceAccessKind.Allow); }
            catch { /* pasta inacessível */ }
        }
    }

    // ---------- Arquivos da interface e capas ----------

    private void OnResource(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (_core is null) return;
        byte[]? data = null;
        string path = "";
        try
        {
            path = Uri.UnescapeDataString(new Uri(e.Request.Uri).AbsolutePath).TrimStart('/');
            if (path.Length == 0) path = "index.html";

            if (path.StartsWith("capa/", StringComparison.Ordinal))
            {
                var file = Path.Combine(LibraryService.CoverDir, Path.GetFileName(path));
                if (File.Exists(file)) data = File.ReadAllBytes(file);
            }
            else if (path.StartsWith("artista/", StringComparison.Ordinal))
            {
                var file = Path.Combine(ArtistPhotos.Dir, Path.GetFileName(path));
                if (File.Exists(file)) data = File.ReadAllBytes(file);
            }
            else
            {
                data = WebAssets.Read(path);
            }
        }
        catch
        {
            data = null;
        }

        e.Response = data is null
            ? _core.Environment.CreateWebResourceResponse(null, 404, "Not Found", "")
            : _core.Environment.CreateWebResourceResponse(new MemoryStream(data), 200, "OK",
                $"Content-Type: {WebAssets.Mime(path)}\r\nCache-Control: no-store");
    }

    // ---------- Mensagens da interface ----------

    private void Post(object message)
    {
        if (_core is null || !_ready) return;
        try { _core.PostWebMessageAsJson(JsonSerializer.Serialize(message)); }
        catch (Exception ex) { App.Log(ex); }
    }

    private static void Safe(Action action)
    {
        try { action(); }
        catch (Exception ex) { App.Log(ex); }
    }

    private static string Str(JsonElement m, string name) =>
        m.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static bool Bool(JsonElement m, string name) =>
        m.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static double Num(JsonElement m, string name) =>
        m.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;

    private async void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        JsonElement m;
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            m = doc.RootElement.Clone();
        }
        catch
        {
            return;
        }
        if (m.ValueKind != JsonValueKind.Object) return;

        switch (Str(m, "type"))
        {
            case "ready":
                // Cada etapa por conta própria: se uma falhar, as outras continuam
                _ready = true;
                Safe(SendSettings);
                Safe(() => SendLibrary(_library.BuildFromCache(Folders), scanning: true));
                Safe(() => SendUpdate(_updateState));
                if (_pendingOpen is { Length: > 0 } pending) { _pendingOpen = null; Safe(() => OpenFiles(pending)); }
                try { await SendSpotifyAsync(); } catch (Exception ex) { App.Log(ex); }
                _ = CheckUpdatesLaterAsync();
                StartWatching();
                await ScanAsync();
                break;

            case "update":
                switch (Str(m, "action"))
                {
                    case "check": await CheckUpdatesAsync(auto: false); break;
                    case "restart": RestartToUpdate(); break;
                    case "page": OpenReleasesPage(); break;
                }
                break;

            case "window":
                HandleWindow(Str(m, "action"), Str(m, "edge"));
                break;

            case "spotify":
                switch (Str(m, "action"))
                {
                    case "toggle": await _media.TogglePlayPauseAsync(); break;
                    case "pause": await _media.PauseAsync(); break;
                    case "next": await _media.NextAsync(); break;
                    case "previous": await _media.PreviousAsync(); break;
                    case "seek": await _media.SeekAsync(TimeSpan.FromSeconds(Num(m, "position"))); break;
                }
                break;

            case "volume":
                _volume.Set(ProcessFor(Str(m, "app")), (float)Num(m, "value"));
                break;

            case "local":
                _localPlaying = Bool(m, "playing");
                var title = Str(m, "title");
                var artist = Str(m, "artist");
                _localText = artist.Length > 0 ? $"{title} — {artist}" : title;
                UpdateTitle();
                break;

            case "folders":
                switch (Str(m, "action"))
                {
                    case "add": AddFolder(); break;
                    case "remove": RemoveFolder(Str(m, "path")); break;
                    case "rescan": await ScanAsync(); break;
                }
                break;

            case "artistPhotos":
            {
                if (!m.TryGetProperty("artists", out var list) || list.ValueKind != JsonValueKind.Array) break;
                var online = Bool(m, "online");
                foreach (var ar in list.EnumerateArray().ToList())
                {
                    var name = Str(ar, "name");
                    string? url = null;
                    try
                    {
                        var folders = Strs(ar, "urls").Select(u => PathFromUrl(u)).OfType<string>()
                            .Select(p => Path.GetDirectoryName(p)).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                        url = await ArtistPhotos.GetAsync(name, Strs(ar, "albums"), Strs(ar, "tracks"), folders, online);
                    }
                    catch (Exception ex) { App.Log(ex); }
                    Post(new { type = "artistPhoto", name, url });
                }
                break;
            }
            case "loudness":
            {
                if (!m.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) break;
                foreach (var it in items.EnumerateArray().ToList())
                {
                    var id = Str(it, "id");
                    double? gain = null;
                    try
                    {
                        var path = PathFromUrl(Str(it, "url"));
                        if (path is not null) gain = await Loudness.GainAsync(path);
                    }
                    catch (Exception ex) { App.Log(ex); }
                    Post(new { type = "loudness", id, gain });
                }
                break;
            }
            case "playlistImport":
            {
                var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Playlist (*.m3u, *.m3u8)|*.m3u;*.m3u8", Title = "Sleeve" };
                if (dialog.ShowDialog(this) != true) break;
                try
                {
                    var file = dialog.FileName;
                    var dir = Path.GetDirectoryName(file) ?? "";
                    var ids = new List<string>();
                    foreach (var raw in File.ReadAllLines(file))
                    {
                        var line = raw.Trim().TrimStart('\uFEFF');
                        if (line.Length == 0 || line.StartsWith('#')) continue;
                        if (Uri.TryCreate(line, UriKind.Absolute, out var u) && u.IsFile) line = u.LocalPath;
                        var full = Path.GetFullPath(Path.IsPathRooted(line) ? line : Path.Combine(dir, line.Replace('/', Path.DirectorySeparatorChar)));
                        ids.Add(LibraryService.Hash(full));
                    }
                    Post(new { type = "playlistImported", name = Path.GetFileNameWithoutExtension(file), ids });
                }
                catch (Exception ex)
                {
                    App.Log(ex);
                    Post(new { type = "playlistImported", name = "", ids = Array.Empty<string>() });
                }
                break;
            }
            case "playlistExport":
            {
                var name = string.Join("_", Str(m, "name").Split(Path.GetInvalidFileNameChars())).Trim();
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Playlist (*.m3u8)|*.m3u8|Playlist (*.m3u)|*.m3u",
                    FileName = name.Length > 0 ? name : "Playlist",
                    Title = "Sleeve",
                };
                if (dialog.ShowDialog(this) != true) break;
                try
                {
                    var lines = new List<string> { "#EXTM3U" };
                    lines.AddRange(Strs(m, "urls").Select(u => PathFromUrl(u)).OfType<string>());
                    File.WriteAllLines(dialog.FileName, lines, new System.Text.UTF8Encoding(false));
                    Post(new { type = "playlistExported", path = dialog.FileName });
                }
                catch (Exception ex) { App.Log(ex); }
                break;
            }
            case "playstate":
                UpdateTaskbar(Bool(m, "playing"), Bool(m, "canPrevious"), Bool(m, "canNext"));
                break;
            case "repair":
            {
                var id = Str(m, "id");
                string? fixedUrl = null;
                try
                {
                    var path = PathFromUrl(Str(m, "url"));
                    var name = path is null ? null : await Task.Run(() => FlacRepair.Repair(path));
                    if (name is not null) fixedUrl = $"https://{FlacRepair.Host}/{name}";
                }
                catch (Exception ex) { App.Log(ex); }
                Post(new { type = "repaired", id, url = fixedUrl });
                break;
            }
            case "lyrics":
            {
                var key = Str(m, "key");
                try
                {
                    var url = Str(m, "url");
                    var path = url.Length > 0 ? PathFromUrl(url) : null;
                    var found = await LyricsService.GetAsync(path, Str(m, "title"), Str(m, "artist"), Str(m, "album"), (int)Num(m, "duration"), Bool(m, "online"));
                    Post(new { type = "lyrics", key, synced = found?.Synced, plain = found?.Plain });
                }
                catch (Exception ex)
                {
                    App.Log(ex);
                    Post(new { type = "lyrics", key, synced = (string?)null, plain = (string?)null });
                }
                break;
            }
            case "language":
                var lang = Str(m, "lang");
                if (Strings.Set(lang)) { _tray.Relabel(); RelabelTaskbar(); }
                if (_settings.Language != Strings.Lang) { _settings.Language = Strings.Lang; _settings.Save(); }
                break;
            case "theme":
                var dark = Bool(m, "dark");
                ApplyTheme(dark);
                if (_settings.Dark != dark) { _settings.Dark = dark; _settings.Save(); }
                break;

            case "setting":
                var on = Bool(m, "value");
                switch (Str(m, "key"))
                {
                    case "hiRes": SetHiRes(on); break;
                    case "startup": StartupManager.Set(on); SendSettings(); break;
                    case "pinned": SetTopmost(on); break;
                    case "autoUpdate":
                        _settings.AutoUpdate = on;
                        _settings.Save();
                        SendSettings();
                        if (on) await CheckUpdatesAsync(auto: true);
                        break;
                }
                break;
        }
    }

    /// <summary>Fundo da janela na cor do tema (aparece enquanto a interface carrega e nas bordas).</summary>
    private void ApplyTheme(bool dark)
    {
        var bg = dark ? System.Drawing.Color.FromArgb(0x0F, 0x0F, 0x12) : System.Drawing.Color.FromArgb(0xF5, 0xF5, 0xF7);
        Web.DefaultBackgroundColor = bg;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(bg.R, bg.G, bg.B));
    }

    private void SendSettings() => Post(new
    {
        type = "settings",
        hiRes = _settings.HiResCovers,
        startup = StartupManager.IsEnabled,
        pinned = Topmost,
        mini = _mini,
        autoUpdate = _settings.AutoUpdate,
    });

    // ---------- Atualização pelo GitHub ----------

    private async Task CheckUpdatesLaterAsync()
    {
        try
        {
            UpdateService.CleanUp();
            await Task.Delay(TimeSpan.FromSeconds(8)); // deixa o Sleeve abrir em paz primeiro
            await CheckUpdatesAsync(auto: true);
        }
        catch (Exception ex)
        {
            App.Log(ex);
        }
    }

    private async Task CheckUpdatesAsync(bool auto)
    {
        if (_updateBusy || _updateState == "ready") return;
        if (auto && !_settings.AutoUpdate) return;
        _updateBusy = true;
        try
        {
            SendUpdate("checking");
            var found = await _updater.CheckAsync();
            if (found is null) { SendUpdate("none"); return; }
            SendUpdate("downloading", found.Value.version);
            await _updater.DownloadAsync(found.Value.version, found.Value.url);
            SendUpdate("ready", found.Value.version);
        }
        catch (Exception ex)
        {
            // Sem internet, repositório fora do ar ou ainda sem versões publicadas
            App.Log(ex);
            Safe(() => SendUpdate(auto ? "idle" : "error"));
        }
        finally
        {
            _updateBusy = false;
        }
    }

    private void SendUpdate(string state, string? version = null)
    {
        _updateState = state;
        if (version is not null) _updateVersion = version;
        Post(new { type = "update", state, version = _updateVersion, current = UpdateService.CurrentText });
    }

    private void RestartToUpdate()
    {
        SavePlacement();
        if (!_updater.ApplyAndRestart())
        {
            // Pasta sem permissão de escrita: abre a página para baixar à mão
            SendUpdate("error");
            OpenReleasesPage();
            return;
        }
        _exiting = true;
        _scanCts?.Cancel();
        _tray.Dispose();
        Close();
        Application.Current.Shutdown();
    }

    private static void OpenReleasesPage()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(UpdateService.ReleasesPage) { UseShellExecute = true }); }
        catch { /* sem navegador padrão */ }
    }

    private void SendLibrary(List<AlbumDto> albums, bool scanning) => Post(new
    {
        type = "library",
        albums,
        folders = Folders,
        scanning,
    });

    // ---------- Biblioteca ----------

    private async Task ScanAsync()
    {
        _scanCts?.Cancel();
        var cts = _scanCts = new CancellationTokenSource();
        try
        {
            var albums = await _library.ScanAsync(Folders.ToList(), cts.Token);
            if (cts.IsCancellationRequested) return;
            SendLibrary(albums, scanning: false);
            if (_settings.HiResCovers) await FillMissingCoversAsync(albums, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // uma leitura nova começou
        }
        catch
        {
            SendLibrary(_library.Albums, scanning: false);
        }
    }

    /// <summary>Álbuns sem capa nenhuma: tenta a capa no iTunes (no máximo 40 por leitura).</summary>
    private async Task FillMissingCoversAsync(List<AlbumDto> albums, CancellationToken token)
    {
        int changed = 0, tried = 0;
        foreach (var album in albums.Where(a => a.cover is null && a.artist.Length > 0).ToList())
        {
            if (token.IsCancellationRequested || ++tried > 40) break;
            var bytes = await HiResCovers.GetAsync(album.artist, album.title, album.tracks.FirstOrDefault()?.title ?? "");
            if (bytes is null) continue;
            var saved = LibraryService.SaveCover(album.id, bytes);
            if (saved is null) continue;
            (album.cover, album.color) = saved.Value;
            if (++changed % 5 == 0) SendLibrary(albums, scanning: false);
        }
        if (changed % 5 != 0) SendLibrary(albums, scanning: false);
    }

    private void AddFolder()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = Strings.Get("folder.pick"),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        var path = dialog.SelectedPath;
        if (Folders.Any(f => f.Equals(path, StringComparison.OrdinalIgnoreCase))) return;

        // Já está dentro de uma pasta da lista: ela já é lida junto, não precisa repetir
        var parent = LibraryService.ParentIn(Folders, path);
        if (parent is not null)
        {
            MessageBox.Show(Strings.Get("folder.nested", parent),
                "Sleeve", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // A pasta nova contém pastas que já estavam na lista: ela substitui essas
        _settings.MusicFolders.RemoveAll(f => LibraryService.ParentIn(new[] { path }, f) is not null);
        _settings.MusicFolders.Add(path);
        _settings.Save();
        FoldersChanged();
    }

    private void RemoveFolder(string path)
    {
        if (_settings.MusicFolders.RemoveAll(f => f.Equals(path, StringComparison.OrdinalIgnoreCase)) == 0) return;
        _settings.Save();
        FoldersChanged();
    }

    private async void FoldersChanged()
    {
        MapFolders();
        StartWatching();
        SendLibrary(_library.BuildFromCache(Folders), scanning: true);
        await ScanAsync();
    }

    // ---------- Músicas abertas pelo Windows (duplo clique, "Abrir com") ----------

    private string[]? _pendingOpen;
    private readonly Dictionary<string, string> _fileHosts = new(StringComparer.OrdinalIgnoreCase);

    private static List<string> Strs(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? "").ToList()
            : new List<string>();

    // ---------- Botões na miniatura da barra de tarefas ----------
    private System.Windows.Shell.ThumbButtonInfo? _tbPrev, _tbPlay, _tbNext;
    private bool _tbPlaying;

    private void SetupTaskbar()
    {
        System.Windows.Shell.ThumbButtonInfo Button(string glyph, string label, string key)
        {
            var b = new System.Windows.Shell.ThumbButtonInfo { ImageSource = Glyph(glyph), Description = label, IsEnabled = false };
            b.Click += (_, _) => Post(new { type = "media-key", key });
            return b;
        }
        _tbPrev = Button(GlyphPrev, Strings.Get("tb.prev"), "previous");
        _tbPlay = Button(GlyphPlay, Strings.Get("tb.play"), "toggle");
        _tbNext = Button(GlyphNext, Strings.Get("tb.next"), "next");
        TaskbarItemInfo = new System.Windows.Shell.TaskbarItemInfo();
        TaskbarItemInfo.ThumbButtonInfos.Add(_tbPrev);
        TaskbarItemInfo.ThumbButtonInfos.Add(_tbPlay);
        TaskbarItemInfo.ThumbButtonInfos.Add(_tbNext);
    }

    private void UpdateTaskbar(bool playing, bool canPrevious, bool canNext)
    {
        if (_tbPlay is null || _tbPrev is null || _tbNext is null) return;
        _tbPlaying = playing;
        _tbPrev.IsEnabled = canPrevious;
        _tbNext.IsEnabled = canNext;
        _tbPlay.IsEnabled = canPrevious || canNext;
        _tbPlay.ImageSource = Glyph(playing ? GlyphPause : GlyphPlay);
        _tbPlay.Description = Strings.Get(playing ? "tb.pause" : "tb.play");
    }

    private void RelabelTaskbar()
    {
        if (_tbPrev is null || _tbPlay is null || _tbNext is null) return;
        _tbPrev.Description = Strings.Get("tb.prev");
        _tbNext.Description = Strings.Get("tb.next");
        _tbPlay.Description = Strings.Get(_tbPlaying ? "tb.pause" : "tb.play");
    }

    private const string GlyphPlay = "M8,5 L19,12 L8,19 Z";
    private const string GlyphPause = "M7,5 H10.5 V19 H7 Z M13.5,5 H17 V19 H13.5 Z";
    private const string GlyphPrev = "M6,5 H8 V19 H6 Z M18,5 L9,12 L18,19 Z";
    private const string GlyphNext = "M16,5 H18 V19 H16 Z M6,5 L15,12 L6,19 Z";

    private static System.Windows.Media.ImageSource Glyph(string data)
    {
        var group = new System.Windows.Media.DrawingGroup();
        // Quadro transparente de 24x24 para todos os ícones terem o mesmo tamanho
        group.Children.Add(new System.Windows.Media.GeometryDrawing(System.Windows.Media.Brushes.Transparent, null,
            new System.Windows.Media.RectangleGeometry(new Rect(0, 0, 24, 24))));
        group.Children.Add(new System.Windows.Media.GeometryDrawing(System.Windows.Media.Brushes.White, null,
            System.Windows.Media.Geometry.Parse(data)));
        var image = new System.Windows.Media.DrawingImage(group);
        image.Freeze();
        return image;
    }

    /// <summary>Caminho no disco de um endereço interno de música (musicaN.sleeve ou arquivoN.sleeve).</summary>
    private string? PathFromUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        var host = uri.Host;
        string? root = null;
        if (host.StartsWith("musica", StringComparison.OrdinalIgnoreCase) && host.EndsWith(".sleeve", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(host[6..^7], out var i) && i >= 0 && i < Folders.Count)
            root = Folders[i];
        else
            root = _fileHosts.FirstOrDefault(kv => kv.Value.Equals(host, StringComparison.OrdinalIgnoreCase)).Key;
        if (root is null) return null;

        var full = Path.GetFullPath(Path.Combine(root, Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')).Replace('/', Path.DirectorySeparatorChar)));
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) && File.Exists(full) ? full : null;
    }

    /// <summary>Toca músicas que o Windows mandou abrir, estejam ou não na biblioteca.</summary>
    public void OpenFiles(string[] paths)
    {
        if (!_ready || _core is null)
        {
            // A interface ainda está carregando: toca assim que ela avisar que está pronta
            _pendingOpen = (_pendingOpen ?? Array.Empty<string>()).Concat(paths).ToArray();
            return;
        }

        var tracks = new List<object>();
        foreach (var path in paths.OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase))
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (dir is null || !LibraryService.Extensions.Contains(Path.GetExtension(path).ToLowerInvariant())) continue;
                if (!_fileHosts.TryGetValue(dir, out var host))
                {
                    // A pasta do arquivo vira um endereço interno, como as pastas da biblioteca
                    host = $"arquivo{_fileHosts.Count}.sleeve";
                    _core.SetVirtualHostNameToFolderMapping(host, dir, CoreWebView2HostResourceAccessKind.Allow);
                    _fileHosts[dir] = host;
                }
                tracks.Add(LibraryService.ReadOne(path, $"https://{host}/" + Uri.EscapeDataString(Path.GetFileName(path))));
            }
            catch (Exception ex)
            {
                App.Log(ex);
            }
        }
        if (tracks.Count > 0) Post(new { type = "open", tracks });
    }

    // ---------- Arquivos novos com o Sleeve aberto ----------

    private readonly List<FileSystemWatcher> _watchers = new();
    private DispatcherTimer? _rescanTimer;

    /// <summary>Fica de olho nas pastas: música nova, apagada ou renomeada faz a biblioteca se atualizar sozinha.</summary>
    private void StartWatching()
    {
        foreach (var w in _watchers) w.Dispose();
        _watchers.Clear();

        _rescanTimer ??= CreateRescanTimer();
        foreach (var folder in Folders)
        {
            if (!Directory.Exists(folder)) continue;
            try
            {
                var w = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    InternalBufferSize = 64 * 1024,
                };
                FileSystemEventHandler changed = (_, e) => OnFolderChanged(e.FullPath);
                w.Created += changed;
                w.Deleted += changed;
                w.Changed += changed;
                w.Renamed += (_, e) => OnFolderChanged(e.FullPath);
                w.Error += (_, _) => OnFolderChanged("");
                w.EnableRaisingEvents = true;
                _watchers.Add(w);
            }
            catch (Exception ex)
            {
                App.Log(ex);
            }
        }
    }

    private DispatcherTimer CreateRescanTimer()
    {
        // Espera as cópias terminarem (um álbum inteiro chega em vários arquivos) antes de ler de novo
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        t.Tick += async (_, _) =>
        {
            t.Stop();
            await ScanAsync();
        };
        return t;
    }

    private void OnFolderChanged(string path)
    {
        // Ignora o que não é música nem pasta (ex.: capas sendo salvas, arquivos temporários)
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext.Length > 0 && !LibraryService.Extensions.Contains(ext) && ext is not (".jpg" or ".jpeg" or ".png")) return;
        Dispatcher.InvokeAsync(() =>
        {
            _rescanTimer?.Stop();
            _rescanTimer?.Start();
        });
    }

    // ---------- Spotify ----------

    private static string ProcessFor(string app) =>
        string.IsNullOrWhiteSpace(app) || app.Equals("Spotify", StringComparison.OrdinalIgnoreCase) ? "Spotify" : app;

    private async Task SendSpotifyAsync()
    {
        if (!_ready) return;
        var info = await _media.ReadAsync();
        if (info is null || string.IsNullOrWhiteSpace(info.Title))
        {
            _spotifyText = "";
            _spotifyPlaying = false;
            UpdateTitle();
            Post(new { type = "spotify", info = (object?)null });
            return;
        }

        var key = LibraryService.Hash($"{info.Title}|{info.Artist}|{info.Album}");
        var (cover, color) = SpotifyCover(key, info.Cover);

        Post(new
        {
            type = "spotify",
            info = new
            {
                title = info.Title,
                artist = info.Artist,
                album = info.Album,
                cover,
                color,
                playing = info.IsPlaying,
                position = info.Position.TotalSeconds,
                duration = info.Duration.TotalSeconds,
                canSeek = info.CanSeek,
                canPrevious = info.CanPrevious,
                canNext = info.CanNext,
                source = info.SourceApp,
                volume = _volume.Get(ProcessFor(info.SourceApp)),
            },
        });

        _spotifyPlaying = info.IsPlaying;
        _spotifyText = info.Artist.Length > 0 ? $"{info.Title} — {info.Artist}" : info.Title;
        UpdateTitle();

        if (_settings.HiResCovers && _hiResTried.Add(key))
            _ = SpotifyHiResAsync(info, key);
    }

    /// <summary>Guarda a capa que o Spotify passou ao Windows e devolve o endereço + a cor marcante.</summary>
    private (string? url, string color) SpotifyCover(string key, byte[]? bytes)
    {
        try
        {
            Directory.CreateDirectory(LibraryService.CoverDir);
            var hi = Path.Combine(LibraryService.CoverDir, $"sp-{key}-hi.jpg");
            var normal = Path.Combine(LibraryService.CoverDir, $"sp-{key}.jpg");
            string? file = _settings.HiResCovers && File.Exists(hi) ? hi : null;
            if (file is null && bytes is not null)
            {
                if (!File.Exists(normal)) File.WriteAllBytes(normal, bytes);
                file = normal;
            }
            if (file is null) return (null, "#7C3AED");

            if (!_coverColors.TryGetValue(file, out var color))
            {
                var bmp = Palette.Decode(File.ReadAllBytes(file));
                var c = bmp is null ? System.Windows.Media.Color.FromRgb(0x7C, 0x3A, 0xED) : Palette.Dominant(bmp);
                color = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                _coverColors[file] = color;
            }
            return ("https://app.sleeve/capa/" + Path.GetFileName(file), color);
        }
        catch
        {
            return (null, "#7C3AED");
        }
    }

    private async Task SpotifyHiResAsync(TrackInfo info, string key)
    {
        var bytes = await HiResCovers.GetAsync(info.Artist, info.Album, info.Title);
        if (bytes is null) return;
        try
        {
            Directory.CreateDirectory(LibraryService.CoverDir);
            await File.WriteAllBytesAsync(Path.Combine(LibraryService.CoverDir, $"sp-{key}-hi.jpg"), bytes);
        }
        catch
        {
            return;
        }
        await SendSpotifyAsync();
    }

    private void SetHiRes(bool on)
    {
        _settings.HiResCovers = on;
        _settings.Save();
        _tray.Sync(_mini, Topmost, on);
        SendSettings();
        _ = SendSpotifyAsync();
    }

    // ---------- Janela ----------

    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private static readonly Dictionary<string, int> HitTest = new()
    {
        ["caption"] = 2, ["left"] = 10, ["right"] = 11, ["top"] = 12, ["topleft"] = 13,
        ["topright"] = 14, ["bottom"] = 15, ["bottomleft"] = 16, ["bottomright"] = 17,
    };

    /// <summary>Faz o Windows arrastar ou redimensionar a janela como se fosse pela borda/barra de título.</summary>
    private void BeginNonClient(string area)
    {
        if (!HitTest.TryGetValue(area, out var ht)) return;
        if (_mini && area != "caption") return;
        var hwnd = new WindowInteropHelper(this).Handle;
        ReleaseCapture();
        SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)ht, IntPtr.Zero);
        SavePlacement();
    }

    private void RoundCorners()
    {
        try
        {
            // Windows 11: cantos arredondados (no Windows 10 isso não faz nada)
            int round = 2;
            DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 33, ref round, sizeof(int));
        }
        catch
        {
            // Windows antigo
        }
    }

    private void HandleWindow(string action, string edge)
    {
        switch (action)
        {
            case "drag": BeginNonClient("caption"); break;
            case "resize": BeginNonClient(edge); break;
            case "minimize": WindowState = WindowState.Minimized; break;
            case "maximize":
                if (_mini) break;
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                break;
            case "close": HideToTray(); break;
            case "mini": SetMini(!_mini); break;
            case "pin": SetTopmost(!Topmost); break;
        }
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        // Maximizada, a janela sem borda passa um pouco da tela: compensa por dentro
        Root.Padding = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
        if (!_mini && WindowState != WindowState.Minimized)
        {
            _settings.Maximized = WindowState == WindowState.Maximized;
            _settings.Save();
        }
    }

    private void SetMini(bool mini)
    {
        if (mini == _mini) return;
        if (mini)
        {
            SavePlacement();
            if (WindowState == WindowState.Maximized) WindowState = WindowState.Normal;
            _mini = true;
            MinHeight = MiniHeight;
            Width = MiniWidth;
            Height = MiniHeight;
        }
        else
        {
            _mini = false;
            Width = _settings.Width ?? DefaultWidth;
            Height = _settings.Height ?? DefaultHeight;
            if (_settings.Maximized) WindowState = WindowState.Maximized;
        }
        _settings.Mini = _mini;
        _settings.Save();
        _tray.Sync(_mini, Topmost, _settings.HiResCovers);
        SendSettings();
    }

    private void SetTopmost(bool on)
    {
        Topmost = on;
        _settings.Topmost = on;
        _settings.Save();
        _tray.Sync(_mini, Topmost, _settings.HiResCovers);
        SendSettings();
    }

    private void UpdateTitle()
    {
        var text = _localPlaying ? _localText : _spotifyPlaying ? _spotifyText : _localText.Length > 0 ? _localText : _spotifyText;
        Title = text.Length > 0 ? text : "Sleeve";
        _tray.SetText(Title);
    }

    // ---------- Bandeja ----------

    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = _settings.Maximized && !_mini ? WindowState.Maximized : WindowState.Normal;
        Activate();
    }

    private void ToggleFromTray()
    {
        if (IsVisible && WindowState != WindowState.Minimized) HideToTray();
        else ShowFromTray();
    }

    private void HideToTray()
    {
        SavePlacement();
        Hide();
        if (!_settings.TrayHintShown)
        {
            _settings.TrayHintShown = true;
            _settings.Save();
            _tray.Hint(Strings.Get("hint.tray"));
        }
    }

    private void ExitApp()
    {
        // Atualização baixada e não aplicada: troca o .exe agora, sem reabrir
        if (_updater.ReadyVersion is not null) _updater.ApplyAndRestart(restart: false);
        _exiting = true;
        _scanCts?.Cancel();
        SavePlacement();
        _tray.Dispose();
        Close();
        Application.Current.Shutdown();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        // Alt+F4 também só esconde; fechar de vez é pelo "Sair"
        if (_exiting) return;
        e.Cancel = true;
        HideToTray();
    }

    private void SavePlacement()
    {
        if (!IsVisible || WindowState != WindowState.Normal) return;
        _settings.Left = Left;
        _settings.Top = Top;
        if (!_mini)
        {
            _settings.Width = Width;
            _settings.Height = Height;
        }
        _settings.Save();
    }

    private void RestorePlacement()
    {
        Topmost = _settings.Topmost;
        Width = Math.Max(MinWidth, _settings.Width ?? DefaultWidth);
        Height = Math.Max(480, _settings.Height ?? DefaultHeight);

        if (_settings.Left is double left && _settings.Top is double top &&
            left > SystemParameters.VirtualScreenLeft - 50 &&
            top > SystemParameters.VirtualScreenTop - 50 &&
            left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 80 &&
            top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 60)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        if (_settings.Mini)
        {
            _mini = true;
            Width = MiniWidth;
            Height = MiniHeight;
        }
        _tray.Sync(_mini, Topmost, _settings.HiResCovers);
    }
}

/// <summary>Os arquivos da interface (pasta wwwroot), que vão dentro do .exe.</summary>
internal static class WebAssets
{
    private static readonly Assembly Self = typeof(WebAssets).Assembly;
    private static readonly Dictionary<string, string> Names = Self.GetManifestResourceNames()
        .Where(n => n.Replace('\\', '/').StartsWith("wwwroot/", StringComparison.OrdinalIgnoreCase))
        .ToDictionary(n => n.Replace('\\', '/').Substring("wwwroot/".Length), n => n, StringComparer.OrdinalIgnoreCase);

    public static byte[]? Read(string path)
    {
        if (!Names.TryGetValue(path.Replace('\\', '/'), out var name)) return null;
        using var stream = Self.GetManifestResourceStream(name);
        if (stream is null) return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public static string Mime(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".js" => "text/javascript; charset=utf-8",
        ".json" => "application/json",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".ttf" => "font/ttf",
        ".woff2" => "font/woff2",
        _ => "application/octet-stream",
    };
}
