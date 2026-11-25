namespace TzIsland;

/// <summary>
/// Barrinhas de espectro de áudio (equalizador). Só consome o espectro e só redesenha enquanto Running = true;
/// ao desligar, libera a captura de áudio.
/// </summary>
public sealed class EqBars : FrameworkElement, IFrameSink
{
    readonly int _n;
    readonly double[] _v;
    bool _running;
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
            if (value) { Svc.Spectrum.Acquire(); MotionHost.Attach(this); }
            else { MotionHost.Detach(this); Svc.Spectrum.Release(); Array.Clear(_v, 0, _n); InvalidateVisual(); }
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(_n * BarWidth + (_n - 1) * Gap, Height);

    double _acc;

    public bool Step(double dt)
    {
        // 30 fps bastam para barrinhas de 3 px: metade dos quadros, metade do custo em repouso.
        _acc += dt;
        if (_acc < 1.0 / 30) return _running;
        dt = _acc;
        _acc = 0;
        long now = Stopwatch.GetTimestamp();
        var sp = Svc.Spectrum;
        bool recent = sp.Live && (now - sp.LastPacket) / (double)Stopwatch.Frequency < 0.12;
        for (int i = 0; i < _n; i++)
        {
            double t;
            if (sp.Live) t = recent ? sp.Levels[Math.Min(SpectrumService.Bands - 1, (int)Math.Round(i * (SpectrumService.Bands - 1) / (double)Math.Max(1, _n - 1)))] : 0;
            // Fase no relógio global: trocar de superfície (pílula ↔ página) não reinicia o espectro.
            else t = Playing ? 0.3 + 0.35 * (0.5 + 0.5 * Math.Sin(MotionHost.Now * (3.1 + i * 0.9) + i * 1.7)) : 0;
            double k = t > _v[i] ? 1 - Math.Pow(0.0005, dt) : 1 - Math.Pow(0.06, dt);     // ataque rápido, queda suave
            _v[i] += (t - _v[i]) * k;
        }
        InvalidateVisual();
        return _running;
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
