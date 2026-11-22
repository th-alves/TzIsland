namespace TzIsland;

/// <summary>
/// Productivity Suite: Corporate dashboard featuring Notes, Task Management,
/// Focus/Pomodoro sessions, Calendar Agenda and Quick Launch Shortcuts.
/// </summary>
public sealed class ProductivityPage : PageBase
{
    readonly Grid _body = new();
    readonly ChipRow _chipRow;
    IPage? _sub;
    bool _shown;

    static readonly (string id, string label)[] Tabs =
    {
        ("notes", "Notas"),
        ("tasks", "Tarefas"),
        ("agenda", "Agenda"),
        ("shortcuts", "Atalhos")
    };

    public ProductivityPage()
    {
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new RowDefinition { Height = K.GL("*") });

        // Abas como "chips" com destaque branco que desliza (mesmo componente do Timer)
        _chipRow = new ChipRow(Tabs.Select(t => t.label), 86, 6) { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 10) };
        _chipRow.Picked += i => Select(Tabs[i].id);
        Root.Children.Add(_chipRow);
        Root.Children.Add(_body.At(row: 1));
        Select("notes", false);
    }

    public override bool KeepOpen => _sub?.KeepOpen ?? false;

    public override IEnumerable<FrameworkElement> RevealTargets =>
        new FrameworkElement[] { _chipRow }.Concat(_sub is PageBase pb ? pb.RevealTargets : new[] { _body });
    public override void Shown() { _shown = true; _sub?.Shown(); }
    public override void Hidden() { _shown = false; _sub?.Hidden(); }

    void Select(string id, bool animate = true)
    {
        if (_sub != null) { if (_shown) _sub.Hidden(); _body.Children.Clear(); }
        _sub = id switch
        {
            "notes" => new NotesView(),
            "tasks" => new TasksView(),
            "agenda" => new AgendaView(),
            _ => new ShortcutsView()
        };
        _body.Children.Add(_sub.View);
        _sub.View.BeginAnimation(UIElement.OpacityProperty, null); _sub.View.Opacity = 1;
        if (animate && Settings.Current.Animations && _sub is PageBase sp) Motion.RevealAll(sp.RevealTargets, 0.0, 0.05, 14, 0.96);
        if (_shown) _sub.Shown();
        _chipRow.Select(Array.FindIndex(Tabs, t => t.id == id), animate);
    }
}

