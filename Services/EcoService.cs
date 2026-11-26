namespace TzIsland;

/// <summary>
/// Modo economia automático: a ilha é sempre-ligada e o tique é fixo. Em bateria fraca (ou com a
/// economia de bateria do Windows ligada) o app espaça o tique, desliga espectro/HUD e suspende
/// clima/calendário até voltar à tomada — sem tocar em nenhuma animação visível.
/// </summary>
public static class Eco
{
    public static bool Active { get; private set; }
    public static string Reason { get; private set; } = "";
    public static event Action? Changed;

    static readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromMinutes(1) };
    static bool _started;

    public static void Start()
    {
        if (_started) return;
        _started = true;
        try { Microsoft.Win32.SystemEvents.PowerModeChanged += (_, _) => Application.Current?.Dispatcher.InvokeAsync(Evaluate); } catch { }
        _t.Tick += (_, _) => Evaluate();
        _t.Start();
        Evaluate();
    }

    public static void Evaluate()
    {
        bool on = false;
        string why = "";
        try
        {
            var ps = System.Windows.Forms.SystemInformation.PowerStatus;
            bool battery = ps.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Offline;
            float level = ps.BatteryLifePercent;         // 0–1; 1 também significa "desconhecido"
            bool saver = BatterySaverOn();
            if (battery && level > 0 && level < 0.20f) { on = true; why = $"bateria {level * 100:0}%"; }
            else if (saver) { on = true; why = "economia de bateria do Windows"; }
        }
        catch (Exception e) { Log.Write(e); }

        if (!Settings.Current.AutoEco) { on = false; why = ""; }
        if (on == Active && why == Reason) return;
        Active = on;
        Reason = why;

        if (on) { Svc.Weather.Suspend(); Svc.Calendar.Suspend(); }
        else { Svc.Weather.Resume(); Svc.Calendar.Resume(); }
        Log.Write(on ? "economia: ligada (" + why + ")" : "economia: desligada");
        Changed?.Invoke();
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct PowerStatusNative
    {
        public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public uint BatteryLifeTime, BatteryFullLifeTime;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    static extern bool GetSystemPowerStatus(out PowerStatusNative status);

    /// <summary>Economia de bateria do Windows (PowerStatus.SystemStatusFlag não existe no .NET moderno).</summary>
    static bool BatterySaverOn()
    {
        try { return GetSystemPowerStatus(out var s) && s.SystemStatusFlag == 1; }
        catch { return false; }
    }
}
