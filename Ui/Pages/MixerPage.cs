namespace TzIsland;

/// <summary>Medidor fininho de nível (pico do áudio) com queda suave, desenhado à mão.</summary>
sealed class PeakBar : FrameworkElement
{
    double _v;
    public PeakBar() { Height = 3; IsHitTestVisible = false; }
    /// <summary>Sobe na hora, desce devagar (como um VU).</summary>
    public void Push(double peak)
    {
        double t = Math.Clamp(peak, 0, 1), nv = t > _v ? t : Math.Max(t, _v - 0.06);
        if (Math.Abs(nv - _v) < 0.004) return;
        _v = nv; InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        dc.DrawRoundedRectangle(K.Stroke, null, new Rect(0, 0, w, h), h / 2, h / 2);
        if (_v > 0.005) dc.DrawRoundedRectangle(K.Green, null, new Rect(0, 0, Math.Max(h, _v * w), h), h / 2, h / 2);
    }
}

/// <summary>Uma linha do mixer: ícone real do app, nome, volume, medidor e botão de mudo.</summary>
sealed class MixerRow : Border
{
    readonly Border _iconHost = new() { Width = 30, Height = 30, CornerRadius = new CornerRadius(9), Background = K.Track };
    readonly TextBlock _name = K.T("", 12, K.Fg, true), _pct = K.Num("", 11.5, K.Label);
    readonly PillSlider _vol = new() { Fill = K.White, Height = 14 };
    readonly PeakBar _peak = new() { Margin = new Thickness(0, 3, 0, 0) };
    readonly Button _mute;
    readonly Border _muteIcon = new();
    public MixerApp App { get; private set; }

    public MixerRow(MixerApp app)
    {
        App = app;
        CornerRadius = new CornerRadius(14); Padding = new Thickness(10, 8, 8, 8); Margin = new Thickness(0, 0, 0, 6);
        Background = K.Card; BorderBrush = K.Stroke; BorderThickness = new Thickness(1);

        var g = K.Cols("40", "*", "40", "34");
        _iconHost.HorizontalAlignment = HorizontalAlignment.Left; _iconHost.VerticalAlignment = VerticalAlignment.Center;
        SetIcon(app);
        g.Children.Add(_iconHost);

        var mid = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _name.TextTrimming = TextTrimming.CharacterEllipsis;
        mid.Children.Add(_name); mid.Children.Add(_vol.M(0, 3, 0, 0)); mid.Children.Add(_peak);
        g.Children.Add(mid.At(col: 1));

        _pct.HorizontalAlignment = HorizontalAlignment.Right;
        g.Children.Add(_pct.At(col: 2).Vc());

        _mute = K.IconBtn(" ", "Silenciar", () => { Svc.Mixer.SetMuted(App, !App.Muted); Sync(); }, 13, 30, 28, K.Fg);
        _mute.Tag = new CornerRadius(9); _mute.Content = _muteIcon; _mute.HorizontalAlignment = HorizontalAlignment.Right;
        g.Children.Add(_mute.At(col: 3).Vc());
        Child = g;

        _vol.Changed += v =>
        {
            Svc.Mixer.SetVolume(App, (float)v);
            if (App.Muted && v > 0) Svc.Mixer.SetMuted(App, false);
            Sync();
        };
        MouseEnter += (_, _) => Background = K.Hex("#FF1A1A1D");
        MouseLeave += (_, _) => Background = K.Card;
        // Rolar o mouse sobre a linha ajusta o volume de 2 em 2 (Shift = de 10 em 10).
        PreviewMouseWheel += (_, e) =>
        {
            e.Handled = true;
            double step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 0.10 : 0.02;
            Svc.Mixer.SetVolume(App, (float)Math.Round(App.Volume + Math.Sign(e.Delta) * step, 2));
            if (App.Muted && e.Delta > 0) Svc.Mixer.SetMuted(App, false);
            Sync();
        };
        Sync();
    }

