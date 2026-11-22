using NAudio.Wave;

namespace TzIsland;

// ============================================================================================
// Motor de sons ambiente. Tudo é sintetizado em tempo real (ruído filtrado + eventos aleatórios),
// então o app não precisa de arquivos de áudio nem de direitos autorais. Se existir um arquivo
// com o id do som em  <pasta do app>\Sounds\  ou  %AppData%\TzIsland\Sounds\  (ex.: rain.mp3),
// ele substitui o som sintetizado (toca em loop).
// ============================================================================================

/// <summary>Gerador xorshift32: barato e sem alocação (roda na thread de áudio).</summary>
public sealed class Rng
{
    uint _s;
    public Rng(uint seed) { _s = seed == 0 ? 2463534242u : seed; }
    uint Step() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return _s; }
    /// <summary>-1 … 1</summary>
    public float Next() => (Step() & 0xFFFFFF) / 8388608f - 1f;
    /// <summary>0 … 1</summary>
    public float Uni() => (Step() & 0xFFFFFF) / 16777216f;
    public float Range(float a, float b) => a + (b - a) * Uni();
}

/// <summary>Filtro biquad (receitas RBJ). Coeficientes podem ser trocados em tempo real.</summary>
public sealed class Biquad
{
    float _b0 = 1, _b1, _b2, _a1, _a2, _z1, _z2;
    const double Fs = AmbLayer.Fs;

    public static Biquad Low(double f, double q = 0.707) { var b = new Biquad(); b.SetLow(f, q); return b; }
    public static Biquad High(double f, double q = 0.707) { var b = new Biquad(); b.SetHigh(f, q); return b; }
    public static Biquad Band(double f, double q = 1) { var b = new Biquad(); b.SetBand(f, q); return b; }

    void Set(double b0, double b1, double b2, double a0, double a1, double a2)
    {
        _b0 = (float)(b0 / a0); _b1 = (float)(b1 / a0); _b2 = (float)(b2 / a0); _a1 = (float)(a1 / a0); _a2 = (float)(a2 / a0);
    }

    static (double cos, double alpha) Prep(double f, double q)
    {
        f = Math.Clamp(f, 20, Fs * 0.45);
        double w0 = 2 * Math.PI * f / Fs;
        return (Math.Cos(w0), Math.Sin(w0) / (2 * Math.Max(0.1, q)));
    }

    public void SetLow(double f, double q = 0.707) { var (c, a) = Prep(f, q); Set((1 - c) / 2, 1 - c, (1 - c) / 2, 1 + a, -2 * c, 1 - a); }
    public void SetHigh(double f, double q = 0.707) { var (c, a) = Prep(f, q); Set((1 + c) / 2, -(1 + c), (1 + c) / 2, 1 + a, -2 * c, 1 - a); }
    public void SetBand(double f, double q = 1) { var (c, a) = Prep(f, q); Set(a, 0, -a, 1 + a, -2 * c, 1 - a); }

    public float Process(float x)
    {
        float y = _b0 * x + _z1;
        _z1 = _b1 * x - _a1 * y + _z2;
        _z2 = _b2 * x - _a2 * y;
        return y;
    }
}

/// <summary>Ruído rosa (Paul Kellet), já normalizado para ~mesma energia do ruído branco.</summary>
public sealed class Pink
{
    float _b0, _b1, _b2, _b3, _b4, _b5, _b6;
    public float Next(float w)
    {
        _b0 = 0.99886f * _b0 + w * 0.0555179f; _b1 = 0.99332f * _b1 + w * 0.0750759f;
        _b2 = 0.96900f * _b2 + w * 0.1538520f; _b3 = 0.86650f * _b3 + w * 0.3104856f;
        _b4 = 0.55000f * _b4 + w * 0.5329522f; _b5 = -0.7616f * _b5 - w * 0.0168980f;
        float p = _b0 + _b1 + _b2 + _b3 + _b4 + _b5 + _b6 + w * 0.5362f;
        _b6 = w * 0.115926f;
        return p * 0.2f;
    }
}

