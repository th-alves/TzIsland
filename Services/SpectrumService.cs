using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;

namespace TzIsland;

/// <summary>
/// Espectro do áudio que está saindo no PC (captura "loopback" do WASAPI + FFT). Só captura enquanto existir
/// pelo menos um consumidor (Acquire/Release), então não gasta CPU com a ilha recolhida e sem música.
/// </summary>
public sealed class SpectrumService
{
    public const int Bands = 7;
    const int N = 1024, M = 10;

    /// <summary>Nível 0…1 por faixa (grave → agudo). Escrito pela thread de áudio, lido pela interface.</summary>
    public readonly float[] Levels = new float[Bands];
    /// <summary>true quando a captura está funcionando; senão a interface usa só uma animação decorativa.</summary>
    public volatile bool Live;
    /// <summary>Momento (Stopwatch) do último bloco recebido. Sem som, o Windows simplesmente não entrega blocos.</summary>
    public long LastPacket;

    readonly object _lock = new();
    readonly float[] _frame = new float[N], _win = new float[N];
    readonly Complex[] _fft = new Complex[N];
    int[] _edges = Array.Empty<int>();
    int _fill, _refs;
    WasapiLoopbackCapture? _cap;

    public SpectrumService()
    {
        for (int i = 0; i < N; i++) _win[i] = (float)(0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (N - 1)));
    }

    public void Acquire() { lock (_lock) { if (++_refs == 1) Start(); } }
    public void Release() { lock (_lock) { if (_refs > 0 && --_refs == 0) Stop(); } }

    void Start()
    {
        try
        {
            _cap = new WasapiLoopbackCapture();
            var wf = _cap.WaveFormat;
            _edges = new int[Bands + 1];
            double lo = 60, hi = Math.Min(12000, wf.SampleRate * 0.45);
            for (int i = 0; i <= Bands; i++)
                _edges[i] = Math.Clamp((int)Math.Round(lo * Math.Pow(hi / lo, i / (double)Bands) / wf.SampleRate * N), 1, N / 2 - 1);
            for (int i = 1; i <= Bands; i++) if (_edges[i] <= _edges[i - 1]) _edges[i] = _edges[i - 1] + 1;
            _cap.DataAvailable += OnData;
            _cap.RecordingStopped += OnStopped;
            _fill = 0;
            _cap.StartRecording();
            Live = true;
        }
        catch (Exception e) { Log.Write(e); Live = false; try { _cap?.Dispose(); } catch { } _cap = null; }
    }

    void Stop()
    {
        var c = _cap; _cap = null; Live = false;
        if (c == null) return;
        try { c.DataAvailable -= OnData; c.RecordingStopped -= OnStopped; c.StopRecording(); c.Dispose(); } catch { }
        Array.Clear(Levels, 0, Bands);
    }

    void OnStopped(object? s, StoppedEventArgs e)
    {
        // dispositivo padrão mudou / foi removido: tenta de novo em 2 s, se alguém ainda precisa do espectro
        Task.Run(async () =>
        {
            await Task.Delay(2000);
            lock (_lock) { if (_refs > 0 && ReferenceEquals(_cap, s)) { var c = _cap; _cap = null; try { c?.Dispose(); } catch { } Start(); } }
        });
    }

    void OnData(object? sender, WaveInEventArgs e)
    {
        var wf = ((WasapiLoopbackCapture)sender!).WaveFormat;
        int ch = Math.Max(1, wf.Channels), bytes = wf.BitsPerSample / 8, frameBytes = ch * bytes;
        bool isFloat = wf.BitsPerSample == 32 && (wf.Encoding == WaveFormatEncoding.IeeeFloat || wf.Encoding == WaveFormatEncoding.Extensible);
        LastPacket = Stopwatch.GetTimestamp();
        for (int off = 0; off + frameBytes <= e.BytesRecorded; off += frameBytes)
        {
            float sum = 0;
            for (int c = 0; c < ch; c++)
            {
                int o = off + c * bytes;
                sum += isFloat ? BitConverter.ToSingle(e.Buffer, o) : wf.BitsPerSample == 16 ? BitConverter.ToInt16(e.Buffer, o) / 32768f : 0f;
            }
            _frame[_fill++] = sum / ch;
            if (_fill == N) { Analyze(); _fill = 0; }
        }
    }

    void Analyze()
    {
        for (int i = 0; i < N; i++) { _fft[i].X = _frame[i] * _win[i]; _fft[i].Y = 0; }
        FastFourierTransform.FFT(true, M, _fft);
        for (int b = 0; b < Bands; b++)
        {
            int a = _edges[b], z = _edges[b + 1];
            double acc = 0;
            for (int k = a; k < z; k++) acc += Math.Sqrt(_fft[k].X * _fft[k].X + _fft[k].Y * _fft[k].Y);
            double mag = acc / Math.Max(1, z - a) * 2;                       // amplitude aproximada da faixa
            double db = 20 * Math.Log10(mag + 1e-7) + b * 2.2;                // compensa a queda natural dos agudos
            Levels[b] = (float)Math.Clamp((db + 64) / 40, 0, 1);
        }
    }
}
