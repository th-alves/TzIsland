namespace TzIsland;

/// <summary>
/// Histórico da área de transferência no estilo NotchNook: cartões com o que você copiou, itens fixáveis no topo.
/// Clique copia de volta · duplo clique cola na janela que estava ativa · o alfinete fixa (e guarda entre sessões).
/// </summary>
public sealed class ClipboardPage : PageBase
{
    readonly StackPanel _list = new();
    readonly TextBlock _count = K.T("", 11.5, K.Dim);
    readonly ChipRow _filter = new(new[] { "Tudo", "Fixados" }, 66, 6);
    bool _onlyPinned;

    public ClipboardPage()
    {
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new RowDefinition { Height = K.GL("*") });
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var head = K.Cols("*", "auto", "auto", "auto");
        head.Margin = new Thickness(2, 0, 2, 8);
        head.Children.Add(FK.Header(Ico.Make("clipboard", 12, K.Label, 1.8), "ÁREA DE TRANSFERÊNCIA"));
        head.Children.Add(_count.At(col: 1).Vc().M(0, 0, 12, 0));
        _filter.HorizontalAlignment = HorizontalAlignment.Right;
        _filter.Picked += i => { _onlyPinned = i == 1; Rebuild(); };
        head.Children.Add(_filter.At(col: 2).Vc().M(0, 0, 10, 0));
        head.Children.Add(K.TextBtn("Limpar histórico", () => Svc.Clipboard.ClearUnpinned()).At(col: 3));
        Root.Children.Add(head);

        Root.Children.Add(K.Scroll(_list).At(row: 1));

