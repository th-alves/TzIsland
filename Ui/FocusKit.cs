using System.Windows.Media.Animation;

namespace TzIsland;

/// <summary>Controles desenhados à mão para a página Foco. Cada um tem seu próprio laço de molas (zero custo em repouso).</summary>
public static class FK
{
    public static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb((byte)(a.A + (b.A - a.A) * t), (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
    }

    /// <summary>Cartão da página: superfície índigo, borda fina, elevação ao passar o mouse.</summary>
    public static Border Card(UIElement child, double radius = 18)
    {
        var border = new SolidColorBrush(((SolidColorBrush)K.Stroke).Color);
        var tt = new TranslateTransform();
        var sc = new ScaleTransform(1, 1);
        var card = new Border
        {
            Background = K.Surface, CornerRadius = new CornerRadius(radius), Padding = new Thickness(15, 13, 15, 13),
            BorderBrush = border, BorderThickness = new Thickness(1), Child = child,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new TransformGroup { Children = { sc, tt } }
        };
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        card.MouseEnter += (_, _) =>
        {
            border.BeginAnimation(SolidColorBrush.ColorProperty, new System.Windows.Media.Animation.ColorAnimation(((SolidColorBrush)K.StrokeHi).Color, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
            tt.BeginAnimation(TranslateTransform.YProperty, new System.Windows.Media.Animation.DoubleAnimation(-2.5, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        };
        card.MouseLeave += (_, _) =>
        {
            border.BeginAnimation(SolidColorBrush.ColorProperty, new System.Windows.Media.Animation.ColorAnimation(((SolidColorBrush)K.Stroke).Color, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });
            tt.BeginAnimation(TranslateTransform.YProperty, new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(420)) { EasingFunction = new ElasticEase { Oscillations = 1, Springiness = 6, EasingMode = EasingMode.EaseOut } });
        };
        return card;
    }

    /// <summary>Cabeçalho do cartão: ícone cinza + RÓTULO em caixa alta espaçada.</summary>
    public static StackPanel Header(string glyph, string title) => Header(K.Glyph(glyph, 11, K.Label), title);

    public static StackPanel Header(UIElement icon, string title)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (icon is FrameworkElement fe) { fe.Margin = new Thickness(0, 0, 7, 0); fe.VerticalAlignment = VerticalAlignment.Center; }
        sp.Children.Add(icon);
        sp.Children.Add(K.Cap(title).Vc());
        return sp;
    }

    /// <summary>Texto numérico/legenda em Inter Medium com algarismos tabulares (nome mantido por compatibilidade).</summary>
    public static TextBlock Mono(string text, double size, Brush? fg = null, bool bold = true)
    {
        var t = K.T(text, size, fg ?? K.Fg, bold);
        if (!bold) { t.FontFamily = K.FontMedium; }
        t.TextTrimming = TextTrimming.None;
        System.Windows.Documents.Typography.SetNumeralAlignment(t, FontNumeralAlignment.Tabular);
        return t;
    }

    /// <summary>Entrada em cascata (mantida por compatibilidade): usa o motor de molas.</summary>
    public static void Enter(Border card, int delayMs) => Motion.Reveal(card, delayMs / 1000.0);

    /// <summary>Botão-pílula cheio (branco por padrão), com glifo opcional.</summary>
    public static Button PillButton(string text, string? glyph, Action click, double h = 38, double radius = 19)
    {
        var b = new Button { Style = K.Fill, Height = h, Background = K.White, Tag = new CornerRadius(radius), Padding = new Thickness(14, 0, 14, 0) };
        SetPillContent(b, text, glyph);
        b.Click += (_, _) => click();
        return b;
    }

    public static void SetPillContent(Button b, string text, string? glyph, Brush? fg = null)
    {
        fg ??= K.Ink;
        var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        if (glyph != null) sp.Children.Add(K.Glyph(glyph, 11, fg).M(0, 0, 7, 0));
        sp.Children.Add(K.T(text, 12.5, fg, true).Vc());
        b.Content = sp;
    }

    /// <summary>Pílula escura (secundária): "Cancelar", "Limpar"…</summary>
    public static Button GhostPill(string text, string? glyph, Action click, double h = 36, double radius = 18)
    {
        var b = new Button { Style = K.Fill, Height = h, Background = K.Track, Tag = new CornerRadius(radius), Padding = new Thickness(14, 0, 14, 0) };
        SetPillContent(b, text, glyph, K.Fg);
        b.Click += (_, _) => click();
        return b;
    }

    public static Button RoundButton(string glyph, string tip, Action click, double size, Brush bg, Brush fg, double glyphSize = 14)
    {
        var b = new Button { Style = K.Fill, Width = size, Height = size, Background = bg, Tag = new CornerRadius(size / 2), ToolTip = tip, Content = K.Glyph(glyph, glyphSize, fg) };
        b.Click += (_, _) => click();
        return b;
    }
}

/// <summary>Anel de progresso com brilho e "cabeça" luminosa na ponta do arco.</summary>
public sealed class RingProgress : FrameworkElement
{
    double _p, _glow;
    static readonly Pen TrackPen = Mk(K.Track, 5);
    public double Progress { get => _p; set { value = Math.Clamp(value, 0, 1); if (Math.Abs(value - _p) > 0.0002) { _p = value; InvalidateVisual(); } } }
    public double Glow { get => _glow; set { if (Math.Abs(value - _glow) > 0.002) { _glow = value; InvalidateVisual(); } } }

    static Pen Mk(Brush b, double t) => new(b, t) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

    static readonly Brush Arc = Brushes.White;

    protected override void OnRender(DrawingContext dc)
    {
        double s = Math.Min(ActualWidth, ActualHeight), r = s / 2 - 7;
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        dc.DrawEllipse(null, TrackPen, c, r, r);
        if (_p <= 0.001) return;
        double ang = _p * 359.9 - 90, rad = ang * Math.PI / 180;
        var start = new Point(c.X, c.Y - r);
        var end = new Point(c.X + r * Math.Cos(rad), c.Y + r * Math.Sin(rad));
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.ArcTo(end, new Size(r, r), 0, _p > 0.5, SweepDirection.Clockwise, true, false);
        }
        g.Freeze();
        dc.DrawGeometry(null, new Pen(Arc, 5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, g);
        if (_glow > 0.02)
        {
            var halo = new SolidColorBrush(Color.FromArgb((byte)(90 * _glow), 255, 255, 255));
            dc.DrawEllipse(halo, null, end, 6 + 3 * _glow, 6 + 3 * _glow);
        }
    }
}

/// <summary>Barras de áudio finas (branco → cinza); repousam baixas e "dançam" quando Activity sobe.</summary>
public sealed class Waveform : FrameworkElement
{
    const int N = 46;
    static readonly double[] Base = BuildBase();
    static readonly Brush[] Fills = BuildFills();
    public double Activity { get; set; }
    public double Phase { get; set; }

    static double[] BuildBase()
    {
        var r = new Random(7); var a = new double[N];
        for (int i = 0; i < N; i++) a[i] = 0.25 + 0.75 * Math.Pow(r.NextDouble(), 1.3);
        return a;
    }

    static Brush[] BuildFills()
    {
        var res = new Brush[N];
        for (int i = 0; i < N; i++)
        {
            double t = i / (double)(N - 1);
            byte v = (byte)(245 - 175 * Math.Pow(t, 0.8));
            var b = new SolidColorBrush(Color.FromRgb(v, v, v)); b.Freeze(); res[i] = b;
        }
        return res;
    }

    public void Redraw() => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        double bar = 2, gap = (w - N * bar) / (N - 1);
        for (int i = 0; i < N; i++)
        {
            double wobble = 0.5 + 0.5 * Math.Sin(Phase * 3.1 + i * 0.62) * Math.Cos(Phase * 1.7 + i * 0.31);
            double amp = Base[i] * (0.4 + 0.6 * Activity * (0.45 + 0.55 * wobble)) + (1 - Activity) * 0.04;
            double bh = Math.Max(3, h * Math.Clamp(amp, 0.1, 1));
            double x = i * (bar + gap), y = (h - bh) / 2;
            dc.DrawRoundedRectangle(Fills[i], null, new Rect(x, y, bar, bh), 1, 1);
        }
    }
}

/// <summary>Gota d'água desenhada em vetor; enche de baixo para cima e "pula" ao encher.</summary>
public sealed class DropGlyph : FrameworkElement
{
    static readonly Geometry Shape = Build();
    readonly Spring _fill = new(0), _pop = new(1);
    readonly MotionLoop _loop;
    public Brush Tint { get; set; } = K.Sky;
    public Action? Clicked { get; set; }

    public DropGlyph()
    {
        Width = 17; Height = 23; Cursor = Cursors.Hand;
        _loop = new MotionLoop(InvalidateVisual);
        _loop.Add(_fill); _loop.Add(_pop);
    }

    static Geometry Build()
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(6, 0.8), true, true);
            c.BezierTo(new Point(3.4, 4.4), new Point(1.2, 7.4), new Point(1.2, 10.2), true, false);
            c.ArcTo(new Point(10.8, 10.2), new Size(4.8, 4.8), 0, false, SweepDirection.Counterclockwise, true, false);
            c.BezierTo(new Point(10.8, 7.4), new Point(8.6, 4.4), new Point(6, 0.8), true, false);
        }
        g.Freeze();
        return g;
    }

    public void SetFilled(bool filled, bool animate, double delay = 0)
    {
        double target = filled ? 1 : 0;
        if (!animate) { _fill.Snap(target); InvalidateVisual(); return; }
        if (Math.Abs(_fill.Target - target) < 0.01) return;
        _fill.To(target, delay, 190, 17);
        if (filled) { _pop.Snap(0.55); _pop.To(1, delay, 330, 9); }
        _loop.Kick();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { e.Handled = true; Clicked?.Invoke(); }

    protected override void OnRender(DrawingContext dc)
    {
        double sc = Math.Min(ActualWidth / 12.0, ActualHeight / 16.0) * Math.Max(0.1, _pop.Value);
        dc.PushTransform(new TranslateTransform(ActualWidth / 2, ActualHeight / 2));
        dc.PushTransform(new ScaleTransform(sc, sc));
        dc.PushTransform(new TranslateTransform(-6, -8));
        var outline = new Pen(_fill.Value > 0.5 ? K.Sky : K.Muted, 1.1) { LineJoin = PenLineJoin.Round };
        dc.DrawGeometry(null, outline, Shape);
        double f = Math.Clamp(_fill.Value, 0, 1.2);
        if (f > 0.01)
        {
            dc.PushClip(new RectangleGeometry(new Rect(0, 16 * (1 - Math.Min(1, f)), 12, 16)));
            dc.DrawGeometry(K.Sky, null, Shape);
            dc.Pop();
        }
        dc.Pop(); dc.Pop(); dc.Pop();
    }
}

