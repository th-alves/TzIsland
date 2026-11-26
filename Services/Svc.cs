namespace TzIsland;

/// <summary>Registro dos serviços (singletons). Cada módulo só inicia se estiver habilitado.</summary>
public static class Svc
{
    public static readonly MediaService Media = new();
    public static readonly BatteryService Battery = new();
    public static readonly AudioService Audio = new();
    public static readonly BrightnessService Brightness = new();
    public static readonly BluetoothService Bluetooth = new();
    public static readonly FileTrayService Files = new();
    public static readonly WindowsNotificationService WinNotifs = new();
    public static readonly WeatherService Weather = new();
    public static readonly CalendarService Calendar = new();
    public static readonly FocusService Focus = new();
    public static readonly WaterTracker Water = new();
    public static readonly NotesStore Notes = new();
    public static readonly TasksStore Tasks = new();
    public static readonly CameraService Camera = new();
    public static readonly DictationService Dictation = new();
    public static readonly AgentMonitor Agents = new();
    public static readonly SpectrumService Spectrum = new();
    public static readonly NetworkService Network = new();
    public static readonly AmbienceService Ambience = new();
    public static readonly DownloadMonitor Downloads = new();
    public static readonly UpdateService Updates = new();
    public static readonly LyricsService Lyrics = new();
    public static readonly ClipboardService Clipboard = new();
    public static readonly MixerService Mixer = new();

    public static void Start()
    {
        var s = Settings.Current;
        Battery.Start();
        Audio.Start();
        Files.Start();
        Weather.Start();
        Calendar.Start();
        Water.Start();
        Clipboard.Start();
        Updates.Start();
        Eco.Start();
        if (s.IsOn("media")) _ = Media.StartAsync();
        if (s.IsOn("inbox")) _ = WinNotifs.StartAsync();
        if (s.IsOn("ai")) Agents.Start();
    }

    public static async Task StopAsync()
    {
        try { Ambience.Shutdown(); await Camera.StopAsync(); await Dictation.StopAsync(); } catch { }
    }
}