/// <summary>Conjunto de "plins" (senoides com decaimento): gotas, tilintar de xícaras, cantos de pássaros…</summary>
public sealed class Pings
{
    readonly double[] _ph, _fr, _amp, _dec;
    public Pings(int n) { _ph = new double[n]; _fr = new double[n]; _amp = new double[n]; _dec = new double[n]; }

    public void Trigger(double hz, double amp, double decaySec)
    {
        int k = 0; double min = double.MaxValue;
        for (int i = 0; i < _amp.Length; i++) if (_amp[i] < min) { min = _amp[i]; k = i; }
        _ph[k] = 0; _fr[k] = 2 * Math.PI * hz / AmbLayer.Fs; _amp[k] = amp;
        _dec[k] = Math.Exp(-1.0 / (Math.Max(0.0005, decaySec) * AmbLayer.Fs));
    }

    public float Next()
    {
        double sum = 0;
        for (int i = 0; i < _amp.Length; i++)
        {
            if (_amp[i] < 1e-5) continue;
            sum += Math.Sin(_ph[i]) * _amp[i];
            _ph[i] += _fr[i]; _amp[i] *= _dec[i];
        }
        return (float)sum;
    }
}

/// <summary>Rajadas curtas de ruído filtrado (estalos de fogo, cliques de teclado).</summary>
public sealed class Bursts
{
    readonly Biquad[] _f; readonly int[] _left, _len; readonly float[] _amp; readonly Rng _r;
    public Bursts(int n, uint seed) { _f = new Biquad[n]; for (int i = 0; i < n; i++) _f[i] = Biquad.Band(2000, 1); _left = new int[n]; _len = new int[n]; _amp = new float[n]; _r = new Rng(seed); }

    public void Trigger(double hz, double q, float amp, double ms)
    {
        int k = 0, min = int.MaxValue;
        for (int i = 0; i < _left.Length; i++) if (_left[i] < min) { min = _left[i]; k = i; }
        _f[k].SetBand(hz, q); _len[k] = _left[k] = Math.Max(8, (int)(ms * 0.001 * AmbLayer.Fs)); _amp[k] = amp;
    }

    public float Next()
    {
        float sum = 0;
        for (int i = 0; i < _left.Length; i++)
        {
            if (_left[i] <= 0) continue;
            float env = _left[i] / (float)_len[i];
            sum += _f[i].Process(_r.Next()) * _amp[i] * env * env;
            _left[i]--;
        }
        return sum;
    }
}

/// <summary>Camada de som (mono). O ganho acompanha o alvo com uma rampa suave (fade in/out de ~1,5 s).</summary>
public abstract class AmbLayer
{
    public const int Fs = 44100;
    public volatile float Target;
    float _gain;
    public bool Audible => Target > 0.0005f || _gain > 0.0005f;
    protected abstract void Fill(float[] buf, int n);

    public void MixInto(float[] acc, float[] tmp, int n, float master)
    {
        if (!Audible) return;
        Fill(tmp, n);
        const float rate = 1f / (Fs * 1.5f);
        float t = Target, g = _gain;
        for (int i = 0; i < n; i++)
        {
            float d = t - g; g += d > rate ? rate : d < -rate ? -rate : d;
            acc[i] += tmp[i] * g * master;
        }
        _gain = g;
    }
}

// ---------------------------------------------------------------------------------------------
sealed class RainLayer : AmbLayer
{
    readonly Rng _r = new(11); readonly Pink _p = new();
    readonly Biquad _hp = Biquad.High(420), _lp = Biquad.Low(7000), _hiss = Biquad.High(3800);
    readonly Pings _drops = new(24);
    protected override void Fill(float[] b, int n)
    {
        for (int i = 0; i < n; i++)
        {
            float w = _r.Next();
            float body = _lp.Process(_hp.Process(_p.Next(w)));
            if (_r.Uni() < 110f / Fs) _drops.Trigger(_r.Range(1600, 6800), _r.Range(0.01f, 0.06f), _r.Range(0.004f, 0.011f));
            b[i] = body * 0.9f + _hiss.Process(w) * 0.035f + _drops.Next();
        }
    }
}

