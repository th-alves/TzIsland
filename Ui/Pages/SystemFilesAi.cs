using Shapes = System.Windows.Shapes;

namespace TzIsland;

/// <summary>
/// Sistema: no mesmo estilo da Início. Uma caixa com o que se liga/desliga (Bateria, Wi-Fi, Bluetooth, Microfone),
/// outra com os controles deslizantes (Volume, Brilho) e uma terceira, empilhada, com os dispositivos conectados.
/// </summary>
public sealed class SystemPage : PageBase, ICardPage
{
    public string PageId => "system";
    public CardBoard Board { get; }
    public ScrollViewer? Scroller { get; private set; }
    public void Relayout(bool animate = false, bool force = false) => Board.Layout(CardCatalog.For(PageId), animate, force);
    public override IEnumerable<FrameworkElement> RevealTargets =>
        Root.Children.OfType<StackPanel>().Cast<FrameworkElement>().Concat(Board.Cards.Select(c => (FrameworkElement)c.Holder));
    // Bateria
    readonly BatteryGlyph _batGlyph = new() { Width = 30, Height = 16 };
    readonly TextBlock _batPct = K.Num("", 15), _batSub = K.T("", 10.5, K.Muted);
    // Wi-Fi
    readonly Border _wifiIcon = new() { Width = 20, Height = 20 };
    readonly TextBlock _wifiSub = K.T("", 10.5, K.Muted);
    readonly SwitchToggle _wifiSw = new();
    // Bluetooth
    readonly Border _btIcon = new() { Width = 20, Height = 20 };
    readonly TextBlock _btSub = K.T("", 10.5, K.Muted);
    readonly SwitchToggle _btSw = new();
    // Microfone
    readonly Border _micIcon = new() { Width = 20, Height = 20 };
    readonly TextBlock _micSub = K.T("", 10.5, K.Muted), _micState = K.T("", 11, K.Label, true);
    // Controles
    readonly PillSlider _vol = new() { Fill = K.White, Height = 14 };
    readonly TextBlock _volNum = K.Num("", 11.5, K.Label);
    readonly Button _mute;
    readonly StackPanel _bri = new();
    readonly StackPanel _devices = new() { Orientation = Orientation.Horizontal };
    readonly TextBlock _devEmpty = K.T("", 11, K.Muted);
    ScrollViewer? _devScroll;
    bool _devFresh = true;     // próxima montagem é uma "entrada" (varredura dos LEDs desde 0)
    readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromSeconds(20) };

    static Border Circle(UIElement inner)
        => new() { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = K.Track, Child = inner, HorizontalAlignment = HorizontalAlignment.Left };

    static Grid Row(UIElement icon, string title, TextBlock sub, UIElement? right)
    {
        var g = K.Cols("44", "*", "auto");
        g.Margin = new Thickness(0, 0, 0, 5);
        g.Children.Add(icon);
        var tx = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        tx.Children.Add(K.T(title, 12.5, K.Fg, true)); tx.Children.Add(sub.M(0, 1, 0, 0));
        g.Children.Add(tx.At(col: 1));
        if (right != null) { ((FrameworkElement)right).VerticalAlignment = VerticalAlignment.Center; g.Children.Add(right.At(col: 2)); }
        return g;
    }

    static void SetIcon(Border host, string id, Brush stroke) => host.Child = Ico.Make(id, 18, stroke, 1.7);

    public SystemPage()
    {
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new RowDefinition { Height = K.GL("*") });

        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 0, 9) };
        head.Children.Add(Ico.Make("monitor", 15, K.Fg, 1.8));
        head.Children.Add(K.T("Sistema", 13, K.Fg, true).M(8, 0, 0, 0).Vc());
        Root.Children.Add(head);

        Board = new CardBoard(2, new[] { "1.08*", "1*" }, null, true);

        // ---------- Caixa 1: itens ativáveis
        var rows = new StackPanel { Margin = new Thickness(0, 9, 0, 0) };
        var batIcon = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = K.Track, HorizontalAlignment = HorizontalAlignment.Left, Child = _batGlyph };
        _batGlyph.HorizontalAlignment = HorizontalAlignment.Center; _batGlyph.VerticalAlignment = VerticalAlignment.Center; _batGlyph.Width = 22; _batGlyph.Height = 12;
        rows.Children.Add(Row(batIcon, "Bateria", _batSub, _batPct));
        rows.Children.Add(Row(Circle(_wifiIcon), "Wi-Fi", _wifiSub, _wifiSw));
        rows.Children.Add(Row(Circle(_btIcon), "Bluetooth", _btSub, _btSw));
        rows.Children.Add(Row(Circle(_micIcon), "Microfone", _micSub, _micState));
        foreach (var sw in new[] { _wifiSw, _btSw }) { sw.RenderTransformOrigin = new Point(1, 0.5); sw.RenderTransform = new ScaleTransform(0.85, 0.85); }
        _wifiIcon.HorizontalAlignment = _btIcon.HorizontalAlignment = _micIcon.HorizontalAlignment = HorizontalAlignment.Center;
        _wifiIcon.VerticalAlignment = _btIcon.VerticalAlignment = _micIcon.VerticalAlignment = VerticalAlignment.Center;

        var g1 = K.Rows("auto", "*");
        g1.Children.Add(FK.Header(Ico.Make("wifi", 12, K.Label, 1.9), "Conexões"));
        g1.Children.Add(rows.At(row: 1));
        var c1 = FK.Card(g1, 20);
        _ = new MovableCard("system.connections", "Conexões", c1, 1, 2);

        // ---------- Caixa 2: controles deslizantes
        var ctl = new StackPanel { Margin = new Thickness(0, 9, 0, 0) };
        _mute = K.IconBtn("\uE767", "Silenciar", () => { Svc.Audio.Muted = !Svc.Audio.Muted; SyncVol(); }, 13, 26, 24, K.Fg);
        ctl.Children.Add(CtlRow(_mute, "Volume", _vol, _volNum, 0));
        ctl.Children.Add(_bri);
        var g2 = K.Rows("auto", "*");
        g2.Children.Add(FK.Header(Ico.Make("sun", 12, K.Label, 1.9), "Controles"));
        g2.Children.Add(ctl.At(row: 1));
        var c2 = FK.Card(g2, 20);
        _ = new MovableCard("system.controls", "Controles", c2);

        // ---------- Caixa 3: dispositivos conectados (itens individuais empilhados)
        var g3 = K.Rows("auto", "*");
        g3.Children.Add(FK.Header(Ico.Make("headphones", 12, K.Label, 1.9), "Dispositivos"));
        // uma fileira só, rolando para o lado com a roda do mouse quando há muitos aparelhos
        var sc = new ScrollViewer { Content = _devices, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(-4, 6, -4, 0), VerticalAlignment = VerticalAlignment.Center };
        sc.PreviewMouseWheel += (_, e) => { if (sc.ScrollableWidth <= 0) return; sc.ScrollToHorizontalOffset(sc.HorizontalOffset - e.Delta * 0.5); e.Handled = true; };
        void Fade()
        {
            if (sc.ScrollableWidth <= 0.5) { sc.OpacityMask = null; return; }
            double w = Math.Max(1, sc.ActualWidth), f = 22 / w;
            bool l = sc.HorizontalOffset > 0.5, r = sc.HorizontalOffset < sc.ScrollableWidth - 0.5;
            var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            g.GradientStops.Add(new GradientStop(l ? Colors.Transparent : Colors.Black, 0));
            g.GradientStops.Add(new GradientStop(Colors.Black, f));
            g.GradientStops.Add(new GradientStop(Colors.Black, 1 - f));
            g.GradientStops.Add(new GradientStop(r ? Colors.Transparent : Colors.Black, 1));
            sc.OpacityMask = g;
        }
        sc.ScrollChanged += (_, _) => Fade();
        sc.SizeChanged += (_, _) => { Fade(); FitTiles(); };
        _devScroll = sc;
        g3.Children.Add(sc.At(row: 1));
        g3.SizeChanged += (_, _) => FitTiles();
        var c3 = FK.Card(g3, 20);
        _ = new MovableCard("system.devices", "Dispositivos", c3);

        // A grade fica num ScrollViewer: se o usuário trouxer mais cartões, a página rola; se sobrar espaço, a última linha o preenche.
        var sv = K.Scroll(Board.Host);
        Scroller = sv;
        sv.SizeChanged += (_, _) => Board.Host.MinHeight = sv.ActualHeight;
        Root.Children.Add(sv.At(row: 1));

        _vol.Changed += v => { Svc.Audio.Volume = (float)v; if (Svc.Audio.Muted && v > 0) Svc.Audio.Muted = false; _volNum.Text = Math.Round(v * 100).ToString(CultureInfo.InvariantCulture); };
        _wifiSw.Toggled += async on => { _wifiSw.IsHitTestVisible = false; await Svc.Network.SetWifiAsync(on); await Task.Delay(700); await Svc.Network.RefreshAsync(); SyncWifi(); _wifiSw.IsHitTestVisible = true; };
        _btSw.Toggled += async on => { _btSw.IsHitTestVisible = false; await Svc.Bluetooth.SetAsync(on); await Task.Delay(500); await Svc.Bluetooth.RefreshAsync(); SyncBt(); _btSw.IsHitTestVisible = true; };
        _t.Tick += async (_, _) => await LoadAsync();
        SyncBat(); SyncWifi(); SyncBt(); SyncMic(); SyncVol();
    }

    public override async void Shown()
    {
        Relayout();
        Svc.Battery.Changed += SyncBat; Svc.Audio.VolumeChanged += SyncVol; Svc.Audio.MicChanged += SyncMic;
        SyncBat(); SyncVol(); SyncMic();
        _t.Start();
        BuildDevices();
        await LoadAsync();
    }

    public override void Hidden()
    {
        Svc.Battery.Changed -= SyncBat; Svc.Audio.VolumeChanged -= SyncVol; Svc.Audio.MicChanged -= SyncMic;
        _t.Stop();
        _devFresh = true;
    }

    void SyncBat()
    {
        var b = Svc.Battery;
        _batGlyph.Set(b.HasBattery ? Math.Max(0, b.Percent) : 100, b.HasBattery && b.Charging);
        _batPct.Text = b.HasBattery ? $"{b.Percent}%" : "";
        _batSub.Text = b.HasBattery ? (b.Charging ? "Carregando" : "Operando na bateria") : "Sem bateria · ligado na tomada";
    }

    void SyncWifi()
    {
        var n = Svc.Network; bool? on = n.WifiOn;
        SetIcon(_wifiIcon, on == true ? "wifi" : "wifi-off", on == true ? K.White : K.Muted);
        _wifiSub.Text = on == null ? "Indisponível neste PC" : on == false ? "Desligado" : n.Connected && n.Ssid.Length > 0 ? n.Ssid : "Sem conexão";
        _wifiSw.IsOn = on == true; _wifiSw.Opacity = on == null ? 0.35 : 1; _wifiSw.IsHitTestVisible = on != null;
    }

    void SyncBt()
    {
        var b = Svc.Bluetooth; bool? on = b.RadioOn;
        SetIcon(_btIcon, "bluetooth", on == true ? K.White : K.Muted);
        _btSub.Text = on == null ? "Indisponível neste PC" : on == false ? "Desligado"
            : b.Devices.Count == 0 ? "Ligado · nenhum dispositivo conectado" : b.Devices.Count == 1 ? "1 dispositivo conectado" : $"{b.Devices.Count} dispositivos conectados";
        _btSw.IsOn = on == true; _btSw.Opacity = on == null ? 0.35 : 1; _btSw.IsHitTestVisible = on != null;
    }

    void SyncVol()
    {
        _vol.Value = Svc.Audio.Volume;
        _volNum.Text = Math.Round(Svc.Audio.Volume * 100).ToString(CultureInfo.InvariantCulture);
        bool off = Svc.Audio.Muted || Svc.Audio.Volume <= 0.001f;
        _mute.Content = Ico.Make(off ? "volume-off" : Svc.Audio.Volume < 0.4f ? "volume-low" : "volume", 17, off ? K.Muted : K.Fg, 1.7);
    }

    void SyncMic()
    {
        var a = Svc.Audio; bool use = a.MicInUse && Settings.Current.MonitorMicrophone;
        SetIcon(_micIcon, "mic", use ? K.Orange : K.Muted);
        _micState.Text = !Settings.Current.MonitorMicrophone ? "Desativado" : use ? "Em uso" : "Livre";
        _micState.Foreground = use ? K.Orange : K.Label;
        _micSub.Text = !Settings.Current.MonitorMicrophone ? "Monitoramento desativado" : use ? string.Join(", ", a.MicApps) : "Nenhum app gravando áudio";
    }

    async Task LoadAsync()
    {
        await Svc.Network.RefreshAsync(); SyncWifi();
        await Svc.Bluetooth.RefreshAsync(); SyncBt(); BuildDevices();
        if (_bri.Children.Count == 0) await Svc.Brightness.RefreshAsync();
        BuildDisplays();
    }

    /// <summary>Escolhe o layout dos blocos pela altura que sobra no card (nome e % embaixo só quando cabem).</summary>
    void FitTiles()
    {
        // a lista é centralizada (mede só o conteúdo), então a altura livre vem da linha do card que a contém
        if (_devScroll?.Parent is not Grid g || g.RowDefinitions.Count < 2 || g.RowDefinitions[1].ActualHeight <= 0) return;
        bool compact = g.RowDefinitions[1].ActualHeight - 6 < 112;
        foreach (var t in _devices.Children.OfType<DeviceTile>()) t.SetCompact(compact);
    }

    void BuildDevices()
    {
        var list = Svc.Bluetooth.Devices;
        if (list.Count == 0)
        {
            _devices.Children.Clear();
            _devEmpty.Text = Svc.Bluetooth.RadioOn == false ? "Bluetooth desligado" : "Nenhum dispositivo conectado";
            _devEmpty.Margin = new Thickness(0, 4, 0, 0);
            _devices.Children.Add(_devEmpty);
            _devFresh = true;
            return;
        }
        _devices.Children.Remove(_devEmpty);
        // reaproveita os blocos existentes (só animam o que mudou); remove quem desconectou e adiciona quem chegou
        var old = _devices.Children.OfType<DeviceTile>().ToDictionary(t => t.Id);
        foreach (var t in old.Values.Where(t => list.All(d => d.Id != t.Id)).ToList()) _devices.Children.Remove(t);
        int i = 0;
        foreach (var d in list)
        {
            double delay = 0.12 + i * 0.07;
            if (old.TryGetValue(d.Id, out var tile))
            {
                tile.Update(d, _devFresh, delay);
                if (_devFresh) Motion.Reveal(tile, delay - 0.08, 10, 0.9);
            }
            else
            {
                tile = new DeviceTile(d);
                _devices.Children.Insert(Math.Min(i, _devices.Children.Count), tile);
                tile.Update(d, true, delay);
                Motion.Reveal(tile, delay - 0.08, 10, 0.9);
            }
            i++;
        }
        FitTiles();
        _devFresh = false;
    }

    void BuildDisplays()
    {
        _bri.Children.Clear();
        var devs = Svc.Brightness.Devices;
        if (devs.Count == 0)
        {
            var t = K.T("Brilho indisponível: nenhum monitor com controle por software (DDC/CI).", 10.5, K.Muted);
            t.TextWrapping = TextWrapping.Wrap; t.Margin = new Thickness(0, 8, 0, 0);
            _bri.Children.Add(t);
            return;
        }
        foreach (var d in devs)
        {
            // linha fina separando cada tela; com mais de uma, o nome vem em caixa alta discreta
            _bri.Children.Add(new Border { Height = 1, Background = K.Stroke, Margin = new Thickness(0, 11, 0, 0) });
            if (devs.Count > 1)
            {
                var n = K.Cap(d.Name, 8.5, K.Muted);
                n.Margin = new Thickness(2, 9, 0, 0); n.ToolTip = d.Name;
                _bri.Children.Add(n);
            }
            _bri.Children.Add(Slider("sun", "Brilho", d.Min, d.Max, d.Cur, v => Svc.Brightness.SetBrightness(d, v), devs.Count > 1 ? 8 : 11));
            if (d.HasContrast) _bri.Children.Add(Slider("contrast", "Contraste", d.CMin, d.CMax, d.CCur, v => Svc.Brightness.SetContrast(d, v), 9));
        }
    }

    /// <summary>Linha padrão dos controles: ícone, rótulo, barra e valor, sempre nas mesmas colunas.</summary>
    static Grid CtlRow(UIElement icon, string label, PillSlider s, TextBlock num, double top)
    {
        var g = K.Cols("26", "62", "*", "32");
        g.Margin = new Thickness(0, top, 0, 0);
        ((FrameworkElement)icon).VerticalAlignment = VerticalAlignment.Center;
        ((FrameworkElement)icon).HorizontalAlignment = HorizontalAlignment.Left;
        g.Children.Add(icon);
        g.Children.Add(K.T(label, 11.5, K.Dim).At(col: 1).Vc());
        g.Children.Add(s.At(col: 2).M(2, 0, 10, 0).Vc());
        num.HorizontalAlignment = HorizontalAlignment.Right;
        g.Children.Add(num.At(col: 3).Vc());
        return g;
    }

    static UIElement Slider(string icon, string label, uint min, uint max, uint cur, Action<uint> set, double top)
    {
        var s = new PillSlider { Value = max > min ? (cur - min) / (double)(max - min) : 0, ToolTip = label, Fill = K.White, Height = 14 };
        var num = K.Num(Math.Round(s.Value * 100).ToString(CultureInfo.InvariantCulture), 11.5, K.Label);
        DateTime last = DateTime.MinValue;
        s.Changed += v =>
        {
            num.Text = Math.Round(v * 100).ToString(CultureInfo.InvariantCulture);
            if ((DateTime.Now - last).TotalMilliseconds < 90) return;
            last = DateTime.Now;
            uint val = (uint)(min + v * (max - min));
            Task.Run(() => set(val));
        };
        return CtlRow(Ico.Make(icon, 15, K.Label, 1.7), label, s, num, top);
    }
}

