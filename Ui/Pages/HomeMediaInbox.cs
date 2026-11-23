using Windows.Media;

namespace TzIsland;

/// <summary>
/// Início: grade de widgets no estilo "Minimal" (preto, cartões finos, números grandes).
/// Linha 1: Sistema + Relógio · Linha 2: Tocando agora + Bateria · Linha 3: Som, Clima e Agenda (role para ver).
/// </summary>
public sealed class HomePage : PageBase
{
    static readonly CultureInfo Pt = new("pt-BR");
    readonly SysStats _sys = new();

    // Sistema
    readonly RingGauge _cpu = new() { Width = 52, Height = 52 }, _ram = new() { Width = 52, Height = 52 }, _disk = new() { Width = 52, Height = 52 };
    // Relógio
    readonly TextBlock _time = K.Num("", 34), _weekday = K.T("", 12.5, K.Label), _date = K.T("", 11, K.Muted);
    // Tocando agora
    readonly TextBlock _title = K.T("Nada tocando", 13.5, K.Fg, true), _artist = K.Cap("", 9.5, K.Label), _cur = K.T("0:00", 10, K.Muted);
    readonly PillSlider _prog = new() { ReadOnly = true, Fill = K.White, ShowThumb = true, Height = 14 };
    readonly Button _play;
    // Bateria
    readonly TextBlock _batNum = K.Num("", 32), _batSub = K.T("", 11, K.Label);
    // Som
    readonly PillSlider _vol = new() { Fill = K.White, Height = 14 }, _bri = new() { Fill = K.White, Height = 14 };
    readonly Button _mute;
    readonly Grid _briRow = K.Cols("22", "*");
    // Clima / Agenda
    readonly TextBlock _temp = K.Num("", 28), _wLabel = K.T("", 11.5, K.Label), _wDays = K.T("", 10.5, K.Muted);
    readonly TextBlock _next = K.T("", 12.5, K.Fg, true), _nextSub = K.T("", 11, K.Muted);
    readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromSeconds(1) };
    int _tick;

    static TextBlock Wrap(TextBlock t) { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.CharacterEllipsis; return t; }

    static UIElement RingWithCaption(RingGauge r, string caption)
    {
        var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        sp.Children.Add(r);
        var c = K.Cap(caption, 8.5, K.Muted); c.HorizontalAlignment = HorizontalAlignment.Center; c.Margin = new Thickness(0, 5, 0, 0);
        sp.Children.Add(c);
        return sp;
    }

    // Cartões do Início, na ordem escolhida pelo usuário (Configurações > Módulos > Cartões da página Início).
    readonly Border _clockCard, _npCard;
    Grid? _homeGrid;

    public HomePage()
    {
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new RowDefinition { Height = K.GL("*") });

        // ---- Cabeçalho: ⌂ Início  qua., 30 set
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 0, 9) };
        head.Children.Add(Ico.Make("home", 15, K.Fg, 1.8).M(0, 0, 8, 0));
        head.Children.Add(K.T("Início", 13, K.Fg, true).Vc());
        var hd = K.T(DateTime.Now.ToString("ddd, d MMM", Pt), 11.5, K.Muted); hd.Margin = new Thickness(9, 1, 0, 0);
        head.Children.Add(hd.Vc());
        Root.Children.Add(head);

        var grid = new Grid();
        for (int i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = K.GL("1*") });
        _homeGrid = grid;

        // ---- SISTEMA
        var sysRow = new UniformGrid3();
        sysRow.Add(RingWithCaption(_cpu, "CPU")); sysRow.Add(RingWithCaption(_ram, "RAM")); sysRow.Add(RingWithCaption(_disk, "Disco"));
        var sysCard = Card("monitor", "Sistema", sysRow.Host);
        sysRow.Host.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(sysCard);

        // ---- RELÓGIO (2 colunas)
        var clock = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        clock.Children.Add(_time); clock.Children.Add(_weekday.M(0, 1, 0, 0)); clock.Children.Add(_date.M(0, 1, 0, 0));
        _clockCard = Card("clock", "Relógio", clock);

        // ---- TOCANDO AGORA (2 colunas)
        _play = FK.RoundButton("\uE768", "Reproduzir/Pausar", () => Svc.Media.PlayPause(), 28, K.White, K.Ink, 10);
        var np = K.Rows("auto", "auto", "auto", "*");
        np.Children.Add(_title);
        np.Children.Add(_artist.At(row: 1).M(0, 2, 0, 0));
        _prog.Margin = new Thickness(0, 6, 0, 0);
        np.Children.Add(_prog.At(row: 2));
        var foot = K.Cols("auto", "*");
        var ctl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        ctl.Children.Add(K.IconBtn("\uE892", "Anterior", () => Svc.Media.Prev(), 12, 28, 26, K.Fg));
        ctl.Children.Add(_play.M(2, 0, 2, 0));
        ctl.Children.Add(K.IconBtn("\uE893", "Próxima", () => Svc.Media.Next(), 12, 28, 26, K.Fg));
        foot.Children.Add(ctl);
        _cur.HorizontalAlignment = HorizontalAlignment.Right;
        foot.Children.Add(_cur.At(col: 1).Vc());
        foot.VerticalAlignment = VerticalAlignment.Bottom;
        np.Children.Add(foot.At(row: 3));
        _npCard = Card("music", "Tocando agora", np);

        // ---- BATERIA
        var bat = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        bat.Children.Add(_batNum); bat.Children.Add(_batSub.M(0, 1, 0, 0));
        var batCard = Card("battery", "Bateria", bat);

        // ---- SOM E LUZ
        _mute = K.IconBtn("\uE767", "Silenciar", () => { Svc.Audio.Muted = !Svc.Audio.Muted; SyncVol(); }, 12, 22, 22, K.Label);
        var volRow = K.Cols("22", "*"); volRow.Children.Add(_mute); volRow.Children.Add(_vol.At(col: 1).M(8, 0, 0, 0));
        _briRow.Children.Add(K.Glyph("\uE706", 12, K.Label)); _briRow.Children.Add(_bri.At(col: 1).M(8, 0, 0, 0));
        _briRow.Margin = new Thickness(0, 8, 0, 0); _briRow.Visibility = Visibility.Collapsed;
        var snd = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        snd.Children.Add(volRow); snd.Children.Add(_briRow);
        var sndCard = Card("volume", "Som e luz", snd);
        _vol.Changed += v => { Svc.Audio.Volume = (float)v; if (Svc.Audio.Muted && v > 0) Svc.Audio.Muted = false; };
        _bri.Changed += v => { var d = Svc.Brightness.Devices.FirstOrDefault(); if (d != null) Svc.Brightness.SetBrightness(d, (uint)(d.Min + v * (d.Max - d.Min))); };

        // ---- CLIMA
        var wx = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        wx.Children.Add(_temp); wx.Children.Add(_wLabel.M(0, 1, 0, 0)); wx.Children.Add(Wrap(_wDays).M(0, 3, 0, 0));
        var wxCard = Card("cloud", "Clima", wx);

        // ---- AGENDA
        var ag = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        ag.Children.Add(Wrap(_next)); ag.Children.Add(Wrap(_nextSub).M(0, 3, 0, 0));
        var agCard = Card("calendar", "Agenda", ag);

        _cards = new Dictionary<string, Border>
        {
            ["system"] = sysCard, ["clock"] = _clockCard, ["nowplaying"] = _npCard, ["battery"] = batCard,
            ["sound"] = sndCard, ["weather"] = wxCard, ["agenda"] = agCard
        };
        LayoutHome();

        var scroll = K.Scroll(grid);
        Root.Children.Add(scroll.At(row: 1));
        _t.Tick += (_, _) => Tick();
        Settings.Changed += OnSettingsChanged;
    }

    /// <summary>Linha com 3 itens igualmente espaçados.</summary>
    sealed class UniformGrid3
    {
        public readonly Grid Host = new();
        int _n;
        public void Add(UIElement e) { Host.ColumnDefinitions.Add(new ColumnDefinition()); Grid.SetColumn(e, _n++); Host.Children.Add(e); }
    }

    static Border Card(string glyph, string title, UIElement body)
    {
        var g = K.Rows("auto", "*");
        g.Children.Add(FK.Header(Ico.Make(glyph, 12, K.Label, 1.9), title));
        g.Children.Add(body.At(row: 1));
        ((FrameworkElement)body).Margin = new Thickness(0, 5, 0, 0);
        var c = FK.Card(g, 18);
        c.Padding = new Thickness(14, 10, 14, 9);
        return c;
    }

    Dictionary<string, Border> _cards = new();

    void OnSettingsChanged()
    {
        if (_homeGrid != null) Root.Dispatcher.InvokeAsync(LayoutHome, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Monta a grade do Início na ordem escolhida pelo usuário (Configurações &gt; Módulos) e sem os cartões desligados.
    /// O primeiro cartão da lista fica na célula da esquerda, em cima; "Tocando agora" (que precisa de largura)
    /// sempre ocupa duas colunas, e os demais preenchem a grade na sequência.
    /// </summary>
    void LayoutHome()
    {
        var grid = _homeGrid;
        if (grid == null) return;

        var on = HomeWidgets.All.Select(w => w.Id).Where(HomeWidgets.IsOn).ToList();
        var order = Settings.Current.HomeOrder ?? new List<string>();
        var seq = order.Where(on.Contains).Distinct().ToList();
        seq.AddRange(on.Where(id => !seq.Contains(id)));

        grid.Children.Clear();
        grid.RowDefinitions.Clear();
        for (int i = 0; i < 3; i++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(i == 0 ? 148 : 124) });

        var slots = new List<(int r, int c, int cs)>();
        int row = 0, col = 0;
        foreach (var id in seq)
        {
            int cs = id == "nowplaying" && col == 0 ? 2 : 1;
            if (col + cs > 3) { row++; col = 0; cs = 1; }
            slots.Add((row, col, cs));
            col += cs;
            if (col >= 3) { row++; col = 0; }
        }

        int k = 0;
        foreach (var id in seq)
        {
            var (r, c, cs) = slots[k++];
            var card = _cards[id];
            card.Margin = new Thickness(c == 0 ? 0 : 4, r == 0 ? 0 : 4, c + cs >= 3 ? 0 : 4, 0);
            grid.Children.Add(card.At(row: r, col: c, colSpan: cs));
        }
    }

    public override void Shown()
    {
        Svc.Media.Changed += OnMedia; Svc.Weather.Changed += OnWeather; Svc.Battery.Changed += OnBattery; Svc.Calendar.Changed += OnNext; Svc.Audio.VolumeChanged += SyncVol;
        OnMedia(); OnWeather(); OnBattery(); OnNext(); SyncVol(); _tick = 0; Tick();
        _t.Start();
        _ = LoadBrightnessAsync();
    }

    public override void Hidden()
    {
        Svc.Media.Changed -= OnMedia; Svc.Weather.Changed -= OnWeather; Svc.Battery.Changed -= OnBattery; Svc.Calendar.Changed -= OnNext; Svc.Audio.VolumeChanged -= SyncVol;
        _t.Stop();
    }

    async Task LoadBrightnessAsync()
    {
        try
        {
            await Svc.Brightness.RefreshAsync();
            var d = Svc.Brightness.Devices.FirstOrDefault();
            if (d == null || d.Max <= d.Min) { _briRow.Visibility = Visibility.Collapsed; return; }
            _briRow.Visibility = Visibility.Visible;
            _bri.Value = (d.Cur - d.Min) / (double)(d.Max - d.Min);
        }
        catch (Exception e) { Log.Write(e); }
    }

    void SyncVol()
    {
        _vol.Value = Svc.Audio.Volume;
        _mute.Content = K.Glyph(Svc.Audio.Muted || Svc.Audio.Volume <= 0.001f ? "\uE74F" : "\uE767", 12, K.Label);
    }

    void Tick()
    {
        var now = DateTime.Now;
        _time.Text = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        _weekday.Text = Pt.TextInfo.ToTitleCase(now.ToString("dddd", Pt));
        _date.Text = now.ToString("d 'de' MMMM 'de' yyyy", Pt);
        var (p, d) = Svc.Media.Timeline();
        _prog.Value = d > 0 ? p / d : 0;
        _cur.Text = d > 0 ? $"{K.Fmt(p)} / {K.Fmt(d)}" : K.Fmt(p);
        if (_tick++ % 2 == 0)
        {
            _sys.Sample();
            _cpu.Value = _sys.Cpu; _ram.Value = _sys.Ram; _disk.Value = _sys.Disk;
        }
    }

    void OnMedia()
    {
        var m = Svc.Media;
        _title.Text = m.HasSession && m.Title.Length > 0 ? m.Title : "Nada tocando";
        _artist.Text = K.Spaced(m.Artist);
        _play.Content = K.Glyph(m.Playing ? "\uE769" : "\uE768", 10, K.Ink);
    }

    void OnWeather()
    {
        var w = Svc.Weather;
        if (!w.Ok) { _temp.Text = "—"; _wLabel.Text = "Clima indisponível"; _wDays.Text = ""; return; }
        _temp.Text = $"{Math.Round(w.Temp)}°";
        _wLabel.Text = $"{WeatherService.Emoji(w.Code)}  {WeatherService.Label(w.Code)}";
        _wDays.Text = string.Join("   ", w.Days.Skip(1).Take(2).Select(d => $"{d.Date.ToString("ddd", Pt)} {Math.Round(d.Max)}°/{Math.Round(d.Min)}°"));
    }

    void OnBattery()
    {
        var b = Svc.Battery;
        _batNum.Text = b.HasBattery ? $"{b.Percent}%" : "—";
        _batSub.Text = b.HasBattery ? (b.Charging ? "carregando" : "na bateria") : "na tomada";
    }

    void OnNext()
    {
        var e = Svc.Calendar.Next();
        if (e == null) { _next.Text = "Sem compromissos"; _nextSub.Text = "Nada agendado em breve"; return; }
        _next.Text = e.Title;
        _nextSub.Text = (e.Start.Date == DateTime.Today ? "hoje" : e.Start.ToString("dd/MM")) + (e.AllDay ? "" : " · " + e.Start.ToString("HH:mm"));
    }
}

