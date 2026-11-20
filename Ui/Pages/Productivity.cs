namespace TzIsland;

public sealed class ProductivityPage : PageBase
{
    readonly Grid _body = new();
    readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
    readonly Dictionary<string, Button> _tabBtn = new();
    string _tab = "notes";
    IPage? _sub;
    bool _shown;

    static readonly (string id, string label)[] Tabs =
    { ("notes", "Notas"), ("tasks", "Tarefas"), ("focus", "Foco"), ("agenda", "Agenda"), ("shortcuts", "Atalhos") };

    public ProductivityPage()
    {
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new RowDefinition { Height = K.GL("*") });
        foreach (var (id, label) in Tabs)
        {
            var b = K.TextBtn(label, () => Select(id));
            b.Margin = new Thickness(0, 0, 6, 0);
            _tabBtn[id] = b; _tabs.Children.Add(b);
        }
        Root.Children.Add(_tabs);
        Root.Children.Add(_body.At(row: 1));
        Select("notes");
    }

    public override bool KeepOpen => _sub?.KeepOpen ?? false;
    public override void Shown() { _shown = true; _sub?.Shown(); }
    public override void Hidden() { _shown = false; _sub?.Hidden(); }

    void Select(string id)
    {
        if (_sub != null) { if (_shown) _sub.Hidden(); _body.Children.Clear(); }
        _tab = id;
        _sub = id switch { "notes" => new NotesView(), "tasks" => new TasksView(), "focus" => new FocusView(), "agenda" => new AgendaView(), _ => new ShortcutsView() };
        _body.Children.Add(_sub.View);
        K.FadeIn(_sub.View, 0, 6);
        if (_shown) _sub.Shown();
        foreach (var kv in _tabBtn) kv.Value.Background = kv.Key == id ? K.CardStrong : K.Card;
    }
}

sealed class NotesView : PageBase
{
    readonly StackPanel _list = new();
    readonly TextBox _edit = K.Box("Escreva sua nota…", true);
    readonly TextBlock _meta = K.T("", 10, K.Faint);
    readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(600) };
    Note? _cur;
    bool _loading;

    public NotesView()
    {
        var g = K.Cols("170", "*");
        var left = K.Rows("auto", "*");
        var add = K.TextBtn("+ Nova nota", () => { var n = Svc.Notes.Add(); Open(n); Refresh(); _edit.Focus(); }, true);
        left.Children.Add(add);
        left.Children.Add(K.Scroll(_list).At(row: 1).M(0, 6, 8, 0));
        g.Children.Add(left);

        var right = K.Rows("*", "auto");
        right.Children.Add(_edit.M(0));
        var foot = K.Cols("*", "auto");
        foot.Children.Add(_meta.Vc());
        foot.Children.Add(K.IconBtn("\uE74D", "Excluir nota", Delete, 14, 34, 28, K.Red).At(col: 1));
        foot.Margin = new Thickness(2, 4, 0, 0);
        right.Children.Add(foot.At(row: 1));
        g.Children.Add(right.At(col: 1));
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
    }

    void UpdateMeta(bool saving) => _meta.Text = _cur == null ? "Crie uma nota para começar" : (saving ? "Salvando…" : "Salvo automaticamente · ") + (saving ? "" : _cur.Updated.ToString("dd/MM HH:mm"));

    void Delete()
    {
        if (_cur == null) return;
        Svc.Notes.Remove(_cur); _cur = null; _loading = true; _edit.Text = ""; _loading = false;
        if (Svc.Notes.Items.Count > 0) Open(Svc.Notes.Items[0]); else { _edit.IsEnabled = false; UpdateMeta(false); }
        Refresh();
    }

    void Refresh()
    {
        _list.Children.Clear();
        foreach (var n in Svc.Notes.Items)
        {
            var note = n;
            var btn = K.TextBtn(note.Title.Length > 22 ? note.Title[..22] + "…" : note.Title, () => Open(note));
            btn.HorizontalContentAlignment = HorizontalAlignment.Left;
            btn.Background = note == _cur ? K.CardStrong : K.Card;
            btn.Margin = new Thickness(0, 0, 0, 4);
            btn.HorizontalAlignment = HorizontalAlignment.Stretch;
            _list.Children.Add(btn);
        }
        if (Svc.Notes.Items.Count == 0) { _edit.IsEnabled = false; UpdateMeta(false); }
    }
}

sealed class TasksView : PageBase
{
    readonly StackPanel _list = new();
    readonly TextBox _input = K.Box("Nova tarefa… (Enter para adicionar)");
    readonly TextBlock _count = K.T("", 11, K.Dim);
    int _prio = 1;
    readonly Button _prioBtn;
    static readonly string[] PrioName = { "Alta", "Média", "Baixa" };
    static readonly Brush[] PrioColor = { K.Red, K.Orange, K.Blue };

