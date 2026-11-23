using Windows.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace TzIsland;

/// <summary>Controles globais de mídia do Windows (SMTC): Spotify, Apple Music, navegadores, players...</summary>
public sealed class MediaService
{
    GlobalSystemMediaTransportControlsSessionManager? _mgr;
    GlobalSystemMediaTransportControlsSession? _s;
    readonly List<GlobalSystemMediaTransportControlsSession> _attached = new();

    public string Title = "", Artist = "", Album = "", App = "";
    public BitmapImage? Cover;
    /// <summary>Cor dominante da capa, já ajustada para ficar viva sobre fundo preto (branco quando não há capa ou a opção está desligada).</summary>
    public Color Accent = Colors.White;
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
            _mgr.SessionsChanged += (_, _) => Ui.InvokeAsync(Attach);
            Attach();
        }
        catch (Exception e) { Log.Write(e); }
    }

    /// <summary>
    /// O Windows tem UMA "sessão atual", mas navegadores (YouTube) e players convivem: se o Spotify estiver aberto/pausado,
    /// ele pode continuar sendo a sessão atual enquanto o vídeo toca no Chrome. Por isso escutamos TODAS as sessões
    /// e damos preferência à que está de fato tocando.
    /// </summary>
    void Attach()
    {
        foreach (var o in _attached) { o.MediaPropertiesChanged -= OnProps; o.PlaybackInfoChanged -= OnPlayback; }
        _attached.Clear();
        try
        {
            if (_mgr != null)
                foreach (var n in _mgr.GetSessions()) { n.MediaPropertiesChanged += OnProps; n.PlaybackInfoChanged += OnPlayback; _attached.Add(n); }
        }
        catch (Exception e) { Log.Write(e); }
        _s = Pick();
        _ = RefreshAsync();
    }

    GlobalSystemMediaTransportControlsSession? Pick()
    {
        GlobalSystemMediaTransportControlsSession? cur = null;
        try { cur = _mgr?.GetCurrentSession(); } catch { }
        try
        {
            var playing = _attached.Where(x => x.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing).ToList();
            if (playing.Count > 0)
            {
                var same = cur == null ? null : playing.FirstOrDefault(x => x.SourceAppUserModelId == cur.SourceAppUserModelId);
                return same ?? playing[0];
            }
        }
        catch (Exception e) { Log.Write(e); }
        return cur ?? _attached.FirstOrDefault();
    }

    void OnProps(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs a) => _ = Ui.InvokeAsync(Reselect);
    void OnPlayback(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs a) => _ = Ui.InvokeAsync(Reselect);

    Task Reselect() { _s = Pick(); return RefreshAsync(); }

    async Task RefreshAsync()
    {
        var s = _s;
        HasSession = s != null;
        if (s == null)
        {
            Title = Artist = Album = App = ""; Cover = null; Playing = false; Accent = Colors.White;
            Changed?.Invoke();
            return;
        }
        try
        {
            var p = await s.TryGetMediaPropertiesAsync();
            Title = p.Title ?? ""; Artist = p.Artist ?? ""; Album = p.AlbumTitle ?? "";
            App = Friendly(s.SourceAppUserModelId);
            Cover = await LoadAsync(p.Thumbnail);
            Accent = Settings.Current.DynamicCoverColor && Cover != null ? CoverColor.Dominant(Cover) : Colors.White;
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

/// <summary>Extrai a cor dominante "viva" da capa do álbum.</summary>
public static class CoverColor
{
    public static Color Dominant(BitmapSource src)
    {
        try
        {
            double sc = 48.0 / Math.Max(src.PixelWidth, src.PixelHeight);
            BitmapSource small = sc < 1 ? new TransformedBitmap(src, new ScaleTransform(sc, sc)) : src;
            var bgra = new FormatConvertedBitmap(small, PixelFormats.Bgra32, null, 0);
            int w = bgra.PixelWidth, h = bgra.PixelHeight, stride = w * 4;
            var px = new byte[stride * h];
            bgra.CopyPixels(px, stride, 0);

            const int Bins = 12;
            var wsum = new double[Bins]; var rs = new double[Bins]; var gs = new double[Bins]; var bs = new double[Bins];
            double total = 0, chroma = 0, lumSum = 0; int cnt = 0;
            for (int i = 0; i + 3 < px.Length; i += 4)
            {
                if (px[i + 3] < 128) continue;
                double b = px[i] / 255.0, g = px[i + 1] / 255.0, r = px[i + 2] / 255.0;
                double mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b)), d = mx - mn;
                cnt++; lumSum += mx; total += 1;
                double sat = mx <= 0 ? 0 : d / mx;
                if (sat < 0.22 || mx < 0.2) continue;           // ignora cinzas e quase-pretos
                double hue = d == 0 ? 0 : mx == r ? ((g - b) / d + 6) % 6 : mx == g ? (b - r) / d + 2 : (r - g) / d + 4;
                int bin = Math.Min(Bins - 1, (int)(hue / 6 * Bins));
                double wgt = sat * (0.35 + mx);                 // cores saturadas e claras pesam mais
                wsum[bin] += wgt; rs[bin] += r * wgt; gs[bin] += g * wgt; bs[bin] += b * wgt; chroma += wgt;
            }
            if (cnt == 0) return Colors.White;
            int best = 0;
            for (int k = 1; k < Bins; k++) if (wsum[k] > wsum[best]) best = k;
            if (chroma < cnt * 0.04 || wsum[best] <= 0)         // capa praticamente cinza/preta: acento neutro
            {
                byte v = (byte)Math.Clamp(170 + lumSum / cnt * 85, 170, 245);
                return Color.FromRgb(v, v, v);
            }
            double rr = rs[best] / wsum[best], gg = gs[best] / wsum[best], bb = bs[best] / wsum[best];
            return Vivid(rr, gg, bb);
        }
        catch { return Colors.White; }
    }

    /// <summary>Garante saturação e brilho bons sobre o preto, mantendo o matiz.</summary>
    static Color Vivid(double r, double g, double b)
    {
        double mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b)), d = mx - mn;
        double h = d == 0 ? 0 : mx == r ? ((g - b) / d + 6) % 6 : mx == g ? (b - r) / d + 2 : (r - g) / d + 4;
        double s = Math.Clamp(mx <= 0 ? 0 : d / mx, 0.5, 0.92), v = Math.Clamp(mx, 0.82, 1.0);
        double c = v * s, x = c * (1 - Math.Abs(h % 2 - 1)), m = v - c, r1, g1, b1;
        switch ((int)h % 6)
        {
            case 0: (r1, g1, b1) = (c, x, 0); break;
            case 1: (r1, g1, b1) = (x, c, 0); break;
            case 2: (r1, g1, b1) = (0, c, x); break;
            case 3: (r1, g1, b1) = (0, x, c); break;
            case 4: (r1, g1, b1) = (x, 0, c); break;
            default: (r1, g1, b1) = (c, 0, x); break;
        }
        return Color.FromRgb((byte)((r1 + m) * 255), (byte)((g1 + m) * 255), (byte)((b1 + m) * 255));
    }
}
