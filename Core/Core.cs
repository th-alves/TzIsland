using System.Text.Json.Serialization;

namespace TzIsland;

public enum IslandStyle { Classic, Floating }
public enum IslandLook { LiquidGlass, DarkGlass, SolidBlack }
public enum IslandAnchor { TopCenter, TopLeft, TopRight, Custom }
public enum FullscreenMode { Hide, NoExpand, Keep }
public enum CollapseMode { Instant, Fast, Smooth }

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
    public IslandLook Look { get; set; } = IslandLook.SolidBlack;
    public double ExpandedWidth { get; set; } = 760;
    public double ExpandedHeight { get; set; } = 356;
    public double CollapsedWidth { get; set; } = 150;
    public double CollapsedHeight { get; set; } = 36;
    public double Opacity { get; set; } = 0.85;     // transparência do vidro (0.3 a 1)
    public double BlurAmount { get; set; } = 36;    // desfoque do vidro (0 desliga)
    public bool Animations { get; set; } = true;
    public IslandAnim IslandAnim { get; set; } = IslandAnim.Fluid;
    public bool MediaBlurBackground { get; set; } = true;

    // Posição
    public IslandAnchor Anchor { get; set; } = IslandAnchor.TopCenter;
    public double CustomX { get; set; }             // centro X absoluto, em pixels
    public double CustomY { get; set; }             // topo, em pixels
    public int Monitor { get; set; }

    // Comportamento
    // Padrão novo: só clique. O hover estava abrindo a ilha mesmo com "clicar" ligado e atrapalhava os gestos da ilha recolhida.
    public bool ExpandOnHover { get; set; } = false;
    public bool ExpandOnClick { get; set; } = true;
    /// <summary>Versão do arquivo de configurações (usada para migrar valores antigos uma única vez).</summary>
    public int SettingsVersion { get; set; }

    // Ilha recolhida: gestos, downloads e agenda
    public bool CollapsedGestures { get; set; } = true;      // rolar = volume · botão do meio = play/pause · arrastar de lado = trocar faixa
    public bool ShowDownloadProgress { get; set; } = true;
    public int AgendaAlertMinutes { get; set; } = 10;        // 0 = não avisar

    // Mídia
    public bool LyricsEnabled { get; set; } = true;          // busca letras na internet (LRCLIB) quando a página de Mídia está aberta
    public bool DynamicCoverColor { get; set; } = true;

    // Área de transferência
    public int ClipboardMax { get; set; } = 40;

    // Não perturbar
    public bool FocusDnd { get; set; } = true;

    // Atualizações (GitHub Releases)
    public bool UpdateCheckEnabled { get; set; } = true;
    public int UpdateHours { get; set; } = 6;
    public string UpdateNotifiedTag { get; set; } = "";

    // Sons
    public bool SoundsEnabled { get; set; }
    public double SoundVolume { get; set; } = 0.35;
    public bool AllowDrag { get; set; } = true;
    public int HoverDelayMs { get; set; } = 140;
    public int CollapseDelayMs { get; set; } = 250;          // espera depois que o mouse sai da ilha
    public double IdleCollapseSeconds { get; set; } = 8;     // recolhe se o mouse ficar parado em cima (0 = nunca)
    public CollapseMode CollapseSpeed { get; set; } = CollapseMode.Fast;
    public FullscreenMode Fullscreen { get; set; } = FullscreenMode.Hide;
    public bool StartWithWindows { get; set; }
    public bool ShowTrayIcon { get; set; } = true;
    public Dictionary<string, bool> Modules { get; set; } = new();
    /// <summary>Ordem dos itens na barra inferior da ilha (ids de Modules). Vazio = ordem padrão.</summary>
    public List<string> NavOrder { get; set; } = new();
    /// <summary>Ordem dos cartões da página Início (ids de HomeWidgets). Vazio = ordem padrão.</summary>
    public List<string> HomeOrder { get; set; } = new();
    /// <summary>Cartões do Início que o usuário escondeu (id =&gt; true). Ausente = aparece.</summary>
    public Dictionary<string, bool> HomeHidden { get; set; } = new();

    // Ambientação
    public double AmbienceVolume { get; set; } = 0.7;
    public int AmbienceFadeMinutes { get; set; }                       // 0 = sem fade-out automático
    public Dictionary<string, double> AmbienceLevels { get; set; } = new();

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
    public int TimerSeconds { get; set; }                // segundos extras do timer (0–59): 1m30s, 0m45s…
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
    /// <summary>Pede à ilha para abrir e fechar sozinha, para o usuário ver o estilo de animação escolhido.</summary>
    public static event Action? PreviewAnimation;
    public static void RequestPreview() => PreviewAnimation?.Invoke();
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
        if (Current.SettingsVersion < 1)
        {
            // v2.4: quem já usava "Expandir ao clicar" não quer que o hover continue abrindo a ilha.
            if (Current.ExpandOnClick) Current.ExpandOnHover = false;
            if (!Current.ExpandOnClick && !Current.ExpandOnHover) Current.ExpandOnClick = true;
            Current.SettingsVersion = 1;
            Save();
        }
        // Itens que ainda não existem na ordem salva entram no fim, na ordem padrão (módulos novos aparecem para todo mundo).
        var known = Current.NavOrder.Where(id => Modules.All.Any(m => m.Id == id)).Distinct().ToList();
        known.AddRange(Modules.All.Select(m => m.Id).Where(id => !known.Contains(id)));
        Current.NavOrder = known;
        var home = Current.HomeOrder.Where(id => HomeWidgets.All.Any(w => w.Id == id)).Distinct().ToList();
        home.AddRange(HomeWidgets.All.Select(w => w.Id).Where(id => !home.Contains(id)));
        Current.HomeOrder = home;
        if (string.IsNullOrWhiteSpace(Current.DownloadsPath))
            Current.DownloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (string.IsNullOrWhiteSpace(Current.ScreenshotsPath))
            Current.ScreenshotsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");
    }

    /// <summary>Chame depois de alterar qualquer valor: salva (com debounce) e avisa a interface.</summary>
    public static void Apply()
    {
        Changed?.Invoke();
        Save();
    }

    /// <summary>Só grava em disco (com debounce), sem avisar a interface — para ajustes que não mudam o layout.</summary>
    public static void Save()
    {
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
        foreach (var n in new[] { "notes", "tasks", "events", "tray", "clipboard" })
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
        new("focus", "\uE916", "Foco"),
        new("ambience", "\uE995", "Ambientação"),
        new("inbox", "\uE715", "Inbox"),
        new("productivity", "\uE70F", "Produtividade"),
        new("clipboard", "\uE77F", "Área de transferência"),
        new("system", "\uE772", "Sistema"),
        new("files", "\uE8B7", "Arquivos"),
        new("ai", "AI", "Agentes de IA"),
        new("mirror", "\uE722", "Mirror"),
        new("dictation", "\uE720", "Ditado"),
    };

    public static string Title(string id) => All.FirstOrDefault(m => m.Id == id)?.Title ?? id;

    /// <summary>Ids na ordem escolhida pelo usuário (Configurações &gt; Módulos).</summary>
    public static IEnumerable<string> Order()
    {
        var saved = Settings.Current.NavOrder;
        if (saved == null || saved.Count == 0) return All.Select(m => m.Id);
        var list = saved.Where(id => All.Any(m => m.Id == id)).Distinct().ToList();
        list.AddRange(All.Select(m => m.Id).Where(id => !list.Contains(id)));
        return list;
    }

    public static void Move(string id, int delta)
    {
        var list = Order().ToList();
        int i = list.IndexOf(id), j = i + delta;
        if (i < 0 || j < 0 || j >= list.Count) return;
        (list[i], list[j]) = (list[j], list[i]);
        Settings.Current.NavOrder = list;
        Settings.Apply();
    }
}