sealed class ForestLayer : AmbLayer
{
    readonly Rng _r = new(23); readonly Pink _p = new();
    readonly Biquad _wind = Biquad.Low(420), _leaf = Biquad.High(2600);
    double _t, _nextBird = Fs * 1.5, _chirpAt = -1; int _chirpsLeft; double _ph, _f0, _f1, _dur, _age = -1, _amp;
    protected override void Fill(float[] b, int n)
    {
        for (int i = 0; i < n; i++, _t++)
        {
            double sec = _t / Fs;
            float gust = (float)(0.55 + 0.25 * Math.Sin(sec * 0.43) + 0.2 * Math.Sin(sec * 1.1 + 1.7));
            float w = _r.Next();
            float v = _wind.Process(_p.Next(w)) * 1.2f * gust + _leaf.Process(w) * 0.02f * gust * gust;

            if (_t >= _nextBird && _chirpsLeft == 0) { _chirpsLeft = _r.Uni() < 0.4f ? 1 : (int)_r.Range(2, 6); _chirpAt = _t; _f0 = _r.Range(2300, 4300); _nextBird = _t + Fs * _r.Range(2.5f, 8f); }
            if (_chirpsLeft > 0 && _t >= _chirpAt)
            {
                _age = 0; _dur = Fs * _r.Range(0.05f, 0.14f); _f1 = _f0 * _r.Range(0.7f, 1.5f); _amp = _r.Range(0.03f, 0.07f); _ph = 0;
                _chirpsLeft--; _chirpAt = _t + Fs * _r.Range(0.09f, 0.2f); _f0 *= _r.Range(0.93f, 1.07f);
            }
            if (_age >= 0)
            {
                double k = _age / _dur;
                if (k >= 1) _age = -1;
                else
                {
                    double hz = _f0 + (_f1 - _f0) * k, env = Math.Sin(Math.PI * k);
                    _ph += 2 * Math.PI * hz / Fs;
                    v += (float)(Math.Sin(_ph) * env * env * _amp);
                    _age++;
                }
            }
            b[i] = v;
        }
    }
}

sealed class CafeLayer : AmbLayer
{
    readonly Rng _r = new(37); readonly Pink _p = new();
    readonly Biquad _m1 = Biquad.Band(650, 0.7), _m2 = Biquad.Band(1450, 0.8), _room = Biquad.Low(280);
    readonly Biquad _s1 = Biquad.Low(4.5), _s2 = Biquad.Low(9);
    readonly Pings _clink = new(8);
    double _nextClink = Fs * 2;
    long _t;
    protected override void Fill(float[] b, int n)
    {
        for (int i = 0; i < n; i++, _t++)
        {
            float w = _r.Next(), p = _p.Next(w);
            // "sílabas": modulação lenta e irregular do murmúrio
            float a1 = Math.Clamp(0.35f + _s1.Process(_r.Next()) * 14f, 0.05f, 1.2f);
            float a2 = Math.Clamp(0.35f + _s2.Process(_r.Next()) * 10f, 0.05f, 1.2f);
            float v = _m1.Process(p) * a1 * 1.1f + _m2.Process(p) * a2 * 0.8f + _room.Process(p) * 0.35f;
            if (_t >= _nextClink)
            {
                if (_r.Uni() < 0.7f) { double f = _r.Range(2600, 5200); _clink.Trigger(f, _r.Range(0.02f, 0.05f), 0.16); _clink.Trigger(f * 2.76, 0.012, 0.09); }
                else _clink.Trigger(_r.Range(700, 1100), _r.Range(0.03f, 0.06f), 0.05);
                _nextClink = _t + Fs * _r.Range(1.8f, 7f);
            }
            b[i] = v + _clink.Next();
        }
    }
}