    public TasksView()
    {
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new RowDefinition { Height = K.GL("*") });
        var top = K.Cols("*", "auto", "auto", "auto");
        top.Children.Add(_input);
        _prioBtn = K.TextBtn("Média", () => { _prio = (_prio + 2) % 3; SyncPrio(); });
        top.Children.Add(_prioBtn.At(col: 1).M(6, 0, 0, 0));
        top.Children.Add(K.IconBtn("\uE710", "Adicionar", Add, 14, 36, 30).At(col: 2).M(4, 0, 0, 0));
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
        if (Svc.Tasks.Items.Count == 0) _list.Children.Add(K.T("Nenhuma tarefa. Adicione a primeira acima.", 12, K.Dim).M(4, 10, 0, 0));
        foreach (var t in Svc.Tasks.Sorted()) _list.Children.Add(Row(t));
    }

    UIElement Row(TaskItem t)
    {
        var g = K.Cols("30", "*", "auto", "auto");
        var check = K.IconBtn(t.Done ? "\uE73E" : "\uEA3A", "Concluir", () => { t.Done = !t.Done; Svc.Tasks.Save(); Refresh(); }, 15, 28, 28, t.Done ? K.Green : K.Dim);
        g.Children.Add(check);
        var txt = K.T(t.Text, 13, t.Done ? K.Faint : K.Fg);
        if (t.Done) txt.TextDecorations = TextDecorations.Strikethrough;
        txt.Vc(); txt.Cursor = Cursors.IBeam; txt.ToolTip = "Duplo clique para editar";
        txt.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) { e.Handled = true; Edit(g, t); } };
        g.Children.Add(txt.At(col: 1));
        var pb = K.IconBtn("\uE91F", "Prioridade: " + PrioName[t.Priority] + " (clique para alterar)", () => { t.Priority = (t.Priority + 2) % 3; Svc.Tasks.Save(); Refresh(); }, 9, 24, 28, PrioColor[t.Priority]);
        g.Children.Add(pb.At(col: 2));
        g.Children.Add(K.IconBtn("\uE74D", "Excluir", () => { Svc.Tasks.Remove(t); Refresh(); }, 12, 28, 28, K.Dim).At(col: 3));
        return new Border { Background = K.Card, CornerRadius = new CornerRadius(11), Padding = new Thickness(4, 2, 4, 2), Margin = new Thickness(0, 0, 0, 4), Child = g };
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

sealed class FocusView : PageBase
{
    readonly TextBlock _time = K.T("25:00", 48, K.Fg), _phase = K.T("Foco", 13, K.Dim), _dots = K.T("", 12, K.Dim);
    readonly Button _startBtn;
    readonly Dictionary<FocusMode, Button> _modes = new();
    readonly StackPanel _timerAdj = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

    public FocusView()
    {
        var s = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var modes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var (m, label) in new[] { (FocusMode.Pomodoro, "Pomodoro"), (FocusMode.Timer, "Temporizador"), (FocusMode.Stopwatch, "Cronômetro") })
        {
            var mm = m;
            var b = K.TextBtn(label, () => Svc.Focus.SetMode(mm)); b.Margin = new Thickness(3, 0, 3, 0);
            _modes[m] = b; modes.Children.Add(b);
        }
        s.Children.Add(modes);
        _time.HorizontalAlignment = HorizontalAlignment.Center; _time.Margin = new Thickness(0, 6, 0, 0);
        _time.FontWeight = FontWeights.Light;
        s.Children.Add(_time);
        _phase.HorizontalAlignment = HorizontalAlignment.Center; s.Children.Add(_phase);
        _dots.HorizontalAlignment = HorizontalAlignment.Center; s.Children.Add(_dots);

        _timerAdj.Children.Add(K.TextBtn("−5 min", () => Svc.Focus.AdjustTimer(-5)));
        _timerAdj.Children.Add(K.TextBtn("−1", () => Svc.Focus.AdjustTimer(-1)).M(4, 0, 0, 0));
        _timerAdj.Children.Add(K.TextBtn("+1", () => Svc.Focus.AdjustTimer(1)).M(4, 0, 0, 0));
        _timerAdj.Children.Add(K.TextBtn("+5 min", () => Svc.Focus.AdjustTimer(5)).M(4, 0, 0, 0));
        _timerAdj.Margin = new Thickness(0, 6, 0, 0);
        s.Children.Add(_timerAdj);

