using System.Windows.Media.Animation;

namespace TzIsland;

/// <summary>Parâmetros de uma mola, em (rigidez, amortecimento) mais tolerâncias próprias por grandeza.</summary>
public readonly struct SpringSpec
{
    public readonly double K, C, Eps, VelEps;

    public SpringSpec(double k, double c, double eps = 0.01, double velEps = 0.02)
    {
        K = k; C = c; Eps = eps; VelEps = velEps;
    }

    /// <summary>Variação pontual de um token, sem criar outro preset.</summary>
    public SpringSpec With(double kScale, double cScale) => new(K * kScale, C * cScale, Eps, VelEps);
}

/// <summary>Handle de uma animação contínua (respiração, varredura). Stop encerra sem salto.</summary>
public sealed class MotionHandle
{
    readonly ITrack _track;
    internal MotionHandle(ITrack track) { _track = track; }
    public void Stop() => _track.Kill();
    public bool IsAlive => !_track.Dead;
}

/// <summary>Identidade de posse: um objeto + uma propriedade.</summary>
internal readonly struct MotionKey : IEquatable<MotionKey>
{
    public readonly DependencyObject Target;
    public readonly DependencyProperty Property;

    public MotionKey(DependencyObject target, DependencyProperty property) { Target = target; Property = property; }

    public bool Equals(MotionKey k) => ReferenceEquals(Target, k.Target) && ReferenceEquals(Property, k.Property);
    public override bool Equals(object? obj) => obj is MotionKey k && Equals(k);
    public override int GetHashCode() =>
        System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Target) * 397 ^ Property.GetHashCode();
}

internal interface ITrack : IFrameSink
{
    MotionKey Key { get; }
    void Kill();
    double Speed { get; }
    bool Dead { get; }
}

/// <summary>
/// Camada de posse de propriedades: cada (objeto, DependencyProperty) tem no máximo uma track.
/// Qualquer nova animação cancela a anterior partindo do valor visual atual e da velocidade atual,
/// então interromper, inverter ou substituir nunca produz salto. Ao assentar, o valor é gravado como
/// valor local e a track sai do registro no mesmo quadro (sem FillBehavior.HoldEnd "preso").
/// </summary>
public static class MotionEngine
{
    static readonly Dictionary<MotionKey, ITrack> Active = new();

    public static int ActiveCount => Active.Count;

    // ---------------- API pública ----------------

    /// <summary>Anima até <paramref name="to"/> partindo do valor visual atual (e da velocidade atual, se havia animação).</summary>
    public static void To(DependencyObject target, DependencyProperty prop, double to, SpringSpec spec, double delay = 0, Action? done = null)
    {
        var key = new MotionKey(target, prop);
        double from = Read(target, prop);
        double vel = 0;
        if (Active.TryGetValue(key, out var old))
        {
            vel = old.Speed;
            old.Kill();
        }
        Release(target, prop);
        target.SetValue(prop, from);            // congela no valor visual antes de assumir a posse
        var t = new PropTrack(key, target, prop, from, vel, to, spec, delay, done);
        Active[key] = t;
        MotionHost.Attach(t);
    }

    /// <summary>Para a animação atual e grava um valor (entrada de elemento, teleporte, reset).</summary>
    public static void Jump(DependencyObject target, DependencyProperty prop, double value)
    {
        var key = new MotionKey(target, prop);
        if (Active.TryGetValue(key, out var old)) old.Kill();
        Release(target, prop);
        target.SetValue(prop, value);
    }

    /// <summary>Encerra a animação assentando onde está, sem mexer no valor.</summary>
    public static void Stop(DependencyObject target, DependencyProperty prop)
    {
        var key = new MotionKey(target, prop);
        if (Active.TryGetValue(key, out var old)) old.Kill();
    }

    public static bool IsAnimating(DependencyObject target, DependencyProperty prop) => Active.ContainsKey(new MotionKey(target, prop));

    /// <summary>Cor animada (acento da capa, tint de hover) com posse: trocar no meio do caminho não salta.</summary>
    public static void ToColor(DependencyObject target, DependencyProperty prop, Color to, double ms = 600)
    {
        var key = new MotionKey(target, prop);
        var from = target.GetValue(prop) is Color c ? c : to;
        if (Active.TryGetValue(key, out var old)) old.Kill();
        Release(target, prop);
        target.SetValue(prop, from);
        var t = new ColorTrack(key, target, prop, from, to, Math.Max(1, ms) / 1000.0);
        Active[key] = t;
        MotionHost.Attach(t);
    }

