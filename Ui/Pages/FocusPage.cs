namespace TzIsland;

/// <summary>
/// Foco: Pomodoro, Timer, Cronômetro e Água num painel só. Os três relógios são independentes e continuam
/// rodando com a ilha recolhida — a pílula mostra o que foi iniciado por último.
/// </summary>
public sealed class FocusPage : PageBase
{
    static FocusService F => Svc.Focus;
    static WaterTracker W => Svc.Water;

    // Pequeno "pulo" elástico aplicado aos números quando o estado muda
    sealed class Pop
    {
        public readonly Spring S = new(1);
        public readonly ScaleTransform T = new(1, 1);
        public Pop(FrameworkElement el) { el.RenderTransformOrigin = new Point(0.5, 0.5); el.RenderTransform = T; }
        public void Hit(double amp = 1.14) { S.Snap(amp); S.To(1, 0, 240, 9); }
        public void Step(double dt)
        {
            if (S.Settled) { if (T.ScaleX != 1) T.ScaleX = T.ScaleY = 1; return; }
            S.Step(dt); T.ScaleX = T.ScaleY = S.Value;
        }
    }

    // ---- Pomodoro
    readonly RingProgress _ring = new() { Width = 124, Height = 124, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _pTime = K.Num("25:00", 24), _pPhase = K.T("Foco", 11.5, K.Label), _pDone = K.T("0 concluídos", 10.5, K.Muted), _pFoot = K.T("", 10.5, K.Muted);
    readonly Button _pPlay;

    // ---- Timer
    readonly TextBlock _tTime = K.Num("10:00", 42);
    readonly Waveform _wave = new() { Height = 30, Margin = new Thickness(0, 2, 0, 0) };
    readonly ChipRow _chips = new(new[] { "5m", "10m", "25m" });
    readonly Button _tStart, _tReset;

    // ---- Cronômetro
    readonly TextBlock _sTime = K.Num("0:00.00", 25);
    readonly StackPanel _lapList = new();
    readonly Button _sPlay, _sFlag, _sReset;
    readonly ColumnDefinition _cancelCol = new() { Width = new GridLength(0) };

    // ---- Água
    readonly SwitchToggle _sw = new();
    readonly TextBlock _wCount = K.Num("0 de 8", 12.5), _wLabel = K.T("copos hoje", 12, K.Label), _wHint = K.T("", 10.5, K.Muted);
    readonly System.Windows.Controls.Primitives.UniformGrid _dropsHost = new() { Rows = 1, Margin = new Thickness(0, 4, 0, 0) };
    readonly List<DropGlyph> _drops = new();

    // ---- Animação
    readonly Spring _ringS = new(0), _glowS = new(0), _waveS = new(0), _resetS = new(0);
    readonly Pop _popP, _popT, _popS, _popW;
    readonly Border[] _cards;
    bool _shown, _hooked, _waterDirty;
    long _last;
    double _clock;
    bool? _lastP, _lastT, _lastS;
    bool _lastTTouched;
    int _lastChip = -2, _lastGlasses, _lastLaps = -1;
    string _lastPhase = "";

    static Button Circle(string glyph, string tip, Action click, double size, bool primary, double gs = 12)
        => FK.RoundButton(glyph, tip, click, size, primary ? K.White : K.Track, primary ? K.Ink : K.Fg, gs);

    public FocusPage()
    {
        Root.RowDefinitions.Add(new RowDefinition());
        var cols = K.Cols("1*", "1.04*", "1.16*");
        Root.Children.Add(cols);

        // ======== POMODORO ========
        _pPlay = Circle("\uE769", "Iniciar / pausar", () => F.PomoToggle(), 34, false, 12);
        var pTop = K.Cols("*", "auto");
        pTop.Children.Add(FK.Header(Ico.Make("timer", 12, K.Label, 1.9), "Pomodoro"));
        pTop.Children.Add(_pDone.At(col: 1).Vc());

        var ringHost = new Grid { Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = "Clique para iniciar / pausar" };
        ringHost.Children.Add(_ring);
        var center = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        _pTime.HorizontalAlignment = HorizontalAlignment.Center; _pPhase.HorizontalAlignment = HorizontalAlignment.Center;
        center.Children.Add(_pTime); center.Children.Add(_pPhase.M(0, 1, 0, 0));
        ringHost.Children.Add(center);
        ringHost.MouseLeftButtonDown += (_, _) => F.PomoToggle();
        _popP = new Pop(_pTime);

        var pCtl = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        pCtl.Children.Add(_pPlay);
        pCtl.Children.Add(Circle("\uE72C", "Reiniciar", () => F.PomoReset(), 34, false, 12).M(8, 0, 0, 0));
        pCtl.Children.Add(Circle("\uE893", "Pular fase", () => F.PomoSkip(), 34, false, 12).M(8, 0, 0, 0));
        var pFootRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 9, 0, 0) };
        pFootRow.Children.Add(Ico.Make("clock", 11, K.Muted, 1.9).M(0, 0, 6, 0));
        pFootRow.Children.Add(_pFoot);
        var pGrid = K.Rows("auto", "*", "auto", "auto");
        pGrid.Children.Add(pTop);
        pGrid.Children.Add(ringHost.At(row: 1));
        pGrid.Children.Add(pCtl.At(row: 2));
        pGrid.Children.Add(pFootRow.At(row: 3));
        var cPomo = FK.Card(pGrid, 20); cPomo.Margin = new Thickness(0, 0, 5, 0);

