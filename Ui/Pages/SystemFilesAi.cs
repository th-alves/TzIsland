using Shapes = System.Windows.Shapes;

namespace TzIsland;

public sealed class SystemPage : PageBase
{
    readonly TextBlock _bat = K.T("", 13, K.Fg, true), _batSub = K.T("", 11, K.Dim), _mic = K.T("", 12), _micSub = K.T("", 11, K.Dim), _btHead = K.T("Bluetooth", 12, K.Fg, true);
    readonly Shapes.Ellipse _micDot = new() { Width = 9, Height = 9, Fill = K.Faint, VerticalAlignment = VerticalAlignment.Center };
    readonly StackPanel _bt = new(), _displays = new();
    readonly PillSlider _vol = new();
    readonly Button _mute;
    readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromSeconds(20) };

    public SystemPage()
    {
        var g = K.Cols("1*", "1*");
        var left = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };

        var batRow = K.Cols("30", "*");
        batRow.Children.Add(K.Glyph("\uEBAA", 20));
        var bs = new StackPanel(); bs.Children.Add(_bat); bs.Children.Add(_batSub);
        batRow.Children.Add(bs.At(col: 1));
        left.Children.Add(K.CardBox(batRow, 14, new Thickness(10)));

        var micRow = K.Cols("18", "*");
        micRow.Children.Add(_micDot);
        var ms = new StackPanel(); ms.Children.Add(_mic); ms.Children.Add(_micSub);
        micRow.Children.Add(ms.At(col: 1));
        left.Children.Add(K.CardBox(micRow, 14, new Thickness(10)).M(0, 6, 0, 0));

        var btBox = new StackPanel(); btBox.Children.Add(_btHead); btBox.Children.Add(_bt);
        left.Children.Add(K.CardBox(btBox, 14, new Thickness(10)).M(0, 6, 0, 0));
        g.Children.Add(left);

        var right = new StackPanel();
        var vrow = K.Cols("auto", "*");
        _mute = K.IconBtn("\uE767", "Silenciar", () => { Svc.Audio.Muted = !Svc.Audio.Muted; SyncVol(); }, 14, 32, 28);
        vrow.Children.Add(_mute); vrow.Children.Add(_vol.At(col: 1).M(6, 0, 4, 0));
        var vb = new StackPanel(); vb.Children.Add(K.T("Volume", 12, K.Fg, true).M(0, 0, 0, 2)); vb.Children.Add(vrow);
        right.Children.Add(K.CardBox(vb, 14, new Thickness(10)));
        right.Children.Add(K.Scroll(_displays).M(0, 6, 0, 0));
        ((ScrollViewer)right.Children[1]).MaxHeight = 170;
        g.Children.Add(right.At(col: 1));
        Root.Children.Add(g);

        _vol.Changed += v => { Svc.Audio.Volume = (float)v; if (Svc.Audio.Muted && v > 0) Svc.Audio.Muted = false; };
        _t.Tick += async (_, _) => await LoadDevicesAsync();
    }

    public override async void Shown()
    {
        Svc.Battery.Changed += SyncBat; Svc.Audio.VolumeChanged += SyncVol; Svc.Audio.MicChanged += SyncMic;
        SyncBat(); SyncVol(); SyncMic();
        _t.Start();
        await LoadDevicesAsync();
    }

    public override void Hidden()
    {
        Svc.Battery.Changed -= SyncBat; Svc.Audio.VolumeChanged -= SyncVol; Svc.Audio.MicChanged -= SyncMic;
        _t.Stop();
    }

    void SyncBat()
    {
        var b = Svc.Battery;
        _bat.Text = b.HasBattery ? $"Bateria {b.Percent}%" : "Sem bateria";
        _batSub.Text = b.HasBattery ? (b.Charging ? "Carregando / na tomada" : "Usando a bateria") : "Este computador usa energia da tomada";
    }

    void SyncVol()
    {
        _vol.Value = Svc.Audio.Volume;
        _mute.Content = K.Glyph(Svc.Audio.Muted || Svc.Audio.Volume <= 0.001f ? "\uE74F" : "\uE767", 14);
    }

    void SyncMic()
    {
        var a = Svc.Audio;
        _micDot.Fill = a.MicInUse ? K.Orange : K.Faint;
        _mic.Text = a.MicInUse ? "Microfone em uso" : "Microfone livre";
        _micSub.Text = !Settings.Current.MonitorMicrophone ? "Monitoramento desativado" : a.MicInUse ? string.Join(", ", a.MicApps) : "Nenhum app está gravando";
    }

    async Task LoadDevicesAsync()
    {
        await Svc.Bluetooth.RefreshAsync();
        _btHead.Text = "Bluetooth" + (Svc.Bluetooth.RadioOn == null ? " · indisponível" : Svc.Bluetooth.RadioOn == true ? " · ligado" : " · desligado");
        _bt.Children.Clear();
        if (Svc.Bluetooth.Devices.Count == 0) _bt.Children.Add(K.T("Nenhum dispositivo conectado", 11, K.Dim));
        foreach (var d in Svc.Bluetooth.Devices)
            _bt.Children.Add(K.T($"{d.Name}  {(d.Battery != null ? d.Battery + "%" : "· bateria não informada")}", 11, K.Dim));

        if (_displays.Children.Count == 0) await Svc.Brightness.RefreshAsync();
        BuildDisplays();
    }

    void BuildDisplays()
    {
        _displays.Children.Clear();
        var devs = Svc.Brightness.Devices;
        if (devs.Count == 0)
        {
            var t = K.T("Nenhuma tela com controle de brilho por software foi encontrada (monitores externos precisam de DDC/CI habilitado).", 11, K.Faint);
            t.TextWrapping = TextWrapping.Wrap;
            _displays.Children.Add(t);
            return;
        }
        foreach (var d in devs)
        {
            var box = new StackPanel();
            box.Children.Add(K.T(d.Name, 12, K.Fg, true));
            box.Children.Add(Slider("\uE706", "Brilho", d.Min, d.Max, d.Cur, v => Svc.Brightness.SetBrightness(d, v)));
            if (d.HasContrast) box.Children.Add(Slider("\uE7F4", "Contraste", d.CMin, d.CMax, d.CCur, v => Svc.Brightness.SetContrast(d, v)));
            _displays.Children.Add(K.CardBox(box, 14, new Thickness(10)).M(0, 0, 0, 6));
        }
    }

    static UIElement Slider(string glyph, string tip, uint min, uint max, uint cur, Action<uint> set)
    {
        var s = new PillSlider { Value = max > min ? (cur - min) / (double)(max - min) : 0, ToolTip = tip };
        DateTime last = DateTime.MinValue;
        s.Changed += v =>
        {
            if ((DateTime.Now - last).TotalMilliseconds < 90) return;   // DDC/CI é lento: limita a taxa
            last = DateTime.Now;
            uint val = (uint)(min + v * (max - min));
            Task.Run(() => set(val));
        };
        var g = K.Cols("22", "*");
        g.Children.Add(K.Glyph(glyph, 13, K.Dim));
        g.Children.Add(s.At(col: 1));
        g.Margin = new Thickness(0, 4, 0, 0);
        return g;
    }
}

