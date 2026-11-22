namespace TzIsland;

public enum FocusPhase { Focus, ShortBreak, LongBreak }
public enum LiveKind { None, Pomodoro, Timer, Stopwatch }

/// <summary>Informação mostrada na pílula recolhida enquanto algo roda em segundo plano.</summary>
public sealed record LiveInfo(LiveKind Kind, string Label, string Time, int Extra);

/// <summary>
/// Relógio baseado em timestamp monotônico: sem deriva, sem depender do intervalo do timer de UI.
/// </summary>
public abstract class ClockBase
{
    long _startTs;
    TimeSpan _accum;
    public bool Running { get; private set; }
    public DateTime StartedAt { get; private set; }

    public TimeSpan Elapsed => _accum + (Running
        ? TimeSpan.FromSeconds((Stopwatch.GetTimestamp() - _startTs) / (double)Stopwatch.Frequency)
        : TimeSpan.Zero);

    public bool Touched => Running || _accum > TimeSpan.Zero;

    public void Start() { if (Running) return; _startTs = Stopwatch.GetTimestamp(); StartedAt = DateTime.UtcNow; Running = true; }
    public void Pause() { if (!Running) return; _accum = Elapsed; Running = false; }
    public void Clear() { _accum = TimeSpan.Zero; Running = false; }
}

public sealed class PomodoroClock : ClockBase
{
    public FocusPhase Phase { get; set; } = FocusPhase.Focus;
    public int Completed { get; set; }

    public TimeSpan Total => TimeSpan.FromMinutes(Math.Max(1, Phase switch
    {
        FocusPhase.Focus => Settings.Current.FocusMinutes,
        FocusPhase.ShortBreak => Settings.Current.ShortBreakMinutes,
        _ => Settings.Current.LongBreakMinutes
    }));

    public TimeSpan Remaining { get { var r = Total - Elapsed; return r < TimeSpan.Zero ? TimeSpan.Zero : r; } }
    public double Progress => Math.Clamp(Elapsed.TotalSeconds / Total.TotalSeconds, 0, 1);
    public string PhaseLabel => Phase switch { FocusPhase.Focus => "Foco", FocusPhase.ShortBreak => "Pausa curta", _ => "Pausa longa" };
}

public sealed class CountdownClock : ClockBase
{
    public int Minutes => Math.Clamp(Settings.Current.TimerMinutes, 1, 180);
    public TimeSpan Total => TimeSpan.FromMinutes(Minutes);
    public TimeSpan Remaining { get { var r = Total - Elapsed; return r < TimeSpan.Zero ? TimeSpan.Zero : r; } }
    public double Progress => Math.Clamp(Elapsed.TotalSeconds / Total.TotalSeconds, 0, 1);
}

public sealed class StopwatchClock : ClockBase
{
    public List<TimeSpan> Laps { get; } = new();
}

/// <summary>
/// Pomodoro, Timer e Cronômetro independentes: os três podem rodar ao mesmo tempo.
/// A pílula recolhida mostra o que foi iniciado por último.
/// </summary>
public sealed class FocusService
{
    public PomodoroClock Pomo { get; } = new();
    public CountdownClock Timer { get; } = new();
    public StopwatchClock Watch { get; } = new();

    /// <summary>Disparado ~4x/s enquanto algo roda e a cada mudança de estado.</summary>
    public event Action? Tick;

    readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromMilliseconds(250) };

    public FocusService() { _t.Tick += OnTick; }

    public bool AnyRunning => Pomo.Running || Timer.Running || Watch.Running;

    // ---------- Pomodoro ----------
    public void PomoToggle() { if (Pomo.Running) Pomo.Pause(); else Pomo.Start(); Changed(); }
    public void PomoReset() { Pomo.Clear(); Changed(); }
    public void PomoSkip() { CompletePomo(false); }

    // ---------- Timer ----------
    public void TimerToggle() { if (Timer.Running) Timer.Pause(); else Timer.Start(); Changed(); }
    public void TimerReset() { Timer.Clear(); Changed(); }
    public void TimerSet(int minutes)
    {
        Settings.Current.TimerMinutes = Math.Clamp(minutes, 1, 180);
        Settings.Save();
        if (!Timer.Running) Timer.Clear();
        Changed();
    }

    // ---------- Cronômetro ----------
    public void WatchToggle() { if (Watch.Running) Watch.Pause(); else Watch.Start(); Changed(); }
    public void WatchReset() { Watch.Clear(); Watch.Laps.Clear(); Changed(); }
    public void WatchLap() { if (Watch.Running) { Watch.Laps.Add(Watch.Elapsed); Changed(); } }

    void Changed()
    {
        if (AnyRunning) { if (!_t.IsEnabled) _t.Start(); } else _t.Stop();
        Tick?.Invoke();
    }

    void OnTick(object? s, EventArgs e)
    {
        if (Pomo.Running && Pomo.Remaining <= TimeSpan.Zero) { CompletePomo(true); return; }
        if (Timer.Running && Timer.Remaining <= TimeSpan.Zero) { CompleteTimer(); return; }
        if (!AnyRunning) { _t.Stop(); }
        Tick?.Invoke();
    }

    void CompleteTimer()
    {
        System.Media.SystemSounds.Asterisk.Play();
        Hub.Notify("Timer", "Timer finalizado", $"{Timer.Minutes} min concluídos.", true);
        Timer.Clear();
        Changed();
    }

    void CompletePomo(bool notify)
    {
        var S = Settings.Current;
        bool wasFocus = Pomo.Phase == FocusPhase.Focus;
        if (wasFocus && notify) Pomo.Completed++;
        Pomo.Phase = wasFocus
            ? (Pomo.Completed > 0 && Pomo.Completed % Math.Max(1, S.SessionsBeforeLongBreak) == 0 ? FocusPhase.LongBreak : FocusPhase.ShortBreak)
            : FocusPhase.Focus;
        Pomo.Clear();
        if (notify)
        {
            System.Media.SystemSounds.Asterisk.Play();
            Hub.Notify("Foco", wasFocus ? "Sessão de foco concluída" : "Pausa terminou",
                wasFocus ? $"Hora de descansar ({Pomo.PhaseLabel.ToLowerInvariant()})." : "Vamos voltar ao foco.", true);
        }
        if (S.AutoStartNextPhase) Pomo.Start();
        Changed();
    }

    /// <summary>O que aparece na pílula: o relógio iniciado mais recentemente + quantos outros rodam.</summary>
    public LiveInfo? Primary()
    {
        var running = new List<(LiveKind k, DateTime at)>();
        if (Pomo.Running) running.Add((LiveKind.Pomodoro, Pomo.StartedAt));
        if (Timer.Running) running.Add((LiveKind.Timer, Timer.StartedAt));
        if (Watch.Running) running.Add((LiveKind.Stopwatch, Watch.StartedAt));
        if (running.Count == 0) return null;
        var top = running.OrderByDescending(r => r.at).First();
        return top.k switch
        {
            LiveKind.Pomodoro => new LiveInfo(top.k, Pomo.PhaseLabel, K.Fmt(Pomo.Remaining), running.Count - 1),
            LiveKind.Timer => new LiveInfo(top.k, "Timer", K.Fmt(Timer.Remaining), running.Count - 1),
            _ => new LiveInfo(top.k, "Cronômetro", K.Fmt(Watch.Elapsed), running.Count - 1)
        };
    }
}

public class WaterData
{
    public string Date { get; set; } = "";
    public int Glasses { get; set; }
    public int Goal { get; set; } = 8;
    public bool Reminders { get; set; } = true;
    public int ReminderMinutes { get; set; } = 60;
}

/// <summary>Contador de copos d'água do dia, com lembretes opcionais pela própria ilha.</summary>
public sealed class WaterTracker
{
    readonly JsonStore<WaterData> _s = new("water");
    readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromMinutes(1) };
    DateTime _lastRemind = DateTime.Now;
    public event Action? Changed;

    public int Goal => Math.Clamp(_s.Data.Goal, 1, 12);
    public bool Reminders => _s.Data.Reminders;
    public int ReminderMinutes => Math.Max(5, _s.Data.ReminderMinutes);

    public int Glasses
    {
        get
        {
            var today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (_s.Data.Date != today) { _s.Data.Date = today; _s.Data.Glasses = 0; _s.Save(); }
            return Math.Clamp(_s.Data.Glasses, 0, Goal);
        }
    }

    public void Set(int n)
    {
        _ = Glasses;
        _s.Data.Glasses = Math.Clamp(n, 0, Goal);
        _s.Save();
        _lastRemind = DateTime.Now;
        Changed?.Invoke();
    }

    public void SetReminders(bool on)
    {
        _s.Data.Reminders = on;
        _lastRemind = DateTime.Now;
        _s.Save();
        Changed?.Invoke();
    }

    public void Start()
    {
        _t.Tick += (_, _) =>
        {
            if (!Reminders || Glasses >= Goal) return;
            var now = DateTime.Now;
            if (now.Hour < 8 || now.Hour >= 22) return;
            if ((now - _lastRemind).TotalMinutes < Math.Max(15, _s.Data.ReminderMinutes)) return;
            _lastRemind = now;
            Hub.Notify("Água", "Hora de beber água", $"{Glasses} de {Goal} copos hoje.", false);
        };
        _t.Start();
    }
}
