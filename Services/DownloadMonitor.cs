namespace TzIsland;

/// <summary>
/// Acompanha downloads em andamento na pasta configurada (Chrome/Edge/Brave: .crdownload · Firefox: .part · Opera: .opdownload).
///
/// IMPORTANTE (honestidade dos dados): o navegador NÃO diz à pasta qual será o tamanho final do arquivo, então o app não tem
/// como calcular uma porcentagem real. O que dá para medir de verdade é o quanto já foi baixado e a velocidade — é isso que
/// a ilha mostra, com uma barra "em andamento" (indeterminada). A barra só enche até o fim quando o arquivo realmente termina.
/// </summary>
public sealed class DownloadMonitor
{
    static readonly string[] Patterns = { "*.crdownload", "*.part", "*.opdownload", "*.partial", "*.download" };
    static readonly CultureInfo Br = new("pt-BR");
    static Dispatcher Ui => Application.Current.Dispatcher;

    sealed class Entry
    {
        public string Path = "";
        public long Size;
        public long Ts;                 // Stopwatch do último cálculo
        public double Speed;            // bytes/s (suavizada)
        public DateTime FirstSeen = DateTime.UtcNow, LastGrowth = DateTime.UtcNow;
    }

    readonly Dictionary<string, Entry> _e = new(StringComparer.OrdinalIgnoreCase);
    readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromMilliseconds(700) };
    bool _busy;
    int _idleScans;
    bool _hadActive;

    public event Action? Changed;

    public int Count { get; private set; }
    public long TotalBytes { get; private set; }
    public double TotalSpeed { get; private set; }
    public string FirstName { get; private set; } = "";
    /// <summary>Momento em que o último download terminou (para a barra "encher" e sumir).</summary>
    public DateTime FinishedAt { get; private set; } = DateTime.MinValue;
    public bool JustFinished => Count == 0 && (DateTime.UtcNow - FinishedAt).TotalSeconds < 1.8;
    public bool Active => Count > 0;

    public DownloadMonitor() { _t.Tick += (_, _) => Scan(); }

    /// <summary>Chamado pelos eventos do FileSystemWatcher (qualquer thread): liga a verificação periódica.</summary>
    public void Wake()
    {
        if (!Settings.Current.WatchDownloads || !Settings.Current.ShowDownloadProgress) return;
        Ui.InvokeAsync(() => { _idleScans = 0; if (!_t.IsEnabled) { _t.Start(); Scan(); } });
    }

    /// <summary>Chamado quando o arquivo final apareceu na pasta (download concluído).</summary>
    public void Completed(string finalPath)
    {
        FinishedAt = DateTime.UtcNow;
        if (_hadActive) { Count = 0; Changed?.Invoke(); }
        Ui.InvokeAsync(async () => { await Task.Delay(2000); Changed?.Invoke(); });   // deixa a ilha apagar o brilho de "concluído"
    }

    void Scan()
    {
        if (_busy) return;
        var dir = Settings.Current.DownloadsPath;
        if (!Settings.Current.WatchDownloads || !Settings.Current.ShowDownloadProgress || !Directory.Exists(dir)) { Reset(); return; }
        _busy = true;
        Task.Run(() =>
        {
            var found = new List<(string path, long len)>();
            try
            {
                foreach (var pat in Patterns)
                    foreach (var f in Directory.EnumerateFiles(dir, pat))
                    {
                        try { found.Add((f, new FileInfo(f).Length)); } catch { }
                    }
            }
            catch (Exception e) { Log.Write(e); }
            return found;
        }).ContinueWith(t =>
        {
            Ui.InvokeAsync(() => { _busy = false; if (t.IsCompletedSuccessfully) Apply(t.Result); });
        });
    }

    void Apply(List<(string path, long len)> found)
    {
        long now = Stopwatch.GetTimestamp();
        var live = new HashSet<string>(found.Select(f => f.path), StringComparer.OrdinalIgnoreCase);
        foreach (var k in _e.Keys.Where(k => !live.Contains(k)).ToList()) _e.Remove(k);

        foreach (var (path, len) in found)
        {
            if (!_e.TryGetValue(path, out var e)) { _e[path] = new Entry { Path = path, Size = len, Ts = now }; continue; }
            double dt = (now - e.Ts) / (double)Stopwatch.Frequency;
            if (dt < 0.2) continue;
            if (len > e.Size) { e.LastGrowth = DateTime.UtcNow; double inst = (len - e.Size) / dt; e.Speed = e.Speed <= 0 ? inst : e.Speed * 0.55 + inst * 0.45; }
            else e.Speed *= 0.5;
            e.Size = len; e.Ts = now;
        }

        // "Em andamento" = cresceu nos últimos 6 s (ou acabou de aparecer). Download pausado sai da ilha e volta se retomar.
        var active = _e.Values.Where(x => (DateTime.UtcNow - x.LastGrowth).TotalSeconds < 6).OrderByDescending(x => x.FirstSeen).ToList();
        int oldCount = Count; long oldBytes = TotalBytes;
        Count = active.Count;
        TotalBytes = active.Sum(x => x.Size);
        TotalSpeed = active.Sum(x => x.Speed);
        FirstName = active.Count > 0 ? Path.GetFileNameWithoutExtension(active[0].Path) : "";
        if (Count > 0) _hadActive = true;
        if (Count == 0 && oldCount > 0 && (DateTime.UtcNow - FinishedAt).TotalSeconds > 3) _hadActive = false;

        _idleScans = found.Count == 0 ? _idleScans + 1 : 0;
        if (_idleScans >= 4) _t.Stop();                       // nada baixando: não gasta nada em segundo plano
        if (Count != oldCount || TotalBytes != oldBytes) Changed?.Invoke();
    }

    void Reset()
    {
        _t.Stop(); _e.Clear();
        if (Count != 0) { Count = 0; TotalBytes = 0; TotalSpeed = 0; Changed?.Invoke(); }
    }

    public static string FormatBytes(long b)
    {
        double v = b; string[] u = { "B", "KB", "MB", "GB", "TB" }; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return (i <= 1 ? v.ToString("0", Br) : v.ToString(v >= 100 ? "0" : "0.#", Br)) + " " + u[i];
    }

    public static string FormatSpeed(double bps) => FormatBytes((long)bps) + "/s";
}