    /// <summary>Cor com mola: o tom "assenta" (com passagem curta) em vez de parar seco numa duração.</summary>
    public static void ToColor(DependencyObject target, DependencyProperty prop, Color to, SpringSpec spec, double delay = 0)
    {
        var key = new MotionKey(target, prop);
        var from = target.GetValue(prop) is Color c ? c : to;
        if (Active.TryGetValue(key, out var old)) old.Kill();
        Release(target, prop);
        target.SetValue(prop, from);
        var t = new ColorSpringTrack(key, target, prop, from, to, spec, delay);
        Active[key] = t;
        MotionHost.Attach(t);
    }

    /// <summary>Grava uma cor agora, encerrando qualquer animação de cor em voo.</summary>
    public static void SetColor(DependencyObject target, DependencyProperty prop, Color c)
    {
        var key = new MotionKey(target, prop);
        if (Active.TryGetValue(key, out var old)) old.Kill();
        Release(target, prop);
        target.SetValue(prop, c);
    }

    /// <summary>
    /// Animação contínua (decoração): recebe o tempo em segundos e devolve o valor da propriedade.
    /// Continua registrada como dona da propriedade — qualquer To/Jump assume e a encerra.
    /// </summary>
    public static MotionHandle Wave(DependencyObject target, DependencyProperty prop, Func<double, double> fn)
    {
        var key = new MotionKey(target, prop);
        if (Active.TryGetValue(key, out var old)) old.Kill();
        Release(target, prop);
        var t = new WaveTrack(key, target, prop, fn);
        Active[key] = t;
        MotionHost.Attach(t);
        return new MotionHandle(t);
    }

    // ---------------- interno ----------------

    static double Read(DependencyObject target, DependencyProperty prop)
    {
        var v = target.GetValue(prop);
        double d = v is double x ? x : v is float f ? f : 0;
        // Height/Width "Auto" valem NaN: uma mola com NaN nunca assenta. Trata como zero.
        return double.IsNaN(d) || double.IsInfinity(d) ? 0 : d;
    }

    static void Release(DependencyObject target, DependencyProperty prop)
    {
        if (target is Animatable a) a.BeginAnimation(prop, null);
    }

    internal static void Forget(ITrack track)
    {
        if (Active.TryGetValue(track.Key, out var cur) && ReferenceEquals(cur, track)) Active.Remove(track.Key);
    }

    // ---------------- tracks ----------------

    sealed class PropTrack : ITrack
    {
        readonly DependencyObject _o;
        readonly DependencyProperty _p;
        readonly SpringSpec _spec;
        Action? _done;
        double _v, _vel, _target, _delay;
        bool _dead;

        public PropTrack(MotionKey key, DependencyObject o, DependencyProperty p, double from, double vel, double to, SpringSpec spec, double delay, Action? done)
        {
            Key = key; _o = o; _p = p; _v = from; _vel = vel; _target = to; _spec = spec; _delay = Math.Max(0, delay); _done = done;
        }

        public MotionKey Key { get; }
        public bool Dead => _dead;
        public double Speed => _vel;

        public bool Step(double dt)
        {
            if (_dead) return false;
            if (_delay > 0)
            {
                _delay -= dt;
                if (_delay > 0) return true;
                dt = -_delay;                 // a sobra do atraso vira movimento, sem engasgo
                _delay = 0;
            }
            if (dt <= 0) return true;
            int n = Math.Max(1, (int)Math.Ceiling(dt / 0.004));
            double h = dt / n;
            for (int i = 0; i < n; i++)
            {
                double a = -_spec.K * (_v - _target) - _spec.C * _vel;
                _vel += a * h;
                _v += _vel * h;
            }
            _o.SetValue(_p, _v);
            if (Math.Abs(_v - _target) < _spec.Eps && Math.Abs(_vel) < _spec.VelEps) { Settle(); return false; }
            return true;
        }

        void Settle()
        {
            _o.SetValue(_p, _target);
            _vel = 0;
            Finish();
        }

        void Finish()
        {
            _dead = true;
            MotionEngine.Forget(this);
            var cb = _done;
            _done = null;
            cb?.Invoke();
        }

