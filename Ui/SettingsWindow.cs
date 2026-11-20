namespace TzIsland;

/// <summary>Central de configurações. Toda alteração é aplicada ao vivo e salva em %AppData%\TzIsland.</summary>
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
        Title = "TzIsland — Configurações"; Width = 640; Height = 640; MinWidth = 520; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var tabs = new TabControl { Margin = new Thickness(8) };
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
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = body, Padding = new Thickness(14) }
    };

    static StackPanel Panel() => new();

    static void Section(StackPanel p, string t) => p.Children.Add(new TextBlock { Text = t, FontWeight = FontWeights.SemiBold, FontSize = 14, Margin = new Thickness(0, 12, 0, 6) });
    static void Note(StackPanel p, string t) => p.Children.Add(new TextBlock { Text = t, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 6) });

    static CheckBox Check(StackPanel p, string label, Func<bool> get, Action<bool> set)
    {
        var c = new CheckBox { Content = label, IsChecked = get(), Margin = new Thickness(0, 3, 0, 3) };
        c.Click += (_, _) => { set(c.IsChecked == true); Settings.Apply(); };
        p.Children.Add(c); return c;
    }

    static void Slide(StackPanel p, string label, double min, double max, Func<double> get, Action<double> set, string fmt = "0")
    {
        var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        g.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        var s = new Slider { Minimum = min, Maximum = max, Value = get() }; Grid.SetColumn(s, 1);
        var v = new TextBlock { Text = get().ToString(fmt, CultureInfo.InvariantCulture), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(v, 2);
        s.ValueChanged += (_, _) => { set(s.Value); v.Text = s.Value.ToString(fmt, CultureInfo.InvariantCulture); Settings.Apply(); };
        g.Children.Add(s); g.Children.Add(v); p.Children.Add(g);
    }

    static void Combo<T>(StackPanel p, string label, (string, T)[] items, Func<T> get, Action<T> set) where T : notnull
    {
        var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
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
        p.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 6, 0, 2) });
        var t = new TextBox { Text = get(), AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multiline ? 64 : 0, Padding = new Thickness(4) };
        t.LostFocus += (_, _) => { set(t.Text); Settings.Apply(); };
        p.Children.Add(t); return t;
    }

    static Button Btn(StackPanel p, string text, Action click)
    {
        var b = new Button { Content = text, Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 6, 8, 0), HorizontalAlignment = HorizontalAlignment.Left };
        b.Click += (_, _) => click(); p.Children.Add(b); return b;
    }

    // ---- abas ----
    StackPanel Appearance()
    {
        var p = Panel();
        Section(p, "Estilo");
        Combo(p, "Formato da ilha", new[] { ("Clássico (notch colado ao topo)", IslandStyle.Classic), ("Flutuante (ilha independente)", IslandStyle.Floating) }, () => S.Style, v => S.Style = v);
        Combo(p, "Aparência visual", new[] { ("Liquid Glass (vidro translúcido)", IslandLook.LiquidGlass), ("Dark Glass (vidro escuro)", IslandLook.DarkGlass), ("Solid Black (preto sólido)", IslandLook.SolidBlack) }, () => S.Look, v => S.Look = v);
        Slide(p, "Transparência do vidro", 0.3, 1, () => S.Opacity, v => S.Opacity = v, "0.00");
        Slide(p, "Desfoque do vidro", 0, 60, () => S.BlurAmount, v => S.BlurAmount = v);
        Note(p, "O desfoque é uma captura do que está atrás da ilha, tirada no momento em que ela expande.");
        Section(p, "Dimensões");
        Slide(p, "Largura expandida", 460, 900, () => S.ExpandedWidth, v => S.ExpandedWidth = v);
        Slide(p, "Altura expandida", 240, 520, () => S.ExpandedHeight, v => S.ExpandedHeight = v);
        Slide(p, "Largura recolhida", 120, 360, () => S.CollapsedWidth, v => S.CollapsedWidth = v);
        Slide(p, "Altura recolhida", 26, 56, () => S.CollapsedHeight, v => S.CollapsedHeight = v);
        Section(p, "Posição");
        Combo(p, "Posição na tela", new[] { ("Topo, centro", IslandAnchor.TopCenter), ("Topo, esquerda", IslandAnchor.TopLeft), ("Topo, direita", IslandAnchor.TopRight), ("Personalizada (arrastada)", IslandAnchor.Custom) }, () => S.Anchor, v => S.Anchor = v);
        var screens = System.Windows.Forms.Screen.AllScreens.Select((s, i) => ($"Monitor {i + 1}{(s.Primary ? " (principal)" : "")}", i)).ToArray();
        Combo(p, "Monitor", screens, () => Math.Min(S.Monitor, screens.Length - 1), v => S.Monitor = v);
        Check(p, "Permitir arrastar a ilha para reposicioná-la", () => S.AllowDrag, v => S.AllowDrag = v);
        Section(p, "Animações");
        Check(p, "Animações e efeitos visuais", () => S.Animations, v => S.Animations = v);
        Check(p, "Fundo desfocado com a capa do álbum", () => S.MediaBlurBackground, v => S.MediaBlurBackground = v);
        Btn(p, "Restaurar aparência padrão", () => { var d = new AppSettings(); S.Style = d.Style; S.Look = d.Look; S.Opacity = d.Opacity; S.BlurAmount = d.BlurAmount; S.ExpandedWidth = d.ExpandedWidth; S.ExpandedHeight = d.ExpandedHeight; S.CollapsedWidth = d.CollapsedWidth; S.CollapsedHeight = d.CollapsedHeight; S.Anchor = IslandAnchor.TopCenter; Settings.Apply(); Close(); ShowSingle(); });
        return p;
    }

    StackPanel Behavior()
    {
        var p = Panel();
        Section(p, "Interação");
        Check(p, "Expandir ao passar o mouse", () => S.ExpandOnHover, v => S.ExpandOnHover = v);
        Slide(p, "Atraso do hover (ms)", 0, 600, () => S.HoverDelayMs, v => S.HoverDelayMs = (int)v);
        Check(p, "Expandir ao clicar", () => S.ExpandOnClick, v => S.ExpandOnClick = v);
        Slide(p, "Recolher após inatividade (s)", 0.5, 10, () => S.AutoCollapseSeconds, v => S.AutoCollapseSeconds = v, "0.0");
        Combo(p, "Em aplicativos de tela cheia", new[] { ("Ocultar a ilha", FullscreenMode.Hide), ("Manter visível, sem expandir", FullscreenMode.NoExpand), ("Manter normal", FullscreenMode.Keep) }, () => S.Fullscreen, v => S.Fullscreen = v);
        Section(p, "Sistema");
        Check(p, "Iniciar com o Windows", () => S.StartWithWindows, v => S.StartWithWindows = v);
        Check(p, "Mostrar ícone na bandeja do sistema", () => S.ShowTrayIcon, v => S.ShowTrayIcon = v);
        Section(p, "Foco (Pomodoro)");
        Slide(p, "Sessão de foco (min)", 5, 90, () => S.FocusMinutes, v => S.FocusMinutes = (int)v);
        Slide(p, "Pausa curta (min)", 1, 30, () => S.ShortBreakMinutes, v => S.ShortBreakMinutes = (int)v);
        Slide(p, "Pausa longa (min)", 5, 60, () => S.LongBreakMinutes, v => S.LongBreakMinutes = (int)v);
        Slide(p, "Sessões até a pausa longa", 2, 8, () => S.SessionsBeforeLongBreak, v => S.SessionsBeforeLongBreak = (int)v);
        Check(p, "Iniciar a próxima fase automaticamente", () => S.AutoStartNextPhase, v => S.AutoStartNextPhase = v);
        Btn(p, "Restaurar TODAS as configurações padrão", () => { if (MessageBox.Show("Restaurar todas as configurações?", "TzIsland", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { Settings.Reset(); Close(); ShowSingle(); } });
        return p;
    }

    StackPanel ModulesTab()
    {
        var p = Panel();
        Section(p, "Funcionalidades");
        Note(p, "Módulos desligados não consomem recursos. Alterações em Mídia, Inbox e Agentes de IA têm efeito completo após reiniciar o app.");
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
        Section(p, "Notificações");
        Check(p, "Mostrar banner no notch ao receber notificações", () => S.BannerEnabled, v => S.BannerEnabled = v);
        Slide(p, "Duração do banner (s)", 2, 12, () => S.BannerSeconds, v => S.BannerSeconds = v, "0.0");
        Check(p, "Capturar notificações do Windows", () => S.ListenWindowsNotifications, v => S.ListenWindowsNotifications = v);
        Check(p, "Mostrar prévia do conteúdo das mensagens", () => S.NotificationPreview, v => S.NotificationPreview = v);
        Note(p, "Status da captura do Windows: " + Svc.WinNotifs.Status);
        Note(p, "WhatsApp e outros apps só aparecem se o Windows entregar a notificação deles. O app nunca lê mensagens por métodos não autorizados.");
        return p;
    }

    StackPanel Integrations()
    {
        var p = Panel();
        Section(p, "Clima");
        var city = Text(p, "Cidade", () => S.City, _ => { });
        Btn(p, "Buscar cidade", async () =>
        {
            var r = await WeatherService.GeocodeAsync(city.Text);
            if (r == null) { MessageBox.Show("Cidade não encontrada."); return; }
            S.City = r.Value.Name; S.Latitude = r.Value.Lat; S.Longitude = r.Value.Lon; city.Text = r.Value.Name; Settings.Apply();
        });
        Section(p, "Calendários externos");
        Text(p, "Links ICS (um por linha — Google Agenda: Configurações > endereço secreto em formato iCal)", () => string.Join("\n", S.IcsUrls), v => { S.IcsUrls = v.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(); _ = Svc.Calendar.RefreshIcsAsync(); }, true);
        Note(p, "Limitação: eventos recorrentes (RRULE) ainda não são expandidos.");
        Section(p, "Endpoints de IA personalizados");
        Text(p, "Um por linha, no formato  Nome|URL  (ex.: Meu servidor|http://localhost:8000/v1/models). Reinicie o app para aplicar.",
            () => string.Join("\n", S.Endpoints.Select(e => e.Name + "|" + e.Url)),
            v => S.Endpoints = v.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split('|', 2)).Select(a => new EndpointItem { Name = a[0], Url = a.Length > 1 ? a[1] : a[0] }).ToList(), true);
        Section(p, "Pastas monitoradas");
        Check(p, "Monitorar Downloads", () => S.WatchDownloads, v => S.WatchDownloads = v);
        Text(p, "Pasta de Downloads", () => S.DownloadsPath, v => S.DownloadsPath = v);
        Check(p, "Monitorar Capturas de Tela", () => S.WatchScreenshots, v => S.WatchScreenshots = v);
        Text(p, "Pasta de capturas", () => S.ScreenshotsPath, v => S.ScreenshotsPath = v);
        Section(p, "Ditado por voz");
        Text(p, "Idioma (código, ex.: pt-BR, en-US — precisa estar instalado no Windows)", () => S.DictationLanguage, v => S.DictationLanguage = v);
        return p;
    }

    StackPanel Hotkeys()
    {
        var p = Panel();
        Section(p, "Atalhos de teclado globais");
        Note(p, "Formato: Ctrl+Alt+D, Ctrl+Shift+Space... (modificadores: Ctrl, Alt, Shift, Win).");
        Text(p, "Expandir / recolher a ilha", () => S.ToggleHotkey, v => S.ToggleHotkey = v);
        Text(p, "Iniciar / parar ditado por voz", () => S.DictationHotkey, v => S.DictationHotkey = v);
        return p;
    }

    StackPanel Privacy()
    {
        var p = Panel();
        Section(p, "Câmera e microfone");
        Note(p, "A câmera (Mirror) só é aberta quando você liga o espelho e é liberada ao desligar ou sair da tela. Nada é gravado nem enviado. O microfone só é usado no ditado, quando você inicia.");
        Check(p, "Detectar quando o microfone está em uso (e por qual app)", () => S.MonitorMicrophone, v => S.MonitorMicrophone = v);
        Check(p, "Habilitar o módulo Mirror (câmera)", () => S.IsOn("mirror"), v => S.Modules["mirror"] = v);
        Check(p, "Habilitar o módulo de Ditado (microfone)", () => S.IsOn("dictation"), v => S.Modules["dictation"] = v);
        Section(p, "Dados");
        Note(p, "Tudo é guardado localmente em " + Settings.DataDir + ". Nada é enviado para servidores. Os únicos acessos à internet são o clima (Open-Meteo) e os links de calendário que você configurar.");
        Btn(p, "Abrir pasta de dados", () => Process.Start("explorer.exe", Settings.DataDir));
        Btn(p, "Limpar Inbox (memória)", () => Hub.Inbox.Clear());
        Btn(p, "Apagar notas, tarefas, eventos e bandeja…", () =>
        {
            if (MessageBox.Show("Apagar permanentemente notas, tarefas, eventos locais e bandeja? Reinicie o app depois.", "TzIsland", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            { Settings.WipeData(); MessageBox.Show("Dados apagados. Reinicie o TzIsland."); }
        });
        return p;
    }
}