/// <summary>Switch estilo iOS com mola: o botão estica ao ser arrastado e salta no destino.</summary>
public sealed class SwitchToggle : FrameworkElement
{
    readonly Spring _x = new(0), _stretch = new(0);
    readonly MotionLoop _loop;
    bool _on;
    public event Action<bool>? Toggled;

    public SwitchToggle()
    {
        Width = 46; Height = 26; Cursor = Cursors.Hand;
        _loop = new MotionLoop(InvalidateVisual);
        _loop.Add(_x); _loop.Add(_stretch);
    }

    public bool IsOn
    {
        get => _on;
        set { if (_on == value) return; _on = value; Animate(); }
    }

    void Animate()
    {
        _x.To(_on ? 1 : 0, 0, 420, 17);
        _stretch.Snap(1); _stretch.To(0, 0, 300, 14);
        _loop.Kick();
    }

    public void SnapTo(bool on) { _on = on; _x.Snap(on ? 1 : 0); _stretch.Snap(0); InvalidateVisual(); }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        e.Handled = true;
        _on = !_on; Animate(); Toggled?.Invoke(_on);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight, t = Math.Clamp(_x.Value, 0, 1);
        var off = Color.FromRgb(0x3A, 0x3A, 0x3E);
        var on = Color.FromRgb(0x30, 0xD1, 0x58);
        dc.DrawRoundedRectangle(new SolidColorBrush(FK.Lerp(off, on, t)), null, new Rect(0, 0, w, h), h / 2, h / 2);
        double d = h - 6, stretch = Math.Clamp(_stretch.Value, 0, 1.3) * 7;
        double x = 3 + (w - d - 6) * _x.Value;
        double left = x - (_on ? stretch : 0), width = d + stretch;
        dc.DrawRoundedRectangle(Brushes.White, null, new Rect(left, 3, width, d), d / 2, d / 2);
    }
}