public sealed class FilesPage : PageBase
{
    readonly WrapPanel _tiles = new();
    readonly Button _dl, _ss;
    readonly TextBlock _hint = K.T("Solte arquivos aqui para guardá-los na bandeja", 12, K.Faint);

    public FilesPage()
    {
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new RowDefinition { Height = K.GL("*") });
        var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        _dl = K.TextBtn("Downloads", () => Toggle(true)); _ss = K.TextBtn("Capturas de tela", () => Toggle(false));
        top.Children.Add(_dl); top.Children.Add(_ss.M(6, 0, 0, 0));
        top.Children.Add(K.TextBtn("+ Adicionar arquivo", Pick, true).M(6, 0, 0, 0));
        top.Children.Add(K.TextBtn("Esvaziar", () => Svc.Files.Clear()).M(6, 0, 0, 0));
        Root.Children.Add(top);
        var host = new Grid();
        host.Children.Add(K.Scroll(_tiles));
        _hint.HorizontalAlignment = HorizontalAlignment.Center; _hint.VerticalAlignment = VerticalAlignment.Center;
        _hint.IsHitTestVisible = false;
        host.Children.Add(_hint);
        Root.Children.Add(host.At(row: 1));
        SyncToggles();
    }

    public override void Shown() { Svc.Files.Items.CollectionChanged += OnChange; Rebuild(); }
    public override void Hidden() { Svc.Files.Items.CollectionChanged -= OnChange; }
    void OnChange(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Rebuild();

    void Toggle(bool downloads)
    {
        if (downloads) Settings.Current.WatchDownloads = !Settings.Current.WatchDownloads;
        else Settings.Current.WatchScreenshots = !Settings.Current.WatchScreenshots;
        Settings.Apply(); SyncToggles();
    }

    void SyncToggles()
    {
        _dl.Background = Settings.Current.WatchDownloads ? K.Blue : K.Card;
        _ss.Background = Settings.Current.WatchScreenshots ? K.Blue : K.Card;
        _dl.ToolTip = "Monitoramento automático da pasta Downloads: " + (Settings.Current.WatchDownloads ? "ligado" : "desligado");
        _ss.ToolTip = "Monitoramento automático da pasta de capturas: " + (Settings.Current.WatchScreenshots ? "ligado" : "desligado");
    }

    void Pick()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Title = "Adicionar à bandeja" };
        if (dlg.ShowDialog() == true) foreach (var f in dlg.FileNames) Svc.Files.Add(f);
    }

    void Rebuild()
    {
        _tiles.Children.Clear();
        _hint.Visibility = Svc.Files.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        int i = 0;
        foreach (var f in Svc.Files.Items)
        {
            var t = Tile(f);
            if (i < 12) K.FadeIn(t, i * 30, 6);
            _tiles.Children.Add(t); i++;
        }
    }

    static readonly string[] ImgExt = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };

    UIElement Tile(TrayFile f)
    {
        var s = new StackPanel { Width = 76 };
        UIElement thumb;
        if (ImgExt.Contains(System.IO.Path.GetExtension(f.Path).ToLowerInvariant()) && File.Exists(f.Path))
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.DecodePixelWidth = 120; bi.UriSource = new Uri(f.Path); bi.EndInit(); bi.Freeze();
                thumb = new Image { Source = bi, Stretch = Stretch.UniformToFill };
            }
            catch { thumb = K.Glyph("\uE8B9", 22); }
        }
        else thumb = K.Glyph(Directory.Exists(f.Path) ? "\uE8B7" : "\uE8A5", 22, K.Dim);
        var tb = new Border { Width = 56, Height = 48, CornerRadius = new CornerRadius(10), Background = K.CardStrong, ClipToBounds = true, Child = thumb, HorizontalAlignment = HorizontalAlignment.Center };
        s.Children.Add(tb);
        var name = K.T(f.Name, 10, K.Fg); name.TextAlignment = TextAlignment.Center; name.Margin = new Thickness(0, 3, 0, 0);
        s.Children.Add(name);
        if (f.Auto) { var o = K.T(f.Origin == "Downloads" ? "↓ novo" : "▣ nova", 9, K.Blue); o.HorizontalAlignment = HorizontalAlignment.Center; s.Children.Add(o); }

        var btn = new Button { Content = s, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(4), Background = K.Clear, Tag = new CornerRadius(12), ToolTip = f.Path };
        btn.Style = K.Ghost;
        btn.Click += (_, _) => { try { Process.Start(new ProcessStartInfo(f.Path) { UseShellExecute = true }); } catch { } };
        Point? down = null;
        btn.PreviewMouseLeftButtonDown += (_, e) => down = e.GetPosition(btn);
        btn.PreviewMouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || down == null) return;
            if ((e.GetPosition(btn) - down.Value).Length < 8) return;
            down = null;
            if (File.Exists(f.Path) || Directory.Exists(f.Path))
                DragDrop.DoDragDrop(btn, new DataObject(DataFormats.FileDrop, new[] { f.Path }), DragDropEffects.Copy | DragDropEffects.Move);
        };
        var menu = new ContextMenu();
        var open = new MenuItem { Header = "Abrir" }; open.Click += (_, _) => { try { Process.Start(new ProcessStartInfo(f.Path) { UseShellExecute = true }); } catch { } };
        var show = new MenuItem { Header = "Mostrar na pasta" }; show.Click += (_, _) => Process.Start("explorer.exe", "/select,\"" + f.Path + "\"");
        var rem = new MenuItem { Header = "Remover da bandeja (não apaga o arquivo)" }; rem.Click += (_, _) => Svc.Files.Remove(f);
        menu.Items.Add(open); menu.Items.Add(show); menu.Items.Add(rem);
        btn.ContextMenu = menu;
        return btn;
    }
}