sealed class OfficeLayer : AmbLayer
{
    readonly Rng _r = new(41); readonly Pink _p = new();
    readonly Biquad _ac = Biquad.Low(190), _hiss = Biquad.High(6500), _murmur = Biquad.Band(600, 0.6), _slow = Biquad.Low(0.5);
    readonly Bursts _keys = new(6, 77); readonly Pings _thock = new(6);
    double _ph100, _nextBurst = Fs * 2, _nextKey; int _keysLeft; long _t;
    protected override void Fill(float[] b, int n)
    {
        for (int i = 0; i < n; i++, _t++)
        {
            float w = _r.Next(), p = _p.Next(w);
            _ph100 += 2 * Math.PI * 100 / Fs;
            float hum = _ac.Process(p) * 0.7f + (float)(Math.Sin(_ph100) * 0.012 + Math.Sin(_ph100 * 2) * 0.006);
            float mur = _murmur.Process(p) * Math.Clamp(0.4f + _slow.Process(_r.Next()) * 6f, 0.05f, 1f) * 0.4f;
            if (_keysLeft == 0 && _t >= _nextBurst) { _keysLeft = (int)_r.Range(6, 30); _nextKey = _t; _nextBurst = _t + Fs * _r.Range(4f, 11f); }
            if (_keysLeft > 0 && _t >= _nextKey)
            {
                _keys.Trigger(_r.Range(2200, 3600), 1.2, _r.Range(0.12f, 0.25f), 5);
                _thock.Trigger(_r.Range(140, 200), _r.Range(0.02f, 0.05f), 0.02);
                _keysLeft--; _nextKey = _t + Fs * _r.Range(0.06f, 0.24f);
            }
            b[i] = hum + mur + _hiss.Process(w) * 0.004f + _keys.Next() + _thock.Next();
        }
    }
}

sealed class FireLayer : AmbLayer
{
    readonly Rng _r = new(53); readonly Pink _p = new();
    readonly Biquad _rumble = Biquad.Low(170), _body = Biquad.Low(1100), _flick = Biquad.Low(3);
    readonly Bursts _crackle = new(14, 91), _pops = new(4, 93);
    protected override void Fill(float[] b, int n)
    {
        for (int i = 0; i < n; i++)
        {
            float w = _r.Next(), p = _p.Next(w);
            float flick = Math.Clamp(0.7f + _flick.Process(_r.Next()) * 9f, 0.25f, 1.3f);
            float v = _rumble.Process(p) * 1.5f * flick + _body.Process(p) * 0.35f * flick;
            float u = _r.Uni();
            if (u < 22f / Fs) _crackle.Trigger(_r.Range(1500, 6500), 1.4, _r.Range(0.1f, 0.45f), _r.Range(1.5f, 7f));
            else if (u > 1f - 1.6f / Fs) _pops.Trigger(_r.Range(500, 1200), 0.9, _r.Range(0.3f, 0.7f), _r.Range(8f, 18f));
            b[i] = v + _crackle.Next() + _pops.Next();
        }
    }
}

sealed class StreamLayer : AmbLayer
{
    readonly Rng _r = new(61); readonly Pink _p = new();
    readonly Biquad _base = Biquad.Band(1900, 0.45), _low = Biquad.Low(520);
    readonly Biquad[] _bub = { Biquad.Band(900, 5), Biquad.Band(1500, 5), Biquad.Band(2300, 5) };
    readonly Biquad[] _wob = { Biquad.Low(9), Biquad.Low(13), Biquad.Low(17) };
    readonly float[] _amp = new float[3]; int _k;
    protected override void Fill(float[] b, int n)
    {
        for (int i = 0; i < n; i++)
        {
            float w = _r.Next(), p = _p.Next(w);
            float v = _base.Process(p) * 0.55f + _low.Process(p) * 0.3f;
            if ((++_k & 31) == 0)
                for (int j = 0; j < 3; j++)
                {
                    float m = _wob[j].Process(_r.Next() * 40f);       // random-walk lento
                    _bub[j].SetBand(Math.Clamp(700 + j * 700 + m * 900, 400, 4200), 6);
                    _amp[j] = Math.Clamp(0.5f + m * 0.8f, 0.1f, 1.3f);
                }
            float bw = _r.Next();
            for (int j = 0; j < 3; j++) v += _bub[j].Process(bw) * _amp[j] * 0.22f;
            b[i] = v;
        }
    }
}