/// <summary>
/// File Tray Page: Quick shelf for drag & drop file stash, download monitoring,
/// screenshot intake and fast Explorer access.
/// </summary>
public sealed class FilesPage : PageBase
{
    readonly WrapPanel _tiles = new();
    readonly Button _dl, _ss;
    readonly TextBlock _hint = K.T("Arraste e solte arquivos aqui para guardá-los na bandeja", 12, K.Faint);

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
        _dl.Background = Settings.Current.WatchDownloads ? K.StrokeHi : K.Card;
        _ss.Background = Settings.Current.WatchScreenshots ? K.StrokeHi : K.Card;
        _dl.ToolTip = "Monitoramento automático da pasta Downloads: " + (Settings.Current.WatchDownloads ? "ativo" : "inativo");
        _ss.ToolTip = "Monitoramento automático da pasta de capturas: " + (Settings.Current.WatchScreenshots ? "ativo" : "inativo");
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
        var s = new StackPanel { Width = 80 };
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

        var tb = new Border { Width = 60, Height = 50, CornerRadius = new CornerRadius(10), Background = K.CardStrong, Child = thumb, HorizontalAlignment = HorizontalAlignment.Center };
        if (thumb is FrameworkElement thumbFe) K.RoundClip(thumbFe, 9);
        s.Children.Add(tb);
        var name = K.T(f.Name, 10, K.Fg); name.TextAlignment = TextAlignment.Center; name.Margin = new Thickness(0, 3, 0, 0);
        s.Children.Add(name);
        if (f.Auto) { var o = K.T(f.Origin == "Downloads" ? "↓ novo" : "▣ nova", 9, K.Sky); o.HorizontalAlignment = HorizontalAlignment.Center; s.Children.Add(o); }

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
        var show = new MenuItem { Header = "Mostrar no Explorador" }; show.Click += (_, _) => Process.Start("explorer.exe", "/select,\"" + f.Path + "\"");
        var rem = new MenuItem { Header = "Remover da bandeja (não apaga o arquivo)" }; rem.Click += (_, _) => Svc.Files.Remove(f);
        menu.Items.Add(open); menu.Items.Add(show); menu.Items.Add(rem);
        btn.ContextMenu = menu;
        return btn;
    }
}

