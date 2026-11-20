using System.Text.Json.Serialization;

namespace TzIsland;

public enum IslandStyle { Classic, Floating }
public enum IslandLook { LiquidGlass, DarkGlass, SolidBlack }
public enum IslandAnchor { TopCenter, TopLeft, TopRight, Custom }
public enum FullscreenMode { Hide, NoExpand, Keep }

public class ShortcutItem
{
    public string Name { get; set; } = "";
    public string Command { get; set; } = "";
    public string Args { get; set; } = "";
}

public class EndpointItem
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}

/// <summary>Todas as preferências do usuário. Persistidas em %AppData%\TzIsland\settings.json.</summary>
public class AppSettings
{
    // Aparência
    public IslandStyle Style { get; set; } = IslandStyle.Classic;
    public IslandLook Look { get; set; } = IslandLook.LiquidGlass;
    public double ExpandedWidth { get; set; } = 680;
    public double ExpandedHeight { get; set; } = 340;
    public double CollapsedWidth { get; set; } = 210;
    public double CollapsedHeight { get; set; } = 36;
    public double Opacity { get; set; } = 0.85;     // transparência do vidro (0.3 a 1)
    public double BlurAmount { get; set; } = 36;    // desfoque do vidro (0 desliga)
    public bool Animations { get; set; } = true;
    public bool MediaBlurBackground { get; set; } = true;

    // Posição
    public IslandAnchor Anchor { get; set; } = IslandAnchor.TopCenter;
    public double CustomX { get; set; }             // centro X absoluto, em pixels
    public double CustomY { get; set; }             // topo, em pixels
    public int Monitor { get; set; }

    // Comportamento
    public bool ExpandOnHover { get; set; } = true;
    public bool ExpandOnClick { get; set; } = true;
    public bool AllowDrag { get; set; } = true;
    public int HoverDelayMs { get; set; } = 140;
    public double AutoCollapseSeconds { get; set; } = 1.6;
    public FullscreenMode Fullscreen { get; set; } = FullscreenMode.Hide;
    public bool StartWithWindows { get; set; }
    public bool ShowTrayIcon { get; set; } = true;
    public Dictionary<string, bool> Modules { get; set; } = new();

    // Notificações
    public bool BannerEnabled { get; set; } = true;
    public double BannerSeconds { get; set; } = 4;
    public bool ListenWindowsNotifications { get; set; } = true;
    public bool NotificationPreview { get; set; } = true;

    // Foco
    public int FocusMinutes { get; set; } = 25;
    public int ShortBreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;
    public int SessionsBeforeLongBreak { get; set; } = 4;
    public int TimerMinutes { get; set; } = 10;
    public bool AutoStartNextPhase { get; set; }

    // Clima / agenda / integrações
    public string City { get; set; } = "São Paulo";
    public double Latitude { get; set; } = -23.55;
    public double Longitude { get; set; } = -46.63;
    public List<string> IcsUrls { get; set; } = new();
    public List<EndpointItem> Endpoints { get; set; } = new();
    public List<ShortcutItem> Shortcuts { get; set; } = new()
    {
        new() { Name = "Configurações", Command = "ms-settings:" },
        new() { Name = "Gerenciador de Tarefas", Command = "taskmgr" },
        new() { Name = "Calculadora", Command = "calc" },
        new() { Name = "Recorte de Tela", Command = "ms-screenclip:" },
        new() { Name = "Bloquear PC", Command = "rundll32.exe", Args = "user32.dll,LockWorkStation" },
        new() { Name = "Explorador", Command = "explorer.exe" },
    };

    // Arquivos
    public bool WatchDownloads { get; set; } = true;
    public bool WatchScreenshots { get; set; } = true;
    public string DownloadsPath { get; set; } = "";
    public string ScreenshotsPath { get; set; } = "";

    // Privacidade / voz / atalhos
    public bool MonitorMicrophone { get; set; } = true;
    public string DictationLanguage { get; set; } = "pt-BR";
    public string DictationHotkey { get; set; } = "Ctrl+Alt+D";
    public string ToggleHotkey { get; set; } = "Ctrl+Alt+Space";
    public string LastCameraId { get; set; } = "";

    public bool IsOn(string module) => !Modules.TryGetValue(module, out var v) || v;
}

public static class Settings
{
    public static AppSettings Current { get; private set; } = new();
    public static event Action? Changed;
    public static string DataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TzIsland");

    public static readonly JsonSerializerOptions JsonOpt = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    static string FilePath => Path.Combine(DataDir, "settings.json");
    static System.Threading.Timer? _timer;

