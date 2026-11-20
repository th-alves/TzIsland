namespace TzIsland;

/// <summary>
/// Mola amortecida (integração semi-implícita com sub-passos). Preserva velocidade ao trocar o alvo
/// no meio da animação — é isso que dá a sensação "física" de interfaces da Apple.
/// </summary>
public sealed class Spring
{
    public double Value, Velocity, Target;
    public double Stiffness = 300, Damping = 28;
    double _delay;

    public Spring(double v) { Value = Target = v; }

    public bool Settled => _delay <= 0 && Math.Abs(Value - Target) < 0.01 && Math.Abs(Velocity) < 0.02;

    public Spring To(double target, double delay = 0, double stiffness = 0, double damping = 0)
    {
        Target = target;
        _delay = delay;
        if (stiffness > 0) Stiffness = stiffness;
        if (damping > 0) Damping = damping;
        return this;
    }

    public void Snap(double v) { Value = Target = v; Velocity = 0; _delay = 0; }

    public void Step(double dt)
    {
        if (_delay > 0) { _delay -= dt; return; }
        int n = Math.Max(1, (int)Math.Ceiling(dt / 0.004));
        double h = dt / n;
        for (int i = 0; i < n; i++)
        {
            double a = -Stiffness * (Value - Target) - Damping * Velocity;
            Velocity += a * h;
            Value += Velocity * h;
        }
        if (Math.Abs(Value - Target) < 0.01 && Math.Abs(Velocity) < 0.02) { Value = Target; Velocity = 0; }
    }
}

/// <summary>Loop de animação que só roda enquanto houver mola em movimento (zero custo em repouso).</summary>
public sealed class MotionLoop
{
    readonly List<Spring> _springs = new();
    readonly Action _frame;
    bool _on;
    long _last;

    public MotionLoop(Action frame) { _frame = frame; }

    public Spring Add(Spring s) { _springs.Add(s); return s; }

    public void Kick()
    {
        if (_on) return;
        _on = true;
        _last = Stopwatch.GetTimestamp();
        CompositionTarget.Rendering += OnRender;
    }

    void OnRender(object? sender, EventArgs e)
    {
        long now = Stopwatch.GetTimestamp();
        double dt = Math.Min(0.05, (now - _last) / (double)Stopwatch.Frequency);
        _last = now;
        foreach (var s in _springs) s.Step(dt);
        _frame();
        if (_springs.All(s => s.Settled))
        {
            CompositionTarget.Rendering -= OnRender;
            _on = false;
        }
    }
}

public static class Geo
{
    /// <summary>Retângulo com cantos superiores (rt) e inferiores (rb) arredondados.</summary>
    public static Geometry Round(double w, double h, double rt, double rb)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(rt, 0), true, true);
            c.LineTo(new Point(w - rt, 0), true, false);
            if (rt > 0) c.ArcTo(new Point(w, rt), new Size(rt, rt), 0, false, SweepDirection.Clockwise, true, false);
            c.LineTo(new Point(w, h - rb), true, false);
            if (rb > 0) c.ArcTo(new Point(w - rb, h), new Size(rb, rb), 0, false, SweepDirection.Clockwise, true, false);
            c.LineTo(new Point(rb, h), true, false);
            if (rb > 0) c.ArcTo(new Point(0, h - rb), new Size(rb, rb), 0, false, SweepDirection.Clockwise, true, false);
            c.LineTo(new Point(0, rt), true, false);
            if (rt > 0) c.ArcTo(new Point(rt, 0), new Size(rt, rt), 0, false, SweepDirection.Clockwise, true, false);
        }
        g.Freeze();
        return g;
    }
}