sealed class NotesView : PageBase
{
    static readonly CultureInfo Pt = new("pt-BR");
    readonly StackPanel _list = new();
    readonly TextBox _edit = K.Box("Escreva sua anotação…", true);
    readonly TextBlock _meta = K.T("", 10.5, K.Muted);
    readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(600) };
    Note? _cur;
    bool _loading;

    public NotesView()
    {
        var g = K.Cols("232", "*");

        // ---- Cartão esquerdo: lista de notas
        var left = K.Rows("auto", "*");
        var lh = K.Cols("*", "auto");
        lh.Children.Add(FK.Header(Ico.Make("file-text", 12, K.Label, 1.9), "Notas"));
        lh.Children.Add(K.IconBtn("\uE710", "Nova nota", () => { var n = Svc.Notes.Add(); Open(n); Refresh(); _edit.Focus(); }, 12, 28, 24, K.Fg).At(col: 1));
        lh.Margin = new Thickness(4, 0, 0, 8);
        left.Children.Add(lh);
        left.Children.Add(K.Scroll(_list).At(row: 1));
        var cLeft = FK.Card(left, 20); cLeft.Padding = new Thickness(10, 12, 10, 10); cLeft.Margin = new Thickness(0, 0, 5, 0);
        g.Children.Add(cLeft);

        // ---- Cartão direito: editor
        var right = K.Rows("auto", "*");
        var rh = K.Cols("*", "auto", "auto");
        rh.Children.Add(FK.Header(Ico.Make("pen", 12, K.Label, 1.9), "Editor"));
        rh.Children.Add(_meta.At(col: 1).Vc().M(0, 0, 6, 0));
        rh.Children.Add(K.IconBtn("\uE74D", "Excluir nota", Delete, 12, 28, 24, K.Red).At(col: 2));
        rh.Margin = new Thickness(2, 0, 0, 8);
        right.Children.Add(rh);
        _edit.Background = K.Bg; _edit.FontFamily = K.Mono; _edit.FontSize = 12.5;
        _edit.Padding = new Thickness(14, 12, 14, 12);
        right.Children.Add(_edit.At(row: 1));
        var cRight = FK.Card(right, 20); cRight.Padding = new Thickness(12, 12, 12, 12); cRight.Margin = new Thickness(5, 0, 0, 0);
        g.Children.Add(cRight.At(col: 1));
        Root.Children.Add(g);

        _edit.TextChanged += (_, _) => { if (_loading || _cur == null) return; _cur.Text = _edit.Text; _cur.Updated = DateTime.Now; _debounce.Stop(); _debounce.Start(); UpdateMeta(true); };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Svc.Notes.Save(); UpdateMeta(false); Refresh(); };
        if (Svc.Notes.Items.Count > 0) Open(Svc.Notes.Items[0]);
        Refresh();
    }

    public override bool KeepOpen => _edit.IsKeyboardFocusWithin;

    public override void Hidden() { if (_debounce.IsEnabled) { _debounce.Stop(); Svc.Notes.Save(); } }

    void Open(Note n)
    {
        _cur = n; _loading = true; _edit.Text = n.Text; _loading = false; _edit.IsEnabled = true; UpdateMeta(false);
        Refresh();
    }

    void UpdateMeta(bool saving)
    {
        if (_cur == null) { _meta.Text = "Crie uma nota para começar"; return; }
        _meta.Text = saving ? "salvando…" : "salvo " + _cur.Updated.ToString("HH:mm", Pt);
    }

    void Delete()
    {
        if (_cur == null) return;
        Svc.Notes.Remove(_cur); _cur = null; _loading = true; _edit.Text = ""; _loading = false;
        if (Svc.Notes.Items.Count > 0) Open(Svc.Notes.Items[0]); else { _edit.IsEnabled = false; UpdateMeta(false); Refresh(); }
    }

    void Refresh()
    {
        _list.Children.Clear();
        if (Svc.Notes.Items.Count == 0)
        {
            var empty = new StackPanel { Margin = new Thickness(0, 26, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
            empty.Children.Add(K.Glyph("\uE70F", 20, K.Muted));
            var t = K.T("Nenhuma nota ainda", 12, K.Label, true); t.HorizontalAlignment = HorizontalAlignment.Center; t.Margin = new Thickness(0, 8, 0, 2);
            var t2 = K.T("Toque em + para criar", 11, K.Muted); t2.HorizontalAlignment = HorizontalAlignment.Center;
            empty.Children.Add(t); empty.Children.Add(t2);
            _list.Children.Add(empty);
            _edit.IsEnabled = false; UpdateMeta(false);
            return;
        }
        foreach (var n in Svc.Notes.Items)
        {
            var note = n;
            bool sel = note == _cur;
            var s = new StackPanel();
            s.Children.Add(K.T(note.Title, 12.5, K.Fg, true));
            s.Children.Add(K.T(note.Updated.ToString("d MMM yyyy 'às' HH:mm", Pt), 10, K.Muted).M(0, 2, 0, 0));
            var btn = new Button
            {
                Style = K.Ghost, Content = s, Background = sel ? K.CardStrong : K.Clear, Tag = new CornerRadius(12),
                Padding = new Thickness(11, 9, 11, 9), Margin = new Thickness(0, 0, 0, 3), HorizontalAlignment = HorizontalAlignment.Stretch
            };
            s.Width = 186;
            btn.Click += (_, _) => Open(note);
            _list.Children.Add(btn);
        }
    }
}

sealed class TasksView : PageBase
{
    readonly StackPanel _list = new();
    readonly TextBox _input = K.Box("Nova tarefa corporativa… (Enter para adicionar)");
    readonly TextBlock _count = K.T("", 11, K.Dim);
    int _prio = 1;
    readonly Button _prioBtn;
    static readonly string[] PrioName = { "Alta", "Média", "Baixa" };
    static readonly Brush[] PrioColor = { K.Red, K.Orange, K.Cyan };

    public TasksView()
    {
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new RowDefinition { Height = K.GL("*") });
        var top = K.Cols("*", "auto", "auto", "auto");
        top.Children.Add(_input);
        _prioBtn = K.TextBtn("Média", () => { _prio = (_prio + 2) % 3; SyncPrio(); });
        top.Children.Add(_prioBtn.At(col: 1).M(6, 0, 0, 0));
        top.Children.Add(K.IconBtn("\uE710", "Adicionar tarefa", Add, 14, 36, 30).At(col: 2).M(4, 0, 0, 0));
        top.Children.Add(_count.At(col: 3).Vc().M(10, 0, 4, 0));
        top.Margin = new Thickness(0, 0, 0, 8);
        Root.Children.Add(top);
        Root.Children.Add(K.Scroll(_list).At(row: 1));
        _input.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Add(); e.Handled = true; } };
        SyncPrio(); Refresh();
    }

    public override bool KeepOpen => _input.IsKeyboardFocusWithin || _list.Children.OfType<Grid>().Any(g => g.Children.OfType<TextBox>().Any());

    void SyncPrio() { _prioBtn.Content = K.T(PrioName[_prio], 12, PrioColor[_prio], true); }

    void Add()
    {
        var t = _input.Text.Trim();
        if (t.Length == 0) return;
        Svc.Tasks.Add(t, _prio); _input.Text = ""; Refresh();
    }

    void Refresh()
    {
        _list.Children.Clear();
        _count.Text = $"{Svc.Tasks.Pending} pendentes";
        if (Svc.Tasks.Items.Count == 0) _list.Children.Add(K.T("Nenhuma tarefa. Adicione sua primeira atividade acima.", 12, K.Dim).M(4, 12, 0, 0));
        foreach (var t in Svc.Tasks.Sorted()) _list.Children.Add(Row(t));
    }

    UIElement Row(TaskItem t)
    {
        var g = K.Cols("32", "*", "auto", "auto");
        var check = K.IconBtn(t.Done ? "\uE73E" : "\uEA3A", "Concluir", () => { t.Done = !t.Done; Svc.Tasks.Save(); Refresh(); }, 15, 28, 28, t.Done ? K.Green : K.Dim);
        g.Children.Add(check);
        var txt = K.T(t.Text, 13, t.Done ? K.Faint : K.Fg);
        if (t.Done) txt.TextDecorations = TextDecorations.Strikethrough;
        txt.Vc(); txt.Cursor = Cursors.IBeam; txt.ToolTip = "Duplo clique para editar";
        txt.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) { e.Handled = true; Edit(g, t); } };
        g.Children.Add(txt.At(col: 1));
        var pb = K.IconBtn("\uE91F", "Prioridade: " + PrioName[t.Priority] + " (clique para alterar)", () => { t.Priority = (t.Priority + 2) % 3; Svc.Tasks.Save(); Refresh(); }, 10, 24, 28, PrioColor[t.Priority]);
        g.Children.Add(pb.At(col: 2));
        g.Children.Add(K.IconBtn("\uE74D", "Excluir", () => { Svc.Tasks.Remove(t); Refresh(); }, 12, 28, 28, K.Dim).At(col: 3));
        return new Border
        {
            Background = K.Card,
            CornerRadius = new CornerRadius(11),
            BorderBrush = K.Stroke,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 0, 0, 4),
            Child = g
        };
    }

    void Edit(Grid row, TaskItem t)
    {
        var tb = K.Box(); tb.Text = t.Text; tb.Padding = new Thickness(8, 4, 8, 4);
        void Commit() { var v = tb.Text.Trim(); if (v.Length > 0) t.Text = v; Svc.Tasks.Save(); Refresh(); }
        tb.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(); else if (e.Key == Key.Escape) Refresh(); };
        tb.LostKeyboardFocus += (_, _) => Commit();
        row.Children.RemoveAt(1);
        row.Children.Add(tb.At(col: 1));
        IslandWindow.Instance?.EnableKeyboard();
        tb.Focus(); tb.SelectAll();
    }
}