        // ======== TIMER ========
        var tGrid = K.Rows("auto", "*", "auto", "auto", "auto");
        tGrid.Children.Add(FK.Header(Ico.Make("hourglass", 12, K.Label, 1.9), "Temporizador"));
        _tTime.HorizontalAlignment = HorizontalAlignment.Center;
        var tDigits = new Border { Background = Brushes.Transparent, Cursor = Cursors.SizeNS, ToolTip = "Role o mouse para ajustar os minutos", Child = _tTime, VerticalAlignment = VerticalAlignment.Center };
        tDigits.PreviewMouseWheel += (_, e) =>
        {
            if (F.Timer.Running || F.Timer.Touched) return;
            F.TimerSet(F.Timer.Minutes + (e.Delta > 0 ? 1 : -1));
            _popT!.Hit(1.06); e.Handled = true;
        };
        _popT = new Pop(_tTime);
        tGrid.Children.Add(tDigits.At(row: 1));
        tGrid.Children.Add(_wave.At(row: 2));
        _chips.Margin = new Thickness(0, 12, 0, 0);
        _chips.Picked += i => F.TimerSet(i switch { 0 => 5, 1 => 10, _ => 25 });
        tGrid.Children.Add(_chips.At(row: 3));
        var tBtns = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        tBtns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tBtns.ColumnDefinitions.Add(_cancelCol);
        _tStart = FK.PillButton("Iniciar", "\uE768", () => F.TimerToggle(), 36, 18);
        _tStart.HorizontalAlignment = HorizontalAlignment.Stretch;
        _tReset = FK.GhostPill("Cancelar", "\uE711", () => F.TimerReset(), 36, 18);
        _tReset.Opacity = 0; _tReset.ClipToBounds = true; _tReset.IsHitTestVisible = false;
        tBtns.Children.Add(_tStart);
        tBtns.Children.Add(_tReset.At(col: 1));
        tGrid.Children.Add(tBtns.At(row: 4));
        var cTimer = FK.Card(tGrid, 20); cTimer.Margin = new Thickness(5, 0, 5, 0);

        // ======== CRONÔMETRO ========
        var sGrid = K.Rows("auto", "auto", "*");
        sGrid.Children.Add(FK.Header(Ico.Make("clock", 12, K.Label, 1.9), "Cronômetro"));
        var sBody = K.Cols("*", "auto");
        sBody.Margin = new Thickness(0, 6, 0, 0);
        _sTime.VerticalAlignment = VerticalAlignment.Center;
        _popS = new Pop(_sTime);
        _sTime.HorizontalAlignment = HorizontalAlignment.Left;
        sBody.Children.Add(_sTime);
        var sCtl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _sFlag = Circle("\uE7C1", "Registrar volta", () => F.WatchLap(), 30, false, 12);
        _sReset = Circle("\uE72C", "Zerar", () => F.WatchReset(), 30, false, 12);
        _sPlay = Circle("\uE768", "Iniciar / pausar", () => F.WatchToggle(), 32, true, 13);
        sCtl.Children.Add(_sFlag); sCtl.Children.Add(_sReset.M(6, 0, 0, 0)); sCtl.Children.Add(_sPlay.M(6, 0, 0, 0));
        sBody.Children.Add(sCtl.At(col: 1));
        sGrid.Children.Add(sBody.At(row: 1));
        var lapScroll = K.Scroll(_lapList); lapScroll.Margin = new Thickness(0, 5, 0, 0);
        sGrid.Children.Add(lapScroll.At(row: 2));
        var cWatch = FK.Card(sGrid, 20); cWatch.Margin = new Thickness(0, 0, 0, 5);

