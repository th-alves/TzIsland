namespace TzIsland;

/// <summary>
/// Mola amortecida (integração semi-implícita com sub-passos). Preserva velocidade ao trocar o alvo
/// no meio da animação — é isso que dá a sensação "física" de interfaces da Apple.
/// </summary>
public sealed class Spring
{
    public double Value, Velocity, Target;
    public double Stiffness = 300, Damping = 28;
    /// <summary>Tolerâncias próprias: comprimento em px, razão e opacidade usam valores diferentes.</summary>
    public double Epsilon = 0.01, VelocityEpsilon = 0.02;
    double _delay;

    public Spring(double v) { Value = Target = v; }

    public bool Settled => _delay <= 0 && Math.Abs(Value - Target) < Epsilon && Math.Abs(Velocity) < VelocityEpsilon;

    public Spring To(double target, double delay = 0, double stiffness = 0, double damping = 0)
    {
        Target = target;
        _delay = delay;
        if (stiffness > 0) Stiffness = stiffness;
        if (damping > 0) Damping = damping;
        return this;
    }

    public Spring Tune(double epsilon, double velocityEpsilon = 0.02)
    {
        Epsilon = epsilon; VelocityEpsilon = velocityEpsilon;
        return this;
    }

    public void Snap(double v) { Value = Target = v; Velocity = 0; _delay = 0; }

    /// <summary>Encerra no valor atual (sem saltar): usado quando outra animação assume a propriedade.</summary>
    public void Cancel() { Target = Value; Velocity = 0; _delay = 0; }

    public void Step(double dt)
    {
        if (_delay > 0) { _delay -= dt; return; }
        if (dt <= 0) return;
        int n = Math.Max(1, (int)Math.Ceiling(dt / 0.004));
        double h = dt / n;
        for (int i = 0; i < n; i++)
        {
            double a = -Stiffness * (Value - Target) - Damping * Velocity;
            Velocity += a * h;
            Value += Velocity * h;
        }
        if (Math.Abs(Value - Target) < Epsilon && Math.Abs(Velocity) < VelocityEpsilon) { Value = Target; Velocity = 0; }
    }
}

/// <summary>
/// Conjunto de molas que anda junto. Não abre assinatura própria de render: registra-se no
/// <see cref="MotionHost"/>, que é o dono único do relógio (um dt por quadro para o app inteiro).
/// </summary>
public sealed class MotionLoop : IFrameSink
{
    readonly List<Spring> _springs = new();
    readonly Action _frame;

    public MotionLoop(Action frame) { _frame = frame; }

    public Spring Add(Spring s) { _springs.Add(s); return s; }

    public void Kick() => MotionHost.Attach(this);

    public void Stop() => MotionHost.Detach(this);