/// <summary>
/// AI Agent Monitor: Real-time telemetry dashboard tracking connected autonomous AI workers,
/// execution phases, progress indicators, and official quota usage metrics.
/// </summary>
public sealed class AiPage : PageBase
{
    readonly StackPanel _list = new();
    readonly TextBlock _foot = K.T("Métricas de uso de IA refletem estritamente os dados reportados pela infraestrutura.", 10, K.Faint);

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
        AgentState.Working => ("Em execução", K.Green),
        AgentState.NeedsInput => ("Aguardando você", K.Orange),
        AgentState.Done => ("Concluído", K.Sky),
        AgentState.Idle => ("Disponível", K.Dim),
        AgentState.Unavailable => ("Indisponível", K.Red),
        _ => ("Offline", K.Faint)
    };

    void Rebuild()
    {
        _list.Children.Clear();
        if (!Settings.Current.IsOn("ai")) { _list.Children.Add(K.T("Módulo de Agentes de IA desativado nas Configurações.", 12, K.Dim)); return; }
        foreach (var a in Svc.Agents.Statuses)
        {
            var g = K.Cols("40", "*", "auto");
            var badge = Brand.BadgeFor(a.Name, a.Letter, a.ColorHex, 34);
            badge.VerticalAlignment = VerticalAlignment.Top;
            g.Children.Add(badge);

            var s = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            var (lbl, col) = Label(a.State);
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(K.T(a.Name, 13, K.Fg, true));
            head.Children.Add(K.T("  ·  " + lbl, 11, col, true).Vc());
            s.Children.Add(head);
            if (a.Message.Length > 0) s.Children.Add(K.T(a.Message, 11, K.Dim));
            if (a.Progress != null)
            {
                var bar = new ProgressBar
                {
                    Height = 2.5,
                    Minimum = 0,
                    Maximum = 100,
                    Value = a.Progress.Value,
                    Foreground = K.AccentGradient,
                    Background = K.Track,
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(0, 4, 0, 0)
                };
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
            else rings.Children.Add(K.T("limites: sem telemetria", 10, K.Faint).Vc());
            g.Children.Add(rings.At(col: 2));

            var card = K.CardBox(g, 14, new Thickness(12));
            card.Margin = new Thickness(0, 0, 0, 6);
            _list.Children.Add(card);
        }
    }

    static UIElement Ring(string cap, double? v)
    {
        var s = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
        s.Children.Add(new RingGauge { Value = v, Width = 46, Height = 46 });
        var c = K.T(cap, 9, K.Faint); c.HorizontalAlignment = HorizontalAlignment.Center; s.Children.Add(c);
        return s;
    }
}