public sealed class AiPage : PageBase
{
    readonly StackPanel _list = new();
    readonly TextBlock _foot = K.T("Limites de uso só aparecem quando uma fonte oficial os informa (veja o README). Nunca estimamos esses valores.", 10, K.Faint);

    public AiPage()
    {
        Root.RowDefinitions.Add(new RowDefinition { Height = K.GL("*") });
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.Children.Add(K.Scroll(_list));
        _foot.TextWrapping = TextWrapping.Wrap; _foot.Margin = new Thickness(4, 6, 4, 0);
        Root.Children.Add(_foot.At(row: 1));
    }

    public override void Shown() { Svc.Agents.Changed += Rebuild; Rebuild(); }
    public override void Hidden() { Svc.Agents.Changed -= Rebuild; }

    static (string text, Brush color) Label(AgentState s) => s switch
    {
        AgentState.Working => ("Trabalhando", K.Green),
        AgentState.NeedsInput => ("Precisa de você", K.Orange),
        AgentState.Done => ("Concluído", K.Blue),
        AgentState.Idle => ("Ocioso", K.Dim),
        AgentState.Unavailable => ("Indisponível", K.Red),
        _ => ("Offline", K.Faint)
    };

    void Rebuild()
    {
        _list.Children.Clear();
        if (!Settings.Current.IsOn("ai")) { _list.Children.Add(K.T("Módulo desativado.", 12, K.Dim)); return; }
        foreach (var a in Svc.Agents.Statuses)
        {
            var g = K.Cols("40", "*", "auto");
            var badge = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Background = K.Hex(a.ColorHex), VerticalAlignment = VerticalAlignment.Top };
            badge.Child = K.T(a.Letter, 14, Brushes.Black, true); ((TextBlock)badge.Child).HorizontalAlignment = HorizontalAlignment.Center; ((TextBlock)badge.Child).VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(badge);

            var s = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            var (lbl, col) = Label(a.State);
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(K.T(a.Name, 13, K.Fg, true));
            head.Children.Add(K.T("  " + lbl, 11, col, true).Vc());
            s.Children.Add(head);
            if (a.Message.Length > 0) s.Children.Add(K.T(a.Message, 11, K.Dim));
            if (a.Progress != null)
            {
                var bar = new ProgressBar { Height = 3, Minimum = 0, Maximum = 100, Value = a.Progress.Value, Foreground = K.Cyan, Background = K.CardStrong, BorderThickness = new Thickness(0), Margin = new Thickness(0, 4, 0, 0) };
                s.Children.Add(bar);
            }
            if (a.Note.Length > 0) { var n = K.T(a.Note, 9, K.Faint); n.ToolTip = a.Note; s.Children.Add(n); }
            g.Children.Add(s.At(col: 1));

            var rings = new StackPanel { Orientation = Orientation.Horizontal };
            if (a.UsageSession != null || a.UsageWeekly != null)
            {
                rings.Children.Add(Ring("sessão", a.UsageSession));
                rings.Children.Add(Ring("semana", a.UsageWeekly));
            }
            else rings.Children.Add(K.T("limites: sem fonte", 10, K.Faint).Vc());
            g.Children.Add(rings.At(col: 2));
            var card = K.CardBox(g, 14, new Thickness(10)); card.Margin = new Thickness(0, 0, 0, 6);
            _list.Children.Add(card);
        }
    }

    static UIElement Ring(string cap, double? v)
    {
        var s = new StackPanel { Margin = new Thickness(4, 0, 0, 0) };
        s.Children.Add(new RingGauge { Value = v, Width = 46, Height = 46 });
        var c = K.T(cap, 9, K.Faint); c.HorizontalAlignment = HorizontalAlignment.Center; s.Children.Add(c);
        return s;
    }
}