sealed class WavesLayer : AmbLayer
{
    readonly Rng _r = new(71); readonly Pink _p = new();
    readonly Biquad _lp = Biquad.Low(600), _foam = Biquad.High(3800);
    double _ph1, _ph2 = 0.37; int _k; float _cut = 600, _gain = 0.2f, _foamG;
    protected override void Fill(float[] b, int n)
    {
        for (int i = 0; i < n; i++)
        {
            _ph1 += 1.0 / (Fs * 9.3); _ph2 += 1.0 / (Fs * 13.7);
            if (_ph1 >= 1) _ph1 -= 1; if (_ph2 >= 1) _ph2 -= 1;
            if ((++_k & 63) == 0)
            {
                double e1 = Math.Pow(0.5 - 0.5 * Math.Cos(2 * Math.PI * _ph1), 1.7), e2 = Math.Pow(0.5 - 0.5 * Math.Cos(2 * Math.PI * _ph2), 1.7);
                double e = Math.Min(1, e1 + 0.55 * e2);
                _lp.SetLow(260 + 2300 * e, 0.8);
                _gain = (float)(0.1 + 0.75 * e); _foamG = (float)(0.2 * e * e * e);
            }
            float w = _r.Next(), p = _p.Next(w);
            b[i] = _lp.Process(p) * _gain * 1.4f + _foam.Process(w) * _foamG * 0.25f;
        }
    }
}

sealed class LofiLayer : AmbLayer
{
    const double Bpm = 76;
    readonly double _stepLen = Fs * 60.0 / Bpm / 4;
    readonly Rng _r = new(83);
    // acordes (MIDI) e baixos: Dm9 – G13 – Cmaj9 – Am9
    static readonly int[][] Chords = { new[] { 53, 57, 60, 64 }, new[] { 53, 59, 62, 64 }, new[] { 52, 55, 59, 62 }, new[] { 55, 59, 60, 64 } };
    static readonly int[] Bass = { 38, 43, 36, 45 };
    static readonly float[] HatVel = { .55f, .22f, .38f, .22f, .5f, .22f, .38f, .22f, .55f, .22f, .38f, .22f, .5f, .22f, .42f, .3f };

    readonly double[] _vPh = new double[16], _vF = new double[16], _vAmp = new double[16], _vDec = new double[16]; readonly int[] _vAge = new int[16];
    readonly Biquad _keysLp = Biquad.Low(2400, 0.6), _hat = Biquad.High(7500), _snare = Biquad.Band(1900, 0.8), _crack = Biquad.High(2500);
    double _kPh, _kF, _kEnv, _sEnv, _sPh, _hEnv, _bPh, _bF, _bEnv, _lfo;
    readonly List<(double at, int kind, int a, float v)> _queue = new();
    double _t, _nextStep; int _step, _bar;

    static double Hz(int midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);

    void Note(double hz, float vel)
    {
        int k = 0, max = -1; for (int i = 0; i < 16; i++) if (_vAge[i] > max) { max = _vAge[i]; k = i; }
        _vPh[k] = 0; _vF[k] = hz; _vAmp[k] = vel; _vDec[k] = Math.Exp(-1.0 / (Fs * 1.1)); _vAge[k] = 0;
    }

    void Schedule(double at, int kind, int a, float v) => _queue.Add((at, kind, a, v));

    void StepEvents()
    {
        int s = _step, ch = _bar & 3;
        double swing = (s & 1) == 1 ? _stepLen * 0.16 : 0, at = _t + swing;
        if (s == 0 || s == 6 || s == 10)
        {
            float vel = s == 0 ? 0.13f : s == 6 ? 0.08f : 0.095f;
            double off = 0;
            foreach (var m in Chords[ch]) { Schedule(at + off, 0, m, vel * _r.Range(0.8f, 1.1f)); off += Fs * 0.011; }
        }
        if (s == 0 || s == 8 && (_bar & 1) == 1) Schedule(at, 1, Bass[ch], 1f);
        if (s == 0 || s == 7 || (s == 10 && (_bar & 1) == 0)) Schedule(at, 2, 0, 1f);
        if (s == 4 || s == 12) Schedule(at, 3, 0, _r.Range(0.8f, 1f));
        Schedule(at, 4, 0, HatVel[s] * _r.Range(0.7f, 1f));
    }

