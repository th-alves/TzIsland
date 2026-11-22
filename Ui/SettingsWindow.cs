using System.Windows.Interop;
using Shapes = System.Windows.Shapes;

namespace TzIsland;

/// <summary>
/// Central de Configurações no mesmo estilo da ilha: barra lateral, cartões índigo, switches e sliders coral.
/// </summary>
public sealed class SettingsWindow : Window
{
    static SettingsWindow? _inst;
    public static SettingsWindow ShowSingle()
    {
        if (_inst == null) { _inst = new SettingsWindow(); _inst.Closed += (_, _) => _inst = null; }
        _inst.Show();
        if (_inst.WindowState == WindowState.Minimized) _inst.WindowState = WindowState.Normal;
        return _inst;
    }

    static AppSettings S => Settings.Current;

    readonly Grid _pageHost = new();
    readonly List<(Button btn, TextBlock glyph, TextBlock label, Func<StackPanel> build)> _nav = new();
    int _tab = -1;

    public SettingsWindow()
    {
        Title = "TzIsland — Configurações";
        Width = 900; Height = 680; MinWidth = 740; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = K.Hex("#FF050505");
        Foreground = K.Fg;
        FontFamily = K.Font;
        UseLayoutRounding = true;
        SourceInitialized += (_, _) =>
        {
            try { int on = 1; Native.DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref on, 4); } catch { }   // barra de título escura
        };

        var root = K.Cols("224", "*");

        // ---- Barra lateral
        var side = new Border { Background = K.Hex("#FF0A0A0B"), BorderBrush = K.Stroke, BorderThickness = new Thickness(0, 0, 1, 0) };
        var sp = new StackPanel { Margin = new Thickness(14, 22, 14, 14) };
        var brand = new StackPanel { Margin = new Thickness(8, 0, 0, 18) };
        brand.Children.Add(FK.Mono("TZISLAND", 15, K.Fg));
        brand.Children.Add(FK.Mono("CONFIGURAÇÕES", 9.5, K.Accent, true).M(0, 2, 0, 0));
        sp.Children.Add(brand);

        AddTab(sp, "\uE706", "Aparência", Appearance);
        AddTab(sp, "\uE713", "Comportamento", Behavior);
        AddTab(sp, "\uE772", "Módulos", ModulesTab);
        AddTab(sp, "\uE715", "Notificações", Notifs);
        AddTab(sp, "\uE71B", "Integrações", Integrations);
        AddTab(sp, "\uE765", "Atalhos", Hotkeys);
        AddTab(sp, "\uE72E", "Privacidade", Privacy);
        side.Child = sp;
        root.Children.Add(side);