sealed class AgendaView : PageBase
{
    readonly StackPanel _list = new();
    readonly TextBox _title = K.Box("Título do evento corporativo"), _when = K.Box("dd/MM HH:mm");

    public AgendaView()
    {
        Root.RowDefinitions.Add(new RowDefinition { Height = K.GL("*") });
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.Children.Add(K.Scroll(_list));
        var add = K.Cols("*", "120", "auto", "auto");
        add.Children.Add(_title);
        add.Children.Add(_when.At(col: 1).M(6, 0, 0, 0));
        add.Children.Add(K.IconBtn("\uE710", "Adicionar evento local", Add, 14, 36, 30).At(col: 2).M(4, 0, 0, 0));
        add.Children.Add(K.IconBtn("\uE72C", "Atualizar calendários externos", () => _ = Svc.Calendar.RefreshIcsAsync(), 13, 36, 30).At(col: 3));
        add.Margin = new Thickness(0, 8, 0, 0);
        Root.Children.Add(add.At(row: 1));
    }

    public override bool KeepOpen => _title.IsKeyboardFocusWithin || _when.IsKeyboardFocusWithin;
    public override void Shown() { Svc.Calendar.Changed += Refresh; Refresh(); }
    public override void Hidden() { Svc.Calendar.Changed -= Refresh; }

