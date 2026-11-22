namespace TzIsland;

/// <summary>
/// Barrinhas de espectro de áudio (equalizador). Só consome o espectro e só redesenha enquanto Running = true;
/// ao desligar, libera a captura de áudio.
/// </summary>
public sealed class EqBars : FrameworkElement
{
    readonly int _n;
    readonly double[] _v;
    bool _running;
    long _last;
    double _clock;
    public Brush Fill { get; set; } = Brushes.White;
    public double BarWidth { get; set; } = 3;
    public double Gap { get; set; } = 2.5;
    /// <summary>Usado só quando a captura de áudio não está disponível (animação decorativa em vez do espectro real).</summary>
    public bool Playing { get; set; } = true;

    public EqBars(int bars = 5) { _n = bars; _v = new double[bars]; IsHitTestVisible = false; Height = 16; }

    public bool Running
    {
        get => _running;
        set
        {
            if (value == _running) return;
            _running = value;
            if (value) { Svc.Spectrum.Acquire(); _last = Stopwatch.GetTimestamp(); CompositionTarget.Rendering += OnRender; }
            else { CompositionTarget.Rendering -= OnRender; Svc.Spectrum.Release(); Array.Clear(_v, 0, _n); InvalidateVisual(); }
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(_n * BarWidth + (_n - 1) * Gap, Height);

    void OnRender(object? s, EventArgs e)
    {
        long now = Stopwatch.GetTimestamp();
        double dt = Math.Min(0.05, (now - _last) / (double)Stopwatch.Frequency);
        _last = now; _clock += dt;
        var sp = Svc.Spectrum;
        bool recent = sp.Live && (now - sp.LastPacket) / (double)Stopwatch.Frequency < 0.12;
        for (int i = 0; i < _n; i++)
        {
            double t;
            if (sp.Live) t = recent ? sp.Levels[Math.Min(SpectrumService.Bands - 1, (int)Math.Round(i * (SpectrumService.Bands - 1) / (double)Math.Max(1, _n - 1)))] : 0;
            else t = Playing ? 0.3 + 0.35 * (0.5 + 0.5 * Math.Sin(_clock * (3.1 + i * 0.9) + i * 1.7)) : 0;
            double k = t > _v[i] ? 1 - Math.Pow(0.0005, dt) : 1 - Math.Pow(0.06, dt);     // ataque rápido, queda suave
            _v[i] += (t - _v[i]) * k;
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double h = ActualHeight, w = BarWidth;
        for (int i = 0; i < _n; i++)
        {
            double bh = Math.Max(w, h * Math.Clamp(0.12 + _v[i] * 0.88, 0, 1));
            dc.DrawRoundedRectangle(Fill, null, new Rect(i * (w + Gap), (h - bh) / 2, w, bh), w / 2, w / 2);
        }
    }
}
