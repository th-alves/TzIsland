namespace TzIsland;

/// <summary>Quem pede um passo por quadro. Devolve false quando assentou (sai do laço).</summary>
public interface IFrameSink
{
    bool Step(double dt);
}

/// <summary>
/// Dono único do relógio de animação: uma assinatura de CompositionTarget.Rendering para o app inteiro,
/// um dt calculado por quadro e distribuído a todos os consumidores (molas, tracks, medidores).
/// Zero custo quando ninguém precisa de quadros — o laço se desliga sozinho.
/// </summary>
public static class MotionHost
{
    static readonly List<IFrameSink> Sinks = new();
    static readonly List<IFrameSink> Buffer = new();
    static bool _on;
    static long _last;
    static bool _paused;

    /// <summary>
    /// Pausado quando a ilha está oculta, a sessão está bloqueada ou a máquina dormindo —
    /// nada de animar o que ninguém vê (é o maior gasto de bateria de uma app sempre-ligada).
    /// </summary>
    public static bool Paused
    {
        get => _paused;
        set
        {
            if (_paused == value) return;
            _paused = value;
            if (value) Stop(); else Kick();
        }
    }

    /// <summary>Tempo acumulado de animação (s). Fases de respiração/espectro usam este relógio para continuar entre superfícies.</summary>
    public static double Now { get; private set; }

    public static int SinksCount => Sinks.Count;
    public static bool Running => _on;
    public static int RenderSubscriptions => _on ? 1 : 0;

    public static void Attach(IFrameSink sink)
    {
        if (sink == null || Sinks.Contains(sink)) return;
        Sinks.Add(sink);
        Kick();
    }

    public static void Detach(IFrameSink sink) => Sinks.Remove(sink);

    public static void Kick()
    {
        if (_on || Paused || Sinks.Count == 0) return;
        _on = true;
        _last = Stopwatch.GetTimestamp();
        CompositionTarget.Rendering += OnRender;
    }

    public static void Stop()
    {
        if (!_on) return;
        CompositionTarget.Rendering -= OnRender;
        _on = false;
        _last = 0;
    }

    static void OnRender(object? sender, EventArgs e)
    {
        long now = Stopwatch.GetTimestamp();
        double dt = (now - _last) / (double)Stopwatch.Frequency;
        _last = now;
        dt = Math.Clamp(dt, 0, 0.25);      // volta de suspensão: não teleporta o estado
        MotionStats.Sample(dt);
        // Um passo por quadro entregue pelo vsync: acumular espera aqui vira soluço entre quadros.
        Now += dt;

        Buffer.Clear();
        Buffer.AddRange(Sinks);
        bool any = false;
        foreach (var s in Buffer)
        {
            if (!Sinks.Contains(s)) continue;          // foi detachado no meio do quadro
            if (s.Step(dt)) any = true;
            else Sinks.Remove(s);
        }
        if (!any || Sinks.Count == 0) Stop();
    }
}

/// <summary>
/// Instrumentação mínima (Etapa 0): tempo de quadro p50/p95, quadros longos e quantos consumidores
/// estão ativos. Ligada por atalho na ilha (Ctrl+Shift+M); custo desprezível quando ninguém lê.
/// </summary>
public static class MotionStats
{
    const int Cap = 180;
    static readonly double[] Samples = new double[Cap];
    static int _n, _i;

    public static long Frames { get; private set; }
    public static long LongFrames { get; private set; }
    public static double LastMs { get; private set; }

    public static void Sample(double dt)
    {
        double ms = dt * 1000;
        LastMs = ms;
        Samples[_i] = ms;
        _i = (_i + 1) % Cap;
        if (_n < Cap) _n++;
        Frames++;
        if (ms > 20) LongFrames++;
    }

    public static double Percentile(double p)
    {
        if (_n == 0) return 0;
        var a = new double[_n];
        Array.Copy(Samples, a, _n);
        Array.Sort(a);
        int idx = (int)Math.Round((a.Length - 1) * Math.Clamp(p, 0, 1));
        return a[idx];
    }

    public static void Reset()
    {
        _n = _i = 0;
        Frames = LongFrames = 0;
        LastMs = 0;
    }

    public static string Line() =>
        $"quadro {LastMs:0.0} ms · p50 {Percentile(0.5):0.0} · p95 {Percentile(0.95):0.0} · longos {LongFrames}/{Frames} · sinks {MotionHost.SinksCount} · render {MotionHost.RenderSubscriptions}";
}