/// <summary>
/// Mídia: cartão grande no estilo "Now Playing" — capa arredondada, origem + espectro, título, barra violeta com bolinha,
/// tempos e três botões (anterior, play branco grande, próxima).
/// </summary>
public sealed class MediaPage : PageBase
{
    readonly Image _cover = new() { Stretch = Stretch.UniformToFill };
    readonly Grid _coverHost = new();
    readonly Border _coverIcon = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
    readonly Image _bg = new() { Stretch = Stretch.UniformToFill, Opacity = 0.16 };
    readonly TextBlock _title = K.T("Nada tocando", 18, K.Fg, true), _artist = K.Cap("", 10.5, K.Label), _app = K.Cap("", 9, K.Label), _cur = K.T("0:00", 10.5, K.Muted), _dur = K.T("0:00", 10.5, K.Muted);
    // Cor dinâmica da capa: tudo que era violeta agora segue a cor dominante do álbum (com transição suave)
    readonly SolidColorBrush _acc = new(((SolidColorBrush)K.Violet).Color);
    readonly GradientStop _g0 = new(Color.FromRgb(0xD0, 0x8C, 0xFF), 0), _g1 = new(Color.FromRgb(0xA8, 0x55, 0xF7), 1);
    readonly GradientStop _glowStop = new(Color.FromArgb(80, 0xB5, 0x66, 0xFF), 0), _glowEdge = new(Color.FromArgb(0, 0xB5, 0x66, 0xFF), 1);
    Color _accTarget = ((SolidColorBrush)K.Violet).Color;
    readonly PillSlider _prog = new() { ReadOnly = true, ShowThumb = true, Height = 16 };
    readonly EqBars _eq = new(4) { BarWidth = 2.5, Gap = 2, Height = 12 };
    // Letra sincronizada
    readonly LyricsPane _pane = new();
    readonly ScrollViewer _plain;
    readonly TextBlock _plainText = K.T("", 13, K.Dim);
    readonly Border _lyricsBtn = new() { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Cursor = Cursors.Hand, Background = K.Hex("#99000000"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 12, 12, 0), ToolTip = "Letra" };
    readonly Border _lyricsHost = new() { Height = 0, Margin = new Thickness(0, 0, 0, 0), ClipToBounds = true };
    readonly Grid _srcRow;
    readonly StackPanel _ctl;
    readonly DispatcherTimer _lt = new() { Interval = TimeSpan.FromMilliseconds(180) };
    static bool _lyricsOn;
    readonly Border _srcIcon = new() { Child = Ico.Make("music", 13, K.Label, 1.8), Margin = new Thickness(0, 0, 7, 0) };
    readonly Button _play, _shuffle, _repeat;
    readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromMilliseconds(500) };

    public MediaPage()
    {
        var shell = new Border { Background = K.Surface, BorderBrush = K.Stroke, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(24) };
        var inner = new Grid();
        K.RoundClip(inner, 23);
        _prog.Fill = new LinearGradientBrush(new GradientStopCollection { _g0, _g1 }, 0);
        _prog.ThumbBrush = _acc;
        _eq.Fill = _acc;
        inner.Children.Add(new Border { Child = _bg, IsHitTestVisible = false });
        inner.Children.Add(new Border { Background = K.Hex("#E6000000"), IsHitTestVisible = false });
        inner.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = 330, Height = 330, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(-51, 0, 0, 0), IsHitTestVisible = false,
            Fill = new RadialGradientBrush(new GradientStopCollection { _glowStop, _glowEdge })
        });
        _cur.FontFamily = K.FontMedium; _dur.FontFamily = K.FontMedium;
        System.Windows.Documents.Typography.SetNumeralAlignment(_cur, FontNumeralAlignment.Tabular);
        System.Windows.Documents.Typography.SetNumeralAlignment(_dur, FontNumeralAlignment.Tabular);

        var g = K.Cols("auto", "*");
        g.Margin = new Thickness(26, 18, 26, 18);
        var cover = new Border
        {
            Width = 176, Height = 176, CornerRadius = new CornerRadius(20), Background = K.CardStrong, Child = _coverHost,
            VerticalAlignment = VerticalAlignment.Center, BorderBrush = K.Stroke, BorderThickness = new Thickness(1)
        };
        K.RoundClip(_cover, 19);
        _coverHost.Children.Add(_coverIcon); _coverHost.Children.Add(_cover);     // sem capa (ex.: alguns vídeos no navegador) fica o ícone
        g.Children.Add(cover);

        var s = new StackPanel { Margin = new Thickness(26, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var src = K.Cols("auto", "*", "auto");
        _srcRow = src;
        src.Children.Add(_srcIcon);
        src.Children.Add(_app.At(col: 1).Vc());
        src.Children.Add(_eq.At(col: 2).Vc());
        s.Children.Add(src);
        s.Children.Add(_title.M(0, 10, 0, 0));
        s.Children.Add(_artist.M(0, 3, 0, 0));
        _plain = K.Scroll(_plainText); _plainText.TextWrapping = TextWrapping.Wrap; _plainText.TextTrimming = TextTrimming.None; _plain.Visibility = Visibility.Collapsed;
        var lg = new Grid(); lg.Children.Add(_pane); lg.Children.Add(_plain);
        _lyricsHost.Child = lg;
        s.Children.Add(_lyricsHost);

        var bar = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
        bar.Children.Add(_prog);
        var times = K.Cols("*", "auto"); times.Margin = new Thickness(0, 2, 0, 0);
        times.Children.Add(_cur); times.Children.Add(_dur.At(col: 1));
        bar.Children.Add(times);
        s.Children.Add(bar);

        _shuffle = K.IconBtn("\uE8B1", "Aleatório", () => Svc.Media.ToggleShuffle(), 13, 34, 30, K.Muted);
        _repeat = K.IconBtn("\uE8EE", "Repetir", () => Svc.Media.CycleRepeat(), 13, 34, 30, K.Muted);
        _play = FK.RoundButton("\uE768", "Reproduzir/Pausar", () => Svc.Media.PlayPause(), 50, K.White, K.Ink, 18);
        var ctl = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
        ctl.Children.Add(_shuffle);
        ctl.Children.Add(FK.RoundButton("\uE892", "Anterior", () => Svc.Media.Prev(), 40, K.Track, K.Fg, 14).M(8, 0, 0, 0));
        ctl.Children.Add(_play.M(10, 0, 10, 0));
        ctl.Children.Add(FK.RoundButton("\uE893", "Próxima", () => Svc.Media.Next(), 40, K.Track, K.Fg, 14).M(0, 0, 8, 0));
        ctl.Children.Add(_repeat);
        _ctl = ctl;
        s.Children.Add(ctl);
        g.Children.Add(s.At(col: 1));
        inner.Children.Add(g);
        _lyricsBtn.Child = Ico.Make("lyrics", 15, K.White, 1.9);
        _lyricsBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; SetLyrics(!_lyricsOn, true); };
        inner.Children.Add(_lyricsBtn);
        _lt.Tick += (_, _) => SyncLyrics();
        shell.Child = inner;
        Root.Children.Add(shell);
        _t.Tick += (_, _) => Tick();
    }

    public override void Shown()
    {
        Svc.Media.Changed += OnMedia; Svc.Lyrics.Changed += OnLyrics;
        ApplyAccent(false); OnMedia(); Tick(); _t.Start(); _eq.Running = true;
        SetLyrics(_lyricsOn && Settings.Current.LyricsEnabled, false);
    }
    public override void Hidden() { Svc.Media.Changed -= OnMedia; Svc.Lyrics.Changed -= OnLyrics; _t.Stop(); _lt.Stop(); _eq.Running = false; }

    // ---------------- Cor dinâmica ----------------
    void ApplyAccent(bool animate)
    {
        var m = Svc.Media;
        var c = Settings.Current.DynamicCoverColor && m.Cover != null ? m.Accent : ((SolidColorBrush)K.Violet).Color;
        if (c == _accTarget) return;
        _accTarget = c;
        var lite = FK.Lerp(c, Colors.White, 0.28); var dark = FK.Lerp(c, Colors.Black, 0.12);
        void To(System.Windows.Media.Animation.Animatable a, DependencyProperty dp, Color col)
        {
            if (!Settings.Current.Animations) { a.BeginAnimation(dp, null); a.SetValue(dp, col); return; }
            a.BeginAnimation(dp, new System.Windows.Media.Animation.ColorAnimation(col, TimeSpan.FromMilliseconds(650)) { EasingFunction = new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut } });
        }
        To(_acc, SolidColorBrush.ColorProperty, c);
        To(_g0, GradientStop.ColorProperty, lite); To(_g1, GradientStop.ColorProperty, dark);
        To(_glowStop, GradientStop.ColorProperty, Color.FromArgb(78, c.R, c.G, c.B)); To(_glowEdge, GradientStop.ColorProperty, Color.FromArgb(0, c.R, c.G, c.B));
        _prog.InvalidateVisual();
    }

    // ---------------- Letra ----------------
    void SetLyrics(bool on, bool animate)
    {
        if (on && !Settings.Current.LyricsEnabled) { _pane.SetMessage("Letras desligadas nas Configurações (Mídia)."); }
        _lyricsOn = on;
        _lyricsBtn.Background = on ? _acc : K.Hex("#99000000");
        _lyricsBtn.Child = Ico.Make("lyrics", 15, on ? K.Ink : K.White, 1.9);
        _srcRow.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        _ctl.LayoutTransform = on ? new ScaleTransform(0.78, 0.78) : Transform.Identity;
        _ctl.Margin = new Thickness(0, on ? 4 : 10, 0, 0);
        double h = on ? 82 : 0;
        if (animate && Settings.Current.Animations)
            _lyricsHost.BeginAnimation(FrameworkElement.HeightProperty, new System.Windows.Media.Animation.DoubleAnimation(h, TimeSpan.FromMilliseconds(380)) { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } });
        else { _lyricsHost.BeginAnimation(FrameworkElement.HeightProperty, null); _lyricsHost.Height = h; }
        _lyricsHost.Margin = new Thickness(0, on ? 8 : 0, 0, 0);
        if (on) { RequestLyrics(); OnLyrics(); _lt.Start(); } else _lt.Stop();
    }

    void RequestLyrics()
    {
        var m = Svc.Media;
        if (!m.HasSession || m.Title.Length == 0) { Svc.Lyrics.Request("", "", "", 0); return; }
        var (_, d) = m.Timeline();
        Svc.Lyrics.Request(m.Title, m.Artist, m.Album, d);
    }

    void OnLyrics()
    {
        if (!_lyricsOn) return;
        var l = Svc.Lyrics;
        _plain.Visibility = l.State == LyricsState.Plain ? Visibility.Visible : Visibility.Collapsed;
        _pane.Visibility = l.State == LyricsState.Plain ? Visibility.Collapsed : Visibility.Visible;
        switch (l.State)
        {
            case LyricsState.Synced: _pane.SetLines(l.Lines); SyncLyrics(); break;
            case LyricsState.Plain: _plainText.Text = l.Plain; break;
            case LyricsState.Loading: _pane.SetMessage("Buscando letra…"); break;
            case LyricsState.Instrumental: _pane.SetMessage("Instrumental"); break;
            case LyricsState.NotFound: _pane.SetMessage("Letra não encontrada para esta faixa"); break;
            case LyricsState.Error: _pane.SetMessage("Sem conexão com o serviço de letras"); break;
            default: _pane.SetMessage(Svc.Media.HasSession ? "" : "Nada tocando"); break;
        }
    }

    void SyncLyrics()
    {
        if (!_lyricsOn || Svc.Lyrics.State != LyricsState.Synced) return;
        var (p, _) = Svc.Media.Timeline();
        _pane.Update(Svc.Lyrics.IndexAt(p + 0.25));      // um pouquinho adiantado: a linha acende junto com a voz
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
        _artist.Text = K.Spaced(m.Artist);
        _app.Text = m.HasSession ? K.Spaced(m.App) : K.Spaced("Abra um reprodutor");
        _srcIcon.Child = Ico.Make(m.IsVideo ? "video" : "music", 13, K.Label, 1.8);
        _cover.Source = m.Cover;
        _coverIcon.Child = Ico.Make(m.IsVideo ? "video" : "music", 44, K.Muted, 1.2);
        _bg.Source = Settings.Current.MediaBlurBackground ? m.Cover : null;
        _eq.Playing = m.Playing;
        _play.Content = K.Glyph(m.Playing ? "\uE769" : "\uE768", 18, K.Ink);
        _shuffle.Content = K.Glyph("\uE8B1", 13, m.Shuffle ? K.Violet : K.Muted); _shuffle.IsEnabled = m.CanShuffle;
        _repeat.Content = K.Glyph(m.Repeat == MediaPlaybackAutoRepeatMode.Track ? "\uE8ED" : "\uE8EE", 13, m.Repeat == MediaPlaybackAutoRepeatMode.None ? K.Muted : K.Violet);
        _repeat.IsEnabled = m.CanRepeat;
        _shuffle.Content = K.Glyph("\uE8B1", 13, m.Shuffle ? _acc : K.Muted);
        _repeat.Content = K.Glyph(m.Repeat == MediaPlaybackAutoRepeatMode.Track ? "\uE8ED" : "\uE8EE", 13, m.Repeat == MediaPlaybackAutoRepeatMode.None ? K.Muted : _acc);
        ApplyAccent(true);
        if (_lyricsOn) RequestLyrics();
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
        head.Children.Add(FK.Header("\uE715", "INBOX & NOTIFICAÇÕES"));
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
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 8, 0, 4) };
            var brandId = Brand.Match(grp.Key);
            if (brandId != null) { var bb = Brand.Badge(brandId, 20); bb.Margin = new Thickness(0, 0, 8, 0); head.Children.Add(bb); }
            head.Children.Add(FK.Mono($"{grp.Key.ToUpperInvariant()}  ·  {grp.Count()}", 10, K.Muted, true).Vc());
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
            CornerRadius = new CornerRadius(14),
            BorderBrush = K.Stroke,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 8, 8, 8),
            Margin = new Thickness(0, 0, 0, 5),
            Child = g
        };
    }
}

