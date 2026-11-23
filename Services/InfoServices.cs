using System.Text.Json.Serialization;

namespace TzIsland;

public sealed class WeatherService
{
    public record Day(DateTime Date, int Code, double Max, double Min);
    public bool Ok;
    public double Temp;
    public int Code;
    public List<Day> Days { get; private set; } = new();
    public event Action? Changed;
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromMinutes(20) };

    public void Start()
    {
        _t.Tick += async (_, _) => await RefreshAsync();
        _t.Start();
        Settings.Changed += OnSettings;
        _ = RefreshAsync();
    }

    double _lat, _lon;
    void OnSettings()
    {
        var s = Settings.Current;
        if (Math.Abs(s.Latitude - _lat) > 0.001 || Math.Abs(s.Longitude - _lon) > 0.001) _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        var s = Settings.Current;
        _lat = s.Latitude; _lon = s.Longitude;
        try
        {
            var url = string.Format(CultureInfo.InvariantCulture,
                "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&current=temperature_2m,weather_code&daily=weather_code,temperature_2m_max,temperature_2m_min&forecast_days=4&timezone=auto",
                s.Latitude, s.Longitude);
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
            var root = doc.RootElement;
            var cur = root.GetProperty("current");
            Temp = cur.GetProperty("temperature_2m").GetDouble();
            Code = cur.GetProperty("weather_code").GetInt32();
            var d = root.GetProperty("daily");
            var times = d.GetProperty("time").EnumerateArray().Select(x => DateTime.Parse(x.GetString()!, CultureInfo.InvariantCulture)).ToList();
            var codes = d.GetProperty("weather_code").EnumerateArray().Select(x => x.GetInt32()).ToList();
            var max = d.GetProperty("temperature_2m_max").EnumerateArray().Select(x => x.GetDouble()).ToList();
            var min = d.GetProperty("temperature_2m_min").EnumerateArray().Select(x => x.GetDouble()).ToList();
            Days = times.Select((t, i) => new Day(t, codes[i], max[i], min[i])).ToList();
            Ok = true;
        }
        catch (Exception e) { Log.Write(e); Ok = false; }
        Changed?.Invoke();
    }

    public static async Task<(string Name, double Lat, double Lon)?> GeocodeAsync(string q)
    {
        try
        {
            var url = "https://geocoding-api.open-meteo.com/v1/search?count=1&language=pt&name=" + Uri.EscapeDataString(q);
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
            if (!doc.RootElement.TryGetProperty("results", out var r) || r.GetArrayLength() == 0) return null;
            var f = r[0];
            return (f.GetProperty("name").GetString() ?? q, f.GetProperty("latitude").GetDouble(), f.GetProperty("longitude").GetDouble());
        }
        catch (Exception e) { Log.Write(e); return null; }
    }

    public static string Emoji(int c) => c switch
    {
        0 => "☀️", 1 or 2 => "🌤️", 3 => "☁️", 45 or 48 => "🌫️", >= 51 and <= 57 => "🌦️",
        >= 61 and <= 67 => "🌧️", >= 71 and <= 77 => "❄️", >= 80 and <= 82 => "🌦️", >= 95 => "⛈️", _ => "🌡️"
    };

    public static string Label(int c) => c switch
    {
        0 => "Céu limpo", 1 or 2 => "Poucas nuvens", 3 => "Nublado", 45 or 48 => "Neblina", >= 51 and <= 57 => "Garoa",
        >= 61 and <= 67 => "Chuva", >= 71 and <= 77 => "Neve", >= 80 and <= 82 => "Pancadas", >= 95 => "Tempestade", _ => "Tempo"
    };
}

public class CalEvent
{
    public string Title { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool AllDay { get; set; }
    public string Location { get; set; } = "";
    public string Source { get; set; } = "Local";
}

/// <summary>Agenda: eventos locais + calendários externos por link ICS (Google, Outlook, iCloud...).</summary>
public sealed class CalendarService
{
    readonly JsonStore<List<CalEvent>> _local = new("events");
    List<CalEvent> _ics = new();
    public event Action? Changed;
    public int ExternalCount => _ics.Count;
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromMinutes(30) };

    public void Start()
    {
        _t.Tick += async (_, _) => await RefreshIcsAsync();
        _t.Start();
        _ = RefreshIcsAsync();
    }

    public async Task RefreshIcsAsync()
    {
        var all = new List<CalEvent>();
        foreach (var raw in Settings.Current.IcsUrls.Where(u => !string.IsNullOrWhiteSpace(u)))
        {
            try
            {
                var url = raw.Trim().Replace("webcal://", "https://", StringComparison.OrdinalIgnoreCase);
                all.AddRange(ParseIcs(await Http.GetStringAsync(url), "Calendário externo"));
            }
            catch (Exception e) { Log.Write(e); }
        }
        _ics = all;
        Changed?.Invoke();
    }

