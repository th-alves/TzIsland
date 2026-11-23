namespace TzIsland;

/// <summary>
/// Sons sutis de abrir/fechar, sintetizados em código (sem arquivos, sem direitos autorais).
/// Um "toque" curto e grave com um estalo bem leve no começo, para dar sensação de peso. Opcional (Configurações → Efeitos).
/// </summary>
public static class SoundFx
{
    const int Rate = 44100;
    static byte[]? _open, _close;
    static double _vol = -1;
    static System.Media.SoundPlayer? _player;
    static readonly object Lock = new();

    public static void Open() => Play(true);
    public static void Close() => Play(false);

    static void Play(bool open)
    {
        if (!Settings.Current.SoundsEnabled) return;
        try
        {
            lock (Lock)
            {
                double v = Math.Clamp(Settings.Current.SoundVolume, 0.05, 1);
                if (_open == null || Math.Abs(v - _vol) > 0.001) { _vol = v; _open = Make(true, v); _close = Make(false, v); }
                _player?.Stop(); _player?.Dispose();
                _player = new System.Media.SoundPlayer(new MemoryStream(open ? _open! : _close!));
                _player.Play();       // assíncrono: não trava a animação
            }
        }
        catch (Exception e) { Log.Write(e); }
    }

    static byte[] Make(bool open, double volume)
    {
        // abrir: sobe de leve (210→300 Hz) e dura um pouco mais · fechar: desce (280→170 Hz), mais curto e macio
        double f0 = open ? 210 : 280, f1 = open ? 300 : 170, tau = open ? 0.055 : 0.042, len = open ? 0.20 : 0.16, gain = (open ? 0.9 : 0.75) * volume * 0.6;
        int n = (int)(Rate * len);
        var pcm = new short[n];
        var rnd = new Random(open ? 7 : 11);
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)Rate, k = t / len;
            double f = f0 + (f1 - f0) * (1 - Math.Exp(-k * 5));
            phase += 2 * Math.PI * f / Rate;
            double env = Math.Min(1, t / 0.003) * Math.Exp(-t / tau);                     // ataque de 3 ms (sem clique) + queda exponencial
            double s = Math.Sin(phase) + 0.42 * Math.Sin(2 * phase) + 0.16 * Math.Sin(3 * phase);   // harmônicas: audível até em alto-falante de notebook
            double tick = t < 0.006 ? (rnd.NextDouble() * 2 - 1) * (1 - t / 0.006) * 0.22 : 0;      // estalo bem leve
            pcm[i] = (short)Math.Clamp((s * env * 0.6 + tick) * gain * 32767, -32767, 32767);
        }
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.ASCII, true))
        {
            w.Write("RIFF".ToCharArray()); w.Write(36 + n * 2); w.Write("WAVE".ToCharArray());
            w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write("data".ToCharArray()); w.Write(n * 2);
            foreach (var s in pcm) w.Write(s);
        }
        return ms.ToArray();
    }
}