/// <summary>
/// Camera Mirror: Quick private video check with low latency, hardware feed cycling
/// and automatic sensor liberation upon dismissal.
/// </summary>
public sealed class MirrorPage : PageBase
{
    readonly Image _view = new() { Stretch = Stretch.UniformToFill, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform(-1, 1) };
    readonly TextBlock _status = K.T("Câmera inativa", 12, K.Dim), _cam = K.T("", 12, K.Fg, true), _err = K.T("", 11, K.Red);
    readonly Button _toggle;
    List<(string Id, string Name)> _cams = new();
    int _idx;
    bool _busy;

    public MirrorPage()
    {
        var g = K.Cols("*", "230");
        var mirrorInner = K.RoundClip(new Grid { Children = { _view, _status } }, 15);
        var frame = new Border { CornerRadius = new CornerRadius(16), Background = K.Surface, BorderBrush = K.Stroke, BorderThickness = new Thickness(1), Child = mirrorInner, Margin = new Thickness(0, 0, 12, 0) };
        _status.HorizontalAlignment = HorizontalAlignment.Center; _status.VerticalAlignment = VerticalAlignment.Center;
        g.Children.Add(frame);

        var s = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        s.Children.Add(FK.Header("\uE722", "ESPELHO DE CÂMERA"));
        var nav = K.Cols("auto", "*", "auto"); nav.Margin = new Thickness(0, 8, 0, 8);
        nav.Children.Add(K.IconBtn("\uE76B", "Câmera anterior", () => Cycle(-1), 12, 28, 28));
        _cam.TextAlignment = TextAlignment.Center; nav.Children.Add(_cam.At(col: 1).Vc());
        nav.Children.Add(K.IconBtn("\uE76C", "Próxima câmera", () => Cycle(1), 12, 28, 28).At(col: 2));
        s.Children.Add(nav);
        _toggle = K.TextBtn("Ativar câmera", () => _ = ToggleAsync(), true);
        s.Children.Add(_toggle);
        _err.TextWrapping = TextWrapping.Wrap; s.Children.Add(_err.M(0, 8, 0, 0));
        var priv = K.T("A câmera é liberada imediatamente ao sair desta tela. Nenhum dado é salvo.", 10, K.Faint);
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
        await Svc.Camera.StopAsync();
    }