/// <summary>Janela de letra sincronizada: a linha atual fica no centro, grande e branca; as vizinhas esmaecem. Rolagem em mola.</summary>
public sealed class LyricsPane : Grid
{
    const double Slot = 27;
    readonly Canvas _canvas = new();
    readonly TranslateTransform _tt = new();
    readonly List<(TextBlock tb, ScaleTransform sc)> _rows = new();
    readonly TextBlock _msg = K.T("", 12.5, K.Dim);
    readonly Spring _y = new(0);
    readonly MotionLoop _loop;
    int _cur = -2;
    IReadOnlyList<LyricLine>? _lines;

    public LyricsPane()
    {
        ClipToBounds = true; Height = 82;
        _canvas.RenderTransform = _tt; _canvas.VerticalAlignment = VerticalAlignment.Top;
        Children.Add(_canvas);
        _msg.HorizontalAlignment = HorizontalAlignment.Left; _msg.VerticalAlignment = VerticalAlignment.Center; _msg.Visibility = Visibility.Collapsed;
        Children.Add(_msg);
        // esmaece de leve só o topo e a base
        OpacityMask = new LinearGradientBrush(new GradientStopCollection
        {
            new GradientStop(Color.FromArgb(0, 0, 0, 0), 0), new GradientStop(Colors.Black, 0.16), new GradientStop(Colors.Black, 0.84), new GradientStop(Color.FromArgb(0, 0, 0, 0), 1)
        }, 90);
        _loop = new MotionLoop(() => _tt.Y = _y.Value);
        _loop.Add(_y);
        SizeChanged += (_, _) => { foreach (var (tb, _) in _rows) tb.Width = Math.Max(10, ActualWidth); if (_lines != null) { _y.Snap(Target(_cur)); _tt.Y = _y.Value; } };
    }