    void SetIcon(MixerApp app)
    {
        if (app.Icon != null)
        {
            _iconHost.Background = Brushes.Transparent;
            var img = new Image { Source = app.Icon, Width = 28, Height = 28, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            _iconHost.Child = img;
        }
        else
        {
            var ic = Ico.Make(app.IsSystem ? "bell" : "volume", 15, K.Label, 1.8);
            ic.HorizontalAlignment = HorizontalAlignment.Center; ic.VerticalAlignment = VerticalAlignment.Center;
            _iconHost.Child = ic;
        }
    }

    /// <summary>Troca o objeto do app (nova leitura das sessões) mantendo a linha (sem piscar).</summary>
    public void Rebind(MixerApp app) { App = app; Sync(); }

    public void Sync()
    {
        _name.Text = App.Name;
        _name.Foreground = App.Active ? K.Fg : K.Dim;
        _vol.Value = App.Volume;
        _vol.Opacity = App.Muted ? 0.35 : 1;
        _pct.Text = App.Muted ? "mudo" : Math.Round(App.Volume * 100).ToString(CultureInfo.InvariantCulture);
        _muteIcon.Child = Ico.Make(App.Muted ? "volume-off" : App.Volume < 0.5 ? "volume-low" : "volume", 15, App.Muted ? K.Red : K.Fg, 1.8);
        _mute.ToolTip = App.Muted ? "Ativar som" : "Silenciar";
    }

    public void Meter() => _peak.Push(App.Muted ? 0 : Svc.Mixer.Peak(App));
}

/// <summary>
/// Mixer de volume: volume de cada app que está usando o som (com o ícone real do programa), volume geral e
/// escolha da saída de áudio (fones, caixas, monitor). Atualiza só enquanto a página está aberta.
/// </summary>
public sealed class MixerPage : PageBase
{
    static MixerService M => Svc.Mixer;

    readonly StackPanel _list = new(), _devices = new();
    readonly Dictionary<string, MixerRow> _rows = new();
    readonly TextBlock _count = K.T("", 11.5, K.Dim), _devName = K.T("", 10.5, K.Muted);
    readonly PillSlider _master = new() { Fill = K.White, Height = 14 };
    readonly TextBlock _masterNum = K.Num("", 20, K.Fg);
    readonly PeakBar _masterPeak = new() { Height = 4, Margin = new Thickness(0, 6, 0, 0) };
    readonly Button _masterMute;
    readonly Border _masterMuteIcon = new();
    readonly UIElement _empty;
    readonly DispatcherTimer _meter = new() { Interval = TimeSpan.FromMilliseconds(50) };
    readonly DispatcherTimer _scan = new() { Interval = TimeSpan.FromMilliseconds(1500) };
    int _scanTicks;

    public MixerPage()
    {
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new RowDefinition { Height = K.GL("*") });

        var head = K.Cols("*", "auto");
        head.Margin = new Thickness(4, 0, 2, 9);
        var title = new StackPanel { Orientation = Orientation.Horizontal };
        title.Children.Add(Ico.Make("sliders", 15, K.Fg, 1.8));
        title.Children.Add(K.T("Mixer de volume", 13, K.Fg, true).M(8, 0, 0, 0).Vc());
        head.Children.Add(title);
        head.Children.Add(_count.At(col: 1).Vc());
        Root.Children.Add(head);

        var body = K.Cols("*", "236");

