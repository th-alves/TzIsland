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

public static class NotchGeo
{
    /// <summary>
    /// Silhueta de notch para o modo Clássico: topo reto colado na borda da tela, "ombros" côncavos de raio r
    /// (a curva que faz a ilha parecer extensão da tela) e base arredondada de raio rb.
    /// Devolve o contorno fechado (para recorte/preenchimento) e uma linha aberta só com as laterais e a base (para a borda).
    /// </summary>
    public static (Geometry fill, Geometry line) Make(double w, double h, double r, double rb)
    {
        r = Math.Clamp(r, 0, Math.Min(w / 4, h / 2));
        rb = Math.Clamp(rb, 0, Math.Min((w - 2 * r) / 2, h - r));
        var sz = new Size(Math.Max(0.01, r), Math.Max(0.01, r)); var sb = new Size(Math.Max(0.01, rb), Math.Max(0.01, rb));

        var fill = new StreamGeometry();
        using (var c = fill.Open())
        {
            c.BeginFigure(new Point(0, 0), true, true);
            c.LineTo(new Point(w, 0), true, false);
            if (r > 0) c.ArcTo(new Point(w - r, r), sz, 0, false, SweepDirection.Counterclockwise, true, false);
            c.LineTo(new Point(w - r, h - rb), true, false);
            if (rb > 0) c.ArcTo(new Point(w - r - rb, h), sb, 0, false, SweepDirection.Clockwise, true, false);
            c.LineTo(new Point(r + rb, h), true, false);
            if (rb > 0) c.ArcTo(new Point(r, h - rb), sb, 0, false, SweepDirection.Clockwise, true, false);
            c.LineTo(new Point(r, r), true, false);
            if (r > 0) c.ArcTo(new Point(0, 0), sz, 0, false, SweepDirection.Counterclockwise, true, false);
        }
        fill.Freeze();

        var line = new StreamGeometry();
        using (var c = line.Open())
        {
            c.BeginFigure(new Point(0, 0), false, false);
            if (r > 0) c.ArcTo(new Point(r, r), sz, 0, false, SweepDirection.Clockwise, true, false);
            c.LineTo(new Point(r, h - rb), true, false);
            if (rb > 0) c.ArcTo(new Point(r + rb, h), sb, 0, false, SweepDirection.Counterclockwise, true, false);
            c.LineTo(new Point(w - r - rb, h), true, false);
            if (rb > 0) c.ArcTo(new Point(w - r, h - rb), sb, 0, false, SweepDirection.Counterclockwise, true, false);
            c.LineTo(new Point(w - r, r), true, false);
            if (r > 0) c.ArcTo(new Point(w, 0), sz, 0, false, SweepDirection.Clockwise, true, false);
        }
        line.Freeze();
        return (fill, line);
    }
}

/// <summary>
/// Camada de "motion design" baseada em molas (mesma ideia do Framer Motion / React Spring): entradas em cascata,
/// presets de física e animações interrompíveis que preservam a velocidade. Sem dependências externas.
/// </summary>
public static class Motion
{
    // Presets (rigidez, amortecimento)
    public static readonly (double k, double c) Snappy = (520, 38), Smooth = (260, 30), Bouncy = (300, 16), Gentle = (170, 24), Wobbly = (200, 12);

    static (ScaleTransform sc, TranslateTransform tt) Transforms(FrameworkElement el)
    {
        if (el.RenderTransform is TransformGroup g && !g.IsFrozen && g.Children.Count >= 2 && g.Children[0] is ScaleTransform s && g.Children[1] is TranslateTransform t)
            return (s, t);
        var sc = new ScaleTransform(1, 1); var tt = new TranslateTransform();
        el.RenderTransformOrigin = new Point(0.5, 0.5);
        el.RenderTransform = new TransformGroup { Children = { sc, tt } };
        return (sc, tt);
    }

    /// <summary>Faz o elemento surgir: fade rápido + subida com mola + escala com leve overshoot.</summary>
    public static void Reveal(FrameworkElement el, double delaySec = 0, double fromY = 18, double fromScale = 0.94)
    {
        var (sc, tt) = Transforms(el);
        // solta animações "seguradas" (ex.: hover dos cartões) para que os valores abaixo valham
        el.BeginAnimation(UIElement.OpacityProperty, null);
        tt.BeginAnimation(TranslateTransform.YProperty, null);
        sc.BeginAnimation(ScaleTransform.ScaleXProperty, null); sc.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        var o = new Spring(0); var s = new Spring(fromScale); var y = new Spring(fromY);
        el.Opacity = 0; sc.ScaleX = sc.ScaleY = fromScale; tt.Y = fromY;
        var loop = new MotionLoop(() =>
        {
            el.Opacity = Math.Clamp(o.Value, 0, 1);
            sc.ScaleX = sc.ScaleY = s.Value;
            tt.Y = y.Value;
        });
        loop.Add(o); loop.Add(s); loop.Add(y);
        o.To(1, delaySec, 380, 36);
        s.To(1, delaySec, 260, 16);
        y.To(0, delaySec, 250, 19);
        loop.Kick();
    }

    public static void RevealAll(IEnumerable<FrameworkElement> items, double baseDelay = 0.06, double step = 0.055, double fromY = 18, double fromScale = 0.94)
    {
        int i = 0;
        foreach (var el in items) Reveal(el, baseDelay + (i++) * step, fromY, fromScale);
    }

    /// <summary>
    /// Acha os blocos que merecem entrar em cascata: filhos diretos da página e, quando um painel é só "layout"
    /// que agrupa cartões (Border com fundo), os próprios cartões.
    /// </summary>
    public static List<FrameworkElement> Targets(FrameworkElement root, int depth = 0)
    {
        var list = new List<FrameworkElement>();
        if (root is not Panel p) { list.Add(root); return list; }
        foreach (var c in p.Children.OfType<FrameworkElement>())
        {
            if (c.Visibility != Visibility.Visible) continue;
            bool groupsCards = c is Panel cp && cp.Background == null && depth < 2 && c is not ChipRow
                               && cp.Children.OfType<Border>().Any(b => b.Background != null);
            if (groupsCards) list.AddRange(Targets(c, depth + 1)); else list.Add(c);
        }
        return list;
    }

    public static void RevealPage(IPage page, double baseDelay = 0.08, double step = 0.055)
    {
        var targets = page is PageBase pb ? pb.RevealTargets.ToList() : Targets(page.View);
        // (sem fallback: a View da página usa um TranslateTransform próprio na troca de abas e não pode ser trocado)
        RevealAll(targets, baseDelay, step);
    }
}