        var ctl = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
        _startBtn = K.TextBtn("Iniciar", () => Svc.Focus.Toggle(), true); _startBtn.MinWidth = 90;
        ctl.Children.Add(_startBtn);
        ctl.Children.Add(K.IconBtn("\uE72C", "Reiniciar", () => Svc.Focus.Reset(), 14, 38, 32).M(6, 0, 0, 0));
        ctl.Children.Add(K.IconBtn("\uE893", "Pular fase", () => Svc.Focus.SkipPhase(), 14, 38, 32));
        s.Children.Add(ctl);
        Root.Children.Add(s);
    }

    public override void Shown() { Svc.Focus.Tick += Sync; Sync(); }
    public override void Hidden() { Svc.Focus.Tick -= Sync; }

    void Sync()
    {
        var f = Svc.Focus;
        _time.Text = K.Fmt(f.Display);
        _phase.Text = f.PhaseLabel;
        _startBtn.Content = K.T(f.Running ? "Pausar" : "Iniciar", 12, K.Fg, true);
        foreach (var kv in _modes) kv.Value.Background = kv.Key == f.Mode ? K.CardStrong : K.Card;
        _timerAdj.Visibility = f.Mode == FocusMode.Timer ? Visibility.Visible : Visibility.Collapsed;
        _dots.Text = f.Mode == FocusMode.Pomodoro ? $"{f.CompletedFocus} sessões concluídas" : "";
    }
}

sealed class AgendaView : PageBase
{
    readonly StackPanel _list = new();
    readonly TextBox _title = K.Box("Título do evento"), _when = K.Box("dd/MM HH:mm");

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
        if (d.Year == 1) d = DateTime.Today.Add(d.TimeOfDay);   // só hora => hoje
        else if (d.Year == DateTime.Now.Year || d.Year == 1) { }
        Svc.Calendar.AddLocal(new CalEvent { Title = _title.Text.Trim(), Start = d, End = d.AddHours(1) });
        _title.Text = ""; _when.Text = "";
    }

    void Refresh()
    {
        _list.Children.Clear();
        var today = DateTime.Today;
        var events = Svc.Calendar.Between(today, today.AddDays(8));
        if (Svc.Calendar.ExternalCount == 0 && Settings.Current.IcsUrls.Count == 0)
            _list.Children.Add(K.T("Dica: adicione o link ICS do seu calendário em Configurações > Integrações.", 11, K.Faint).M(4, 0, 0, 6));
        if (events.Count == 0) _list.Children.Add(K.T("Sem eventos nos próximos 7 dias.", 12, K.Dim).M(4, 6, 0, 0));
        foreach (var grp in events.GroupBy(e => e.Start.Date))
        {
            _list.Children.Add(K.T(grp.Key == today ? "HOJE" : grp.Key.ToString("dddd, dd/MM", new CultureInfo("pt-BR")).ToUpperInvariant(), 10, K.Faint, true).M(4, 6, 0, 3));
            foreach (var e in grp)
            {
                var g = K.Cols("62", "*", "auto");
                g.Children.Add(K.T(e.AllDay ? "dia todo" : e.Start.ToString("HH:mm"), 12, K.Blue, true).Vc());
                var s = new StackPanel();
                s.Children.Add(K.T(e.Title, 13));
                if (e.Location.Length > 0) s.Children.Add(K.T(e.Location, 11, K.Dim));
                g.Children.Add(s.At(col: 1));
                var ev = e;
                if (Svc.Calendar.IsLocal(e)) g.Children.Add(K.IconBtn("\uE711", "Remover", () => Svc.Calendar.RemoveLocal(ev), 11, 26, 24, K.Dim).At(col: 2));
                _list.Children.Add(new Border { Background = K.Card, CornerRadius = new CornerRadius(11), Padding = new Thickness(10, 6, 6, 6), Margin = new Thickness(0, 0, 0, 4), Child = g });
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
        var f = K.Cols("110", "*", "110", "auto");
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
            var b = K.TextBtn(s.Name, () => Run(s));
            b.Width = 138; b.Height = 40; b.Margin = new Thickness(0, 0, 6, 6);
            var menu = new ContextMenu();
            var del = new MenuItem { Header = "Remover atalho" };
            del.Click += (_, _) => { Settings.Current.Shortcuts.Remove(s); Settings.Apply(); Refresh(); };
            menu.Items.Add(del);
            b.ContextMenu = menu;
            b.ToolTip = s.Command + (s.Args.Length > 0 ? " " + s.Args : "") + "  (clique direito para remover)";
            _tiles.Children.Add(b);
        }
    }
}