        // ---------- Lista de apps
        var empty = new StackPanel { Margin = new Thickness(0, 40, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
        var eic = Ico.Make("volume", 30, K.Muted, 1.4); eic.HorizontalAlignment = HorizontalAlignment.Center;
        empty.Children.Add(eic);
        empty.Children.Add(K.T("Nenhum app usando o som", 14, K.Fg, true).M(0, 10, 0, 2));
        var eh = K.T("Toque uma música ou um vídeo e o app aparece aqui.", 11.5, K.Faint); eh.HorizontalAlignment = HorizontalAlignment.Center;
        empty.Children.Add(eh);
        _empty = empty;
        var listHost = new Grid();
        listHost.Children.Add(K.Scroll(_list));
        listHost.Children.Add(_empty);
        body.Children.Add(listHost.M(0, 0, 8, 0));

        // ---------- Painel geral: volume do sistema + saída
        var side = new StackPanel();
        side.Children.Add(FK.Header(Ico.Make("volume", 12, K.Label, 1.9), "Geral"));
        var mrow = K.Cols("*", "auto"); mrow.Margin = new Thickness(0, 8, 0, 0);
        var mtx = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        mtx.Children.Add(_masterNum);
        _devName.TextTrimming = TextTrimming.CharacterEllipsis; mtx.Children.Add(_devName);
        mrow.Children.Add(mtx);
        _masterMute = K.IconBtn(" ", "Silenciar tudo", () => { M.MasterMuted = !M.MasterMuted; SyncMaster(); }, 13, 32, 30, K.Fg);
        _masterMute.Tag = new CornerRadius(10); _masterMute.Content = _masterMuteIcon;
        mrow.Children.Add(_masterMute.At(col: 1).Vc());
        side.Children.Add(mrow);
        side.Children.Add(_master.M(0, 8, 0, 0));
        side.Children.Add(_masterPeak);
        side.Children.Add(new Border { Height = 1, Background = K.Stroke, Margin = new Thickness(0, 11, 0, 9) });
        side.Children.Add(FK.Header(Ico.Make("headphones", 12, K.Label, 1.9), "Saída"));
        side.Children.Add(_devices.M(0, 6, 0, 0));

        var card = FK.Card(K.Scroll(side), 20); card.Padding = new Thickness(15, 13, 12, 12);
        body.Children.Add(card.At(col: 1));
        Root.Children.Add(body.At(row: 1));

        _master.Changed += v => { M.MasterVolume = (float)v; if (M.MasterMuted && v > 0) M.MasterMuted = false; SyncMaster(); };
        _meter.Tick += (_, _) => Meter();
        _scan.Tick += (_, _) => Scan();
    }

    public override void Shown()
    {
        Svc.Audio.VolumeChanged += SyncMaster;
        _scanTicks = 0;
        M.Refresh(devices: true);
        Rebuild(); SyncMaster(); BuildDevices();
        _meter.Start(); _scan.Start();
    }

    public override void Hidden()
    {
        Svc.Audio.VolumeChanged -= SyncMaster;
        _meter.Stop(); _scan.Stop();
    }

    void Scan()
    {
        bool devs = ++_scanTicks % 4 == 0;          // saídas: a cada ~6 s; apps: a cada 1,5 s
        string before = M.DeviceName;
        M.Refresh(devs);
        foreach (var a in M.Apps) M.Reread(a);
        Rebuild();
        if (devs || before != M.DeviceName) { BuildDevices(); SyncMaster(); }
    }

    void Meter()
    {
        foreach (var r in _rows.Values) r.Meter();
        _masterPeak.Push(M.MasterMuted ? 0 : M.MasterPeak);
    }

    /// <summary>Atualiza a lista mantendo as linhas que já existem (o slider que você está arrastando não some).</summary>
    void Rebuild()
    {
        var apps = M.Apps;
        var keys = apps.Select(a => a.Key).ToHashSet();
        foreach (var k in _rows.Keys.Where(k => !keys.Contains(k)).ToList()) { _list.Children.Remove(_rows[k]); _rows.Remove(k); }
        for (int i = 0; i < apps.Count; i++)
        {
            var a = apps[i];
            if (_rows.TryGetValue(a.Key, out var row)) row.Rebind(a);
            else { row = new MixerRow(a); _rows[a.Key] = row; }
            int at = _list.Children.IndexOf(row);
            if (at != i) { if (at >= 0) _list.Children.RemoveAt(at); _list.Children.Insert(Math.Min(i, _list.Children.Count), row); }
        }
        int n = apps.Count(a => !a.IsSystem);
        _count.Text = !M.HasDevice ? "" : n == 0 ? "" : n == 1 ? "1 app" : $"{n} apps";
        _empty.Visibility = apps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    void SyncMaster()
    {
        float v = M.MasterVolume; bool mute = M.MasterMuted;
        _master.Value = v; _master.Opacity = mute ? 0.35 : 1;
        _masterNum.Text = mute ? "Mudo" : $"{Math.Round(v * 100)}%";
        _devName.Text = M.HasDevice ? M.DeviceName : "Nenhuma saída de áudio";
        _masterMuteIcon.Child = Ico.Make(mute ? "volume-off" : v < 0.5 ? "volume-low" : "volume", 16, mute ? K.Red : K.Fg, 1.8);
        _masterMute.ToolTip = mute ? "Ativar som" : "Silenciar tudo";
    }

    void BuildDevices()
    {
        _devices.Children.Clear();
        if (M.Devices.Count == 0) { _devices.Children.Add(K.T("Nenhuma saída encontrada", 11, K.Muted)); return; }
        foreach (var d in M.Devices)
        {
            var dev = d;
            var g = K.Cols("*", "18");
            var name = K.T(d.Name, 11.5, d.IsDefault ? K.Fg : K.Dim, d.IsDefault); name.TextTrimming = TextTrimming.CharacterEllipsis;
            g.Children.Add(name.Vc());
            if (d.IsDefault) g.Children.Add(Ico.Make("check", 13, K.Green, 2.2).At(col: 1).Vc());
            var b = new Border
            {
                Child = g, Padding = new Thickness(9, 7, 7, 7), CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 0, 0, 4),
                Background = d.IsDefault ? K.CardStrong : K.Card, Cursor = d.IsDefault ? Cursors.Arrow : Cursors.Hand, ToolTip = d.Name
            };
            if (!d.IsDefault)
            {
                b.MouseEnter += (_, _) => b.Background = K.Hex("#FF1A1A1D");
                b.MouseLeave += (_, _) => b.Background = K.Card;
                b.MouseLeftButtonDown += (_, e) =>
                {
                    e.Handled = true;
                    if (M.SetDefaultDevice(dev.Id)) { M.Refresh(devices: true); Rebuild(); BuildDevices(); SyncMaster(); }
                };
            }
            _devices.Children.Add(b);
        }
    }
}