    /// <summary>Deslocamento que deixa a linha <paramref name="idx"/> no meio vertical do painel.</summary>
    double Target(int idx)
    {
        double h = ActualHeight > 0 ? ActualHeight : Height;
        return h / 2 - Slot / 2 - Math.Max(idx, 0) * Slot;
    }

    public void SetMessage(string text)
    {
        _lines = null; _rows.Clear(); _canvas.Children.Clear(); _cur = -2;
        _msg.Text = text; _msg.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetLines(IReadOnlyList<LyricLine> lines)
    {
        if (ReferenceEquals(lines, _lines)) return;
        _lines = lines; _msg.Visibility = Visibility.Collapsed;
        _rows.Clear(); _canvas.Children.Clear(); _cur = -2;
        for (int i = 0; i < lines.Count; i++)
        {
            var tb = K.T(lines[i].Text.Length == 0 ? "♪" : lines[i].Text, 16, K.Fg, true);
            tb.TextTrimming = TextTrimming.CharacterEllipsis; tb.Width = Math.Max(10, ActualWidth); tb.Height = Slot; tb.VerticalAlignment = VerticalAlignment.Center;
            // fundo próprio (a letra fica legível sobre a capa desfocada) e sombra suave para separar do fundo
            tb.Background = K.Hex("#B3050507"); tb.Padding = new Thickness(8, 0, 8, 0);
            tb.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 8, ShadowDepth = 0, Opacity = 0.55, Color = Colors.Black };
            var sc = new ScaleTransform(0.82, 0.82);
            tb.RenderTransformOrigin = new Point(0, 0.5); tb.RenderTransform = sc;
            Canvas.SetTop(tb, i * Slot);
            _canvas.Children.Add(tb); _rows.Add((tb, sc));
        }
        _y.Snap(Target(-1));
        _tt.Y = _y.Value;
    }

