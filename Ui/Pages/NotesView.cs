using System.Text;
using System.Windows.Media.Animation;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;

namespace TzIsland;

/// <summary>
/// Notas: painel de formatação (ou lista de notas) à esquerda, editor de texto formatado à direita.
/// O cabeçalho (contador, alternador Lista/Formatar e ações) é exibido pela página Produtividade, ao lado das abas.
/// </summary>
sealed class NotesView : PageBase
{
    static readonly CultureInfo Pt = new("pt-BR");
    static readonly Brush EditorBg = K.Hex("#FF1C1C1E"), EditorStroke = K.Hex("#FF2A2A2D"), Hover = K.Hex("#FF1A1A1D"),
        CodeBg = K.Hex("#FF2C2C30"), Selection = K.Hex("#FF4A6CF7");
    const double HeadingSize = 17, BodySize = 13;

    readonly RichTextBox _edit = new();
    readonly Border _editorCard;
    readonly TextBlock _hint = K.T("Escreva sua anotação…", BodySize, K.Muted);
    readonly Grid _layout = K.Cols("auto", "auto", "*");
    readonly Border _leftHost = new() { Width = 236 }, _divider = new() { Width = 1, Margin = new Thickness(14, 2, 14, 2), Background = K.Hex("#FF1F1F22") };
    readonly UniformGrid _format = new() { Columns = 4, VerticalAlignment = VerticalAlignment.Top };
    readonly StackPanel _list = new();
    readonly Dictionary<string, Button> _fmtBtns = new();
    readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(600) };
    readonly TextBlock _count = K.T("", 12, K.Muted, true);
    readonly Border _segHl = new() { Width = 30, Height = 24, CornerRadius = new CornerRadius(12), Background = K.Hex("#FF3A3A3E"), HorizontalAlignment = HorizontalAlignment.Left, RenderTransform = new TranslateTransform() };
    readonly Border _savedDot = new() { Width = 26, Height = 26, CornerRadius = new CornerRadius(13) };
    readonly Button _maxBtn;
    Note? _cur;
    bool _loading, _showList, _maximized;

    /// <summary>Barra que fica no cabeçalho da página (ao lado das abas).</summary>
    public FrameworkElement Toolbar { get; }

    public NotesView()
    {
        // ---------------- Editor
        _edit.Background = Brushes.Transparent; _edit.BorderThickness = new Thickness(0); _edit.Foreground = K.Fg;
        _edit.CaretBrush = K.White; _edit.SelectionBrush = Selection; _edit.SelectionOpacity = 0.45;
        _edit.FontFamily = K.Font; _edit.FontSize = BodySize; _edit.Padding = new Thickness(0);
        _edit.FocusVisualStyle = null; _edit.AcceptsTab = true; _edit.IsDocumentEnabled = true;
        _edit.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        SpellCheck.SetIsEnabled(_edit, false);
        _edit.Document = NewDoc();
        _hint.IsHitTestVisible = false; _hint.Margin = new Thickness(2, 0, 0, 0); _hint.VerticalAlignment = VerticalAlignment.Top;

        var edGrid = new Grid();
        edGrid.Children.Add(_edit);
        edGrid.Children.Add(_hint);
        _maxBtn = RoundBtn("maximize", "Expandir editor", ToggleMax, 26, 13);
        _maxBtn.HorizontalAlignment = HorizontalAlignment.Right; _maxBtn.VerticalAlignment = VerticalAlignment.Bottom;
        _maxBtn.Margin = new Thickness(0, 0, -6, -4);
        edGrid.Children.Add(_maxBtn);
        _editorCard = new Border
        {
            Background = EditorBg, BorderBrush = EditorStroke, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18),
            Padding = new Thickness(16, 12, 14, 12), Child = edGrid, Cursor = Cursors.IBeam
        };
        _editorCard.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource == _editorCard || e.OriginalSource == edGrid) { _edit.Focus(); _edit.CaretPosition = _edit.Document.ContentEnd; } };

        // ---------------- Painel de formatação
        AddFmt("bold", TextIcon("B", 15, FontWeights.Bold), "Negrito", "Negrito (Ctrl+B)", () => EditingCommands.ToggleBold.Execute(null, _edit));
        AddFmt("italic", TextIcon("I", 15, FontWeights.Normal, italic: true), "Itálico", "Itálico (Ctrl+I)", () => EditingCommands.ToggleItalic.Execute(null, _edit));
        AddFmt("strike", TextIcon("S", 15, FontWeights.Normal, strike: true), "Riscado", "Riscado (Ctrl+Shift+X)", ToggleStrike);
        AddFmt("code", Ico.Make("code", 16, K.Fg, 1.9), "Código", "Código (Ctrl+E)", ToggleCode);
        AddFmt("list", Ico.Make("list", 16, K.Fg, 1.9), "Lista", "Lista com marcadores (Ctrl+Shift+L)", () => EditingCommands.ToggleBullets.Execute(null, _edit));
        AddFmt("number", Ico.Make("list-ordered", 16, K.Fg, 1.8), "Numerada", "Lista numerada (Ctrl+Shift+N)", () => EditingCommands.ToggleNumbering.Execute(null, _edit));
        AddFmt("heading", TextIcon("AA", 13, FontWeights.SemiBold, small: true), "Título", "Título (Ctrl+Shift+H)", ToggleHeading);

        _leftHost.Child = _format;
        _layout.Children.Add(_leftHost);
        _layout.Children.Add(_divider.At(col: 1));
        _layout.Children.Add(_editorCard.At(col: 2));
        Root.Children.Add(_layout);

        Toolbar = BuildToolbar();

        // ---------------- Eventos
        _edit.TextChanged += (_, _) =>
        {
            SyncHint();
            if (_loading || _cur == null) return;
            _debounce.Stop(); _debounce.Start(); SetSaved(false);
        };
        _edit.SelectionChanged += (_, _) => SyncFmtState();
        _edit.PreviewKeyDown += OnKeys;
        _debounce.Tick += (_, _) => { _debounce.Stop(); Persist(); };

        if (Svc.Notes.Items.Count == 0) Svc.Notes.Add();
        Open(Svc.Notes.Items[0]);
    }

    public override bool KeepOpen => _edit.IsKeyboardFocusWithin;
    public override IEnumerable<FrameworkElement> RevealTargets => new FrameworkElement[] { _leftHost, _editorCard };
    public override void Hidden() { if (_debounce.IsEnabled) { _debounce.Stop(); Persist(); } }

    // ======================================================================= Cabeçalho
    FrameworkElement BuildToolbar()
    {
        var g = K.Cols("auto", "auto", "auto", "*", "auto");
        g.Children.Add(_count.Vc().M(0, 0, 10, 0));
        g.Children.Add(new Border { Width = 1, Height = 16, Background = K.Hex("#FF2A2A2D"), Margin = new Thickness(0, 0, 10, 0) }.At(col: 1).Vc());

        // Alternador [lista | Aa]
        var seg = new Grid { Height = 28 };
        seg.Children.Add(new Border { CornerRadius = new CornerRadius(14), Background = K.Hex("#FF1C1C1E"), BorderBrush = EditorStroke, BorderThickness = new Thickness(1) });
        var segIn = new Grid { Margin = new Thickness(2) };
        segIn.Children.Add(_segHl);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(SegBtn(Ico.Make("list", 14, K.Fg, 1.9), "Lista de notas", () => SetMode(true)));
        row.Children.Add(SegBtn(TextIcon("Aa", 12.5, FontWeights.SemiBold), "Formatação", () => SetMode(false)));
        segIn.Children.Add(row);
        seg.Children.Add(segIn);
        g.Children.Add(seg.At(col: 2).Vc());

        // Ações à direita: salvo · nova · exportar · excluir · ajuda
        var acts = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        _savedDot.ToolTip = "Salvo";
        acts.Children.Add(_savedDot.M(0, 0, 6, 0));
        acts.Children.Add(RoundBtn("plus", "Nova nota", NewNote, 28, 14, solid: true).M(0, 0, 6, 0));
        acts.Children.Add(RoundBtn("download", "Exportar nota (.txt)", Export, 28, 14, solid: true).M(0, 0, 6, 0));
        acts.Children.Add(RoundBtn("trash", "Excluir nota", Delete, 28, 14).M(0, 0, 6, 0));
        var info = RoundBtn("info", "", () => { }, 28, 15);
        info.ToolTip = "Atalhos:\nCtrl+B negrito · Ctrl+I itálico · Ctrl+Shift+X riscado\nCtrl+E código · Ctrl+Shift+L lista · Ctrl+Shift+N numerada\nCtrl+Shift+H título · Ctrl+N nova nota\n\nAs notas são salvas sozinhas, só neste PC.";
        ToolTipService.SetInitialShowDelay(info, 150); ToolTipService.SetShowDuration(info, 20000);
        acts.Children.Add(info);
        g.Children.Add(acts.At(col: 4));
        SetMode(false, false);
        return g;
    }

    Button SegBtn(UIElement icon, string tip, Action click)
    {
        var b = new Button { Style = K.Ghost, Width = 30, Height = 24, Tag = new CornerRadius(12), Content = icon, ToolTip = tip, Background = Brushes.Transparent };
        b.Click += (_, _) => click();
        return b;
    }

    static Button RoundBtn(string icon, string tip, Action click, double size, double ico, bool solid = false)
    {
        var bg = solid ? K.Hex("#FFF5F5F7") : K.Hex("#FF2C2C2E");
        var b = new Button
        {
            Style = K.Ghost, Width = size, Height = size, Tag = new CornerRadius(size / 2), Background = bg,
            Content = Ico.Make(icon, ico, solid ? K.Ink : K.Fg, 2.0)
        };
        if (tip.Length > 0) b.ToolTip = tip;
        b.Click += (_, _) => click();
        return b;
    }

    void SetMode(bool list, bool animate = true)
    {
        _showList = list;
        _leftHost.Child = list ? K.Scroll(_list) : _format;
        if (list) RefreshList();
        var tt = (TranslateTransform)_segHl.RenderTransform;
        double x = list ? 0 : 30;
        if (animate && Settings.Current.Animations)
            tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(x, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        else { tt.BeginAnimation(TranslateTransform.XProperty, null); tt.X = x; }
        if (_maximized) ToggleMax();
    }

    void SetSaved(bool saved)
    {
        _savedDot.Background = saved ? K.Hex("#FF2C2C2E") : Brushes.Transparent;
        _savedDot.BorderBrush = K.Hex("#FF3A3A3E"); _savedDot.BorderThickness = new Thickness(saved ? 0 : 1);
        _savedDot.Child = saved ? Ico.Make("check", 13, K.Label, 2.2) : K.T("…", 12, K.Muted, true).Vc();
        if (_savedDot.Child is FrameworkElement fe) fe.HorizontalAlignment = HorizontalAlignment.Center;
        _savedDot.ToolTip = saved ? (_cur != null ? "Salvo às " + _cur.Updated.ToString("HH:mm", Pt) : "Salvo") : "Salvando…";
    }

    void UpdateCount()
    {
        int n = Svc.Notes.Items.Count;
        _count.Text = n == 1 ? "1 nota" : $"{n} notas";
    }

    // ======================================================================= Painel de formatação
    void AddFmt(string id, UIElement icon, string label, string tip, Action run)
    {
        var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var ih = new Grid { Height = 20 };
        if (icon is FrameworkElement fe) { fe.HorizontalAlignment = HorizontalAlignment.Center; fe.VerticalAlignment = VerticalAlignment.Center; }
        ih.Children.Add(icon);
        sp.Children.Add(ih);
        var t = K.T(label, 10.5, K.Fg, true); t.TextTrimming = TextTrimming.None; t.HorizontalAlignment = HorizontalAlignment.Center; t.Margin = new Thickness(0, 3, 0, 0);
        sp.Children.Add(t);
        var b = new Button
        {
            Style = K.Ghost, Content = sp, Tag = new CornerRadius(12), Height = 54, Margin = new Thickness(1, 0, 1, 4),
            Padding = new Thickness(2, 6, 2, 6), ToolTip = tip, Background = Brushes.Transparent
        };
        b.Click += (_, _) => { run(); _edit.Focus(); SyncFmtState(); };
        _fmtBtns[id] = b;
        _format.Children.Add(b);
    }

    static UIElement TextIcon(string s, double size, FontWeight w, bool italic = false, bool strike = false, bool small = false)
    {
        var t = new TextBlock
        {
            Text = s, FontSize = size, FontWeight = w, Foreground = K.Fg,
            FontFamily = italic ? new FontFamily("Georgia, Times New Roman") : K.Font,
            FontStyle = italic ? FontStyles.Italic : FontStyles.Normal
        };
        if (strike) t.TextDecorations = TextDecorations.Strikethrough;
        if (small) t.Typography.Capitals = FontCapitals.Normal;
        return t;
    }

    /// <summary>Acende os botões do que está aplicado no trecho selecionado / onde está o cursor.</summary>
    void SyncFmtState()
    {
        if (_fmtBtns.Count == 0) return;
        var sel = _edit.Selection;
        bool bold = Eq(sel.GetPropertyValue(TextElement.FontWeightProperty), FontWeights.Bold);
        bool italic = Eq(sel.GetPropertyValue(TextElement.FontStyleProperty), FontStyles.Italic);
        bool strike = sel.GetPropertyValue(Inline.TextDecorationsProperty) is TextDecorationCollection td && td.Count > 0;
        bool code = sel.GetPropertyValue(TextElement.FontFamilyProperty) is FontFamily ff && ff.Source == K.Mono.Source;
        var para = sel.Start.Paragraph;
        var list = para?.Parent as ListItem is ListItem li ? li.List : null;
        bool heading = para != null && para.FontSize >= HeadingSize - 0.1;
        Set("bold", bold && !heading); Set("italic", italic); Set("strike", strike); Set("code", code);
        Set("list", list != null && list.MarkerStyle != TextMarkerStyle.Decimal);
        Set("number", list != null && list.MarkerStyle == TextMarkerStyle.Decimal);
        Set("heading", heading);

        static bool Eq(object v, object want) => v != DependencyProperty.UnsetValue && Equals(v, want);
        void Set(string id, bool on) => _fmtBtns[id].Background = on ? K.Hex("#FF26262A") : Brushes.Transparent;
    }

    void ToggleStrike()
    {
        var sel = _edit.Selection;
        bool on = sel.GetPropertyValue(Inline.TextDecorationsProperty) is TextDecorationCollection td && td.Count > 0;
        sel.ApplyPropertyValue(Inline.TextDecorationsProperty, on ? new TextDecorationCollection() : TextDecorations.Strikethrough);
    }

    void ToggleCode()
    {
        var sel = _edit.Selection;
        bool on = sel.GetPropertyValue(TextElement.FontFamilyProperty) is FontFamily ff && ff.Source == K.Mono.Source;
        if (sel.IsEmpty && !on)
        {
            // Sem seleção: o próximo texto digitado já sai como código.
            var run = new Run("", _edit.CaretPosition) { FontFamily = K.Mono, Background = CodeBg, Foreground = K.Hex("#FFE6E6EB") };
            _edit.CaretPosition = run.ContentStart;
            return;
        }
        sel.ApplyPropertyValue(TextElement.FontFamilyProperty, on ? K.Font : K.Mono);
        sel.ApplyPropertyValue(TextElement.BackgroundProperty, on ? null : CodeBg);
        sel.ApplyPropertyValue(TextElement.ForegroundProperty, on ? K.Fg : K.Hex("#FFE6E6EB"));
        sel.ApplyPropertyValue(TextElement.FontSizeProperty, on ? BodySize : BodySize - 0.5);
    }

    void ToggleHeading()
    {
        var sel = _edit.Selection;
        var start = sel.Start.Paragraph; if (start == null) return;
        bool on = start.FontSize >= HeadingSize - 0.1;
        foreach (var p in ParagraphsIn(sel))
        {
            p.FontSize = on ? BodySize : HeadingSize;
            p.FontWeight = on ? FontWeights.Normal : FontWeights.Bold;
            p.Margin = on ? new Thickness(0) : new Thickness(0, 4, 0, 2);
            // remove tamanhos/pesos aplicados por trecho, senão o título fica "manchado"
            new TextRange(p.ContentStart, p.ContentEnd).ApplyPropertyValue(TextElement.FontSizeProperty, p.FontSize);
        }
    }

    static IEnumerable<Paragraph> ParagraphsIn(TextSelection sel)
    {
        var first = sel.Start.Paragraph; var last = sel.End.Paragraph;
        if (first == null) yield break;
        var seen = new HashSet<Paragraph>();
        for (var pos = sel.Start; pos != null && pos.CompareTo(sel.End) <= 0; pos = pos.GetNextContextPosition(LogicalDirection.Forward))
            if (pos.Paragraph is Paragraph p && seen.Add(p)) yield return p;
        if (last != null && seen.Add(last)) yield return last;
    }

    void OnKeys(object sender, KeyEventArgs e)
    {
        var m = Keyboard.Modifiers;
        bool ctrl = (m & ModifierKeys.Control) != 0, shift = (m & ModifierKeys.Shift) != 0;
        if (!ctrl) return;
        Action? a = (e.Key, shift) switch
        {
            (Key.X, true) => ToggleStrike,
            (Key.E, false) => ToggleCode,
            (Key.L, true) => () => EditingCommands.ToggleBullets.Execute(null, _edit),
            (Key.N, true) => () => EditingCommands.ToggleNumbering.Execute(null, _edit),
            (Key.H, true) => ToggleHeading,
            (Key.N, false) => NewNote,
            (Key.U, false) => () => { },          // sem sublinhado: não faz parte do visual das notas
            _ => null
        };
        if (a == null) return;
        e.Handled = true; a(); SyncFmtState();
    }

    void ToggleMax()
    {
        _maximized = !_maximized;
        _leftHost.Visibility = _divider.Visibility = _maximized ? Visibility.Collapsed : Visibility.Visible;
        _maxBtn.Content = Ico.Make(_maximized ? "minimize" : "maximize", 13, K.Fg, 2.0);
        _maxBtn.ToolTip = _maximized ? "Recolher editor" : "Expandir editor";
        _edit.Focus();
    }

    // ======================================================================= Lista de notas
    void RefreshList()
    {
        _list.Children.Clear();
        foreach (var n in Svc.Notes.Items)
        {
            var note = n;
            bool sel = note == _cur;
            var s = new StackPanel();
            var title = K.T(note.Title, 12, sel ? K.Fg : K.Dim, true); title.TextTrimming = TextTrimming.CharacterEllipsis;
            s.Children.Add(title);
            s.Children.Add(K.T(note.Updated.ToString("d MMM 'às' HH:mm", Pt), 10, K.Muted).M(0, 2, 0, 0));
            var b = new Button
            {
                Style = K.Ghost, Content = s, Background = sel ? K.Hex("#FF1C1C1E") : Brushes.Transparent, Tag = new CornerRadius(12),
                Padding = new Thickness(11, 8, 11, 8), Margin = new Thickness(0, 0, 0, 3), HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            s.Width = 186;
            b.Click += (_, _) => { Open(note); SetMode(false); };
            _list.Children.Add(b);
        }
    }

    // ======================================================================= Dados
    void Open(Note n)
    {
        if (_debounce.IsEnabled) { _debounce.Stop(); Persist(); }
        _cur = n; _loading = true;
        var doc = NewDoc();
        bool ok = false;
        if (!string.IsNullOrEmpty(n.Rich))
        {
            try
            {
                var r = new TextRange(doc.ContentStart, doc.ContentEnd);
                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(n.Rich));
                r.Load(ms, DataFormats.Xaml); ok = true;
            }
            catch (Exception e) { Log.Write(e); }
        }
        if (!ok)
        {
            doc.Blocks.Clear();
            foreach (var line in (n.Text ?? "").Replace("\r", "").Split('\n')) doc.Blocks.Add(new Paragraph(new Run(line)));
        }
        _edit.Document = doc;
        _loading = false;
        SyncHint(); SetSaved(true); UpdateCount(); SyncFmtState();
        if (_showList) RefreshList();
    }

    FlowDocument NewDoc()
    {
        var d = new FlowDocument { PagePadding = new Thickness(0), FontFamily = K.Font, FontSize = BodySize, Foreground = K.Fg, LineHeight = 19 };
        d.Resources.Add(typeof(Paragraph), new Style(typeof(Paragraph)) { Setters = { new Setter(Block.MarginProperty, new Thickness(0)) } });
        d.Resources.Add(typeof(List), new Style(typeof(List))
        {
            Setters = { new Setter(Block.MarginProperty, new Thickness(0)), new Setter(List.PaddingProperty, new Thickness(18, 0, 0, 0)), new Setter(List.MarkerOffsetProperty, 8.0) }
        });
        return d;
    }

    void Persist()
    {
        if (_cur == null) return;
        var r = new TextRange(_edit.Document.ContentStart, _edit.Document.ContentEnd);
        _cur.Text = r.Text.TrimEnd('\r', '\n');
        try
        {
            using var ms = new MemoryStream();
            r.Save(ms, DataFormats.Xaml);
            _cur.Rich = Encoding.UTF8.GetString(ms.ToArray());
        }
        catch (Exception e) { Log.Write(e); }
        _cur.Updated = DateTime.Now;
        // a nota editada sobe para o topo
        var items = Svc.Notes.Items;
        if (items.IndexOf(_cur) > 0) { items.Remove(_cur); items.Insert(0, _cur); }
        Svc.Notes.Save();
        SetSaved(true);
        if (_showList) RefreshList();
    }

    void SyncHint()
    {
        var d = _edit.Document;
        bool empty = d.Blocks.Count == 0 || d.Blocks.Count == 1 && d.Blocks.FirstBlock is Paragraph p && p.Inlines.Count == 0
                     || new TextRange(d.ContentStart, d.ContentEnd).Text.Trim().Length == 0 && !d.Blocks.OfType<List>().Any();
        _hint.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
    }

    void NewNote()
    {
        if (_debounce.IsEnabled) { _debounce.Stop(); Persist(); }
        // não empilha notas vazias: reaproveita a vazia que já está no topo
        var top = Svc.Notes.Items.FirstOrDefault();
        var n = top != null && string.IsNullOrWhiteSpace(top.Text) && top == _cur ? top : Svc.Notes.Add();
        Open(n);
        if (_showList) SetMode(false);
        Root.Dispatcher.BeginInvoke(() => { _edit.Focus(); Keyboard.Focus(_edit); }, DispatcherPriority.Input);
    }

    void Delete()
    {
        if (_cur == null) return;
        _debounce.Stop();
        Svc.Notes.Remove(_cur); _cur = null;
        if (Svc.Notes.Items.Count == 0) Svc.Notes.Add();
        Open(Svc.Notes.Items[0]);
    }

    void Export()
    {
        if (_cur == null) return;
        if (_debounce.IsEnabled) { _debounce.Stop(); Persist(); }
        try
        {
            string safe = string.Concat(_cur.Title.Take(40).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
            if (safe.Length == 0) safe = "Nota";
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var file = Path.Combine(dir, $"{safe}.txt");
            for (int i = 2; File.Exists(file); i++) file = Path.Combine(dir, $"{safe} ({i}).txt");
            File.WriteAllText(file, PlainWithMarkers(), new UTF8Encoding(true));
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = true });
        }
        catch (Exception e) { Log.Write(e); }
    }

    /// <summary>Texto da nota com marcadores de lista ("• " e "1. ") para o arquivo exportado.</summary>
    string PlainWithMarkers()
    {
        var sb = new StringBuilder();
        void Blocks(IEnumerable<Block> blocks, int depth)
        {
            foreach (var b in blocks)
            {
                if (b is Paragraph p) sb.AppendLine(new TextRange(p.ContentStart, p.ContentEnd).Text);
                else if (b is List l)
                {
                    int i = 1;
                    foreach (var li in l.ListItems)
                    {
                        string mk = l.MarkerStyle == TextMarkerStyle.Decimal ? $"{i++}. " : "• ";
                        bool first = true;
                        foreach (var inner in li.Blocks)
                        {
                            if (inner is Paragraph ip) { sb.Append(new string(' ', depth * 2)).Append(first ? mk : "  ").AppendLine(new TextRange(ip.ContentStart, ip.ContentEnd).Text); first = false; }
                            else Blocks(new[] { inner }, depth + 1);
                        }
                    }
                }
                else if (b is Section s) Blocks(s.Blocks, depth);
            }
        }
        Blocks(_edit.Document.Blocks, 0);
        return sb.ToString().TrimEnd();
    }
}
