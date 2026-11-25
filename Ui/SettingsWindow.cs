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
        Icon = AppIcon.Window();
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
        AddTab(sp, "\uE7C9", "Gestos", Gestures);
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

    static TextBlock Note(StackPanel p, string t)
    {
        var n = K.T(t, 11.5, K.Muted);
        n.TextWrapping = TextWrapping.Wrap; n.TextTrimming = TextTrimming.None; n.Margin = new Thickness(0, 8, 0, 6);
        Host(p).Children.Add(n);
        return n;
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
        var l = K.T(label, 13, K.Fg); l.TextTrimming = TextTrimming.None; l.TextWrapping = TextWrapping.Wrap; l.Margin = new Thickness(0, 0, 12, 0);
        // Campo numérico: permite digitar o valor exato em vez de arrastar a barra milimetricamente.
        var v = K.NumBox(get().ToString(fmt, CultureInfo.InvariantCulture));
        v.Foreground = K.Accent; v.MaxLength = 8;
        top.Children.Add(l.Vc()); top.Children.Add(v.At(col: 1).Vc());
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
        void Commit()
        {
            var txt = v.Text.Replace(',', '.').Replace("%", "").Trim();
            if (!double.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            { v.Text = get().ToString(fmt, CultureInfo.InvariantCulture); return; }
            val = Math.Clamp(val, min, max);
            if (fmt == "0") val = Math.Round(val);
            set(val);
            s.Value = max > min ? (val - min) / (max - min) : 0;
            v.Text = val.ToString(fmt, CultureInfo.InvariantCulture);
            apply.Stop(); apply.Start();
        }
        v.LostFocus += (_, _) => Commit();
        v.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Commit(); e.Handled = true; } };
        v.GotKeyboardFocus += (_, _) => v.SelectAll();
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
        Slide(p, "Opacidade do vidro (%)", 1, 100, () => Math.Round(S.Opacity * 100), v => S.Opacity = v / 100);
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
        int primaryIdx = Math.Max(0, Array.FindIndex(System.Windows.Forms.Screen.AllScreens, s => s.Primary));
        Combo(p, "Monitor ativo", screens, () => S.Monitor < 0 ? primaryIdx : Math.Min(S.Monitor, screens.Length - 1), v => S.Monitor = v);
        Check(p, "Permitir arrastar para reposicionar", () => S.AllowDrag, v => S.AllowDrag = v);

        Section(p, "Efeitos e Fluidez");
        Check(p, "Animações físicas baseadas em molas", () => S.Animations, v => S.Animations = v);
        Combo(p, "Estilo da animação ao abrir e fechar", AnimProfiles.All, () => S.IslandAnim, v => S.IslandAnim = v);
        Note(p, "Fluida: padrão, em morph.  Elástica: passa do ponto e volta, estilo Alcove.  Cristal: lenta e sem quicar.  Precisa: rápida e seca.  Bolha: cresce do centro, cartão por cartão. Ao trocar, a ilha faz uma prévia sozinha.");
        Btn(p, "Pré-visualizar animação", () => Settings.RequestPreview());
        Check(p, "Desfoque de fundo com a arte do álbum", () => S.MediaBlurBackground, v => S.MediaBlurBackground = v);
        Check(p, "Cor dinâmica da capa (espectro e barra de progresso)", () => S.DynamicCoverColor, v => S.DynamicCoverColor = v);

        Section(p, "Sons");
        Check(p, "Sons sutis ao abrir e fechar a ilha", () => S.SoundsEnabled, v => S.SoundsEnabled = v);
        Slide(p, "Volume dos sons (%)", 5, 100, () => Math.Round(S.SoundVolume * 100), v => S.SoundVolume = v / 100);
        Btn(p, "Testar som", () => { SoundFx.Open(); var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) }; t.Tick += (_, _) => { t.Stop(); SoundFx.Close(); }; t.Start(); });
        Note(p, "Os sons são gerados pelo próprio app (sem arquivos). Só tocam se a opção acima estiver ligada.");
        Btn(p, "Restaurar layout padrão", () => { var d = new AppSettings(); S.Style = d.Style; S.Look = d.Look; S.Opacity = d.Opacity; S.BlurAmount = d.BlurAmount; S.ExpandedWidth = d.ExpandedWidth; S.ExpandedHeight = d.ExpandedHeight; S.CollapsedWidth = d.CollapsedWidth; S.CollapsedHeight = d.CollapsedHeight; S.Anchor = IslandAnchor.TopCenter; Settings.Apply(); Close(); ShowSingle(); });
        return p;
    }


    // ---------------- Gestos da ilha recolhida ----------------
    StackPanel Gestures()
    {
        var p = Panel();
        Section(p, "Gestos na ilha recolhida");
        Check(p, "Ativar gestos", () => S.CollapsedGestures, v => S.CollapsedGestures = v);
        Note(p, "Funcionam com a ilha fechada, sem precisar abrir. Os gestos de mídia só valem enquanto algo estiver tocando.");
        AddRow(p, GestureRow("volume", "Rolar o mouse sobre a ilha", "Muda o volume do sistema, com um indicador na própria ilha."));
        AddRow(p, GestureRow("play", "Clique do meio", "Dá play ou pausa na mídia que estiver tocando."));
        AddRow(p, GestureRow("skip-forward", "Arrastar para a esquerda", "Pula para a próxima faixa."));
        AddRow(p, GestureRow("skip-back", "Arrastar para a direita", "Volta para a faixa anterior."));
        AddRow(p, GestureRow("move", "Ctrl + arrastar", "Move a ilha de lugar (com mídia tocando, o arrastar simples troca de faixa)."));

        Section(p, "O que a ilha mostra recolhida");
        Check(p, "Download em andamento (tamanho, velocidade e barra)", () => S.ShowDownloadProgress, v => S.ShowDownloadProgress = v);
        Slide(p, "Avisar compromisso da Agenda com antecedência (min, 0 = desligado)", 0, 60, () => S.AgendaAlertMinutes, v => S.AgendaAlertMinutes = (int)v);
        AddRow(p, GestureRow("download", "Downloads", "Uma linha animada aparece embaixo enquanto o arquivo baixa, e fica verde ao terminar."));
        AddRow(p, GestureRow("calendar", "Próximo compromisso", "Mostra o título e a contagem regressiva; nos últimos 5 minutos ela fica laranja."));
        AddRow(p, GestureRow("music", "Mídia e Timer", "Ícone e espectro da música, ou o tempo restante do foco, ficam sempre à vista."));
        return p;
    }

    /// <summary>Linha de "gesto → o que faz". Reutilizável (ex.: tela de boas-vindas).</summary>
    public static UIElement GestureRow(string icon, string title, string desc)
    {
        var g = K.Cols("auto", "*");
        g.Margin = new Thickness(0, 10, 0, 10);
        var ic = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = K.CardStrong, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center, Child = Ico.Make(icon, 16, K.Fg, 1.8) };
        ((FrameworkElement)ic.Child).HorizontalAlignment = HorizontalAlignment.Center; ((FrameworkElement)ic.Child).VerticalAlignment = VerticalAlignment.Center;
        g.Children.Add(ic);
        var t = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        t.Children.Add(K.T(title, 13, K.Fg, true));
        var d = K.T(desc, 11.5, K.Muted); d.TextWrapping = TextWrapping.Wrap; d.TextTrimming = TextTrimming.None; d.Margin = new Thickness(0, 2, 0, 0);
        t.Children.Add(d);
        g.Children.Add(t.At(col: 1));
        return g;
    }

    StackPanel Behavior()
    {
        var p = Panel();
        Section(p, "Interatividade");
        Combo(p, "Abrir a ilha ao", new[] { ("Clicar", 0), ("Passar o mouse", 1), ("Os dois", 2) },
            () => S.ExpandOnHover && S.ExpandOnClick ? 2 : S.ExpandOnHover ? 1 : 0,
            v => { S.ExpandOnHover = v != 0; S.ExpandOnClick = v != 1; });
        Slide(p, "Tempo de espera do hover (ms) — só vale em \"Passar o mouse\" e \"Os dois\"", 0, 600, () => S.HoverDelayMs, v => S.HoverDelayMs = (int)v);
        Note(p, "Com \"Clicar\" a ilha só abre quando você clica nela (recomendado: não abre sem querer e libera os gestos abaixo).");

        Section(p, "Atualizações");
        Check(p, "Verificar atualizações automaticamente (Velopack + GitHub Releases)", () => S.UpdateCheckEnabled, v => { S.UpdateCheckEnabled = v; Svc.Updates.ApplyInterval(); });
        Slide(p, "Verificar a cada (horas)", 1, 24, () => S.UpdateHours, v => { S.UpdateHours = (int)v; Svc.Updates.ApplyInterval(); });
        var upNote = Note(p, "Versão instalada: " + UpdateService.CurrentText + (Svc.Updates.Status.Length > 0 ? " — " + Svc.Updates.Status : ""));
        Btn(p, "Verificar agora", async () => { await Svc.Updates.CheckAsync(true); upNote.Text = "Versão instalada: " + UpdateService.CurrentText + " — " + Svc.Updates.Status; });
        Btn(p, "Atualizar agora (baixa e reinicia)", () => _ = Svc.Updates.InstallAsync());
        if (!Svc.Updates.IsInstalled) Note(p, "Esta cópia não foi instalada pelo Velopack (build de desenvolvimento): verificação e atualização ficam desativadas.");

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
        Check(p, "Não perturbar durante o foco (segura os banners e entrega um resumo no fim)", () => S.FocusDnd, v => S.FocusDnd = v);
        Note(p, "As notificações continuam chegando ao Inbox; só o banner na ilha fica retido. Alarmes do Timer e o aviso de fim do foco aparecem normalmente.");
        Btn(p, "Restaurar todas as opções", danger: true, click: () => { if (MessageBox.Show("Deseja restaurar todas as configurações de fábrica?", "TzIsland", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { Settings.Reset(); Close(); ShowSingle(); } });
        return p;
    }

    StackPanel ModulesTab()
    {
        var p = Panel();
        Section(p, "Módulos Corporativos");
        Note(p, "Módulos inativos não consomem recursos de CPU ou memória em segundo plano.");
        var orderNote = Note(p, "A ordem abaixo é a mesma da barra lateral da ilha (de cima para baixo). Use as setas para mudar um item de lugar: a mudança aparece na hora.");
        foreach (var id in Modules.Order())
        {
            var mid = id;
            AddRow(p, ModuleRow(mid, orderNote));
        }
        Btn(p, "Restaurar ordem padrão da barra", () => { S.NavOrder = Modules.All.Select(m => m.Id).ToList(); Settings.Apply(); Close(); ShowSingle(); });

        Section(p, "Cartões da página Início");
        var homeNote = Note(p, "Estes são os cartões que aparecem no Início. Desligue os que não usa e use as setas para mudar a ordem: o primeiro da lista é o primeiro da grade, e \"Tocando agora\" ocupa sempre duas colunas.");
        foreach (var id in HomeWidgets.Order())
        {
            var hid = id;
            AddRow(p, HomeRow(hid, homeNote));
        }
        Btn(p, "Restaurar cartões padrão do Início", () => { S.HomeOrder = HomeWidgets.All.Select(w => w.Id).ToList(); S.HomeHidden.Clear(); Settings.Apply(); Close(); ShowSingle(); });
        return p;
    }

    /// <summary>Linha de um cartão do Início: mostra/oculta + ordem na grade (mesmo desenho da linha de módulos).</summary>
    static Grid HomeRow(string id, TextBlock note)
    {
        var wrap = K.Cols("*", "auto", "auto", "auto");
        wrap.Margin = new Thickness(0, 10, 0, 10);
        var l = K.T(HomeWidgets.Title(id), 13, K.Fg); l.TextTrimming = TextTrimming.None; l.Margin = new Thickness(0, 0, 14, 0);
        void Moved(int d) { HomeWidgets.Move(id, d); note.Text = "Ordem atual no Início: " + string.Join(" · ", HomeWidgets.Order().Where(HomeWidgets.IsOn).Select(HomeWidgets.Title)); }
        var left = ArrowBtn("chevron-left", "Mover para a esquerda / para cima", () => Moved(-1));
        var right = ArrowBtn("chevron-right", "Mover para a direita / para baixo", () => Moved(+1));
        left.Margin = new Thickness(0, 0, 4, 0);
        var sw = new SwitchToggle(); sw.SnapTo(HomeWidgets.IsOn(id));
        sw.Toggled += on => { S.HomeHidden[id] = !on; Settings.Apply(); };
        sw.Margin = new Thickness(12, 0, 0, 0);
        wrap.Children.Add(l.Vc());
        wrap.Children.Add(left.At(col: 1).Vc());
        wrap.Children.Add(right.At(col: 2).Vc());
        wrap.Children.Add(sw.At(col: 3).Vc());
        return wrap;
    }

    /// <summary>Linha de um módulo na aba Módulos: liga/desliga + ordem na barra lateral.</summary>
    static Grid ModuleRow(string id, TextBlock orderNote)
    {
        var wrap = K.Cols("*", "auto", "auto", "auto");
        wrap.Margin = new Thickness(0, 10, 0, 10);
        var l = K.T(Modules.Title(id), 13, K.Fg); l.TextWrapping = TextWrapping.Wrap; l.TextTrimming = TextTrimming.None; l.Margin = new Thickness(0, 0, 14, 0);
        void Moved(int d) { Modules.Move(id, d); orderNote.Text = "Ordem atual: " + string.Join(" · ", Modules.Order().Where(Settings.Current.IsOn).Select(Modules.Title)); }
        var left = ArrowBtn("chevron-left", "Mover para cima na barra", () => Moved(-1));
        var right = ArrowBtn("chevron-right", "Mover para baixo na barra", () => Moved(+1));
        left.Margin = new Thickness(0, 0, 4, 0);
        var sw = new SwitchToggle(); sw.SnapTo(Settings.Current.IsOn(id));
        sw.Toggled += on => { Settings.Current.Modules[id] = on; Settings.Apply(); };
        sw.Margin = new Thickness(12, 0, 0, 0);
        wrap.Children.Add(l.Vc());
        wrap.Children.Add(left.At(col: 1).Vc());
        wrap.Children.Add(right.At(col: 2).Vc());
        wrap.Children.Add(sw.At(col: 3).Vc());
        return wrap;
    }

    static Button ArrowBtn(string icon, string tip, Action click)
    {
        var b = K.IconBtn(" ", tip, click, 12, 28, 26, K.Label);
        b.Content = Ico.Make(icon, 14, K.Label, 1.9);
        return b;
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

        Section(p, "Letras e Área de Transferência");
        Check(p, "Buscar letras sincronizadas na internet (LRCLIB) quando a página de Mídia estiver aberta", () => S.LyricsEnabled, v => S.LyricsEnabled = v);
        Note(p, "Só o título, o artista e a duração da faixa são enviados ao buscar a letra. Nada mais sai do seu PC.");
        Check(p, "Histórico da área de transferência", () => S.IsOn("clipboard"), v => S.Modules["clipboard"] = v);
        Slide(p, "Itens no histórico (sem contar os fixados)", 10, 100, () => S.ClipboardMax, v => S.ClipboardMax = (int)v);
        Note(p, "O histórico comum fica só na memória e some ao sair do app; somente os itens que você fixa são salvos em disco. Conteúdo que gerenciadores de senha marcam como privado é ignorado.");
        Btn(p, "Limpar histórico da área de transferência (mantém os fixados)", () => Svc.Clipboard.ClearUnpinned());

        Section(p, "Armazenamento Local");
        Note(p, "Todos os dados de produtividade ficam salvos exclusivamente no seu computador, numa pasta do seu próprio perfil de usuário (pasta \"TzIsland\"). Nada é enviado para a internet. Use o botão abaixo para abrir essa pasta.");
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