public sealed class MirrorPage : PageBase
{
    readonly Image _view = new() { Stretch = Stretch.UniformToFill, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform(-1, 1) };
    readonly TextBlock _status = K.T("Câmera desligada", 12, K.Dim), _cam = K.T("", 12, K.Fg, true), _err = K.T("", 11, K.Red);
    readonly Button _toggle;
    List<(string Id, string Name)> _cams = new();
    int _idx;
    bool _busy;

    public MirrorPage()
    {
        var g = K.Cols("*", "220");
        var frame = new Border { CornerRadius = new CornerRadius(16), Background = Brushes.Black, ClipToBounds = true, Child = new Grid { Children = { _view, _status } }, Margin = new Thickness(0, 0, 12, 0) };
        _status.HorizontalAlignment = HorizontalAlignment.Center; _status.VerticalAlignment = VerticalAlignment.Center;
        g.Children.Add(frame);

        var s = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        s.Children.Add(K.T("Mirror", 16, K.Fg, true));
        var nav = K.Cols("auto", "*", "auto"); nav.Margin = new Thickness(0, 8, 0, 8);
        nav.Children.Add(K.IconBtn("\uE76B", "Câmera anterior", () => Cycle(-1), 12, 28, 28));
        _cam.TextAlignment = TextAlignment.Center; nav.Children.Add(_cam.At(col: 1).Vc());
        nav.Children.Add(K.IconBtn("\uE76C", "Próxima câmera", () => Cycle(1), 12, 28, 28).At(col: 2));
        s.Children.Add(nav);
        _toggle = K.TextBtn("Ligar espelho", () => _ = ToggleAsync(), true);
        s.Children.Add(_toggle);
        _err.TextWrapping = TextWrapping.Wrap; s.Children.Add(_err.M(0, 8, 0, 0));
        var priv = K.T("A câmera só fica ativa enquanto o espelho está ligado. Nada é gravado, salvo ou enviado.", 10, K.Faint);
        priv.TextWrapping = TextWrapping.Wrap; s.Children.Add(priv.M(0, 10, 0, 0));
        g.Children.Add(s.At(col: 1));
        Root.Children.Add(g);
    }