        var foot = K.T("Clique copia · duplo clique cola · o alfinete fixa o item (só os fixados ficam salvos no disco)", 10, K.Faint);
        foot.Margin = new Thickness(4, 6, 4, 0); foot.TextTrimming = TextTrimming.CharacterEllipsis;
        Root.Children.Add(foot.At(row: 2));
        _filter.Select(0, false);
    }

    public override void Shown() { Svc.Clipboard.Changed += OnChanged; Rebuild(); }
    public override void Hidden() { Svc.Clipboard.Changed -= OnChanged; }
    void OnChanged() => Dispatcher_Invoke(Rebuild);
    static void Dispatcher_Invoke(Action a) => Application.Current.Dispatcher.InvokeAsync(a);

    void Rebuild()
    {
        _list.Children.Clear();
        var all = Svc.Clipboard.Items.ToList();
        var pinned = all.Where(x => x.Pinned).ToList();
        var recent = _onlyPinned ? new List<ClipItem>() : all.Where(x => !x.Pinned).ToList();
        _count.Text = all.Count == 0 ? "" : all.Count == 1 ? "1 item" : $"{all.Count} itens";

        if (pinned.Count == 0 && recent.Count == 0)
        {
            var box = new StackPanel { Margin = new Thickness(0, 34, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
            var ic = Ico.Make("clipboard", 30, K.Muted, 1.4); ic.HorizontalAlignment = HorizontalAlignment.Center;
            box.Children.Add(ic);
            box.Children.Add(K.T(_onlyPinned ? "Nenhum item fixado" : "Nada copiado ainda", 14, K.Fg, true).M(0, 10, 0, 2));
            var hint = K.T(_onlyPinned ? "Fixe um item com o alfinete para guardá-lo aqui." : "Copie algo (Ctrl+C) e ele aparece aqui.", 11.5, K.Faint);
            hint.HorizontalAlignment = HorizontalAlignment.Center; box.Children.Add(hint);
            _list.Children.Add(box);
            return;
        }
        Section("FIXADOS", pinned);
        Section("RECENTES", recent);
    }

    void Section(string title, List<ClipItem> items)
    {
        if (items.Count == 0) return;
        _list.Children.Add(K.Cap(title, 9.5, K.Label).M(4, 4, 0, 6));
        var wrap = new WrapPanel();
        foreach (var it in items) wrap.Children.Add(Card(it));
        _list.Children.Add(wrap);
    }

    static string Ago(DateTime t)
    {
        var d = DateTime.Now - t;
        if (d.TotalSeconds < 60) return "agora";
        if (d.TotalMinutes < 60) return $"há {(int)d.TotalMinutes} min";
        return t.Date == DateTime.Today ? t.ToString("HH:mm", CultureInfo.InvariantCulture) : t.ToString("dd/MM", CultureInfo.InvariantCulture);
    }

    UIElement Card(ClipItem it)
    {
        var grid = new Grid();
        string kindIcon = it.Kind switch { ClipKind.Image => "image", ClipKind.Files => "file", _ => "file-text" };

        var body = new StackPanel { Margin = new Thickness(11, 9, 11, 9) };
        var top = K.Cols("auto", "*");
        top.Children.Add(Ico.Make(kindIcon, 12, K.Label, 1.8).M(0, 0, 6, 0).Vc());
        top.Children.Add(K.Cap(Ago(it.Time), 9, K.Muted).At(col: 1).Vc());
        body.Children.Add(top);

        if (it.Kind == ClipKind.Image && it.Image != null)
        {
            var img = new Image { Source = it.Image, Stretch = Stretch.UniformToFill };
            var host = new Border { Height = 46, CornerRadius = new CornerRadius(8), Background = K.CardStrong, Child = img, Margin = new Thickness(0, 6, 0, 0) };
            K.RoundClip(img, 8);
            body.Children.Add(host);
        }
        else
        {
            string txt = it.Kind == ClipKind.Text ? CompactWs(it.Text) : it.Preview;
            var tb = K.T(txt, 11.5, K.Fg);
            tb.TextWrapping = TextWrapping.Wrap; tb.TextTrimming = TextTrimming.CharacterEllipsis; tb.MaxHeight = 48; tb.Margin = new Thickness(0, 6, 0, 0);
            if (it.Kind == ClipKind.Text && LooksLikeCode(it.Text)) tb.FontFamily = K.Mono;
            body.Children.Add(tb);
        }
        grid.Children.Add(body);

        // botões (alfinete / remover) no canto
        var pin = SmallBtn("pin", it.Pinned ? K.White : K.Dim);
        pin.ToolTip = it.Pinned ? "Desafixar" : "Fixar";
        pin.Click += (_, _) => Svc.Clipboard.TogglePin(it);
        var del = SmallBtn("x", K.Dim); del.ToolTip = "Remover";
        del.Click += (_, _) => Svc.Clipboard.Remove(it);
        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 4, 0) };
        btns.Children.Add(pin); btns.Children.Add(del.M(2, 0, 0, 0));
        btns.Opacity = it.Pinned ? 1 : 0;
        if (it.Pinned) del.Visibility = Visibility.Collapsed;
        grid.Children.Add(btns);

        // aviso "Copiado"
        var done = new Border { Background = K.Hex("#E6000000"), CornerRadius = new CornerRadius(14), Opacity = 0, IsHitTestVisible = false };
        var dsp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        dsp.Children.Add(Ico.Make("check", 15, K.Green, 2.2).M(0, 0, 6, 0)); dsp.Children.Add(K.T("Copiado", 12, K.Fg, true));
        done.Child = dsp; grid.Children.Add(done);

        var card = new Border
        {
            Width = 176, MinHeight = 82, Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(14), Child = grid, Cursor = Cursors.Hand,
            Background = K.Card, BorderThickness = new Thickness(1), BorderBrush = it.Pinned ? K.StrokeHi : K.Stroke
        };
        K.RoundClip(grid, 13);
        card.MouseEnter += (_, _) => { if (!it.Pinned) btns.Opacity = 1; card.Background = K.Hex("#FF1A1A1D"); };
        card.MouseLeave += (_, _) => { if (!it.Pinned) btns.Opacity = 0; card.Background = K.Card; };
        card.MouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject d && IsInside<Button>(d)) return;
            e.Handled = true;
            if (e.ClickCount >= 2) { PasteSoon(); return; }
            if (!Svc.Clipboard.Copy(it)) return;
            // Sequência de tracks: aparece, espera e some — interrompível e sem keyframes presos.
            MotionEngine.To(done, UIElement.OpacityProperty, 1, Tz.Springs.Fade, 0,
                () => MotionEngine.To(done, UIElement.OpacityProperty, 0, Tz.Springs.Fade, 0.55));
        };
        return card;
    }

    static bool IsInside<T>(DependencyObject d) where T : DependencyObject
    {
        while (d != null) { if (d is T) return true; d = VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d); }
        return false;
    }

    static Button SmallBtn(string icon, Brush fg) => new()
    {
        Style = K.Ghost, Content = Ico.Make(icon, 12, fg, 1.9), Padding = new Thickness(5), Tag = new CornerRadius(8), Background = K.Hex("#CC000000")
    };

    /// <summary>Duplo clique: o item já está na área de transferência; manda Ctrl+V para a janela que estava ativa (a ilha não rouba o foco).</summary>
    static void PasteSoon()
    {
        Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            await Task.Delay(140);
            const byte Ctrl = 0x11, V = 0x56; const uint Up = 2;
            Native.keybd_event(Ctrl, 0, 0, UIntPtr.Zero); Native.keybd_event(V, 0, 0, UIntPtr.Zero);
            Native.keybd_event(V, 0, Up, UIntPtr.Zero); Native.keybd_event(Ctrl, 0, Up, UIntPtr.Zero);
        });
    }

    static string CompactWs(string s)
    {
        var t = System.Text.RegularExpressions.Regex.Replace(s.Trim(), @"[ \t]*\r?\n[ \t]*", "↵ ");
        return t.Length > 160 ? t[..160] : t;
    }

    static bool LooksLikeCode(string s) => s.Contains('{') && s.Contains('}') || s.Contains("=>") || s.Contains(";\n") || s.Contains("</");
}