/// <summary>Chips de seleção com um destaque branco que desliza (e esmaga/estica) entre as opções.</summary>
public sealed class ChipRow : Grid
{
    readonly List<(Border box, TextBlock text, SolidColorBrush fg)> _chips = new();
    readonly Border _hl = new() { Background = K.White, CornerRadius = new CornerRadius(14), HorizontalAlignment = HorizontalAlignment.Left, Height = 28, IsHitTestVisible = false };
    readonly Spring _x = new(0), _o = new(0);
    readonly MotionLoop _loop;
    readonly TranslateTransform _tt = new();
    readonly double _w, _gap;
    int _sel = -1;
    public event Action<int>? Picked;

    public ChipRow(IEnumerable<string> labels, double chipWidth = 52, double gap = 7)
    {
        _w = chipWidth; _gap = gap;
        Height = 28; HorizontalAlignment = HorizontalAlignment.Center;
        _hl.Width = chipWidth; _hl.RenderTransform = _tt; _hl.Opacity = 0;
        Children.Add(_hl);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        int i = 0;
        foreach (var l in labels)
        {
            int idx = i++;
            var fg = new SolidColorBrush(((SolidColorBrush)K.Label).Color);
            var tb = K.T(l, 12, fg, true); tb.FontFamily = K.FontMedium; tb.TextTrimming = TextTrimming.None;
            tb.HorizontalAlignment = HorizontalAlignment.Center; tb.VerticalAlignment = VerticalAlignment.Center;
            var box = new Border
            {
                Width = chipWidth, Height = 28, CornerRadius = new CornerRadius(14), Background = K.Card,
                BorderBrush = K.Stroke, BorderThickness = new Thickness(1), Cursor = Cursors.Hand, Child = tb
            };
            box.Margin = new Thickness(idx == 0 ? 0 : gap, 0, 0, 0);
            box.MouseLeftButtonDown += (_, e) => { e.Handled = true; Select(idx, true); Picked?.Invoke(idx); };
            _chips.Add((box, tb, fg));
            row.Children.Add(box);
        }
        Children.Add(row);
        _loop = new MotionLoop(Frame);
        _loop.Add(_x); _loop.Add(_o);
    }