    public override bool KeepOpen => Svc.Camera.IsOn;

    public override async void Shown()
    {
        Svc.Camera.FrameUpdated += OnFrame; Svc.Camera.StateChanged += OnState;
        _cams = await Svc.Camera.ListAsync();
        _idx = Math.Max(0, _cams.FindIndex(c => c.Id == Settings.Current.LastCameraId));
        SyncCam(); OnState();
    }

    public override async void Hidden()
    {
        Svc.Camera.FrameUpdated -= OnFrame; Svc.Camera.StateChanged -= OnState;
        await Svc.Camera.StopAsync();   // sai da tela => libera a câmera
    }

    void OnFrame() => _view.Source = Svc.Camera.Frame;

    void OnState()
    {
        bool on = Svc.Camera.IsOn;
        _toggle.Content = K.T(on ? "Desligar espelho" : "Ligar espelho", 12, K.Fg, true);
        _toggle.Background = on ? K.Red : K.Blue;
        _status.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        if (!on) _view.Source = null;
        IslandWindow.Instance?.RefreshMini();
    }

    void SyncCam() { _cam.Text = _cams.Count == 0 ? "Nenhuma câmera encontrada" : _cams[_idx].Name; }

    async void Cycle(int d)
    {
        if (_cams.Count < 2) return;
        _idx = (_idx + d + _cams.Count) % _cams.Count;
        SyncCam();
        if (Svc.Camera.IsOn) { await Svc.Camera.StopAsync(); await ToggleAsync(); }
    }