    void Fire(int kind, int a, float v)
    {
        switch (kind)
        {
            case 0: Note(Hz(a), v); break;
            case 1: _bF = Hz(a); _bEnv = 0.2; _bPh = 0; break;
            case 2: _kEnv = 1; _kF = 130; _kPh = 0; break;
            case 3: _sEnv = v; _sPh = 0; break;
            case 4: _hEnv = Math.Max(_hEnv, v); break;
        }
    }

    protected override void Fill(float[] b, int n)
    {
        for (int i = 0; i < n; i++, _t++)
        {
            if (_t >= _nextStep) { StepEvents(); _nextStep += _stepLen; _step = (_step + 1) & 15; if (_step == 0) _bar++; }
            for (int q = _queue.Count - 1; q >= 0; q--) if (_t >= _queue[q].at) { Fire(_queue[q].kind, _queue[q].a, _queue[q].v); _queue.RemoveAt(q); }

            _lfo += 2 * Math.PI * 0.55 / Fs;
            double vib = 1 + 0.0016 * Math.Sin(_lfo);
            double keys = 0;
            for (int k = 0; k < 16; k++)
            {
                if (_vAmp[k] < 1e-4) continue;
                double a = _vAmp[k], atk = Math.Min(1, _vAge[k] / (Fs * 0.006)), p = _vPh[k];
                keys += (Math.Sin(p) + 0.3 * Math.Sin(2 * p) * (a * 9) + 0.1 * Math.Sin(3 * p) * (a * a * 70)) * a * atk;
                _vPh[k] += 2 * Math.PI * _vF[k] * vib / Fs; _vAmp[k] *= _vDec[k]; _vAge[k]++;
            }
            float mix = _keysLp.Process((float)keys) * 1.2f;

            if (_bEnv > 1e-4) { mix += (float)(Math.Sin(_bPh) * _bEnv); _bPh += 2 * Math.PI * _bF / Fs; _bEnv *= 0.99992; }
            if (_kEnv > 1e-4) { _kF = Math.Max(48, _kF * 0.9996); mix += (float)(Math.Sin(_kPh) * _kEnv * 0.55); _kPh += 2 * Math.PI * _kF / Fs; _kEnv *= 0.99985; }
            float w = _r.Next();
            if (_sEnv > 1e-4) { mix += (float)(_snare.Process(w) * _sEnv * 0.26 + Math.Sin(_sPh) * _sEnv * 0.05); _sPh += 2 * Math.PI * 190 / Fs; _sEnv *= 0.99966; }
            if (_hEnv > 1e-4) { mix += _hat.Process(w) * (float)_hEnv * 0.07f; _hEnv *= 0.9988; }
            // chiado de vinil
            if (_r.Uni() < 6f / Fs) mix += _r.Next() * 0.05f;
            mix += _crack.Process(w) * 0.0025f;
            b[i] = mix;
        }
    }
}

/// <summary>Toca um arquivo do usuário em loop no lugar do som sintetizado.</summary>
sealed class FileLayer : AmbLayer
{
    readonly NAudio.Wave.AudioFileReader _rd; readonly ISampleProvider _src; readonly float[] _tmp = new float[8192];
    FileLayer(NAudio.Wave.AudioFileReader rd, ISampleProvider src) { _rd = rd; _src = src; }

    public static AmbLayer? TryCreate(string id)
    {
        try
        {
            foreach (var dir in new[] { Path.Combine(AppContext.BaseDirectory, "Sounds"), Path.Combine(Settings.DataDir, "Sounds") })
                foreach (var ext in new[] { ".mp3", ".wav", ".ogg", ".flac", ".m4a" })
                {
                    var f = Path.Combine(dir, id + ext);
                    if (!File.Exists(f)) continue;
                    var rd = new NAudio.Wave.AudioFileReader(f);
                    ISampleProvider s = rd;
                    if (s.WaveFormat.Channels == 2) s = new NAudio.Wave.SampleProviders.StereoToMonoSampleProvider(s) { LeftVolume = 0.5f, RightVolume = 0.5f };
                    if (s.WaveFormat.SampleRate != Fs) s = new NAudio.Wave.SampleProviders.WdlResamplingSampleProvider(s, Fs);
                    return new FileLayer(rd, s);
                }
        }
        catch (Exception e) { Log.Write(e); }
        return null;
    }

