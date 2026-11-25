using System.Windows.Media.Animation;

namespace TzIsland;

/// <summary>
/// Anel de LEDs em volta do ícone do aparelho: segmentos que acendem conforme a %, com cor por nível
/// (verde → âmbar → vermelho), brilho suave nos acesos e varredura com mola quando o valor muda.
/// Sem % conhecida, o anel fica apagado. Abaixo de 15% os LEDs acesos "respiram".
/// </summary>
public sealed class BatteryRing : FrameworkElement, IFrameSink
{
    const int Segs = 24;
    const double Gap = 4.2;          // graus de folga entre segmentos
    readonly Spring _v = new(0);     // 0..1 animado
    readonly Spring _on = new(0);    // intensidade geral (entrada)
    readonly MotionLoop _loop;
    double _target = -1;             // -1 = desconhecido
    double _pulse = 1;
    bool _breathing;
    double _breathT;
    readonly UIElement _icon;

    public double Thickness { get; set; } = 3.6;

    public BatteryRing(UIElement icon)
    {
        _icon = icon;
        AddVisualChild(icon);
        _loop = new MotionLoop(InvalidateVisual);
        _loop.Add(_v); _loop.Add(_on);
        Unloaded += (_, _) => StopBreath();
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _icon;
    protected override Size MeasureOverride(Size s) { _icon.Measure(s); return new Size(double.IsInfinity(s.Width) ? 56 : s.Width, double.IsInfinity(s.Height) ? 56 : s.Height); }
    protected override Size ArrangeOverride(Size s)
    {
        var d = _icon.DesiredSize;
        _icon.Arrange(new Rect((s.Width - d.Width) / 2, (s.Height - d.Height) / 2, d.Width, d.Height));
        return s;
    }

    /// <summary>Define a % (null = desconhecida). <paramref name="sweepFromZero"/> refaz a varredura desde 0 (entrada).</summary>
    public void Set(int? pct, bool sweepFromZero, double delay = 0)
    {
        double t = pct is int p ? Math.Clamp(p, 0, 100) / 100.0 : -1;
        if (sweepFromZero) { _v.Snap(0); _on.Snap(0); }
        _target = t;
        _on.To(1, delay, 120, 18);
        _v.To(Math.Max(0, t), delay + 0.08, 70, 13);       // mola mais lenta: os LEDs "enchem" um a um, com leve sobra
        if (t >= 0 && t < 0.15) StartBreath(); else StopBreath();
        _loop.Kick();
    }

    void StartBreath()
    {
        if (_breathing) return;
        _breathing = true;
        MotionHost.Attach(this);      // respiração no relógio único (alinhada ao quadro, não a um timer de 33 ms)
    }

    void StopBreath()
    {
        if (_breathing) { _breathing = false; MotionHost.Detach(this); }
        _pulse = 1;
    }

    public bool Step(double dt)
    {
        if (!_breathing) return false;
        _breathT += dt;
        _pulse = 0.55 + 0.45 * (0.5 + 0.5 * Math.Cos(_breathT * 2.6));
        InvalidateVisual();
        return true;
    }

    public static Color LevelColor(double t)
    {
        // vermelho (≤15%) → âmbar (~35%) → verde (≥60%)
        Color red = Color.FromRgb(0xFF, 0x45, 0x3A), amber = Color.FromRgb(0xFF, 0xB0, 0x20), green = Color.FromRgb(0x30, 0xD1, 0x58);
        if (t <= 0.15) return red;
        if (t <= 0.35) return FK.Lerp(red, amber, (t - 0.15) / 0.20);
        if (t <= 0.60) return FK.Lerp(amber, green, (t - 0.35) / 0.25);
        return green;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        var c = new Point(w / 2, h / 2);
        double r = Math.Min(w, h) / 2 - Thickness / 2 - 2.2;

        // fundo do círculo (mesmo tom dos outros ícones da seção)
        dc.DrawEllipse(K.Track, null, c, r - Thickness / 2 - 2.6, r - Thickness / 2 - 2.6);

        double v = Math.Clamp(_v.Value, 0, 1.04), on = Math.Clamp(_on.Value, 0, 1);
        int lit = _target < 0 ? 0 : (int)Math.Round(v * Segs);
        if (_target > 0 && lit == 0 && v > 0.004) lit = 1;            // 1–4% ainda mostra um LED
        var col = LevelColor(_target < 0 ? 0 : Math.Min(1, v));

        double step = 360.0 / Segs, sweep = step - Gap;
        for (int i = 0; i < Segs; i++)
        {
            double a0 = -90 + i * step + Gap / 2, a1 = a0 + sweep;
            var geo = Arc(c, r, a0, a1);
            bool isLit = i < lit;
            if (!isLit)
            {
                dc.DrawGeometry(null, Pen(Color.FromArgb((byte)(255 * (0.10 + 0.06 * on)), 255, 255, 255), Thickness), geo);
                continue;
            }
            // o LED da "ponta" acende mais forte conforme a fração; os anteriores ficam cheios
            double head = i == lit - 1 ? Math.Clamp(v * Segs - i, 0.35, 1) : 1;
            double a = on * head * (_target < 0.15 ? _pulse : 1);
            dc.DrawGeometry(null, Pen(Color.FromArgb((byte)(70 * a), col.R, col.G, col.B), Thickness + 4.5), geo);   // brilho
            dc.DrawGeometry(null, Pen(Color.FromArgb((byte)(255 * a), col.R, col.G, col.B), Thickness), geo);
        }
    }

    static Pen Pen(Color c, double t) => new(new SolidColorBrush(c), t) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

    static Geometry Arc(Point c, double r, double a0, double a1)
    {
        Point P(double deg) { double rad = deg * Math.PI / 180; return new Point(c.X + r * Math.Cos(rad), c.Y + r * Math.Sin(rad)); }
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(P(a0), false, false);
            ctx.ArcTo(P(a1), new Size(r, r), 0, a1 - a0 > 180, SweepDirection.Clockwise, true, false);
        }
        g.Freeze();
        return g;
    }
}