    void OnFrame() => _view.Source = Svc.Camera.Frame;

    void OnState()
    {
        bool on = Svc.Camera.IsOn;
        _toggle.Content = K.T(on ? "Desativar câmera" : "Ativar câmera", 12, on ? K.Fg : K.Ink, true);
        _toggle.Background = on ? K.Red : K.White;
        _status.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        if (!on) _view.Source = null;
        IslandWindow.Instance?.RefreshMini();
    }

    void SyncCam() { _cam.Text = _cams.Count == 0 ? "Nenhum dispositivo encontrado" : _cams[_idx].Name; }

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
            if (_cams.Count == 0) { _err.Text = "Nenhuma câmera detectada."; return; }
            Settings.Current.LastCameraId = _cams[_idx].Id; Settings.Apply();
            _status.Text = "Iniciando feed…";
            var err = await Svc.Camera.StartAsync(_cams[_idx].Id);
            if (err != null) { _err.Text = err; _status.Text = "Câmera inativa"; }
        }
        finally { _busy = false; }
    }
}

/// <summary>
/// Voice Dictation Page: Hands-free enterprise speech recognition with live hypotheses,
/// pulsing audio indicators, and active window injection.
/// </summary>
public sealed class DictationPage : PageBase
{
    readonly TextBox _text = K.Box("O áudio reconhecido aparecerá aqui em tempo real…", true);
    readonly TextBlock _status = K.T("", 12, K.Dim), _err = K.T("", 11, K.Red);
    readonly Shapes.Ellipse _rec = new() { Width = 10, Height = 10, Fill = K.Red, Opacity = 0, VerticalAlignment = VerticalAlignment.Center };
    readonly Button _mic;

    public DictationPage()
    {
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new RowDefinition { Height = K.GL("*") });
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var top = K.Cols("auto", "auto", "*");
        _mic = K.IconBtn("\uE720", "Iniciar/parar ditado por voz", () => _ = Svc.Dictation.ToggleAsync(), 20, 48, 40);
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
        _status.Text = d.Listening ? $"Ouvindo… ({Settings.Current.DictationLanguage})" : $"Pronto · Atalho global {Settings.Current.DictationHotkey}";
        _mic.Content = K.Glyph(d.Listening ? "\uE71A" : "\uE720", 20, d.Listening ? K.Red : K.Fg);
        _err.Text = d.Error ?? "";
        // Indicador "gravando" no relógio único (pausável) em vez de storyboard infinito.
        if (d.Listening)
        {
            if (!MotionEngine.IsAnimating(_rec, UIElement.OpacityProperty))
                MotionEngine.Wave(_rec, UIElement.OpacityProperty, t => 0.575 + 0.425 * Math.Sin(Math.PI * t / 0.7));
        }
        else MotionEngine.Jump(_rec, UIElement.OpacityProperty, 0);
        IslandWindow.Instance?.RefreshMini();
    }
}