    double PosOf(int i) => i * (_w + _gap);

    /// <summary>idx &lt; 0 esconde o destaque (valor personalizado).</summary>
    public void Select(int idx, bool animate)
    {
        if (idx == _sel && animate) return;
        bool first = _sel < 0;
        _sel = idx;
        if (idx < 0) { if (animate) _o.To(0, 0, 300, 28); else _o.Snap(0); }
        else
        {
            if (!animate || first) _x.Snap(PosOf(idx)); else _x.To(PosOf(idx), 0, 520, 20);
            if (animate) _o.To(1, 0, 300, 28); else _o.Snap(1);
        }
        _loop.Kick(); Frame();
    }

    void Frame()
    {
        double vel = Math.Abs(_x.Velocity), stretch = Math.Min(vel * 0.03, 26);
        _hl.Width = _w + stretch;
        _tt.X = _x.Value - stretch / 2;
        _hl.Opacity = Math.Clamp(_o.Value, 0, 1);
        double hlC = _x.Value + _w / 2;
        var dark = Color.FromRgb(0x0A, 0x0A, 0x0B); var light = ((SolidColorBrush)K.Label).Color;
        for (int i = 0; i < _chips.Count; i++)
        {
            double cc = PosOf(i) + _w / 2;
            double t = Math.Clamp(1 - Math.Abs(hlC - cc) / (_w * 0.75), 0, 1) * Math.Clamp(_o.Value, 0, 1);
            _chips[i].fg.Color = FK.Lerp(light, dark, t);
        }
    }
}