/// <summary>Bloco de um aparelho: anel com o ícone, nome e % (cor do nível) contando junto com a varredura.</summary>
public sealed class DeviceTile : Border
{
    public string Id { get; }
    readonly BatteryRing _ring;
    readonly Border _iconHost = new() { Width = 22, Height = 22 };
    readonly TextBlock _name = K.T("", 11, K.Fg, true), _pct = K.Num("", 11, K.Label);
    readonly Spring _num = new(0);
    readonly MotionLoop _numLoop;
    readonly Grid _grid = new();
    readonly StackPanel _txt = new() { VerticalAlignment = VerticalAlignment.Center };
    int? _shown;
    bool? _compact;
    BtKind _kind = (BtKind)(-1);

    public static string IconFor(BtKind k) => k switch
    {
        BtKind.Headphones => "headphones",
        BtKind.Earbuds => "earbuds",
        BtKind.Speaker => "speaker",
        BtKind.Mouse => "mouse",
        BtKind.Keyboard => "keyboard",
        BtKind.Gamepad => "gamepad",
        BtKind.Phone => "smartphone",
        BtKind.Watch => "watch",
        BtKind.Computer => "laptop",
        BtKind.Pen => "pen-tool",
        _ => "bluetooth"
    };

    static string KindName(BtKind k) => k switch
    {
        BtKind.Headphones => "Fone de ouvido", BtKind.Earbuds => "Fone intra-auricular", BtKind.Speaker => "Caixa de som",
        BtKind.Mouse => "Mouse", BtKind.Keyboard => "Teclado", BtKind.Gamepad => "Controle", BtKind.Phone => "Celular",
        BtKind.Watch => "Relógio / pulseira", BtKind.Computer => "Computador", BtKind.Pen => "Caneta", _ => "Dispositivo Bluetooth"
    };