        // ======== ÁGUA ========
        var wGrid = K.Rows("auto", "auto", "*", "auto");
        var wTop = K.Cols("*", "auto");
        wTop.Children.Add(FK.Header(new DropGlyph { Width = 10, Height = 14, IsHitTestVisible = false, Cursor = Cursors.Arrow }, "Beber água"));
        wTop.Children.Add(_sw.At(col: 1));
        _sw.RenderTransformOrigin = new Point(1, 0.5); _sw.RenderTransform = new ScaleTransform(0.82, 0.82);
        wGrid.Children.Add(wTop);
        var wBody = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
        _popW = new Pop(_wCount);
        wBody.Children.Add(_wCount.Vc());
        wBody.Children.Add(_wLabel.M(5, 0, 0, 0).Vc());
        wGrid.Children.Add(wBody.At(row: 1));
        wGrid.Children.Add(_dropsHost.At(row: 2));
        _wHint.TextWrapping = TextWrapping.Wrap; _wHint.TextTrimming = TextTrimming.None;
        wGrid.Children.Add(_wHint.At(row: 3));
        _sw.Toggled += on => W.SetReminders(on);
        var cWater = FK.Card(wGrid, 20); cWater.Margin = new Thickness(0, 5, 0, 0);

        var right = K.Rows("1.08*", "1*");
        right.Margin = new Thickness(5, 0, 0, 0);
        right.Children.Add(cWatch);
        right.Children.Add(cWater.At(row: 1));