    public static void Load()
    {
        Directory.CreateDirectory(DataDir);
        try
        {
            if (File.Exists(FilePath))
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOpt) ?? new();
        }
        catch (Exception e) { Log.Write(e); }
        if (string.IsNullOrWhiteSpace(Current.DownloadsPath))
            Current.DownloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (string.IsNullOrWhiteSpace(Current.ScreenshotsPath))
            Current.ScreenshotsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");
    }

    /// <summary>Chame depois de alterar qualquer valor: salva (com debounce) e avisa a interface.</summary>
    public static void Apply()
    {
        Changed?.Invoke();
        _timer?.Dispose();
        _timer = new System.Threading.Timer(_ =>
        {
            try { File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, JsonOpt)); }
            catch (Exception e) { Log.Write(e); }
        }, null, 400, System.Threading.Timeout.Infinite);
    }

    public static void Reset()
    {
        Current = new();
        Load();
        Apply();
    }

    public static void WipeData()
    {
        foreach (var n in new[] { "notes", "tasks", "events", "tray" })
        {
            try { File.Delete(Path.Combine(DataDir, n + ".json")); } catch { }
        }
    }
}

/// <summary>Persistência simples em JSON, com gravação adiada.</summary>
public sealed class JsonStore<T> where T : new()
{
    readonly string _path;
    readonly object _lock = new();
    System.Threading.Timer? _timer;
    string _pending = "";
    public T Data { get; private set; }

    public JsonStore(string name)
    {
        _path = Path.Combine(Settings.DataDir, name + ".json");
        Data = new T();
        try
        {
            if (File.Exists(_path))
                Data = JsonSerializer.Deserialize<T>(File.ReadAllText(_path), Settings.JsonOpt) ?? new T();
        }
        catch (Exception e) { Log.Write(e); }
    }

    public void Save()
    {
        lock (_lock)
        {
            _pending = JsonSerializer.Serialize(Data, Settings.JsonOpt);
            _timer?.Dispose();
            _timer = new System.Threading.Timer(_ =>
            {
                try { lock (_lock) File.WriteAllText(_path, _pending); }
                catch (Exception e) { Log.Write(e); }
            }, null, 500, System.Threading.Timeout.Infinite);
        }
    }
}

public static class Log
{
    public static void Write(object? o)
    {
        try { File.AppendAllText(Path.Combine(Settings.DataDir, "log.txt"), $"[{DateTime.Now:s}] {o}\n"); }
        catch { }
    }
}

public record ModuleInfo(string Id, string Glyph, string Title);

public static class Modules
{
    public static readonly ModuleInfo[] All =
    {
        new("home", "\uE80F", "Início"),
        new("media", "\uE8D6", "Mídia"),
        new("inbox", "\uE715", "Inbox"),
        new("productivity", "\uE70F", "Produtividade"),
        new("system", "\uE772", "Sistema"),
        new("files", "\uE8B7", "Arquivos"),
        new("ai", "AI", "Agentes de IA"),
        new("mirror", "\uE722", "Mirror"),
        new("dictation", "\uE720", "Ditado"),
    };
}

public class InboxItem
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string Source { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime Time { get; set; } = DateTime.Now;
    public bool Important { get; set; }
}

/// <summary>Barramento central: qualquer módulo publica notificações e eventos de agentes aqui.</summary>
public static class Hub
{
    public static ObservableCollection<InboxItem> Inbox { get; } = new();
    public static event Action<InboxItem>? Banner;
    public static event Action<AgentEvent>? AgentEventRaised;

    public static void Notify(string source, string title, string text, bool important = false, bool banner = true)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var it = new InboxItem { Source = source, Title = title, Text = text, Important = important };
            Inbox.Insert(0, it);
            while (Inbox.Count > 200) Inbox.RemoveAt(Inbox.Count - 1);
            if (banner && Settings.Current.BannerEnabled) Banner?.Invoke(it);
        });
    }

    public static void HandleIpc(IpcMessage m)
    {
        switch (m.Type)
        {
            case "notify":
                Notify("Mensagem", m.Title ?? "", m.Text ?? "");
                break;
            case "agent":
                if (!string.IsNullOrWhiteSpace(m.Agent))
                    AgentEventRaised?.Invoke(new AgentEvent(m.Agent!.ToLowerInvariant(), m.State, m.Text, m.Progress, m.UsageSession, m.UsageWeekly));
                break;
        }
    }
}