    public bool Step(double dt)
    {
        foreach (var s in _springs) s.Step(dt);
        _frame();
        foreach (var s in _springs) if (!s.Settled) return true;
        return false;
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
        // Math.Clamp com máximo negativo lança: durante um overshoot de mola a forma pode chegar degenerada.
        r = Math.Clamp(r, 0, Math.Max(0, Math.Min(w / 4, h / 2)));
        rb = Math.Clamp(rb, 0, Math.Max(0, Math.Min((w - 2 * r) / 2, h - r)));
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
/// Área da janela que responde ao mouse. A janela é grande (a silhueta mais o halo da sombra), mas só a
/// parte visível da ilha deve capturar cliques: o resto precisa atravessar para o app de baixo. Isso é
/// feito com SetWindowRgn, e a região é uma lista de retângulos (o GDI não tem região de cantos redondos).
/// </summary>
public static class HitRegion
{
    /// <summary>
    /// Retângulos que cobrem exatamente a silhueta. O corpo é a caixa cheia; em cada canto redondo sobra uma
    /// "escada" de faixas de 1 px cuja largura sai do próprio círculo, então nenhum pixel de fora da ilha
    /// fica clicável e nenhum pixel de dentro é perdido. Coordenadas em pixels, inteiras.
    /// </summary>
    public static List<Int32Rect> Rectangles(int x, int y, int w, int h, int radius)
    {
        var rects = new List<Int32Rect>();
        if (w <= 0 || h <= 0) return rects;
        int r = Math.Clamp(radius, 0, Math.Min(w / 2, h / 2));
        if (r <= 0) { rects.Add(new Int32Rect(x, y, w, h)); return rects; }

        int midTop = y + r, midH = h - 2 * r;
        if (midH > 0) rects.Add(new Int32Rect(x, midTop, w, midH));          // faixa do meio, largura inteira

        // Cantos: para cada linha a `d` px do topo (ou do fundo), quanto o círculo "entra" na caixa.
        // inset = r - floor(sqrt(r² - (r-d)²)): o pixel só é incluído quando o centro dele está dentro do arco.
        for (int d = 0; d < r; d++)
        {
            double dy = r - d - 0.5;
            int inset = r - (int)Math.Floor(Math.Sqrt(Math.Max(0, r * r - dy * dy)));
            int rowW = w - 2 * inset;
            if (rowW <= 0) continue;
            rects.Add(new Int32Rect(x + inset, y + d, rowW, 1));
            rects.Add(new Int32Rect(x + inset, y + h - 1 - d, rowW, 1));
        }
        return rects;
    }

    /// <summary>
    /// Retângulos exatamente da silhueta de notch (modo Clássico): topo reto colado na borda, ombros côncavos de
    /// raio <paramref name="ear"/> e base arredondada de raio <paramref name="bottom"/>. O corpo recua
    /// <paramref name="ear"/> de cada lado em toda a altura — é o espaço vazio dos ombros, por onde passam as abas
    /// do navegador. Uma caixa arredondada ali (o que se usava) engolia esses cliques.
    /// </summary>
    public static List<Int32Rect> Notch(int x, int y, int w, int h, int ear, int bottom)
    {
        var rects = new List<Int32Rect>();
        if (w <= 0 || h <= 0) return rects;
        int r = Math.Clamp(ear, 0, Math.Min(w / 4, h / 2));
        int rb = Math.Clamp(bottom, 0, Math.Max(0, Math.Min((w - 2 * r) / 2, h - r)));
        int bodyW = w - 2 * r;
        if (bodyW <= 0) { rects.Add(new Int32Rect(x, y, w, h)); return rects; }

        int bodyTop = y + r, bodyBottom = y + h - rb;
        if (bodyBottom > bodyTop) rects.Add(new Int32Rect(x + r, bodyTop, bodyW, bodyBottom - bodyTop));

        // Ombros: nas primeiras `r` linhas a silhueta tem a largura toda e a curva côncava come as pontas.
        for (int d = 0; d < r; d++)
        {
            double t = d + 0.5;
            int grow = (int)Math.Floor(Math.Sqrt(Math.Max(0, (double)r * r - t * t)));
            int rowW = bodyW + 2 * grow;
            if (rowW <= 0) continue;
            rects.Add(new Int32Rect(x + r - grow, y + d, rowW, 1));
        }

        // Base arredondada (mesma escada de 1 px dos cantos da caixa).
        for (int d = 0; d < rb; d++)
        {
            double dy = rb - d - 0.5;
            int inset = rb - (int)Math.Floor(Math.Sqrt(Math.Max(0, (double)rb * rb - dy * dy)));
            int rowW = bodyW - 2 * inset;
            if (rowW <= 0) continue;
            rects.Add(new Int32Rect(x + r + inset, y + h - 1 - d, rowW, 1));
        }
        return rects;
    }

}

/// <summary>
/// Camada de "motion design" do app. Entradas em cascata, presets de física e animações
/// interrompíveis: tudo passa pelo <see cref="MotionEngine"/>, que garante um único dono por propriedade.
/// </summary>
public static class Motion
{
    // Presets (rigidez, amortecimento)
    public static readonly (double k, double c) Snappy = (520, 38), Smooth = (260, 30), Bouncy = (300, 16), Gentle = (170, 24), Wobbly = (200, 12);

    /// <summary>
    /// Canais de transformação do elemento, na ordem que o motor espera: (escala, deslocamento).
    /// Reaproveita o grupo existente para não perder inclinação nem outras transformações.
    /// </summary>
    public static (ScaleTransform sc, TranslateTransform tt) Channels(FrameworkElement el)
    {
        if (el.RenderTransform is TransformGroup g && !g.IsFrozen && g.Children.Count >= 2
            && g.Children[0] is ScaleTransform s && g.Children[1] is TranslateTransform t)
            return (s, t);
        var sc = new ScaleTransform(1, 1); var tt = new TranslateTransform();
        el.RenderTransformOrigin = new Point(0.5, 0.5);
        el.RenderTransform = new TransformGroup { Children = { sc, tt } };
        return (sc, tt);
    }

    static (ScaleTransform sc, TranslateTransform tt) Transforms(FrameworkElement el) => Channels(el);

    /// <summary>Faz o elemento surgir: fade rápido + subida com mola + escala com leve overshoot.</summary>
    public static void Reveal(FrameworkElement el, double delaySec = 0, double fromY = Tz.Dist.RevealY, double fromScale = Tz.Dist.RevealScale)
    {
        var (sc, tt) = Transforms(el);
        // A posse é do motor: nenhuma animação anterior fica "presa" nessas propriedades.
        MotionEngine.Jump(el, UIElement.OpacityProperty, 0);
        MotionEngine.Jump(tt, TranslateTransform.YProperty, fromY);
        MotionEngine.Jump(sc, ScaleTransform.ScaleXProperty, fromScale);
        MotionEngine.Jump(sc, ScaleTransform.ScaleYProperty, fromScale);
        MotionEngine.To(el, UIElement.OpacityProperty, 1, Tz.Springs.Fade, delaySec);
        MotionEngine.To(tt, TranslateTransform.YProperty, 0, Tz.Springs.Content, delaySec);
        MotionEngine.To(sc, ScaleTransform.ScaleXProperty, 1, new SpringSpec(260, 16), delaySec);
        MotionEngine.To(sc, ScaleTransform.ScaleYProperty, 1, new SpringSpec(260, 16), delaySec);
    }

    public static void RevealAll(IEnumerable<FrameworkElement> items, double baseDelay = 0.06, double step = Tz.Delays.Stagger, double fromY = Tz.Dist.RevealY, double fromScale = Tz.Dist.RevealScale)
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

    public static void RevealPage(IPage page, double baseDelay = 0.08, double step = Tz.Delays.Stagger, double fromY = Tz.Dist.RevealY, double fromScale = Tz.Dist.RevealScale)
    {
        var targets = page is PageBase pb ? pb.RevealTargets.ToList() : Targets(page.View);
        // (sem fallback: a View da página usa um TranslateTransform próprio na troca de abas e não pode ser trocado)
        RevealAll(targets, baseDelay, step, fromY, fromScale);
    }

    /// <summary>Deslocamento vertical do elemento, criando o canal se necessário (sem sobrescrever escala).</summary>
    static TranslateTransform Slide(FrameworkElement el)
    {
        if (el.RenderTransform is TransformGroup g && !g.IsFrozen && g.Children.OfType<TranslateTransform>().Any())
            return g.Children.OfType<TranslateTransform>().First();
        if (el.RenderTransform is TranslateTransform tt && !tt.IsFrozen) return tt;
        var n = new TranslateTransform();
        el.RenderTransform = n;
        return n;
    }

    /// <summary>
    /// Entrada de item de lista. Antes usava storyboard com FillBehavior.HoldEnd (a opacidade ficava
    /// "presa" em 1 e brigava com o reveal). Agora é track com posse: quem chamar depois assume.
    /// </summary>
    public static void FadeIn(UIElement el, int delayMs = 0, double fromY = 10)
    {
        double delay = Math.Max(0, delayMs) / 1000.0;
        MotionEngine.Jump(el, UIElement.OpacityProperty, 0);
        MotionEngine.To(el, UIElement.OpacityProperty, 1, Tz.Springs.Fade, delay);
        if (el is FrameworkElement fe)
        {
            var tt = Slide(fe);
            MotionEngine.Jump(tt, TranslateTransform.YProperty, fromY);
            MotionEngine.To(tt, TranslateTransform.YProperty, 0, Tz.Springs.Content, delay);
        }
    }

    /// <summary>Desloca o elemento verticalmente agora, sem animar (ponto de partida de uma entrada).</summary>
    public static void SetSlide(FrameworkElement el, double y)
    {
        if (el.RenderTransform is TranslateTransform t) MotionEngine.Jump(t, TranslateTransform.YProperty, y);
        else if (el.RenderTransform is TransformGroup g && g.Children.Count >= 2 && g.Children[1] is TranslateTransform t2)
            MotionEngine.Jump(t2, TranslateTransform.YProperty, y);
    }

    /// <summary>Anima o deslocamento vertical até <paramref name="y"/>, com atraso opcional.</summary>
    public static void SlideTo(FrameworkElement el, double y, double delay = 0)
    {
        if (el.RenderTransform is TranslateTransform t) MotionEngine.To(t, TranslateTransform.YProperty, y, Tz.Springs.Content, delay);
        else if (el.RenderTransform is TransformGroup g && g.Children.Count >= 2 && g.Children[1] is TranslateTransform t2)
            MotionEngine.To(t2, TranslateTransform.YProperty, y, Tz.Springs.Content, delay);
    }

    /// <summary>Encerra animações dessas propriedades e grava o valor (usado ao desmontar uma página).</summary>
    public static void Rest(FrameworkElement el, double opacity = 1, double y = 0)
    {
        MotionEngine.Jump(el, UIElement.OpacityProperty, opacity);
        if (el.RenderTransform is TranslateTransform t) MotionEngine.Jump(t, TranslateTransform.YProperty, y);
        else if (el.RenderTransform is TransformGroup g && g.Children.Count >= 2 && g.Children[1] is TranslateTransform t2)
            MotionEngine.Jump(t2, TranslateTransform.YProperty, y);
    }
}