    public List<CalEvent> Between(DateTime from, DateTime to) =>
        _local.Data.Concat(_ics).Where(e => e.End > from && e.Start < to).OrderBy(e => e.Start).ToList();

    public CalEvent? Next() => Between(DateTime.Now, DateTime.Now.AddDays(14)).FirstOrDefault(e => e.End > DateTime.Now);

    public void AddLocal(CalEvent e) { _local.Data.Add(e); _local.Save(); Changed?.Invoke(); }
    public void RemoveLocal(CalEvent e) { if (_local.Data.Remove(e)) { _local.Save(); Changed?.Invoke(); } }
    public bool IsLocal(CalEvent e) => _local.Data.Contains(e);

    // Parser ICS mínimo. Limitações conhecidas: não expande RRULE (eventos recorrentes) e ignora TZID.
    static List<CalEvent> ParseIcs(string text, string source)
    {
        var lines = new List<string>();
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            if ((raw.StartsWith(' ') || raw.StartsWith('\t')) && lines.Count > 0) lines[^1] += raw[1..];
            else lines.Add(raw);
        }
        var res = new List<CalEvent>();
        CalEvent? cur = null;
        foreach (var l in lines)
        {
            if (l == "BEGIN:VEVENT") { cur = new CalEvent { Source = source }; continue; }
            if (l == "END:VEVENT")
            {
                if (cur != null && cur.Start != default)
                {
                    if (cur.End == default) cur.End = cur.AllDay ? cur.Start.AddDays(1) : cur.Start.AddHours(1);
                    res.Add(cur);
                }
                cur = null; continue;
            }
            if (cur == null) continue;
            int c = l.IndexOf(':');
            if (c < 0) continue;
            var key = l[..c]; var val = l[(c + 1)..];
            switch (key.Split(';')[0])
            {
                case "SUMMARY": cur.Title = Unescape(val); break;
                case "LOCATION": cur.Location = Unescape(val); break;
                case "DTSTART": { var (d, all) = ParseDt(key, val); cur.Start = d; cur.AllDay = all; break; }
                case "DTEND": cur.End = ParseDt(key, val).Item1; break;
            }
        }
        return res;
    }

    static string Unescape(string s) => s.Replace("\\n", " ").Replace("\\,", ",").Replace("\\;", ";").Replace("\\\\", "\\");

    static (DateTime, bool) ParseDt(string key, string val)
    {
        if (key.Contains("VALUE=DATE") && !key.Contains("DATE-TIME") || val.Length == 8)
            return DateTime.TryParseExact(val[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? (d, true) : (default, false);
        bool utc = val.EndsWith('Z');
        var v = val.TrimEnd('Z');
        if (DateTime.TryParseExact(v, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return (utc ? DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToLocalTime() : dt, false);
        return (default, false);
    }
}

public class Note
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";
    /// <summary>Conteúdo formatado (negrito, listas, títulos…) em XAML de FlowDocument. Vazio = nota antiga só com texto.</summary>
    public string? Rich { get; set; }
    public DateTime Updated { get; set; } = DateTime.Now;
    [JsonIgnore]
    public string Title
    {
        get { var l = Text.Split('\n').FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim(); return string.IsNullOrEmpty(l) ? "Nota vazia" : l!; }
    }
}

public class TaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";
    public bool Done { get; set; }
    public int Priority { get; set; } = 1;   // 0 alta, 1 média, 2 baixa
    public DateTime Created { get; set; } = DateTime.Now;
}

public sealed class NotesStore
{
    readonly JsonStore<List<Note>> _s = new("notes");
    public List<Note> Items => _s.Data;
    public Note Add() { var n = new Note(); Items.Insert(0, n); _s.Save(); return n; }
    public void Remove(Note n) { Items.Remove(n); _s.Save(); }
    public void Save() => _s.Save();
}

public sealed class TasksStore
{
    readonly JsonStore<List<TaskItem>> _s = new("tasks");
    public List<TaskItem> Items => _s.Data;
    public int Pending => Items.Count(t => !t.Done);
    public event Action? Changed;
    public void Add(string text, int prio) { Items.Add(new TaskItem { Text = text, Priority = prio }); Save(); }
    public void Remove(TaskItem t) { Items.Remove(t); Save(); }
    public void Save() { _s.Save(); Changed?.Invoke(); }
    public IEnumerable<TaskItem> Sorted() => Items.OrderBy(t => t.Done).ThenBy(t => t.Priority).ThenBy(t => t.Created);
}
