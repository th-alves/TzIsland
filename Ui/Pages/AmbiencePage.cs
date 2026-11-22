namespace TzIsland;

/// <summary>Um som da Ambientação: ícone, nome e volume próprio. O estado ligado/desligado anima com mola (borda, ícone e slider).</summary>
sealed class AmbTile : Border
{
    static readonly Color BorderOff = Color.FromRgb(0x1D, 0x1D, 0x20), BorderHover = Color.FromRgb(0x32, 0x32, 0x36), BorderOn = Color.FromRgb(0xEE, 0xEE, 0xF0);
    static readonly Color BgOff = Color.FromRgb(0x12, 0x12, 0x14), BgOn = Color.FromRgb(0x1A, 0x1A, 0x1D);
    static readonly Color CircleOff = Color.FromRgb(0x1C, 0x1C, 0x1F), CircleOn = Color.FromRgb(0x30, 0x30, 0x34);
    static readonly Color IconOff = Color.FromRgb(0x8E, 0x8E, 0x93), IconOn = Color.FromRgb(0xFF, 0xFF, 0xFF);

    readonly Spring _act = new(0), _hov = new(0), _pop = new(1);
    readonly MotionLoop _loop;
    readonly SolidColorBrush _bd = new(BorderOff), _bg = new(BgOff), _circle = new(CircleOff), _iconBrush = new(IconOff), _label = new(IconOff);
    readonly ScaleTransform _sc = new(1, 1);
    readonly IconHost _icon;
    readonly PillSlider _slider = new() { Fill = K.White, Height = 12, Margin = new Thickness(0, 9, 0, 0) };

    public readonly string Id;
    public event Action<string>? Clicked;
    public event Action<string, double>? LevelChanged;

    public AmbTile(string id, string name)
    {
        Id = id;
        CornerRadius = new CornerRadius(18); BorderThickness = new Thickness(1); Cursor = Cursors.Hand;
        BorderBrush = _bd; Background = _bg; Padding = new Thickness(12, 12, 12, 10);
        RenderTransformOrigin = new Point(0.5, 0.5); RenderTransform = _sc;

        _icon = Ico.Make(id, 18, _iconBrush, 1.6);
        var circle = new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(19), Background = _circle, Child = _icon, HorizontalAlignment = HorizontalAlignment.Center };
        var text = K.T(name, 11.5, _label, true); text.HorizontalAlignment = HorizontalAlignment.Center; text.Margin = new Thickness(0, 8, 0, 0);

        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(circle); sp.Children.Add(text); sp.Children.Add(_slider);
        Child = sp;

        _loop = new MotionLoop(Frame);
        _loop.Add(_act); _loop.Add(_hov); _loop.Add(_pop);
        MouseEnter += (_, _) => { _hov.To(1, 0, 360, 24); _loop.Kick(); };
        MouseLeave += (_, _) => { _hov.To(0, 0, 360, 24); _loop.Kick(); };
        MouseLeftButtonDown += (_, e) => { e.Handled = true; _pop.Snap(0.94); _pop.To(1, 0, 340, 11); _loop.Kick(); Clicked?.Invoke(Id); };
        _slider.Changed += v => LevelChanged?.Invoke(Id, v);
        Frame();
    }

    public void SetLevel(double v) => _slider.Value = v;

    public void SetActive(bool on, bool animate)
    {
        if (!animate) _act.Snap(on ? 1 : 0); else _act.To(on ? 1 : 0, 0, 300, 22);
        _loop.Kick(); Frame();
    }

    void Frame()
    {
        double a = Math.Clamp(_act.Value, 0, 1.08), h = Math.Clamp(_hov.Value, 0, 1);
        var edge = FK.Lerp(FK.Lerp(BorderOff, BorderHover, h), BorderOn, Math.Clamp(a, 0, 1));
        _bd.Color = edge;
        _bg.Color = FK.Lerp(BgOff, BgOn, Math.Clamp(a, 0, 1));
        _circle.Color = FK.Lerp(CircleOff, CircleOn, Math.Clamp(a, 0, 1));
        var ic = FK.Lerp(IconOff, IconOn, Math.Clamp(a, 0, 1));
        _iconBrush.Color = ic; _label.Color = FK.Lerp(IconOff, IconOn, Math.Clamp(a, 0, 1));
        _slider.Opacity = 0.4 + 0.6 * Math.Clamp(a, 0, 1);
        double s = _pop.Value * (1 + 0.022 * h) * (1 + 0.03 * Math.Max(0, a - 1));
        _sc.ScaleX = _sc.ScaleY = s;
    }
}

/// <summary>
/// Ambientação: oito sons em camadas (chuva, floresta, cafeteria, escritório, lo-fi, fogueira, riacho, ondas),
/// cada um com volume próprio, mais o painel "Mix": volume geral, desligar sozinho (15/30/60 min) e "Parar tudo".
/// </summary>
public sealed class AmbiencePage : PageBase
{
    static AmbienceService A => Svc.Ambience;

