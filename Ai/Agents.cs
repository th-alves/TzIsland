namespace TzIsland;

public enum AgentState { Offline, Idle, Working, NeedsInput, Done, Unavailable }

public record AgentEvent(string AgentId, string? State, string? Message, double? Progress, double? UsageSession, double? UsageWeekly);

public class AgentStatus
{
    public string Id = "", Name = "", Letter = "", ColorHex = "#888888";
    public AgentState State = AgentState.Offline;
    public string Message = "";
    public string Note = "";                 // origem/limitação da informação, mostrada na interface
    public double? Progress;                 // 0..100, só se o agente informar
    public double? UsageSession;             // 0..100, só se uma fonte real informar
    public double? UsageWeekly;
    public DateTime Updated = DateTime.Now;
}

/// <summary>Resultado de uma sondagem de um provedor.</summary>
public record Probe(AgentState State, string Message, string Note);

/// <summary>Cada provedor é um adaptador independente: basta implementar esta interface e registrá-lo no AgentMonitor.</summary>
public interface IAgentAdapter
{
    string Id { get; }
    string Name { get; }
    string Letter { get; }
    string ColorHex { get; }
    bool NeedsNetwork { get; }
    Task<Probe> PollAsync();
}

/// <summary>Detecta se o programa está aberto pelo nome do processo.</summary>
public class ProcessAdapter : IAgentAdapter
{
    readonly string[] _names;
    public string Id { get; }
    public string Name { get; }
    public string Letter { get; }
    public string ColorHex { get; }
    public virtual bool NeedsNetwork => false;

    public ProcessAdapter(string id, string name, string letter, string color, params string[] processNames)
    { Id = id; Name = name; Letter = letter; ColorHex = color; _names = processNames; }

    protected bool IsRunning() => _names.Any(n => Process.GetProcessesByName(n).Length > 0);

    public virtual Task<Probe> PollAsync() => Task.FromResult(IsRunning()
        ? new Probe(AgentState.Idle, "Aberto", "Detectado pelo processo. O estado de trabalho só é conhecido via hooks (veja o README).")
        : new Probe(AgentState.Offline, "Não está em execução", ""));
}

/// <summary>Claude Code: processo + atividade nos arquivos de sessão (~/.claude/projects). Hooks dão o estado exato.</summary>
public sealed class ClaudeCodeAdapter : ProcessAdapter
{
    FileSystemWatcher? _w;
    DateTime _lastWrite = DateTime.MinValue;
    public event Action? Pulse;

    public ClaudeCodeAdapter() : base("claude", "Claude Code", "C", "#D97757", "claude") { }

    public void StartWatching()
    {
        try
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");
            if (!Directory.Exists(root)) return;
            _w = new FileSystemWatcher(root, "*.jsonl") { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size, EnableRaisingEvents = true };
            _w.Changed += (_, _) => { _lastWrite = DateTime.UtcNow; Pulse?.Invoke(); };
        }
        catch (Exception e) { Log.Write(e); }
    }

    public override Task<Probe> PollAsync()
    {
        bool running = IsRunning();
        if ((DateTime.UtcNow - _lastWrite).TotalSeconds < 8)
            return Task.FromResult(new Probe(AgentState.Working, "Atividade detectada na sessão", "Inferido pela escrita nos arquivos de sessão (aproximado). Use hooks para o estado exato."));
        return Task.FromResult(running
            ? new Probe(AgentState.Idle, "Aberto", "Estado exato disponível via hooks (veja o README).")
            : new Probe(AgentState.Offline, "Não está em execução", ""));
    }
}