/// <summary>Cartões da página Início, que o usuário liga, desliga e reordena (Configurações &gt; Módulos).</summary>
public static class HomeWidgets
{
    public static readonly (string Id, string Title)[] All =
    {
        ("clock", "Relógio"),
        ("system", "Sistema"),
        ("nowplaying", "Tocando agora"),
        ("battery", "Bateria"),
        ("sound", "Som e luz"),
        ("weather", "Clima"),
        ("agenda", "Agenda"),
    };

    public static string Title(string id) => All.FirstOrDefault(w => w.Id == id).Title ?? id;

    /// <summary>O cartão aparece no Início? (ausente em HomeHidden = sim)</summary>
    public static bool IsOn(string id) => !Settings.Current.HomeHidden.TryGetValue(id, out var hidden) || !hidden;

    public static IEnumerable<string> Order()
    {
        var saved = Settings.Current.HomeOrder;
        if (saved == null || saved.Count == 0) return All.Select(w => w.Id);
        var list = saved.Where(id => All.Any(w => w.Id == id)).Distinct().ToList();
        list.AddRange(All.Select(w => w.Id).Where(id => !list.Contains(id)));
        return list;
    }

    public static void Move(string id, int delta)
    {
        var list = Order().ToList();
        int i = list.IndexOf(id), j = i + delta;
        if (i < 0 || j < 0 || j >= list.Count) return;
        (list[i], list[j]) = (list[j], list[i]);
        Settings.Current.HomeOrder = list;
        Settings.Apply();
    }
}