    void Add()
    {
        if (string.IsNullOrWhiteSpace(_title.Text)) return;
        if (!DateTime.TryParseExact(_when.Text.Trim(), new[] { "dd/MM HH:mm", "dd/MM/yyyy HH:mm", "HH:mm" }, new CultureInfo("pt-BR"), DateTimeStyles.None, out var d))
        { _when.Text = ""; _when.Tag = "Use dd/MM HH:mm"; return; }
        if (d.Year == 1) d = DateTime.Today.Add(d.TimeOfDay);
        Svc.Calendar.AddLocal(new CalEvent { Title = _title.Text.Trim(), Start = d, End = d.AddHours(1) });
        _title.Text = ""; _when.Text = "";
    }

    void Refresh()
    {
        _list.Children.Clear();
        var today = DateTime.Today;
        var events = Svc.Calendar.Between(today, today.AddDays(8));
        if (Svc.Calendar.ExternalCount == 0 && Settings.Current.IcsUrls.Count == 0)
            _list.Children.Add(K.T("Dica: adicione o link ICS do calendário em Configurações > Integrações.", 11, K.Faint).M(4, 0, 0, 6));
        if (events.Count == 0) _list.Children.Add(K.T("Sem compromissos agendados nos próximos 7 dias.", 12, K.Dim).M(4, 8, 0, 0));
        foreach (var grp in events.GroupBy(e => e.Start.Date))
        {
            var isToday = grp.Key == today;
            var headerText = isToday ? "HOJE" : grp.Key.ToString("dddd, dd/MM", new CultureInfo("pt-BR")).ToUpperInvariant();
            var headerColor = isToday ? K.Sky : K.Faint;
            _list.Children.Add(K.T(headerText, 10, headerColor, true).M(4, 8, 0, 4));
            foreach (var e in grp)
            {
                var g = K.Cols("66", "*", "auto");
                g.Children.Add(K.T(e.AllDay ? "dia todo" : e.Start.ToString("HH:mm"), 12, K.Sky, true).Vc());
                var s = new StackPanel();
                s.Children.Add(K.T(e.Title, 13, K.Fg, true));
                if (e.Location.Length > 0) s.Children.Add(K.T(e.Location, 11, K.Dim));
                g.Children.Add(s.At(col: 1));
                var ev = e;
                if (Svc.Calendar.IsLocal(e)) g.Children.Add(K.IconBtn("\uE711", "Remover", () => Svc.Calendar.RemoveLocal(ev), 11, 26, 24, K.Dim).At(col: 2));
                _list.Children.Add(new Border
                {
                    Background = K.Card,
                    CornerRadius = new CornerRadius(11),
                    BorderBrush = K.Stroke,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 7, 8, 7),
                    Margin = new Thickness(0, 0, 0, 4),
                    Child = g
                });
            }
        }
    }
}

