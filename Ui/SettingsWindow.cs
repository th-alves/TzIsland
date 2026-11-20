namespace TzIsland;

/// <summary>
/// Settings Center: Unified Apple-style control center for visual customization,
/// behavior controls, hardware monitor integrations, and privacy safeguards.
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

    public SettingsWindow()
    {
        Title = "TzIsland — Configurações";
        Width = 660; Height = 660; MinWidth = 540; MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E));
        Foreground = K.Fg;

        var tabs = new TabControl { Margin = new Thickness(10) };
        tabs.Items.Add(Tab("Aparência", Appearance()));
        tabs.Items.Add(Tab("Comportamento", Behavior()));
        tabs.Items.Add(Tab("Módulos", ModulesTab()));
        tabs.Items.Add(Tab("Notificações", Notifs()));
        tabs.Items.Add(Tab("Integrações", Integrations()));
        tabs.Items.Add(Tab("Atalhos", Hotkeys()));
        tabs.Items.Add(Tab("Privacidade", Privacy()));
        Content = tabs;
    }

    static TabItem Tab(string header, StackPanel body) => new()
    {
        Header = header,
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = body, Padding = new Thickness(16) }
    };

    static StackPanel Panel() => new();

    static void Section(StackPanel p, string t) => p.Children.Add(new TextBlock
    {
        Text = t,
        FontWeight = FontWeights.SemiBold,
        FontSize = 14,
        Foreground = K.Fg,
        Margin = new Thickness(0, 14, 0, 6)
    });

    static void Note(StackPanel p, string t) => p.Children.Add(new TextBlock
    {
        Text = t,
        Foreground = K.Dim,
        FontSize = 11,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 2, 0, 6)
    });

    static CheckBox Check(StackPanel p, string label, Func<bool> get, Action<bool> set)
    {
        var c = new CheckBox { Content = label, IsChecked = get(), Margin = new Thickness(0, 4, 0, 4) };
        c.Click += (_, _) => { set(c.IsChecked == true); Settings.Apply(); };
        p.Children.Add(c); return c;
    }

    static void Slide(StackPanel p, string label, double min, double max, Func<double> get, Action<double> set, string fmt = "0")
    {
        var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        g.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        var s = new Slider { Minimum = min, Maximum = max, Value = get() }; Grid.SetColumn(s, 1);
        var v = new TextBlock { Text = get().ToString(fmt, CultureInfo.InvariantCulture), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Foreground = K.Dim }; Grid.SetColumn(v, 2);
        s.ValueChanged += (_, _) => { set(s.Value); v.Text = s.Value.ToString(fmt, CultureInfo.InvariantCulture); Settings.Apply(); };
        g.Children.Add(s); g.Children.Add(v); p.Children.Add(g);
    }

    static void Combo<T>(StackPanel p, string label, (string, T)[] items, Func<T> get, Action<T> set) where T : notnull
    {
        var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        var c = new ComboBox(); Grid.SetColumn(c, 1);
        foreach (var (l, v) in items) c.Items.Add(new ComboBoxItem { Content = l, Tag = v });
        foreach (ComboBoxItem it in c.Items) if (it.Tag is T t && EqualityComparer<T>.Default.Equals(t, get())) c.SelectedItem = it;
        c.SelectionChanged += (_, _) => { if (c.SelectedItem is ComboBoxItem ci && ci.Tag is T v) { set(v); Settings.Apply(); } };
        g.Children.Add(c); p.Children.Add(g);
    }

    static TextBox Text(StackPanel p, string label, Func<string> get, Action<string> set, bool multiline = false)
    {
        p.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 3) });
        var t = new TextBox { Text = get(), AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multiline ? 64 : 0, Padding = new Thickness(6, 4, 6, 4) };
        t.LostFocus += (_, _) => { set(t.Text); Settings.Apply(); };
        p.Children.Add(t); return t;
    }

    static Button Btn(StackPanel p, string text, Action click)
    {
        var b = new Button { Content = text, Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 8, 8, 0), HorizontalAlignment = HorizontalAlignment.Left };
        b.Click += (_, _) => click(); p.Children.Add(b); return b;
    }

    // ---- Tabs ----
    StackPanel Appearance()
    {
        var p = Panel();
        Section(p, "Estilo da Ilha");
        Combo(p, "Formato da ilha", new[] { ("Clássico (notch superior)", IslandStyle.Classic), ("Flutuante (estilo Dynamic Island)", IslandStyle.Floating) }, () => S.Style, v => S.Style = v);
        Combo(p, "Aparência visual", new[] { ("Liquid Glass (vidro translúcido aero)", IslandLook.LiquidGlass), ("Dark Glass (vidro escuro executivo)", IslandLook.DarkGlass), ("Solid Black (preto OLED)", IslandLook.SolidBlack) }, () => S.Look, v => S.Look = v);
        Slide(p, "Opacidade do vidro", 0.3, 1, () => S.Opacity, v => S.Opacity = v, "0.00");
        Slide(p, "Intensidade do desfoque", 0, 60, () => S.BlurAmount, v => S.BlurAmount = v);
        Note(p, "O desfoque captura realisticamente a área atrás da ilha no momento da expansão.");

        Section(p, "Dimensões");
        Slide(p, "Largura expandida", 460, 900, () => S.ExpandedWidth, v => S.ExpandedWidth = v);
        Slide(p, "Altura expandida", 240, 520, () => S.ExpandedHeight, v => S.ExpandedHeight = v);
        Slide(p, "Largura recolhida", 140, 360, () => S.CollapsedWidth, v => S.CollapsedWidth = v);
        Slide(p, "Altura recolhida", 28, 56, () => S.CollapsedHeight, v => S.CollapsedHeight = v);

        Section(p, "Posicionamento");
        Combo(p, "Alinhamento na tela", new[] { ("Topo, centro", IslandAnchor.TopCenter), ("Topo, esquerda", IslandAnchor.TopLeft), ("Topo, direita", IslandAnchor.TopRight), ("Personalizado (arraste livre)", IslandAnchor.Custom) }, () => S.Anchor, v => S.Anchor = v);
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
        Slide(p, "Recolher automaticamente após (s)", 0.5, 10, () => S.AutoCollapseSeconds, v => S.AutoCollapseSeconds = v, "0.0");
        Combo(p, "Em aplicativos em tela inteira", new[] { ("Ocultar a ilha", FullscreenMode.Hide), ("Manter visível, sem expansão", FullscreenMode.NoExpand), ("Comportamento padrão", FullscreenMode.Keep) }, () => S.Fullscreen, v => S.Fullscreen = v);

        Section(p, "Integração com o Sistema");
        Check(p, "Iniciar automaticamente com o Windows", () => S.StartWithWindows, v => S.StartWithWindows = v);
        Check(p, "Exibir ícone na bandeja do sistema", () => S.ShowTrayIcon, v => S.ShowTrayIcon = v);

        Section(p, "Sessões de Foco & Pomodoro");
        Slide(p, "Duração do foco (min)", 5, 90, () => S.FocusMinutes, v => S.FocusMinutes = (int)v);
        Slide(p, "Pausa curta (min)", 1, 30, () => S.ShortBreakMinutes, v => S.ShortBreakMinutes = (int)v);
        Slide(p, "Pausa longa (min)", 5, 60, () => S.LongBreakMinutes, v => S.LongBreakMinutes = (int)v);
        Slide(p, "Ciclos até a pausa longa", 2, 8, () => S.SessionsBeforeLongBreak, v => S.SessionsBeforeLongBreak = (int)v);
        Check(p, "Avançar fase automaticamente", () => S.AutoStartNextPhase, v => S.AutoStartNextPhase = v);
        Btn(p, "Restaurar todas as opções", () => { if (MessageBox.Show("Deseja restaurar todas as configurações de fábrica?", "TzIsland", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { Settings.Reset(); Close(); ShowSingle(); } });
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
        Btn(p, "Excluir dados de tarefas, notas e agenda…", () =>
        {
            if (MessageBox.Show("Deseja apagar permanentemente todas as anotações, tarefas e eventos locais?", "TzIsland", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            { Settings.WipeData(); MessageBox.Show("Dados removidos com sucesso."); }
        });
        return p;
    }
}
