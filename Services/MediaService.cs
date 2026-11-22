using Windows.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace TzIsland;

/// <summary>Controles globais de mídia do Windows (SMTC): Spotify, Apple Music, navegadores, players...</summary>
public sealed class MediaService
{
    GlobalSystemMediaTransportControlsSessionManager? _mgr;
    GlobalSystemMediaTransportControlsSession? _s;

    public string Title = "", Artist = "", Album = "", App = "";
    public BitmapImage? Cover;
    public bool HasSession, Playing, Shuffle, CanShuffle, CanRepeat, CanPrev = true, CanNext = true;
    public MediaPlaybackAutoRepeatMode Repeat = MediaPlaybackAutoRepeatMode.None;
    public event Action? Changed;

    /// <summary>Heurística: miniatura em formato paisagem (vídeo) ou player de vídeo conhecido.</summary>
    public bool IsVideo
    {
        get
        {
            if (Cover != null && Cover.PixelHeight > 0 && Cover.PixelWidth / (double)Cover.PixelHeight > 1.3) return true;
            var a = App.ToLowerInvariant();
            return a.Contains("vlc") || a.Contains("mpc") || a.Contains("potplayer") || a.Contains("zunevideo") || a.Contains("netflix") || a.Contains("primevideo") || a.Contains("plex");
        }
    }

    static Dispatcher Ui => Application.Current.Dispatcher;

    public async Task StartAsync()
    {
        try
        {
            _mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _mgr.CurrentSessionChanged += (_, _) => Ui.InvokeAsync(Attach);
            Attach();
        }
        catch (Exception e) { Log.Write(e); }
    }

    void Attach()
    {
        if (_s != null)
        {
            _s.MediaPropertiesChanged -= OnProps;
            _s.PlaybackInfoChanged -= OnPlayback;
        }
        _s = _mgr?.GetCurrentSession();
        if (_s != null)
        {
            _s.MediaPropertiesChanged += OnProps;
            _s.PlaybackInfoChanged += OnPlayback;
        }
        _ = RefreshAsync();
    }

    void OnProps(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs a) => _ = Ui.InvokeAsync(RefreshAsync);
    void OnPlayback(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs a) => _ = Ui.InvokeAsync(RefreshAsync);

    async Task RefreshAsync()
    {
        var s = _s;
        HasSession = s != null;
        if (s == null)
        {
            Title = Artist = Album = App = ""; Cover = null; Playing = false;
            Changed?.Invoke();
            return;
        }
        try
        {
            var p = await s.TryGetMediaPropertiesAsync();
            Title = p.Title ?? ""; Artist = p.Artist ?? ""; Album = p.AlbumTitle ?? "";
            App = Friendly(s.SourceAppUserModelId);
            Cover = await LoadAsync(p.Thumbnail);
            var info = s.GetPlaybackInfo();
            Playing = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            Shuffle = info.IsShuffleActive ?? false;
            Repeat = info.AutoRepeatMode ?? MediaPlaybackAutoRepeatMode.None;
            var c = info.Controls;
            CanShuffle = c.IsShuffleEnabled; CanRepeat = c.IsRepeatEnabled;
            CanPrev = c.IsPreviousEnabled; CanNext = c.IsNextEnabled;
        }
        catch (Exception e) { Log.Write(e); }
        Changed?.Invoke();
    }

    static string Friendly(string? id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        var n = id.Split('!')[0].Split('_')[0];
        if (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) n = n[..^4];
        return n.Contains('.') ? n[(n.LastIndexOf('.') + 1)..] : n;
    }

    static async Task<BitmapImage?> LoadAsync(IRandomAccessStreamReference? r)
    {
        if (r == null) return null;
        try
        {
            using var rs = await r.OpenReadAsync();
            using var st = rs.AsStreamForRead();
            var ms = new MemoryStream();
            await st.CopyToAsync(ms);
            ms.Position = 0;
            var b = new BitmapImage();
            b.BeginInit();
            b.CacheOption = BitmapCacheOption.OnLoad;
            b.DecodePixelWidth = 400;
            b.StreamSource = ms;
            b.EndInit();
            b.Freeze();
            return b;
        }
        catch { return null; }
    }

    /// <summary>Posição e duração em segundos (duração 0 = desconhecida).</summary>
    public (double pos, double dur) Timeline()
    {
        try
        {
            if (_s == null) return (0, 0);
            var t = _s.GetTimelineProperties();
            double dur = (t.EndTime - t.StartTime).TotalSeconds;
            if (dur <= 0) return (0, 0);
            var pos = t.Position;
            if (Playing) pos += DateTimeOffset.Now - t.LastUpdatedTime;
            return (Math.Clamp((pos - t.StartTime).TotalSeconds, 0, dur), dur);
        }
        catch { return (0, 0); }
    }

    public async void PlayPause() { if (_s != null) await _s.TryTogglePlayPauseAsync(); }
    public async void Next() { if (_s != null) await _s.TrySkipNextAsync(); }
    public async void Prev() { if (_s != null) await _s.TrySkipPreviousAsync(); }
    public async void ToggleShuffle() { if (_s != null) await _s.TryChangeShuffleActiveAsync(!Shuffle); }
    public async void CycleRepeat()
    {
        if (_s == null) return;
        var next = Repeat switch
        {
            MediaPlaybackAutoRepeatMode.None => MediaPlaybackAutoRepeatMode.List,
            MediaPlaybackAutoRepeatMode.List => MediaPlaybackAutoRepeatMode.Track,
            _ => MediaPlaybackAutoRepeatMode.None
        };
        await _s.TryChangeAutoRepeatModeAsync(next);
    }
}
