namespace TzIsland;

public class TrayFile
{
    public string Path { get; set; } = "";
    public DateTime Added { get; set; } = DateTime.Now;
    public bool Auto { get; set; }
    public string Origin { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore] public string Name => System.IO.Path.GetFileName(Path.TrimEnd('\\', '/'));
}

/// <summary>Bandeja de arquivos (somente referências — nunca copia nem apaga arquivos) + monitoramento de pastas.</summary>
public sealed class FileTrayService
{
    readonly JsonStore<List<TrayFile>> _store = new("tray");
    public ObservableCollection<TrayFile> Items { get; } = new();
    FileSystemWatcher? _dl, _ss;
    readonly HashSet<string> _recent = new();
    static Dispatcher Ui => Application.Current.Dispatcher;
    static readonly string[] Temp = { ".crdownload", ".tmp", ".part", ".partial", ".download", ".opdownload" };

    public void Start()
    {
        foreach (var f in _store.Data.Where(f => File.Exists(f.Path) || Directory.Exists(f.Path))) Items.Add(f);
        Rewatch();
        Settings.Changed += Rewatch;
    }

    void Rewatch()
    {
        var s = Settings.Current;
        Setup(ref _dl, s.WatchDownloads, s.DownloadsPath, "Downloads");
        Setup(ref _ss, s.WatchScreenshots, s.ScreenshotsPath, "Capturas de tela");
    }

    void Setup(ref FileSystemWatcher? w, bool on, string path, string origin)
    {
        if (w != null && on && string.Equals(w.Path, path, StringComparison.OrdinalIgnoreCase)) return;
        w?.Dispose(); w = null;
        if (!on || !Directory.Exists(path)) return;
        try
        {
            var nw = new FileSystemWatcher(path) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite, EnableRaisingEvents = true };
            nw.Created += (_, e) => OnFs(e.FullPath, origin);
            nw.Renamed += (_, e) => OnFs(e.FullPath, origin);
            nw.Changed += (_, e) => { if (origin == "Downloads" && Temp.Contains(System.IO.Path.GetExtension(e.FullPath).ToLowerInvariant())) Svc.Downloads.Wake(); };
            w = nw;
        }
        catch (Exception e) { Log.Write(e); }
    }

    void OnFs(string path, string origin)
    {
        if (Temp.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant())) { if (origin == "Downloads") Svc.Downloads.Wake(); return; }
        Detected(path, origin);
    }

    void Detected(string path, string origin)
    {
        if (Temp.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant())) return;
        lock (_recent) { if (!_recent.Add(path)) return; }
        Task.Run(async () =>
        {
            for (int i = 0; i < 60; i++)   // espera o arquivo terminar de ser gravado
            {
                try { using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None); break; }
                catch (FileNotFoundException) { return; }
                catch { await Task.Delay(500); }
            }
            if (!File.Exists(path)) return;
            await Ui.InvokeAsync(() =>
            {
                Add(path, true, origin);
                if (origin == "Downloads") Svc.Downloads.Completed(path);
                Hub.Notify(origin, origin == "Downloads" ? "Novo download" : "Nova captura", System.IO.Path.GetFileName(path));
            });
            await Task.Delay(5000);
            lock (_recent) _recent.Remove(path);
        });
    }

    public void Add(string path, bool auto = false, string origin = "Manual")
    {
        if (Items.Any(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase))) return;
        var t = new TrayFile { Path = path, Auto = auto, Origin = origin };
        Items.Insert(0, t);
        while (Items.Count > 60) Items.RemoveAt(Items.Count - 1);
        Persist();
    }

    /// <summary>Remove somente da bandeja. O arquivo no disco permanece intacto.</summary>
    public void Remove(TrayFile f) { Items.Remove(f); Persist(); }

    void Persist()
    {
        _store.Data.Clear();
        _store.Data.AddRange(Items);
        _store.Save();
    }

    public void Clear() { Items.Clear(); Persist(); }
}