sealed class ShortcutsView : PageBase
{
    readonly WrapPanel _tiles = new();
    readonly TextBox _name = K.Box("Nome"), _cmd = K.Box("Programa, pasta, site ou ms-settings:…"), _args = K.Box("Argumentos (opcional)");

    public ShortcutsView()
    {
        Root.RowDefinitions.Add(new RowDefinition { Height = K.GL("*") });
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.Children.Add(K.Scroll(_tiles));
        var f = K.Cols("120", "*", "120", "auto");
        f.Children.Add(_name);
        f.Children.Add(_cmd.At(col: 1).M(6, 0, 0, 0));
        f.Children.Add(_args.At(col: 2).M(6, 0, 0, 0));
        f.Children.Add(K.IconBtn("\uE710", "Criar atalho", Add, 14, 36, 30).At(col: 3).M(4, 0, 0, 0));
        f.Margin = new Thickness(0, 8, 0, 0);
        Root.Children.Add(f.At(row: 1));
        Refresh();
    }

    public override bool KeepOpen => _name.IsKeyboardFocusWithin || _cmd.IsKeyboardFocusWithin || _args.IsKeyboardFocusWithin;

    void Add()
    {
        if (string.IsNullOrWhiteSpace(_name.Text) || string.IsNullOrWhiteSpace(_cmd.Text)) return;
        Settings.Current.Shortcuts.Add(new ShortcutItem { Name = _name.Text.Trim(), Command = _cmd.Text.Trim(), Args = _args.Text.Trim() });
        Settings.Apply();
        _name.Text = _cmd.Text = _args.Text = "";
        Refresh();
    }

    static void Run(ShortcutItem s)
    {
        try { Process.Start(new ProcessStartInfo(s.Command, s.Args) { UseShellExecute = true }); }
        catch (Exception e) { Hub.Notify("Atalhos", "Não foi possível executar", s.Name + ": " + e.Message, false); }
    }

    void Refresh()
    {
        _tiles.Children.Clear();
        foreach (var sc in Settings.Current.Shortcuts.ToList())
        {
            var s = sc;
            var g = K.Cols("24", "*");
            g.Children.Add(K.Glyph("\uE71B", 13, K.Sky));
            g.Children.Add(K.T(s.Name, 12, K.Fg, true).Vc().At(col: 1));

            var b = new Button
            {
                Style = K.Ghost,
                Content = g,
                Width = 142,
                Height = 40,
                Margin = new Thickness(0, 0, 6, 6),
                Background = K.Card,
                Tag = new CornerRadius(10),
                Padding = new Thickness(8, 0, 8, 0)
            };
            b.Click += (_, _) => Run(s);
            var menu = new ContextMenu();
            var del = new MenuItem { Header = "Remover atalho" };
            del.Click += (_, _) => { Settings.Current.Shortcuts.Remove(s); Settings.Apply(); Refresh(); };
            menu.Items.Add(del);
            b.ContextMenu = menu;
            b.ToolTip = s.Command + (s.Args.Length > 0 ? " " + s.Args : "") + " (clique direito para remover)";
            _tiles.Children.Add(b);
        }
    }
}
