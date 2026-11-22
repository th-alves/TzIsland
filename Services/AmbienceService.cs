using NAudio.Wave;

namespace TzIsland;

/// <summary>
/// Ambientação: liga/desliga cada som, ajusta o volume de cada camada e o volume geral, e fecha tudo sozinho
/// depois de N minutos (fade-out de ~12 s). A saída de áudio só existe enquanto há algo tocando.
/// </summary>
public sealed class AmbienceService
{
    readonly AmbienceMixer _mix = new();
    WaveOutEvent? _out;
    readonly HashSet<string> _on = new();
    readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromSeconds(1) };
    DateTime? _fadeAt;
    bool _fading;

    public event Action? Changed;

    static AppSettings S => Settings.Current;

    public AmbienceService()
    {
        _t.Tick += (_, _) => Tick();
    }

    public IReadOnlyCollection<string> Playing => _on;
    public bool IsOn(string id) => _on.Contains(id);

    public double Level(string id) => S.AmbienceLevels.TryGetValue(id, out var v) ? Math.Clamp(v, 0, 1) : 0.6;

    public double Volume
    {
        get => Math.Clamp(S.AmbienceVolume, 0, 1);
        set { S.AmbienceVolume = Math.Clamp(value, 0, 1); _mix.Master = (float)S.AmbienceVolume; Settings.Save(); }
    }

    public int FadeMinutes
    {
        get => S.AmbienceFadeMinutes;
        set { S.AmbienceFadeMinutes = value; Settings.Save(); Rearm(); Changed?.Invoke(); }
    }

    /// <summary>Segundos que faltam para o fade-out, ou null se não houver timer armado.</summary>
    public double? SecondsToFade => _fadeAt == null ? null : Math.Max(0, (_fadeAt.Value - DateTime.Now).TotalSeconds);

    public void Toggle(string id)
    {
        if (_on.Remove(id)) { _mix.Layers[id].Target = 0; }
        else
        {
            if (_fading) CancelFade();
            _on.Add(id); _mix.Layers[id].Target = (float)Level(id);
            EnsureOutput();
        }
        if (_on.Count > 0 && _fadeAt == null) Rearm();
        if (_on.Count == 0) _fadeAt = null;
        Changed?.Invoke();
    }

    public void SetLevel(string id, double v)
    {
        v = Math.Clamp(v, 0, 1);
        S.AmbienceLevels[id] = v; Settings.Save();
        if (_on.Contains(id)) _mix.Layers[id].Target = (float)v;
    }

    public void StopAll()
    {
        foreach (var id in _on.ToList()) _mix.Layers[id].Target = 0;
        _on.Clear(); _fadeAt = null; CancelFade();
        Changed?.Invoke();
    }

    void Rearm()
    {
        _fadeAt = S.AmbienceFadeMinutes > 0 && _on.Count > 0 ? DateTime.Now.AddMinutes(S.AmbienceFadeMinutes) : null;
    }

    void CancelFade() { _fading = false; _mix.FadeTarget = 1f; }

    void EnsureOutput()
    {
        _mix.Master = (float)Volume;
        if (_out == null)
        {
            try
            {
                _out = new WaveOutEvent { DesiredLatency = 140, NumberOfBuffers = 3 };
                _out.Init(new NAudio.Wave.SampleProviders.SampleToWaveProvider16(_mix));
                _out.Play();
            }
            catch (Exception e) { Log.Write(e); _out?.Dispose(); _out = null; }
        }
        else if (_out.PlaybackState != PlaybackState.Playing) _out.Play();
        _t.Start();
    }

    void Tick()
    {
        if (_fadeAt != null && !_fading && DateTime.Now >= _fadeAt) { _fading = true; _mix.FadeTarget = 0f; }
        if (_fading && _mix.FadeDone) { StopAll(); }
        // nada tocando e tudo já silenciou (fade das camadas terminou): fecha o dispositivo de saída
        if (_on.Count == 0 && !_mix.AnyAudible) { CloseOutput(); _t.Stop(); }
        if (_fadeAt != null) Changed?.Invoke();       // atualiza a contagem regressiva na tela
    }

    void CloseOutput()
    {
        try { _out?.Stop(); _out?.Dispose(); } catch { }
        _out = null;
    }

    public void Shutdown() { _t.Stop(); CloseOutput(); }
}
