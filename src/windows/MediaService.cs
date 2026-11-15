using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Sleeve;

/// <summary>Retrato do que está tocando num dado instante.</summary>
public sealed record TrackInfo(
    string Title,
    string Artist,
    string Album,
    byte[]? Cover,
    bool IsPlaying,
    TimeSpan Position,
    TimeSpan Duration,
    DateTimeOffset PositionAt,
    string SourceApp,
    bool CanPrevious,
    bool CanNext,
    bool CanSeek);

/// <summary>
/// Lê e controla a música pelo controle de mídia do Windows (SMTC),
/// o mesmo que aparece no painel de volume. Dá preferência ao Spotify;
/// se ele não estiver aberto, usa o app de mídia ativo.
/// </summary>
public sealed class MediaService
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private string? _sessionId;

    private string? _coverKey;
    private byte[]? _coverBytes;

    /// <summary>Disparado (em outra thread) quando qualquer coisa muda.</summary>
    public event Action? Changed;

    public async Task InitAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.SessionsChanged += (_, _) => PickSession();
        _manager.CurrentSessionChanged += (_, _) => PickSession();
        PickSession();
    }

    private void PickSession()
    {
        if (_manager is null) return;

        GlobalSystemMediaTransportControlsSession? chosen = null;
        try
        {
            // Ignora a sessão do próprio Sleeve (o som dos seus arquivos), senão ele se mostraria duas vezes
            var sessions = _manager.GetSessions().Where(s => !IsOwnSession(s)).ToList();
            var current = _manager.GetCurrentSession();
            chosen = sessions.FirstOrDefault(s => SafeId(s).Contains("spotify", StringComparison.OrdinalIgnoreCase))
                     ?? (current is not null && !IsOwnSession(current) ? current : null);
        }
        catch
        {
            // sessão sumiu no meio da leitura; tenta de novo no próximo evento
        }

        var id = chosen is null ? null : SafeId(chosen);
        if (id != _sessionId)
        {
            Detach();
            _session = chosen;
            _sessionId = id;
            Attach();
        }

        Changed?.Invoke();
    }

    private void Attach()
    {
        if (_session is null) return;
        _session.MediaPropertiesChanged += OnMediaChanged;
        _session.PlaybackInfoChanged += OnPlaybackChanged;
        _session.TimelinePropertiesChanged += OnTimelineChanged;
    }

    private void Detach()
    {
        if (_session is null) return;
        try
        {
            _session.MediaPropertiesChanged -= OnMediaChanged;
            _session.PlaybackInfoChanged -= OnPlaybackChanged;
            _session.TimelinePropertiesChanged -= OnTimelineChanged;
        }
        catch
        {
            // sessão já encerrada
        }
    }

    private void OnMediaChanged(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs e) => Changed?.Invoke();
    private void OnPlaybackChanged(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs e) => Changed?.Invoke();
    private void OnTimelineChanged(GlobalSystemMediaTransportControlsSession s, TimelinePropertiesChangedEventArgs e) => Changed?.Invoke();

    public async Task<TrackInfo?> ReadAsync()
    {
        var s = _session;
        if (s is null) return null;

        try
        {
            var props = await s.TryGetMediaPropertiesAsync();
            var playback = s.GetPlaybackInfo();
            var timeline = s.GetTimelineProperties();

            var title = props.Title ?? "";
            var artist = string.IsNullOrWhiteSpace(props.Artist) ? props.AlbumArtist ?? "" : props.Artist;
            var album = props.AlbumTitle ?? "";

            // A capa só é relida quando a faixa muda
            var key = $"{title}|{artist}|{album}";
            byte[]? cover = key == _coverKey ? _coverBytes : null;
            if (cover is null && props.Thumbnail is not null)
            {
                cover = await ReadThumbnailAsync(props.Thumbnail);
                if (cover is not null)
                {
                    _coverKey = key;
                    _coverBytes = cover;
                }
            }

            var now = DateTimeOffset.Now;
            var playing = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var duration = timeline.EndTime - timeline.StartTime;
            var position = timeline.Position - timeline.StartTime;

            // A posição informada é de quando o app atualizou; compensa o tempo desde então
            var age = now - timeline.LastUpdatedTime;
            if (playing && age > TimeSpan.Zero && age < TimeSpan.FromHours(1))
                position += age;

            var controls = playback.Controls;
            return new TrackInfo(
                title, artist, album, cover, playing, position, duration, now,
                FriendlyName(SafeId(s)),
                controls.IsPreviousEnabled,
                controls.IsNextEnabled,
                controls.IsPlaybackPositionEnabled);
        }
        catch
        {
            return null;
        }
    }

    public Task TogglePlayPauseAsync() => Run(s => s.TryTogglePlayPauseAsync());
    public Task PauseAsync() => Run(s => s.TryPauseAsync());
    public Task NextAsync() => Run(s => s.TrySkipNextAsync());
    public Task PreviousAsync() => Run(s => s.TrySkipPreviousAsync());
    public Task SeekAsync(TimeSpan position) => Run(s => s.TryChangePlaybackPositionAsync(position.Ticks));

    private async Task Run(Func<GlobalSystemMediaTransportControlsSession, IAsyncOperation<bool>> action)
    {
        var s = _session;
        if (s is null) return;
        try { await action(s); }
        catch { /* o app recusou o comando */ }
    }

    private static async Task<byte[]?> ReadThumbnailAsync(IRandomAccessStreamReference reference)
    {
        try
        {
            // Lê em blocos até acabar: alguns apps informam o tamanho errado (ou zero)
            using var stream = await reference.OpenReadAsync();
            using var reader = new DataReader(stream.GetInputStreamAt(0));
            using var buffer = new System.IO.MemoryStream();
            while (true)
            {
                uint read = await reader.LoadAsync(64 * 1024);
                if (read == 0) break;
                var chunk = new byte[read];
                reader.ReadBytes(chunk);
                buffer.Write(chunk, 0, chunk.Length);
            }
            return buffer.Length > 0 ? buffer.ToArray() : null;
        }
        catch
        {
            return null;
        }
    }

    private static readonly string OwnProcess = System.Diagnostics.Process.GetCurrentProcess().ProcessName;

    private static bool IsOwnSession(GlobalSystemMediaTransportControlsSession s)
    {
        var id = SafeId(s);
        return id.Contains(OwnProcess, StringComparison.OrdinalIgnoreCase)
            || id.Contains("msedgewebview2", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Sleeve", StringComparison.OrdinalIgnoreCase);
    }

    private static string SafeId(GlobalSystemMediaTransportControlsSession s)
    {
        try { return s.SourceAppUserModelId ?? ""; }
        catch { return ""; }
    }

    private static string FriendlyName(string appId)
    {
        if (appId.Contains("spotify", StringComparison.OrdinalIgnoreCase)) return "Spotify";
        if (string.IsNullOrWhiteSpace(appId)) return "Mídia";

        var name = appId.Contains('!') ? appId[(appId.LastIndexOf('!') + 1)..] : appId;
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        return name.Length > 0 ? char.ToUpper(name[0]) + name[1..] : "Mídia";
    }
}
