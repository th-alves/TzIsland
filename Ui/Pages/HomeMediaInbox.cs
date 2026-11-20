using System.Windows.Media.Effects;
using Windows.Media;

namespace TzIsland;

/// <summary>
/// Home Dashboard: Executive widget hub with live media card, Apple-style clock,
/// real-time meteorological summary, battery status and upcoming calendar events.
/// </summary>
public sealed class HomePage : PageBase
{
    readonly Image _cover = new() { Stretch = Stretch.UniformToFill };
    readonly TextBlock _title = K.T("Nada tocando", 15, K.Fg, true), _artist = K.T("", 12, K.Dim);
    readonly TextBlock _time = K.T("", 38, K.Fg), _date = K.T("", 12, K.Dim), _weather = K.T("", 12), _battery = K.T("", 12, K.Dim), _next = K.T("", 12, K.Dim);
    readonly StackPanel _forecast = new() { Orientation = Orientation.Horizontal };
    readonly ProgressBar _bar = new()
    {
        Height = 2.5,
        Minimum = 0,
        Maximum = 1,
        Foreground = new LinearGradientBrush(Color.FromRgb(0x0A, 0x84, 0xFF), Color.FromRgb(0x5E, 0x5C, 0xE6), 0),
        Background = K.CardStrong,
        BorderThickness = new Thickness(0),
        Margin = new Thickness(0, 8, 0, 0)
    };
    readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly Button _playRef;

    public HomePage()
    {
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = K.GL("1.18*") });
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = K.GL("1*") });

        // Left column: Apple Music style media card
        var card = K.CardBox(radius: 16, pad: new Thickness(14));
        card.Margin = new Thickness(0, 0, 10, 0);

        var g = K.Cols("72", "*");
        var coverBox = new Border
        {
            Width = 72,
            Height = 72,
            CornerRadius = new CornerRadius(14),
            Background = K.CardStrong,
            Child = _cover,
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Center,
            Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 2, Opacity = 0.4, Color = Color.FromRgb(0, 32, 64) }
        };
        g.Children.Add(coverBox);

        var stack = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(_title);
        stack.Children.Add(_artist);
        stack.Children.Add(_bar);

        var ctl = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
        ctl.Children.Add(K.IconBtn("\uE892", "Anterior", () => Svc.Media.Prev(), 14, 34, 28));
        _playRef = K.IconBtn("\uE768", "Reproduzir/Pausar", () => Svc.Media.PlayPause(), 18, 44, 32);
        ctl.Children.Add(_playRef.M(4, 0, 4, 0));
        ctl.Children.Add(K.IconBtn("\uE893", "Próxima", () => Svc.Media.Next(), 14, 34, 28));
        stack.Children.Add(ctl);

        g.Children.Add(stack.At(col: 1));
        card.Child = g;
        Root.Children.Add(card);

        // Right column: Executive corporate status (Clock, Weather, Calendar, Power)
        var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
        _time.FontWeight = FontWeights.Light;
        right.Children.Add(_time);
        right.Children.Add(_date);
        right.Children.Add(_weather.M(0, 8, 0, 0));
        right.Children.Add(_forecast.M(0, 3, 0, 0));
        right.Children.Add(_battery.M(0, 8, 0, 0));
        right.Children.Add(_next.M(0, 3, 0, 0));
        Root.Children.Add(right.At(col: 1));

        _t.Tick += (_, _) => Tick();
    }

    public override void Shown()
    {
        Svc.Media.Changed += OnMedia; Svc.Weather.Changed += OnWeather; Svc.Battery.Changed += OnBattery; Svc.Calendar.Changed += OnNext;
        OnMedia(); OnWeather(); OnBattery(); OnNext(); Tick();
        _t.Start();
    }

    public override void Hidden()
    {
        Svc.Media.Changed -= OnMedia; Svc.Weather.Changed -= OnWeather; Svc.Battery.Changed -= OnBattery; Svc.Calendar.Changed -= OnNext;
        _t.Stop();
    }

    void Tick()
    {
        _time.Text = DateTime.Now.ToString("HH:mm", CultureInfo.CurrentCulture);
        _date.Text = DateTime.Now.ToString("dddd, d 'de' MMMM", new CultureInfo("pt-BR"));
        var (p, d) = Svc.Media.Timeline();
        _bar.Value = d > 0 ? p / d : 0;
    }

    void OnMedia()
    {
        var m = Svc.Media;
        _title.Text = m.HasSession && m.Title.Length > 0 ? m.Title : "Nada tocando";
        _artist.Text = m.Artist;
        _cover.Source = m.Cover;
        _playRef.Content = K.Glyph(m.Playing ? "\uE769" : "\uE768", 18);
    }

    void OnWeather()
    {
        var w = Svc.Weather;
        _weather.Text = w.Ok ? $"{WeatherService.Emoji(w.Code)}  {Math.Round(w.Temp)}°C · {WeatherService.Label(w.Code)}" : "Clima indisponível";
        _forecast.Children.Clear();
        if (!w.Ok) return;
        foreach (var d in w.Days.Skip(1).Take(3))
        {
            var p = new Border
            {
                Background = K.Card,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 4, 0),
                Child = K.T($"{d.Date.ToString("ddd", new CultureInfo("pt-BR"))} {WeatherService.Emoji(d.Code)} {Math.Round(d.Max)}°/{Math.Round(d.Min)}°", 10, K.Dim)
            };
            _forecast.Children.Add(p);
        }
    }

    void OnBattery()
    {
        var b = Svc.Battery;
        _battery.Text = b.HasBattery ? $"Bateria {b.Percent}%{(b.Charging ? " · carregando" : "")}" : "Alimentação via tomada";
    }

    void OnNext()
    {
        var e = Svc.Calendar.Next();
        _next.Text = e == null ? "Sem próximos compromissos" : $"Próximo: {e.Title} · {(e.Start.Date == DateTime.Today ? "hoje" : e.Start.ToString("dd/MM"))} {(e.AllDay ? "" : e.Start.ToString("HH:mm"))}";
    }
}