    readonly List<AmbTile> _tiles = new();
    readonly PillSlider _vol = new() { Fill = K.White, Height = 14 };
    readonly TextBlock _volNum = K.Num("70", 12, K.Fg), _status = K.T("", 11, K.Label), _fadeInfo = K.T("", 10.5, K.Muted);
    readonly ChipRow _chips = new(new[] { "15m", "30m", "60m" }, 54, 7) { HorizontalAlignment = HorizontalAlignment.Left };
    readonly Button _stop;
    readonly TextBlock _stopText = K.T("Parar tudo", 12, K.Red, true);
    int _chipSel = -2;

    public AmbiencePage()
    {
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new RowDefinition { Height = K.GL("*") });

        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 0, 9) };
        head.Children.Add(Ico.Make("waves", 15, K.Fg, 1.8));
        head.Children.Add(K.T("Ambientação", 13, K.Fg, true).M(8, 0, 0, 0).Vc());
        Root.Children.Add(head);

        var body = K.Cols("*", "238");
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Rows = 2, Columns = 4 };
        foreach (var (id, name) in AmbienceMixer.Sounds)
        {
            var t = new AmbTile(id, name) { Margin = new Thickness(0, 0, 8, 8) };
            t.Clicked += OnTile; t.LevelChanged += (i, v) => A.SetLevel(i, v);
            _tiles.Add(t); grid.Children.Add(t);
        }
        grid.Margin = new Thickness(0, 0, 0, -8);
        body.Children.Add(grid);

        // ---- Painel MIX
        var mix = new StackPanel();
        mix.Children.Add(FK.Header("\\uE767", "Mix"));
        var vrow = K.Cols("*", "auto"); vrow.Margin = new Thickness(0, 10, 0, 0);
        vrow.Children.Add(K.T("Volume", 12, K.Fg, true)); vrow.Children.Add(_volNum.At(col: 1));
        mix.Children.Add(vrow);
        _vol.Margin = new Thickness(0, 6, 0, 0);
        mix.Children.Add(_vol);
        mix.Children.Add(new Border { Height = 1, Background = K.Stroke, Margin = new Thickness(0, 9, 0, 9) });
        mix.Children.Add(K.T("Desligar depois de", 12, K.Fg, true));
        _chips.Margin = new Thickness(0, 8, 0, 0);
        mix.Children.Add(_chips);
        mix.Children.Add(_fadeInfo.M(0, 5, 0, 0));
        var hint = K.T("Toque num som para entrar aos poucos. Eles se misturam.", 10.5, K.Muted);
        hint.TextWrapping = TextWrapping.Wrap; hint.Margin = new Thickness(0, 6, 0, 0);
        mix.Children.Add(hint);
        mix.Children.Add(_status.M(0, 6, 0, 0));

        _stop = new Button { Style = K.Fill, Height = 34, Background = K.Hex("#1FFF453A"), Tag = new CornerRadius(12), Margin = new Thickness(0, 8, 0, 0) };
        var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        sp.Children.Add(K.Glyph("\\uE71A", 10, K.Red).M(0, 0, 8, 0)); sp.Children.Add(_stopText.Vc());
        _stop.Content = sp;
        _stop.Click += (_, _) => A.StopAll();
        mix.Children.Add(_stop);

        var card = FK.Card(K.Scroll(mix), 20); card.Padding = new Thickness(15, 13, 12, 12);
        body.Children.Add(card.At(col: 1));
        Root.Children.Add(body.At(row: 1));

        _vol.Changed += v => { A.Volume = v; _volNum.Text = Math.Round(v * 100).ToString(CultureInfo.InvariantCulture); };
        _chips.Picked += i =>
        {
            int minutes = i switch { 0 => 15, 1 => 30, _ => 60 };
            if (A.FadeMinutes == minutes) { A.FadeMinutes = 0; _chips.Select(-1, true); _chipSel = -1; }   // tocar de novo desliga o timer
            else { A.FadeMinutes = minutes; _chipSel = i; }
        };
    }

    void OnTile(string id) => A.Toggle(id);

    public override void Shown()
    {
        A.Changed += Sync;
        foreach (var t in _tiles) t.SetLevel(A.Level(t.Id));
        _vol.Value = A.Volume; _volNum.Text = Math.Round(A.Volume * 100).ToString(CultureInfo.InvariantCulture);
        Sync(true);
    }

    public override void Hidden() { A.Changed -= Sync; }

    void Sync() => Sync(false);

    void Sync(bool snap)
    {
        foreach (var t in _tiles) t.SetActive(A.IsOn(t.Id), !snap);
        int n = A.Playing.Count;
        if (n == 0) _status.Text = "Nada tocando";
        else
        {
            var names = AmbienceMixer.Sounds.Where(s => A.IsOn(s.Id)).Select(s => s.Name);
            _status.Text = $"{n} tocando · {string.Join(", ", names)}";
        }
        _stop.IsEnabled = n > 0; _stop.Opacity = n > 0 ? 1 : 0.4;

        int sel = A.FadeMinutes switch { 15 => 0, 30 => 1, 60 => 2, _ => -1 };
        if (sel != _chipSel) { _chips.Select(sel, !snap && _chipSel != -2); _chipSel = sel; }
        var left = A.SecondsToFade;
        _fadeInfo.Text = left == null ? "" : $"Desliga em {K.Fmt(Math.Ceiling(left.Value))}";
        _fadeInfo.Visibility = left == null ? Visibility.Collapsed : Visibility.Visible;
    }
}
