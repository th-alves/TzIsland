namespace TzIsland;

/// <summary>
/// Tokens de movimento do TzIsland — a fonte única de números para animação.
/// A identidade é "física silenciosa": amortecimento perto do crítico, amplitudes pequenas,
/// saída mais rápida que a entrada e o raio acompanhando a forma com um leve atraso.
/// </summary>
public static class Tz
{
    public static class Springs
    {
        public static readonly SpringSpec Form = new(320, 24);              // silhueta: largura/altura
        public static readonly SpringSpec Edge = new(260, 26);              // raio e ombros do notch
        public static readonly SpringSpec Content = new(240, 19);           // escala/posição de conteúdo
        public static readonly SpringSpec Fade = new(380, 36, 0.002);       // opacidade (tolerância fina: 0,2%)
        public static readonly SpringSpec Lift = new(520, 30);              // hover/press
        public static readonly SpringSpec Lens = new(380, 21);              // lente da barra e dos chips
        public static readonly SpringSpec Sheet = new(300, 19);             // folhas e painéis
        public static readonly SpringSpec Exit = new(640, 50);              // saídas rápidas
        public static readonly SpringSpec Settle = new(170, 24);            // assentamentos longos e discretos
    }

    public static class Delays
    {
        public const double Edge = 0.02;      // o raio entra depois da forma
        public const double Content = 0.03;   // conteúdo depois da forma
        public const double PageIn = 0.07;    // a página antiga sai antes da nova entrar
        public const double Stagger = 0.05;   // cascata
    }

    public static class Dist
    {
        public const double Lift = -2.5;      // elevação no hover
        public const double EnterY = 26;      // deslocamento de entrada de página
        public const double ExitY = 18;       // deslocamento de saída de página
        public const double RevealY = 18;
        public const double RevealScale = 0.94;
    }

    /// <summary>Defasagem de cascata por índice (lista).</summary>
    public static double Stagger(int index, double step = Delays.Stagger) => index * step;

    /// <summary>Defasagem de cascata radial: o conteúdo nasce do centro da forma, não da lista.</summary>
    public static double Radial(double x, double y, double cx, double cy, double maxSec = 0.22, double radius = 260)
    {
        double d = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        return Math.Min(maxSec, d / radius * maxSec);
    }
}

/// <summary>
/// Micro-interações implícitas: hover, press e tint. Tudo em cima do motor, então sair no meio
/// de um hover reinicia de onde está (sem o salto típico de storyboard com "To" sem "From").
/// </summary>
public static class Interaction
{
    /// <summary>Eleva o elemento no hover e devolve na saída, com mola.</summary>
    public static void Lift(FrameworkElement el, double dy = Tz.Dist.Lift, Action<bool>? extra = null)
    {
        var (_, tt) = Motion.Channels(el);
        el.MouseEnter += (_, _) =>
        {
            MotionEngine.To(tt, TranslateTransform.YProperty, dy, Tz.Springs.Lift);
            extra?.Invoke(true);
        };
        el.MouseLeave += (_, _) =>
        {
            MotionEngine.To(tt, TranslateTransform.YProperty, 0, Tz.Springs.Lift.With(0.85, 1.2));
            extra?.Invoke(false);
        };
    }

    /// <summary>Encolhe levemente ao pressionar e volta com mola ao soltar — interrompível.</summary>
    public static void Press(FrameworkElement el, double scale = 0.94)
    {
        var (sc, _) = Motion.Channels(el);
        void To(double v, SpringSpec s)
        {
            MotionEngine.To(sc, ScaleTransform.ScaleXProperty, v, s);
            MotionEngine.To(sc, ScaleTransform.ScaleYProperty, v, s);
        }
        el.PreviewMouseLeftButtonDown += (_, _) => To(scale, Tz.Springs.Lift);
        el.PreviewMouseLeftButtonUp += (_, _) => To(1, Tz.Springs.Lift.With(0.7, 1));
        el.LostMouseCapture += (_, _) => To(1, Tz.Springs.Lift.With(0.7, 1));
        el.MouseLeave += (_, _) => To(1, Tz.Springs.Lift.With(0.7, 1));
    }

    /// <summary>Troca a cor de um brush (borda, fundo, ícone) com transição suave e com posse.</summary>
    public static void Tint(SolidColorBrush brush, Color to, double ms = 220) =>
        MotionEngine.ToColor(brush, SolidColorBrush.ColorProperty, to, Tz.Springs.Lift);   // por mola: assenta em vez de parar
}