/// <summary>
/// Media Player View: High-fidelity Apple Music glass deck with ambient blurred art,
/// full transport controls, dynamic scrubber, and system volume mixing.
/// </summary>
public sealed class MediaPage : PageBase
{
    readonly Image _cover = new() { Stretch = Stretch.UniformToFill };
    readonly Image _bg = new() { Stretch = Stretch.UniformToFill, Opacity = 0.35, Effect = new BlurEffect { Radius = 50, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Performance } };
    readonly TextBlock _title = K.T("Nada tocando", 19, K.Fg, true), _artist = K.T("", 13, K.Dim), _app = K.T("", 11, K.Faint), _cur = K.T("0:00", 10, K.Dim), _dur = K.T("0:00", 10, K.Dim);
    readonly PillSlider _prog = new() { ReadOnly = true, Fill = new LinearGradientBrush(Color.FromRgb(0x0A, 0x84, 0xFF), Color.FromRgb(0x5E, 0x5C, 0xE6), 0) };
    readonly PillSlider _vol = new();
    readonly Button _play, _shuffle, _repeat, _mute;
    readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromMilliseconds(500) };

    public MediaPage()
    {
        var bgHost = new Border { ClipToBounds = true, CornerRadius = new CornerRadius(18), Child = _bg, IsHitTestVisible = false };
        Root.Children.Add(bgHost);

        var g = K.Cols("155", "*");
        g.Margin = new Thickness(10, 6, 10, 6);
        var cover = new Border
        {
            Width = 155,
            Height = 155,
            CornerRadius = new CornerRadius(16),
            Background = K.CardStrong,
            Child = _cover,
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Center,
            Effect = new DropShadowEffect { BlurRadius = 32, ShadowDepth = 5, Opacity = 0.55, Color = Color.FromRgb(0, 20, 50) }
        };
        g.Children.Add(cover);

        var s = new StackPanel { Margin = new Thickness(18, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        s.Children.Add(_title); s.Children.Add(_artist); s.Children.Add(_app);

        var times = K.Cols("auto", "*", "auto"); times.Margin = new Thickness(0, 14, 0, 0);
        times.Children.Add(_cur); times.Children.Add(_prog.At(col: 1).M(8, 0, 8, 0)); times.Children.Add(_dur.At(col: 2));
        s.Children.Add(times);

        _shuffle = K.IconBtn("\uE8B1", "Aleatório", () => Svc.Media.ToggleShuffle());
        _repeat = K.IconBtn("\uE8EE", "Repetir", () => Svc.Media.CycleRepeat());
        _play = K.IconBtn("\uE768", "Reproduzir/Pausar", () => Svc.Media.PlayPause(), 22, 54, 42);
        var ctl = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
        ctl.Children.Add(_shuffle);
        ctl.Children.Add(K.IconBtn("\uE892", "Anterior", () => Svc.Media.Prev(), 18, 42, 36).M(4, 0, 0, 0));
        ctl.Children.Add(_play.M(6, 0, 6, 0));
        ctl.Children.Add(K.IconBtn("\uE893", "Próxima", () => Svc.Media.Next(), 18, 42, 36).M(0, 0, 4, 0));
        ctl.Children.Add(_repeat);
        s.Children.Add(ctl);

        var vrow = K.Cols("auto", "*"); vrow.Margin = new Thickness(0, 6, 0, 0);
        _mute = K.IconBtn("\uE767", "Silenciar", () => { Svc.Audio.Muted = !Svc.Audio.Muted; SyncVol(); }, 14, 30, 26);
        vrow.Children.Add(_mute);
        vrow.Children.Add(_vol.At(col: 1).M(6, 0, 4, 0));
        s.Children.Add(vrow);
        g.Children.Add(s.At(col: 1));
        Root.Children.Add(g);

        _vol.Changed += v => { Svc.Audio.Volume = (float)v; if (Svc.Audio.Muted && v > 0) Svc.Audio.Muted = false; };
        _t.Tick += (_, _) => Tick();
    }

    public override void Shown()
    {
        Svc.Media.Changed += OnMedia; Svc.Audio.VolumeChanged += SyncVol;
        OnMedia(); SyncVol(); Tick(); _t.Start();
    }

    public override void Hidden() { Svc.Media.Changed -= OnMedia; Svc.Audio.VolumeChanged -= SyncVol; _t.Stop(); }

    void SyncVol()
    {
        _vol.Value = Svc.Audio.Volume;
        _mute.Content = K.Glyph(Svc.Audio.Muted || Svc.Audio.Volume <= 0.001f ? "\uE74F" : "\uE767", 14);
    }

    void Tick()
    {
        var (p, d) = Svc.Media.Timeline();
        _prog.Value = d > 0 ? p / d : 0;
        _cur.Text = K.Fmt(p); _dur.Text = d > 0 ? K.Fmt(d) : "--:--";
    }

    void OnMedia()
    {
        var m = Svc.Media;
        _title.Text = m.HasSession && m.Title.Length > 0 ? m.Title : "Nada tocando";
        _artist.Text = m.Artist; _app.Text = m.HasSession ? "via " + m.App : "Abra um reprodutor compatível";
        _cover.Source = m.Cover;
        _bg.Source = Settings.Current.MediaBlurBackground ? m.Cover : null;
        _play.Content = K.Glyph(m.Playing ? "\uE769" : "\uE768", 22);
        _shuffle.Content = K.Glyph("\uE8B1", 15, m.Shuffle ? K.Blue : K.Fg); _shuffle.IsEnabled = m.CanShuffle;
        _repeat.Content = K.Glyph(m.Repeat == MediaPlaybackAutoRepeatMode.Track ? "\uE8ED" : "\uE8EE", 15, m.Repeat == MediaPlaybackAutoRepeatMode.None ? K.Fg : K.Blue);
        _repeat.IsEnabled = m.CanRepeat;
    }
}

/// <summary>
/// Inbox / Notification Center: Apple Notification Hub with category stacks,
/// priority highlights, and instant triage capabilities.
/// </summary>
public sealed class InboxPage : PageBase
{
    readonly StackPanel _list = new();
    readonly TextBlock _count = K.T("", 12, K.Dim);

    public InboxPage()
    {
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new RowDefinition { Height = K.GL("*") });
        var head = K.Cols("*", "auto", "auto");
        head.Children.Add(K.T("Inbox & Notificações", 16, K.Fg, true).Vc());
        head.Children.Add(_count.At(col: 1).Vc().M(0, 0, 10, 0));
        head.Children.Add(K.TextBtn("Limpar tudo", () => Hub.Inbox.Clear()).At(col: 2));
        head.Margin = new Thickness(2, 0, 2, 8);
        Root.Children.Add(head);
        Root.Children.Add(K.Scroll(_list).At(row: 1));
    }

    public override void Shown() { Hub.Inbox.CollectionChanged += OnChange; Rebuild(true); }
    public override void Hidden() { Hub.Inbox.CollectionChanged -= OnChange; }
    void OnChange(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Rebuild(false);

    void Rebuild(bool animate)
    {
        _list.Children.Clear();
        _count.Text = Hub.Inbox.Count == 0 ? "" : $"{Hub.Inbox.Count} notificações";
        if (Hub.Inbox.Count == 0)
        {
            var w = Svc.WinNotifs;
            var emptyBox = new StackPanel { Margin = new Thickness(0, 20, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
            emptyBox.Children.Add(K.Glyph("\uE7F4", 28, K.Dim));
            emptyBox.Children.Add(K.T("Tudo limpo por aqui.", 14, K.Fg, true).M(0, 8, 0, 2));
            emptyBox.Children.Add(K.T("Notificações do Windows: " + w.Status, 11, K.Faint).M(0, 0, 0, 0));
            _list.Children.Add(emptyBox);
            return;
        }
        int i = 0;
        foreach (var grp in Hub.Inbox.GroupBy(x => x.Source).OrderByDescending(g => g.Any(x => x.Important)).ThenByDescending(g => g.Max(x => x.Time)))
        {
            var head = K.T($"{grp.Key.ToUpperInvariant()}  ·  {grp.Count()}", 10, K.Faint, true);
            head.Margin = new Thickness(4, 8, 0, 4);
            _list.Children.Add(head);
            foreach (var it in grp.OrderByDescending(x => x.Important).ThenByDescending(x => x.Time))
            {
                var row = Build(it);
                if (animate) K.FadeIn(row, Math.Min(i * 35, 380));
                _list.Children.Add(row);
                i++;
            }
        }
    }

    UIElement Build(InboxItem it)
    {
        var g = K.Cols("8", "*", "auto", "auto");
        var dot = new Border
        {
            Width = 6,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = it.Important ? K.Orange : K.Clear,
            VerticalAlignment = VerticalAlignment.Center
        };
        g.Children.Add(dot);

        var s = new StackPanel { Margin = new Thickness(10, 0, 8, 0) };
        s.Children.Add(K.T(it.Title, 13, K.Fg, true));
        if (it.Text.Length > 0) s.Children.Add(K.T(it.Text, 12, K.Dim));
        g.Children.Add(s.At(col: 1));

        g.Children.Add(K.T(it.Time.ToString("HH:mm"), 10, K.Faint).At(col: 2).Vc().M(0, 0, 8, 0));
        var btns = new StackPanel { Orientation = Orientation.Horizontal };
        btns.Children.Add(K.IconBtn(it.Important ? "\uE735" : "\uE734", "Marcar como importante", () => { it.Important = !it.Important; Rebuild(false); }, 12, 28, 26, it.Important ? K.Orange : K.Dim));
        btns.Children.Add(K.IconBtn("\uE711", "Dispensar", () => Hub.Inbox.Remove(it), 11, 28, 26, K.Dim));
        g.Children.Add(btns.At(col: 3));

        return new Border
        {
            Background = K.Card,
            CornerRadius = new CornerRadius(13),
            BorderBrush = K.Hex("#12FFFFFF"),
            BorderThickness = new Thickness(0.5),
            Padding = new Thickness(12, 8, 8, 8),
            Margin = new Thickness(0, 0, 0, 5),
            Child = g
        };
    }
}