        _pageHost.Margin = new Thickness(26, 8, 8, 8);
        root.Children.Add(_pageHost.At(col: 1));
        Content = root;
        SelectTab(0);
    }

    void AddTab(StackPanel host, string glyph, string title, Func<StackPanel> build)
    {
        int idx = _nav.Count;
        var g = K.Glyph(glyph, 14, K.Label);
        g.Width = 22; g.HorizontalAlignment = HorizontalAlignment.Left;
        var t = K.T(title, 13, K.Label, true);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Width = 168 };
        row.Children.Add(g); row.Children.Add(t.Vc());
        var b = new Button { Style = K.Ghost, Content = row, Height = 40, Margin = new Thickness(0, 0, 0, 4), Tag = new CornerRadius(12), Padding = new Thickness(12, 0, 8, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
        b.Click += (_, _) => SelectTab(idx);
        host.Children.Add(b);
        _nav.Add((b, g, t, build));
    }

    void SelectTab(int i)
    {
        if (i == _tab) return;
        _tab = i;
        for (int k = 0; k < _nav.Count; k++)
        {
            bool on = k == i;
            _nav[k].btn.Background = on ? K.White : K.Clear;
            _nav[k].glyph.Foreground = on ? K.Ink : K.Label;
            _nav[k].label.Foreground = on ? K.Ink : K.Label;
        }
        var body = _nav[i].build();
        body.Margin = new Thickness(0, 6, 18, 28);
        var sv = K.Scroll(body);
        _pageHost.Children.Clear();
        _pageHost.Children.Add(sv);
        K.FadeIn(sv, 0, 14);
    }

    // ---------------- Componentes ----------------
    static readonly Dictionary<StackPanel, StackPanel> Cards = new();
    static StackPanel Panel() => new();
    static StackPanel Host(StackPanel p) => Cards.TryGetValue(p, out var c) ? c : p;

    static void AddRow(StackPanel p, UIElement row)
    {
        var h = Host(p);
        if (!ReferenceEquals(h, p) && h.Children.Count > 0)
            h.Children.Add(new Border { Height = 1, Background = K.Stroke, Margin = new Thickness(0, 0, 0, 0) });
        h.Children.Add(row);
    }

    static void Section(StackPanel p, string t)
    {
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 18, 0, 8) };
        head.Children.Add(new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), Background = K.Accent, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 9, 0) });
        head.Children.Add(FK.Mono(t.ToUpperInvariant(), 11, K.Label, true).Vc());
        p.Children.Add(head);
        var inner = new StackPanel();
        p.Children.Add(K.CardBox(inner, 16, new Thickness(18, 4, 18, 8)));
        Cards[p] = inner;
    }

    static void Note(StackPanel p, string t)
    {
        var n = K.T(t, 11.5, K.Muted);
        n.TextWrapping = TextWrapping.Wrap; n.TextTrimming = TextTrimming.None; n.Margin = new Thickness(0, 8, 0, 6);
        Host(p).Children.Add(n);
    }

    static void Check(StackPanel p, string label, Func<bool> get, Action<bool> set)
    {
        var g = K.Cols("*", "auto");
        g.Margin = new Thickness(0, 12, 0, 12);
        var l = K.T(label, 13, K.Fg); l.TextWrapping = TextWrapping.Wrap; l.TextTrimming = TextTrimming.None; l.Margin = new Thickness(0, 0, 16, 0);
        g.Children.Add(l.Vc());
        var sw = new SwitchToggle(); sw.SnapTo(get());
        sw.Toggled += on => { set(on); Settings.Apply(); };
        g.Children.Add(sw.At(col: 1));
        AddRow(p, g);
    }

    static void Slide(StackPanel p, string label, double min, double max, Func<double> get, Action<double> set, string fmt = "0")
    {
        var box = new StackPanel { Margin = new Thickness(0, 12, 0, 12) };
        var top = K.Cols("*", "auto");
        var l = K.T(label, 13, K.Fg); l.TextTrimming = TextTrimming.None;
        var v = FK.Mono(get().ToString(fmt, CultureInfo.InvariantCulture), 12, K.Accent, true);
        top.Children.Add(l); top.Children.Add(v.At(col: 1));
        box.Children.Add(top);
        var s = new PillSlider { Value = max > min ? (get() - min) / (max - min) : 0, Margin = new Thickness(0, 6, 0, 0) };
        var apply = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(70) };
        apply.Tick += (_, _) => { apply.Stop(); Settings.Apply(); };
        s.Changed += x =>
        {
            double val = min + x * (max - min);
            if (fmt == "0") val = Math.Round(val);
            set(val);
            v.Text = val.ToString(fmt, CultureInfo.InvariantCulture);
            apply.Stop(); apply.Start();
        };
        box.Children.Add(s);
        AddRow(p, box);
    }

    static void Combo<T>(StackPanel p, string label, (string, T)[] items, Func<T> get, Action<T> set) where T : notnull
    {
        var box = new StackPanel { Margin = new Thickness(0, 12, 0, 6) };
        var l = K.T(label, 13, K.Fg); l.TextTrimming = TextTrimming.None;
        box.Children.Add(l);
        var wrap = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        var opts = new List<(Button b, TextBlock t, T v)>();
        void Paint()
        {
            foreach (var o in opts)
            {
                bool on = EqualityComparer<T>.Default.Equals(o.v, get());
                o.b.Background = on ? K.White : K.CardStrong;
                o.t.Foreground = on ? K.Ink : K.Label;
            }
        }
        foreach (var (text, value) in items)
        {
            var tb = K.T(text, 12, K.Label, true); tb.TextTrimming = TextTrimming.None;
            var b = new Button { Style = K.Ghost, Content = tb, Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(0, 0, 8, 8), Tag = new CornerRadius(10) };
            var val = value;
            b.Click += (_, _) => { set(val); Paint(); Settings.Apply(); };
            opts.Add((b, tb, value)); wrap.Children.Add(b);
        }
        Paint();
        box.Children.Add(wrap);
        AddRow(p, box);
    }

    static TextBox Text(StackPanel p, string label, Func<string> get, Action<string> set, bool multiline = false)
    {
        var box = new StackPanel { Margin = new Thickness(0, 12, 0, 12) };
        var l = K.T(label, 12.5, K.Dim); l.TextTrimming = TextTrimming.None; l.TextWrapping = TextWrapping.Wrap;
        box.Children.Add(l);
        var t = K.Box("", multiline);
        t.Text = get(); t.Margin = new Thickness(0, 7, 0, 0); t.Padding = new Thickness(12, 9, 12, 9); t.FontSize = 13;
        if (multiline) t.MinHeight = 76;
        t.LostFocus += (_, _) => { set(t.Text); Settings.Apply(); };
        box.Children.Add(t);
        AddRow(p, box);
        return t;
    }

    static Button Btn(StackPanel p, string text, Action click, bool danger = false)
    {
        var b = K.TextBtn(text, click, false, 12.5);
        b.Padding = new Thickness(16, 9, 16, 9);
        b.Margin = new Thickness(0, 10, 8, 8);
        b.HorizontalAlignment = HorizontalAlignment.Left;
        b.Background = danger ? K.Hex("#33FF453A") : K.CardStrong;
        if (danger) b.Content = K.T(text, 12.5, K.Red, true);
        Host(p).Children.Add(b);
        return b;
    }

    // ---- Tabs ----
    StackPanel Appearance()
    {
        var p = Panel();
        Section(p, "Estilo da Ilha");
        Combo(p, "Formato da ilha", new[] { ("Clássico", IslandStyle.Classic), ("Flutuante", IslandStyle.Floating) }, () => S.Style, v => S.Style = v);
        Combo(p, "Aparência visual", new[] { ("Liquid Glass", IslandLook.LiquidGlass), ("Dark Glass", IslandLook.DarkGlass), ("Solid Black", IslandLook.SolidBlack) }, () => S.Look, v => S.Look = v);
        Slide(p, "Opacidade do vidro", 0.3, 1, () => S.Opacity, v => S.Opacity = v, "0.00");
        Slide(p, "Intensidade do desfoque", 0, 60, () => S.BlurAmount, v => S.BlurAmount = v);
        Note(p, "O desfoque captura realisticamente a área atrás da ilha no momento da expansão.");

        Section(p, "Dimensões");
        Slide(p, "Largura expandida", 460, 900, () => S.ExpandedWidth, v => S.ExpandedWidth = v);
        Slide(p, "Altura expandida", 240, 520, () => S.ExpandedHeight, v => S.ExpandedHeight = v);
        Slide(p, "Largura recolhida", 140, 360, () => S.CollapsedWidth, v => S.CollapsedWidth = v);
        Slide(p, "Altura recolhida", 28, 56, () => S.CollapsedHeight, v => S.CollapsedHeight = v);

        Section(p, "Posicionamento");
        Combo(p, "Alinhamento na tela", new[] { ("Centro", IslandAnchor.TopCenter), ("Esquerda", IslandAnchor.TopLeft), ("Direita", IslandAnchor.TopRight), ("Livre (arrastar)", IslandAnchor.Custom) }, () => S.Anchor, v => S.Anchor = v);
        var screens = System.Windows.Forms.Screen.AllScreens.Select((s, i) => ($"Monitor {i + 1}{(s.Primary ? " (principal)" : "")}", i)).ToArray();
        Combo(p, "Monitor ativo", screens, () => Math.Min(S.Monitor, screens.Length - 1), v => S.Monitor = v);
        Check(p, "Permitir arrastar para reposicionar", () => S.AllowDrag, v => S.AllowDrag = v);

        Section(p, "Efeitos e Fluidez");
        Check(p, "Animações físicas baseadas em molas", () => S.Animations, v => S.Animations = v);
        Check(p, "Desfoque de fundo com a arte do álbum", () => S.MediaBlurBackground, v => S.MediaBlurBackground = v);
        Btn(p, "Restaurar layout padrão", () => { var d = new AppSettings(); S.Style = d.Style; S.Look = d.Look; S.Opacity = d.Opacity; S.BlurAmount = d.BlurAmount; S.ExpandedWidth = d.ExpandedWidth; S.ExpandedHeight = d.ExpandedHeight; S.CollapsedWidth = d.CollapsedWidth; S.CollapsedHeight = d.CollapsedHeight; S.Anchor = IslandAnchor.TopCenter; Settings.Apply(); Close(); ShowSingle(); });
        return p;
    }

    StackPanel Behavior()
    {
        var p = Panel();
        Section(p, "Interatividade");
        Check(p, "Expandir ao passar o cursor do mouse", () => S.ExpandOnHover, v => S.ExpandOnHover = v);
        Slide(p, "Tempo de espera do hover (ms)", 0, 600, () => S.HoverDelayMs, v => S.HoverDelayMs = (int)v);
        Check(p, "Expandir ao clicar", () => S.ExpandOnClick, v => S.ExpandOnClick = v);
        Section(p, "Recolher");
        Combo(p, "Animação ao recolher", new[] { ("Instantânea", CollapseMode.Instant), ("Rápida", CollapseMode.Fast), ("Suave", CollapseMode.Smooth) }, () => S.CollapseSpeed, v => S.CollapseSpeed = v);
        Slide(p, "Atraso ao tirar o mouse (ms)", 0, 2000, () => S.CollapseDelayMs, v => S.CollapseDelayMs = (int)v);
        Slide(p, "Recolher por inatividade (s, 0 = nunca)", 0, 30, () => S.IdleCollapseSeconds, v => S.IdleCollapseSeconds = v);
        Note(p, "O atraso conta a partir do momento em que o mouse sai da ilha. Com 0 ms ela recolhe na hora.");

        Section(p, "Janelas e Tela Cheia");
        Combo(p, "Em aplicativos em tela inteira", new[] { ("Ocultar", FullscreenMode.Hide), ("Visível, sem expandir", FullscreenMode.NoExpand), ("Normal", FullscreenMode.Keep) }, () => S.Fullscreen, v => S.Fullscreen = v);

        Section(p, "Integração com o Sistema");
        Check(p, "Iniciar automaticamente com o Windows", () => S.StartWithWindows, v => S.StartWithWindows = v);
        Check(p, "Exibir ícone na bandeja do sistema", () => S.ShowTrayIcon, v => S.ShowTrayIcon = v);

        Section(p, "Sessões de Foco & Pomodoro");
        Slide(p, "Duração do foco (min)", 5, 90, () => S.FocusMinutes, v => S.FocusMinutes = (int)v);
        Slide(p, "Pausa curta (min)", 1, 30, () => S.ShortBreakMinutes, v => S.ShortBreakMinutes = (int)v);
        Slide(p, "Pausa longa (min)", 5, 60, () => S.LongBreakMinutes, v => S.LongBreakMinutes = (int)v);
        Slide(p, "Ciclos até a pausa longa", 2, 8, () => S.SessionsBeforeLongBreak, v => S.SessionsBeforeLongBreak = (int)v);
        Check(p, "Avançar fase automaticamente", () => S.AutoStartNextPhase, v => S.AutoStartNextPhase = v);
        Btn(p, "Restaurar todas as opções", danger: true, click: () => { if (MessageBox.Show("Deseja restaurar todas as configurações de fábrica?", "TzIsland", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { Settings.Reset(); Close(); ShowSingle(); } });
        return p;
    }

    StackPanel ModulesTab()
    {
        var p = Panel();
        Section(p, "Módulos Corporativos");
        Note(p, "Módulos inativos não consomem recursos de CPU ou memória em segundo plano.");
        foreach (var m in Modules.All)
        {
            var id = m.Id;
            Check(p, m.Title, () => S.IsOn(id), v => S.Modules[id] = v);
        }
        return p;
    }

    StackPanel Notifs()
    {
        var p = Panel();
        Section(p, "Notificações & Alertas");
        Check(p, "Exibir banner dinâmico na ilha ao receber eventos", () => S.BannerEnabled, v => S.BannerEnabled = v);
        Slide(p, "Duração do banner (s)", 2, 12, () => S.BannerSeconds, v => S.BannerSeconds = v, "0.0");
        Check(p, "Integrar à central de notificações do Windows", () => S.ListenWindowsNotifications, v => S.ListenWindowsNotifications = v);
        Check(p, "Exibir pré-visualização do conteúdo", () => S.NotificationPreview, v => S.NotificationPreview = v);
        Note(p, "Status da API de notificações do Windows: " + Svc.WinNotifs.Status);
        return p;
    }

    StackPanel Integrations()
    {
        var p = Panel();
        Section(p, "Serviço de Clima");
        var city = Text(p, "Localidade", () => S.City, _ => { });
        Btn(p, "Localizar coordenadas", async () =>
        {
            var r = await WeatherService.GeocodeAsync(city.Text);
            if (r == null) { MessageBox.Show("Cidade não encontrada."); return; }
            S.City = r.Value.Name; S.Latitude = r.Value.Lat; S.Longitude = r.Value.Lon; city.Text = r.Value.Name; Settings.Apply();
        });

        Section(p, "Calendários Externos");
        Text(p, "URLs de calendários iCalendar (.ics) — um por linha", () => string.Join("\n", S.IcsUrls), v => { S.IcsUrls = v.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(); _ = Svc.Calendar.RefreshIcsAsync(); }, true);

        Section(p, "Endpoints de IA Personalizados");
        Text(p, "Formato: Nome|URL (ex.: Ollama Local|http://localhost:11434/v1/models)",
            () => string.Join("\n", S.Endpoints.Select(e => e.Name + "|" + e.Url)),
            v => S.Endpoints = v.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split('|', 2)).Select(a => new EndpointItem { Name = a[0], Url = a.Length > 1 ? a[1] : a[0] }).ToList(), true);

        Section(p, "Monitoramento de Diretórios");
        Check(p, "Monitorar pasta Downloads", () => S.WatchDownloads, v => S.WatchDownloads = v);
        Text(p, "Caminho de Downloads", () => S.DownloadsPath, v => S.DownloadsPath = v);
        Check(p, "Monitorar capturas de tela", () => S.WatchScreenshots, v => S.WatchScreenshots = v);
        Text(p, "Caminho de Capturas", () => S.ScreenshotsPath, v => S.ScreenshotsPath = v);

        Section(p, "Reconhecimento de Fala");
        Text(p, "Idioma do Ditado (código BCP-47, ex.: pt-BR, en-US)", () => S.DictationLanguage, v => S.DictationLanguage = v);
        return p;
    }

    StackPanel Hotkeys()
    {
        var p = Panel();
        Section(p, "Atalhos Globais do Sistema");
        Note(p, "Formatos aceitos: Ctrl+Alt+Space, Ctrl+Shift+D... Teclas modificadoras: Ctrl, Alt, Shift, Win.");
        Text(p, "Expandir / recolher ilha", () => S.ToggleHotkey, v => S.ToggleHotkey = v);
        Text(p, "Ativar / pausar ditado por voz", () => S.DictationHotkey, v => S.DictationHotkey = v);
        return p;
    }

    StackPanel Privacy()
    {
        var p = Panel();
        Section(p, "Sensores de Privacidade");
        Note(p, "Acesso à câmera e microfone é estritamente pontual. Nenhum fluxo é transmitido para a internet.");
        Check(p, "Monitorar uso do microfone por aplicativos", () => S.MonitorMicrophone, v => S.MonitorMicrophone = v);
        Check(p, "Habilitar módulo de Câmera (Mirror)", () => S.IsOn("mirror"), v => S.Modules["mirror"] = v);
        Check(p, "Habilitar módulo de Ditado de Voz", () => S.IsOn("dictation"), v => S.Modules["dictation"] = v);

        Section(p, "Armazenamento Local");
        Note(p, "Todos os dados de produtividade são armazenados exclusivamente em " + Settings.DataDir);
        Btn(p, "Abrir pasta de dados no Explorador", () => Process.Start("explorer.exe", Settings.DataDir));
        Btn(p, "Limpar histórico do Inbox", () => Hub.Inbox.Clear());
        Btn(p, "Excluir dados de tarefas, notas e agenda…", danger: true, click: () =>
        {
            if (MessageBox.Show("Deseja apagar permanentemente todas as anotações, tarefas e eventos locais?", "TzIsland", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            { Settings.WipeData(); MessageBox.Show("Dados removidos com sucesso."); }
        });
        return p;
    }
}