    public DeviceTile(BtDevice d)
    {
        Id = d.Id;
        Width = 88; Padding = new Thickness(4, 6, 4, 6); CornerRadius = new CornerRadius(14);
        Background = Brushes.Transparent;
        _ring = new BatteryRing(_iconHost) { Width = 58, Height = 58 };
        _name.TextTrimming = TextTrimming.CharacterEllipsis;
        _txt.Children.Add(_name); _txt.Children.Add(_pct);
        _grid.Children.Add(_ring); _grid.Children.Add(_txt);
        Child = _grid;
        SetCompact(false);

        // clique abre a tela de Bluetooth do Windows (onde ele mostra a % de cada aparelho)
        Cursor = Cursors.Hand;
        MouseLeftButtonUp += (_, _) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true }); }
            catch (Exception e) { Log.Write(e); }
        };

        _numLoop = new MotionLoop(() => { if (_shown is int) _pct.Text = $"{Math.Round(Math.Clamp(_num.Value, 0, 100))}%"; });
        _numLoop.Add(_num);

        // hover: fundo sutil + leve elevação do anel
        RenderTransformOrigin = new Point(0.5, 0.5);
        var sc = new ScaleTransform(1, 1); _ring.RenderTransformOrigin = new Point(0.5, 0.5); _ring.RenderTransform = sc;
        void Hover(bool inn)
        {
            Background = inn ? K.Hex("#0FFFFFFF") : Brushes.Transparent;
            MotionEngine.To(sc, ScaleTransform.ScaleXProperty, inn ? 1.06 : 1, Tz.Springs.Lift);
            MotionEngine.To(sc, ScaleTransform.ScaleYProperty, inn ? 1.06 : 1, Tz.Springs.Lift);
        }
        MouseEnter += (_, _) => Hover(true); MouseLeave += (_, _) => Hover(false);

        Update(d, true, 0);
    }

    /// <summary>
    /// Normal: anel 58 px com nome e % embaixo (precisa de ~108 px de altura).
    /// Compacto: anel 46 px com nome e % ao lado, para quando o card Dispositivos fica baixo
    /// (ex.: monitor externo com brilho e contraste deixa o card Controles mais alto).
    /// </summary>
    public void SetCompact(bool compact)
    {
        if (_compact == compact) return;
        _compact = compact;
        _grid.ColumnDefinitions.Clear(); _grid.RowDefinitions.Clear();
        if (compact)
        {
            Width = double.NaN; Padding = new Thickness(4, 4, 10, 4);
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _ring.Width = _ring.Height = 46; _ring.Thickness = 3.2; _ring.HorizontalAlignment = HorizontalAlignment.Left;
            Grid.SetRow(_txt, 0); Grid.SetColumn(_txt, 1);
            _txt.Margin = new Thickness(8, 0, 0, 0); _txt.HorizontalAlignment = HorizontalAlignment.Left;
            _name.HorizontalAlignment = _pct.HorizontalAlignment = HorizontalAlignment.Left;
            _name.MaxWidth = 96; _pct.Margin = new Thickness(0, 1, 0, 0);
        }
        else
        {
            Width = 88; Padding = new Thickness(4, 6, 4, 6);
            _grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _ring.Width = _ring.Height = 58; _ring.Thickness = 3.6; _ring.HorizontalAlignment = HorizontalAlignment.Center;
            Grid.SetRow(_txt, 1); Grid.SetColumn(_txt, 0);
            _txt.Margin = new Thickness(0, 6, 0, 0); _txt.HorizontalAlignment = HorizontalAlignment.Center;
            _name.HorizontalAlignment = _pct.HorizontalAlignment = HorizontalAlignment.Center;
            _name.MaxWidth = 80; _pct.Margin = new Thickness(0, 1, 0, 0);
        }
        _ring.InvalidateVisual();
    }

    public void Update(BtDevice d, bool entering, double delay)
    {
        _name.Text = d.Name;
        ToolTip = $"{d.Name}\n{KindName(d.Kind)}" + (d.Battery is int b ? $" · bateria {b}%" : " · bateria não informada") + "\nClique para abrir o Bluetooth do Windows";
        if (d.Kind != _kind) { _kind = d.Kind; _iconHost.Child = Ico.Make(IconFor(d.Kind), 20, K.Fg, 1.7); }
        if (!entering && d.Battery == _shown) return;

        _ring.Set(d.Battery, entering, delay);
        if (d.Battery is int p)
        {
            var col = BatteryRing.LevelColor(p / 100.0);
            _pct.Foreground = new SolidColorBrush(p > 35 ? Color.FromRgb(0xC7, 0xC7, 0xCC) : col);
            if (entering) _num.Snap(0);
            _shown = p;
            _num.To(p, delay + 0.08, 70, 13);
            _numLoop.Kick();
        }
        else { _shown = null; _pct.Text = "—"; _pct.Foreground = K.Muted; }
    }
}