    protected override void Fill(float[] b, int n)
    {
        int done = 0;
        while (done < n)
        {
            int want = Math.Min(n - done, _tmp.Length);
            int got = _src.Read(_tmp, 0, want);
            if (got <= 0) { _rd.Position = 0; if (--want < 0) break; got = _src.Read(_tmp, 0, want); if (got <= 0) { Array.Clear(b, done, n - done); return; } }
            Array.Copy(_tmp, 0, b, done, got);
            done += got;
        }
    }
}

/// <summary>Mistura as camadas, aplica volume geral com rampa e o fade-out do timer de sono. Saída estéreo 44,1 kHz.</summary>
public sealed class AmbienceMixer : ISampleProvider
{
    public static readonly (string Id, string Name)[] Sounds =
    {
        ("rain", "Chuva"), ("forest", "Floresta"), ("cafe", "Cafeteria"), ("office", "Escritório"),
        ("lofi", "Lo-fi"), ("fire", "Fogueira"), ("stream", "Riacho"), ("waves", "Ondas")
    };

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(AmbLayer.Fs, 2);
    public readonly Dictionary<string, AmbLayer> Layers = new();
    public volatile float Master = 0.7f;
    public volatile float FadeTarget = 1f;        // 1 = normal; 0 = sumindo (timer de sono)
    float _master, _fade = 1f;
    float[] _acc = new float[4096], _tmp = new float[4096];

    // Ganho de normalização medido por camada (cada som sai com energia parecida)
    static readonly Dictionary<string, float> Norm = new()
    {
        ["rain"] = 0.62f, ["forest"] = 0.56f, ["cafe"] = 0.84f, ["office"] = 0.46f, ["lofi"] = 0.88f, ["fire"] = 0.37f, ["stream"] = 0.86f, ["waves"] = 0.45f
    };

    public AmbienceMixer()
    {
        _master = Master;
        foreach (var (id, _) in Sounds)
            Layers[id] = FileLayer.TryCreate(id) ?? id switch
            {
                "rain" => new RainLayer(), "forest" => new ForestLayer(), "cafe" => new CafeLayer(), "office" => new OfficeLayer(),
                "lofi" => new LofiLayer(), "fire" => new FireLayer(), "stream" => new StreamLayer(), _ => new WavesLayer()
            };
    }

    public bool AnyAudible => Layers.Values.Any(l => l.Audible);
    public bool FadeDone => _fade < 0.002f;

    public int Read(float[] buffer, int offset, int count)
    {
        int frames = count / 2;
        if (_acc.Length < frames) { _acc = new float[frames]; _tmp = new float[frames]; }
        Array.Clear(_acc, 0, frames);
        foreach (var kv in Layers) if (kv.Value.Audible) kv.Value.MixInto(_acc, _tmp, frames, Norm.GetValueOrDefault(kv.Key, 1f));

        float mt = Master, ft = FadeTarget;
        const float mRate = 1f / (AmbLayer.Fs * 0.12f), fRate = 1f / (AmbLayer.Fs * 12f);
        for (int i = 0; i < frames; i++)
        {
            float d = mt - _master; _master += d > mRate ? mRate : d < -mRate ? -mRate : d;
            float e = ft - _fade; _fade += e > fRate ? fRate : e < -fRate ? -fRate : e;
            float x = _acc[i] * _master * _fade;
            x = x / (1f + 0.6f * Math.Abs(x));          // limitador suave (nunca estoura)
            buffer[offset + i * 2] = x; buffer[offset + i * 2 + 1] = x;
        }
        return frames * 2;
    }
}