    async Task ToggleAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            _err.Text = "";
            if (Svc.Camera.IsOn) { await Svc.Camera.StopAsync(); return; }
            if (_cams.Count == 0) { _err.Text = "Nenhuma câmera disponível."; return; }
            Settings.Current.LastCameraId = _cams[_idx].Id; Settings.Apply();
            _status.Text = "Abrindo…";
            var err = await Svc.Camera.StartAsync(_cams[_idx].Id);
            if (err != null) { _err.Text = err; _status.Text = "Câmera desligada"; }
        }
        finally { _busy = false; }
    }
}

public sealed class DictationPage : PageBase
{
    readonly TextBox _text = K.Box("O texto reconhecido aparece aqui…", true);
    readonly TextBlock _status = K.T("", 12, K.Dim), _err = K.T("", 11, K.Red);
    readonly Shapes.Ellipse _rec = new() { Width = 10, Height = 10, Fill = K.Red, Opacity = 0, VerticalAlignment = VerticalAlignment.Center };
    readonly Button _mic;

    public DictationPage()
    {
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new RowDefinition { Height = K.GL("*") });
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var top = K.Cols("auto", "auto", "*");
        _mic = K.IconBtn("\uE720", "Iniciar/parar ditado", () => _ = Svc.Dictation.ToggleAsync(), 20, 48, 40);
        top.Children.Add(_mic);
        top.Children.Add(_rec.At(col: 1).M(8, 0, 6, 0));
        var st = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        st.Children.Add(_status);
        top.Children.Add(st.At(col: 2));
        top.Margin = new Thickness(0, 0, 0, 8);
        Root.Children.Add(top);
        _text.IsReadOnly = true;
        Root.Children.Add(_text.At(row: 1));
        var foot = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        foot.Children.Add(K.TextBtn("Copiar", () => { if (Svc.Dictation.Text.Length > 0) Clipboard.SetText(Svc.Dictation.Text); }));
        foot.Children.Add(K.TextBtn("Colar na janela ativa", () => Svc.Dictation.PasteToActiveWindow(), true).M(6, 0, 0, 0));
        foot.Children.Add(K.TextBtn("Limpar", () => Svc.Dictation.Clear()).M(6, 0, 0, 0));
        _err.TextWrapping = TextWrapping.Wrap; foot.Children.Add(_err.Vc().M(10, 0, 0, 0));
        Root.Children.Add(foot.At(row: 2));
    }

    public override bool KeepOpen => Svc.Dictation.Listening;
    public override void Shown() { Svc.Dictation.Changed += Sync; Sync(); }
    public override void Hidden() { Svc.Dictation.Changed -= Sync; }

    void Sync()
    {
        var d = Svc.Dictation;
        _text.Text = d.Text + (d.Hypothesis.Length > 0 ? (d.Text.Length > 0 ? " " : "") + d.Hypothesis : "");
        _text.ScrollToEnd();
        _status.Text = d.Listening ? $"Ouvindo… ({Settings.Current.DictationLanguage})" : $"Pronto · atalho {Settings.Current.DictationHotkey}";
        _mic.Content = K.Glyph(d.Listening ? "\uE71A" : "\uE720", 20, d.Listening ? K.Red : K.Fg);
        _err.Text = d.Error ?? "";
        if (d.Listening) _rec.BeginAnimation(UIElement.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0.15, 1, TimeSpan.FromMilliseconds(700)) { AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever });
        else { _rec.BeginAnimation(UIElement.OpacityProperty, null); _rec.Opacity = 0; }
        IslandWindow.Instance?.RefreshMini();
    }
}