public class InboxItem
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string Source { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime Time { get; set; } = DateTime.Now;
    public bool Important { get; set; }
    /// <summary>Ação ao tocar no banner (ex.: "update"); vazio = abrir o Inbox.</summary>
    public string Action { get; set; } = "";
}

/// <summary>Barramento central: qualquer módulo publica notificações e eventos de agentes aqui.</summary>
public static class Hub
{
    public static ObservableCollection<InboxItem> Inbox { get; } = new();
    public static event Action<InboxItem>? Banner;
    public static event Action<AgentEvent>? AgentEventRaised;

    // ---- Não perturbar (Pomodoro): durante o foco os banners ficam retidos; as notificações continuam indo para o Inbox.
    static readonly List<InboxItem> _held = new();
    static bool _dndWas;

    /// <summary>true enquanto um Pomodoro de foco está rodando e a opção está ligada.</summary>
    public static bool DndActive => Settings.Current.FocusDnd && Svc.Focus.Pomo.Running && Svc.Focus.Pomo.Phase == FocusPhase.Focus;
    public static int HeldCount => _held.Count;
    public static event Action? HeldChanged;

    public static void Notify(string source, string title, string text, bool important = false, bool banner = true, bool bypassDnd = false)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var it = new InboxItem { Source = source, Title = title, Text = text, Important = important, Action = source == "Atualização" && title.StartsWith("Nova versão") ? "update" : "" };
            Inbox.Insert(0, it);
            while (Inbox.Count > 200) Inbox.RemoveAt(Inbox.Count - 1);
            if (!banner || !Settings.Current.BannerEnabled) return;
            if (!bypassDnd && source != "Bateria" && DndActive)   // bateria crítica nunca fica retida
             { _held.Add(it); _dndWas = true; HeldChanged?.Invoke(); return; }
            Banner?.Invoke(it);
        });
    }

    /// <summary>Chamado pelo FocusService a cada mudança: se o foco acabou (ou foi pausado), entrega o resumo do que chegou.</summary>
    public static void EvaluateDnd()
    {
        bool now = DndActive;
        if (_dndWas && !now) { var held = TakeHeld(); if (held.Count > 0) Banner?.Invoke(Summary(held)); }
        _dndWas = now;
    }

    /// <summary>Esvazia a fila de retidos (usado quando o próprio aviso de fim de foco vai carregar o resumo).</summary>
    public static List<InboxItem> TakeHeld()
    {
        var l = _held.ToList(); _held.Clear(); _dndWas = DndActive;
        if (l.Count > 0) HeldChanged?.Invoke();
        return l;
    }

    public static InboxItem Summary(List<InboxItem> held)
    {
        var names = held.Select(h => h.Source).Where(x => x.Length > 0).GroupBy(x => x).OrderByDescending(g => g.Count()).Select(g => g.Key).ToList();
        string who = names.Count switch { 0 => "", 1 => names[0], 2 => $"{names[0]} e {names[1]}", _ => $"{names[0]}, {names[1]} e mais {names.Count - 2}" };
        return new InboxItem
        {
            Source = "Foco",
            Title = held.Count == 1 ? "1 notificação durante o foco" : $"{held.Count} notificações durante o foco",
            Text = who.Length > 0 ? $"{who} — toque para abrir o Inbox" : "Toque para abrir o Inbox",
            Important = true
        };
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