/// <summary>Servidores locais compatíveis com HTTP (Ollama, LM Studio, endpoints personalizados).</summary>
public class HttpAdapter : IAgentAdapter
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMilliseconds(900) };
    readonly string _url;
    readonly Func<JsonElement, string?> _describe;
    public string Id { get; }
    public string Name { get; }
    public string Letter { get; }
    public string ColorHex { get; }
    public bool NeedsNetwork => true;

    public HttpAdapter(string id, string name, string letter, string color, string url, Func<JsonElement, string?> describe)
    { Id = id; Name = name; Letter = letter; ColorHex = color; _url = url; _describe = describe; }

    public async Task<Probe> PollAsync()
    {
        try
        {
            var sw = Stopwatch.StartNew();
            using var resp = await Http.GetAsync(_url);
            if (!resp.IsSuccessStatusCode)
                return new Probe(AgentState.Unavailable, $"Respondeu HTTP {(int)resp.StatusCode}", _url);
            string body = await resp.Content.ReadAsStringAsync();
            string? detail = null;
            try { using var doc = JsonDocument.Parse(body); detail = _describe(doc.RootElement.Clone()); } catch { }
            return new Probe(AgentState.Idle, detail ?? $"Online ({sw.ElapsedMilliseconds} ms)", "Servidor acessível. Estado de geração em andamento não é exposto pela API.");
        }
        catch { return new Probe(AgentState.Offline, "Servidor não encontrado", _url); }
    }

    public static string? Models(JsonElement root, string arrayName, string nameProp)
    {
        if (!root.TryGetProperty(arrayName, out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        var names = arr.EnumerateArray().Select(m => m.TryGetProperty(nameProp, out var n) ? n.GetString() : null).Where(n => n != null).ToList();
        return names.Count == 0 ? "Online, nenhum modelo carregado" : "Modelos: " + string.Join(", ", names.Take(2)) + (names.Count > 2 ? $" +{names.Count - 2}" : "");
    }
}

public sealed class AgentMonitor
{
    readonly List<IAgentAdapter> _adapters = new();
    readonly Dictionary<string, AgentStatus> _status = new();
    readonly Dictionary<string, (AgentState state, string msg, DateTime at)> _hook = new();
    readonly Dictionary<string, (bool w80, bool w95)> _alerts = new();
    readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromSeconds(2) };
    int _tick;
    bool _busy;
    ClaudeCodeAdapter? _claude;

    public event Action? Changed;
    public IReadOnlyList<AgentStatus> Statuses => _adapters.Select(a => _status[a.Id]).ToList();

    public AgentStatus? Attention => Statuses.FirstOrDefault(s => s.State == AgentState.NeedsInput);
    public AgentStatus? Working => Statuses.FirstOrDefault(s => s.State == AgentState.Working);

    public void Start()
    {
        _claude = new ClaudeCodeAdapter();
        _claude.StartWatching();
        _claude.Pulse += () => Application.Current.Dispatcher.InvokeAsync(async () => await PollAsync());
        _adapters.Add(_claude);
        _adapters.Add(new ProcessAdapter("codex", "OpenAI Codex", "X", "#10A37F", "codex"));
        _adapters.Add(new ProcessAdapter("cursor", "Cursor", "▲", "#C9C9C9", "Cursor"));
        _adapters.Add(new HttpAdapter("ollama", "Ollama", "O", "#E5E5E5", "http://localhost:11434/api/ps",
            r => HttpAdapter.Models(r, "models", "name")));
        _adapters.Add(new HttpAdapter("lmstudio", "LM Studio", "L", "#7B61FF", "http://localhost:1234/v1/models",
            r => HttpAdapter.Models(r, "data", "id")));
        foreach (var e in Settings.Current.Endpoints.Where(e => !string.IsNullOrWhiteSpace(e.Url)))
            _adapters.Add(new HttpAdapter("custom:" + e.Name, string.IsNullOrWhiteSpace(e.Name) ? e.Url : e.Name, "◆", "#5AC8FA", e.Url, _ => null));

        foreach (var a in _adapters)
            _status[a.Id] = new AgentStatus { Id = a.Id, Name = a.Name, Letter = a.Letter, ColorHex = a.ColorHex };

        Hub.AgentEventRaised += OnEvent;
        _t.Tick += async (_, _) => await PollAsync();
        _t.Start();
        _ = PollAsync();
    }

    async Task PollAsync()
    {
        if (_busy) return;
        _busy = true;
        _tick++;
        bool changed = false;
        try
        {
            foreach (var a in _adapters)
            {
                if (a.NeedsNetwork && _tick % 3 != 1) continue;   // HTTP a cada ~6 s, processos a cada 2 s
                var p = await Task.Run(a.PollAsync);
                var st = _status[a.Id];
                var (state, msg) = Merge(a.Id, p);
                if (st.State != state || st.Message != msg || st.Note != p.Note)
                {
                    bool wasWorking = st.State == AgentState.Working;
                    st.State = state; st.Message = msg; st.Note = p.Note; st.Updated = DateTime.Now;
                    changed = true;
                    if (wasWorking && state == AgentState.Idle && !_hook.ContainsKey(a.Id))
                        Hub.Notify(st.Name, "Agente concluiu a atividade", "Sem novas gravações na sessão.", false);
                }
            }
        }
        finally { _busy = false; }
        if (changed) Changed?.Invoke();
        _t.Interval = Working != null ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(2);
    }

    // Eventos de hook têm prioridade sobre a detecção passiva.
    (AgentState, string) Merge(string id, Probe polled)
    {
        if (_hook.TryGetValue(id, out var h))
        {
            double age = (DateTime.Now - h.at).TotalSeconds;
            bool keep = h.state switch
            {
                AgentState.Working => age < 600,
                AgentState.NeedsInput => age < 3600,
                AgentState.Done => age < 45,
                _ => false
            };
            if (keep) return (h.state, h.msg);
            _hook.Remove(id);
        }
        return (polled.State, polled.Message);
    }

    void OnEvent(AgentEvent e)
    {
        if (!_status.TryGetValue(e.AgentId, out var st)) return;
        var state = ParseState(e.State);
        if (state != null)
        {
            string msg = e.Message ?? state switch
            {
                AgentState.Working => "Trabalhando…",
                AgentState.NeedsInput => "Precisa da sua ação",
                AgentState.Done => "Tarefa concluída",
                _ => "Ocioso"
            };
            _hook[e.AgentId] = (state.Value, msg, DateTime.Now);
            st.State = state.Value; st.Message = msg; st.Note = "Estado informado por hook (exato)."; st.Updated = DateTime.Now;
            if (state == AgentState.NeedsInput) Hub.Notify(st.Name, "Precisa da sua ação", msg, true);
            else if (state == AgentState.Done) Hub.Notify(st.Name, "Tarefa concluída", msg, false);
            if (state != AgentState.Working) st.Progress = null;
        }
        if (e.Progress != null) st.Progress = Math.Clamp(e.Progress.Value, 0, 100);
        if (e.UsageSession != null || e.UsageWeekly != null) ApplyUsage(st, e);
        Changed?.Invoke();
    }

    static AgentState? ParseState(string? s) => s?.ToLowerInvariant() switch
    {
        "working" or "busy" => AgentState.Working,
        "needs_input" or "input" or "attention" => AgentState.NeedsInput,
        "done" or "finished" or "complete" => AgentState.Done,
        "idle" => AgentState.Idle,
        _ => null
    };

    void ApplyUsage(AgentStatus st, AgentEvent e)
    {
        double? old = st.UsageSession;
        if (e.UsageSession != null) st.UsageSession = Math.Clamp(e.UsageSession.Value, 0, 100);
        if (e.UsageWeekly != null) st.UsageWeekly = Math.Clamp(e.UsageWeekly.Value, 0, 100);
        _alerts.TryGetValue(st.Id, out var al);
        foreach (var (label, v) in new[] { ("sessão", st.UsageSession), ("semanal", st.UsageWeekly) })
        {
            if (v == null) continue;
            if (v >= 95 && !al.w95) { al.w95 = true; Hub.Notify(st.Name, $"Limite {label} quase esgotado", $"{v:0}% utilizado.", true); }
            else if (v >= 80 && !al.w80) { al.w80 = true; Hub.Notify(st.Name, $"Limite {label} próximo", $"{v:0}% utilizado.", false); }
        }
        if (old != null && st.UsageSession != null && old - st.UsageSession > 30)
        {
            al = (false, false);
            Hub.Notify(st.Name, "Limite de sessão renovado", $"Uso voltou para {st.UsageSession:0}%.", false);
        }
        _alerts[st.Id] = al;
    }
}