        cols.Children.Add(cPomo);
        cols.Children.Add(cTimer.At(col: 1));
        cols.Children.Add(right.At(col: 2));
        _cards = new[] { cPomo, cTimer, cWatch, cWater };
    }

    public override void Shown()
    {
        _shown = true;
        W.Changed += OnWater;
        BuildDrops();
        _lastGlasses = W.Glasses;
        _ringS.Snap(F.Pomo.Progress);
        _glowS.Snap(F.Pomo.Running ? 1 : 0);
        _waveS.Snap(F.Timer.Running ? 1 : 0);
        _resetS.Snap(F.Timer.Touched ? 1 : 0);
        _lastP = _lastT = _lastS = null; _lastChip = -2; _lastLaps = -1; _lastPhase = "";
        SyncWater(true);
        Hook();
        Frame(0);
    }

    public override void Hidden()
    {
        _shown = false;
        W.Changed -= OnWater;
        if (_hooked) { CompositionTarget.Rendering -= OnRender; _hooked = false; }
    }

    void Hook()
    {
        if (_hooked) return;
        _hooked = true; _last = Stopwatch.GetTimestamp();
        CompositionTarget.Rendering += OnRender;
    }

    void OnRender(object? sender, EventArgs e)
    {
        long now = Stopwatch.GetTimestamp();
        double dt = Math.Min(0.05, (now - _last) / (double)Stopwatch.Frequency);
        _last = now;
        Frame(dt);
    }

    void OnWater() { _waterDirty = true; }

    // ---------------- helpers ----------------
    static TimeSpan Ceil(TimeSpan t) => TimeSpan.FromSeconds(Math.Ceiling(Math.Max(0, t.TotalSeconds)));
    static void Set(TextBlock t, string s) { if (t.Text != s) t.Text = s; }
    static string Fmt2(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}.{t.Milliseconds / 10:00}";

    void SetPlay(Button b, bool running, Brush fg, double size)
    {
        b.Content = K.Glyph(running ? "\uE769" : "\uE768", size, fg);
    }

    void BuildDrops()
    {
        int goal = W.Goal;
        if (_drops.Count == goal && _dropsHost.Children.Count == goal) return;
        _drops.Clear(); _dropsHost.Children.Clear();
        for (int i = 0; i < goal; i++)
        {
            int n = i + 1;
            var d = new DropGlyph { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, Width = double.NaN, Height = double.NaN, VisualScale = 1.25 };
            d.Clicked = () => W.Set(W.Glasses == n ? n - 1 : n);
            _drops.Add(d); _dropsHost.Children.Add(d);
        }
        _dropsHost.Columns = goal;
    }

    void SyncWater(bool snap)
    {
        _waterDirty = false;
        BuildDrops();
        int g = W.Glasses, goal = W.Goal;
        Set(_wCount, $"{g} de {goal}");
        Set(_wHint, W.Reminders ? $"Toque numa gota a cada copo. Lembrete a cada {W.ReminderMinutes} min." : "Toque numa gota a cada copo.");
        for (int i = 0; i < _drops.Count; i++)
        {
            bool filled = i < g;
            double delay = filled ? Math.Max(0, i - _lastGlasses) * 0.05 : Math.Max(0, _lastGlasses - 1 - i) * 0.03;
            _drops[i].SetFilled(filled, !snap, delay);
        }
        if (!snap && g != _lastGlasses) _popW.Hit(1.16);
        _lastGlasses = g;
        if (snap) _sw.SnapTo(W.Reminders); else _sw.IsOn = W.Reminders;
        Set(_wHint, W.Reminders ? $"Toque numa gota a cada copo. Lembrete a cada {W.ReminderMinutes} min." : "Toque numa gota a cada copo.");
    }

    void RebuildLaps(StopwatchClock sw)
    {
        _lapList.Children.Clear();
        if (sw.Laps.Count == 0)
        {
            _lapList.Children.Add(K.T("As voltas aparecem aqui", 10.5, K.Muted).M(0, 4, 0, 0));
            return;
        }
        for (int i = sw.Laps.Count - 1; i >= 0; i--)
        {
            var prev = i > 0 ? sw.Laps[i - 1] : TimeSpan.Zero;
            var row = K.Cols("*", "auto");
            row.Margin = new Thickness(0, 0, 0, 3);
            row.Children.Add(K.T($"Volta {i + 1}", 10.5, K.Label));
            row.Children.Add(FK.Mono(Fmt2(sw.Laps[i] - prev), 10.5, i == sw.Laps.Count - 1 ? K.Fg : K.Label, false).At(col: 1));
            _lapList.Children.Add(row);
        }
        if (_lastLaps != -1) K.FadeIn(_lapList.Children[0], 0, 6);
    }

    // ---------------- frame ----------------
    void Frame(double dt)
    {
        _clock += dt;
        var f = F;
        if (_waterDirty) SyncWater(false);

        // Pomodoro
        var pm = f.Pomo;
        bool pRun = pm.Running;
        Set(_pTime, K.Fmt(Ceil(pm.Remaining)));
        Set(_pPhase, pm.PhaseLabel);
        Set(_pDone, pm.Completed == 1 ? "1 concluído" : $"{pm.Completed} concluídos");
        var S = Settings.Current;
        Set(_pFoot, pm.Phase == FocusPhase.Focus ? $"{S.FocusMinutes}m foco / {S.ShortBreakMinutes}m pausa" : pm.Phase == FocusPhase.ShortBreak ? "Respire fundo" : "Pausa longa merecida");
        if (pRun != _lastP) { SetPlay(_pPlay, pRun, K.Fg, 12); if (_lastP != null) _popP.Hit(); _lastP = pRun; }
        if (pm.Phase.ToString() != _lastPhase) { if (_lastPhase != "") _popP.Hit(1.2); _lastPhase = pm.Phase.ToString(); }
        _ringS.To(pm.Progress, 0, 90, 17);
        _ringS.Step(dt);
        _ring.Progress = _ringS.Value;
        _glowS.To(pRun ? 1 : 0, 0, 120, 14); _glowS.Step(dt);
        _ring.Glow = _glowS.Value;

        // Timer
        var tm = f.Timer;
        bool tRun = tm.Running;
        Set(_tTime, K.Fmt(Ceil(tm.Remaining)));
        _waveS.To(tRun ? 1 : 0, 0, 70, 12); _waveS.Step(dt);
        _wave.Activity = Math.Clamp(_waveS.Value, 0, 1.1);
        _wave.Phase = _clock;
        if (_waveS.Value > 0.002 || !_waveS.Settled || tRun) _wave.Redraw();
        int chip = tm.Minutes == 5 ? 0 : tm.Minutes == 10 ? 1 : tm.Minutes == 25 ? 2 : -1;
        if (chip != _lastChip) { _chips.Select(chip, _lastChip != -2); _lastChip = chip; }
        bool touched = tm.Touched;
        if (tRun != _lastT || touched != _lastTTouched)
        {
            string label = tRun ? "Pausar" : touched ? "Continuar" : "Iniciar";
            FK.SetPillContent(_tStart, label, tRun ? "\uE769" : "\uE768");
            if (_lastT != null && tRun != _lastT) _popT.Hit(1.12);
            _lastT = tRun; _lastTTouched = touched;
        }
        _resetS.To(touched ? 1 : 0, 0, 380, 17); _resetS.Step(dt);
        double rr = Math.Clamp(_resetS.Value, 0, 1.12);
        _cancelCol.Width = new GridLength(Math.Max(0, rr), GridUnitType.Star);
        _tReset.Margin = new Thickness(8 * Math.Clamp(rr, 0, 1), 0, 0, 0);
        _tReset.Opacity = Math.Clamp(rr, 0, 1);
        _tReset.IsHitTestVisible = rr > 0.6;

        // Cronômetro
        var sw = f.Watch;
        bool sRun = sw.Running;
        if (sRun || sw.Touched || _lastS == null) Set(_sTime, Fmt2(sw.Elapsed));
        else Set(_sTime, "0:00.00");
        if (sRun != _lastS) { SetPlay(_sPlay, sRun, K.Ink, 13); if (_lastS != null) _popS.Hit(1.1); _lastS = sRun; }
        _sFlag.Opacity = sRun ? 1 : 0.35;
        int laps = sw.Laps.Count;
        if (laps != _lastLaps)
        {
            RebuildLaps(sw);
            _lastLaps = laps;
        }

        // Pops
        _popP.Step(dt); _popT.Step(dt); _popS.Step(dt); _popW.Step(dt);
    }
}