        public void Kill()
        {
            if (_dead) return;
            _o.SetValue(_p, _v);
            Finish();
        }
    }

    sealed class ColorTrack : ITrack
    {
        readonly DependencyObject _o;
        readonly DependencyProperty _p;
        readonly Color _from, _to;
        readonly double _dur;
        double _t;
        bool _dead;

        public ColorTrack(MotionKey key, DependencyObject o, DependencyProperty p, Color from, Color to, double dur)
        { Key = key; _o = o; _p = p; _from = from; _to = to; _dur = dur; }

        public MotionKey Key { get; }
        public bool Dead => _dead;
        public double Speed => 0;

        public bool Step(double dt)
        {
            if (_dead) return false;
            _t += dt;
            double k = Math.Clamp(_t / _dur, 0, 1);
            double e = 0.5 - 0.5 * Math.Cos(Math.PI * k);      // suave na entrada e na saída, sem alocar easing
            _o.SetValue(_p, Color.FromArgb(
                (byte)(_from.A + (_to.A - _from.A) * e),
                (byte)(_from.R + (_to.R - _from.R) * e),
                (byte)(_from.G + (_to.G - _from.G) * e),
                (byte)(_from.B + (_to.B - _from.B) * e)));
            if (k >= 1) { Finish(); return false; }
            return true;
        }

        void Finish()
        {
            _dead = true;
            MotionEngine.Forget(this);
        }

        public void Kill()
        {
            if (_dead) return;
            Finish();
        }
    }

    /// <summary>Cor dirigida por uma mola em t (0 → 1): interromper no meio preserva velocidade e tom.</summary>
    sealed class ColorSpringTrack : ITrack
    {
        readonly DependencyObject _o;
        readonly DependencyProperty _p;
        readonly Color _from, _to;
        readonly SpringSpec _spec;
        double _v, _vel, _delay;
        bool _dead;

        public ColorSpringTrack(MotionKey key, DependencyObject o, DependencyProperty p, Color from, Color to, SpringSpec spec, double delay)
        { Key = key; _o = o; _p = p; _from = from; _to = to; _spec = spec; _delay = Math.Max(0, delay); }

        public MotionKey Key { get; }
        public bool Dead => _dead;
        public double Speed => _vel;

        public bool Step(double dt)
        {
            if (_dead) return false;
            if (_delay > 0)
            {
                _delay -= dt;
                if (_delay > 0) return true;
                dt = -_delay; _delay = 0;
            }
            if (dt <= 0) return true;
            int n = Math.Max(1, (int)Math.Ceiling(dt / 0.004));
            double h = dt / n;
            for (int i = 0; i < n; i++)
            {
                double a = -_spec.K * (_v - 1) - _spec.C * _vel;
                _vel += a * h;
                _v += _vel * h;
            }
            Write(_v);
            if (Math.Abs(_v - 1) < _spec.Eps && Math.Abs(_vel) < _spec.VelEps) { Write(1); _dead = true; MotionEngine.Forget(this); return false; }
            return true;
        }

        void Write(double t)
        {
            double e = Math.Clamp(t, 0, 1);
            _o.SetValue(_p, Color.FromArgb(
                (byte)(_from.A + (_to.A - _from.A) * e),
                (byte)(_from.R + (_to.R - _from.R) * e),
                (byte)(_from.G + (_to.G - _from.G) * e),
                (byte)(_from.B + (_to.B - _from.B) * e)));
        }

        public void Kill()
        {
            if (_dead) return;
            _dead = true;
            MotionEngine.Forget(this);
        }
    }

    sealed class WaveTrack : ITrack
    {
        readonly DependencyObject _o;
        readonly DependencyProperty _p;
        readonly Func<double, double> _fn;
        double _t;
        bool _dead;

        public WaveTrack(MotionKey key, DependencyObject o, DependencyProperty p, Func<double, double> fn)
        { Key = key; _o = o; _p = p; _fn = fn; }

        public MotionKey Key { get; }
        public bool Dead => _dead;
        public double Speed => 0;

        public bool Step(double dt)
        {
            if (_dead) return false;
            _t += dt;
            _o.SetValue(_p, _fn(_t));
            return true;
        }

        public void Kill()
        {
            if (_dead) return;
            _dead = true;
            MotionEngine.Forget(this);
        }
    }
}