    public void Update(int idx)
    {
        if (_lines == null || idx == _cur) return;
        int old = _cur; _cur = idx;
        void Style(int i, bool animate)
        {
            if (i < 0 || i >= _rows.Count) return;
            int dist = Math.Abs(i - Math.Max(idx, 0));
            double op = i == idx ? 1 : dist == 1 ? 0.62 : 0.4, sz = i == idx ? 1 : 0.86;
            var (tb, sc) = _rows[i];
            if (!animate || !Settings.Current.Animations) { tb.BeginAnimation(OpacityProperty, null); tb.Opacity = op; sc.BeginAnimation(ScaleTransform.ScaleXProperty, null); sc.BeginAnimation(ScaleTransform.ScaleYProperty, null); sc.ScaleX = sc.ScaleY = sz; return; }
            var d = TimeSpan.FromMilliseconds(260);
            tb.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(op, d));
            var a = new System.Windows.Media.Animation.DoubleAnimation(sz, d) { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, a); sc.BeginAnimation(ScaleTransform.ScaleYProperty, a.Clone());
        }
        for (int i = Math.Max(0, Math.Min(old, idx) - 3); i <= Math.Min(_rows.Count - 1, Math.Max(old, idx) + 3); i++) Style(i, true);
        _y.To(Target(idx), 0, 190, 24);
        _loop.Kick();
    }
}
