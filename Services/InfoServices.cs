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

    /// <summary>Modo economia: para de consultar a rede até voltar à tomada.</summary>
    public void Suspend() { _t.Stop(); }
    public void Resume() { if (!_t.IsEnabled) _t.Start(); _ = RefreshAsync(); }

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

    /// <summary>Ícone vetorial do clima (mesmo traço dos outros ícones da interface, sem emoji colorido).</summary>
    public static string Icon(int c) => c switch
    {
        0 => "sun", 1 or 2 => "partly", 3 => "cloud", 45 or 48 => "fog", >= 51 and <= 57 => "drizzle",
        >= 61 and <= 67 or >= 80 and <= 82 => "rain", >= 71 and <= 77 => "snow", >= 95 => "storm", _ => "cloud"
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
    /// <summary>UID do ICS: identifica o evento mestre e suas exceções (RECURRENCE-ID).</summary>
    public string Uid { get; set; } = "";
    /// <summary>Início original de uma ocorrência remarcada; nulo = evento normal.</summary>
    public DateTime? RecurrenceId { get; set; }
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

    /// <summary>Modo economia: sem busca de ICS em segundo plano; ao sair, atualiza na hora.</summary>
    public void Suspend() => _t.Stop();
    public void Resume() { if (!_t.IsEnabled) _t.Start(); _ = RefreshIcsAsync(); }

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

    /// <summary>
    /// Janela de expansão das recorrências. A Agenda e a pílula só consultam de agora a ~14 dias;
    /// 90 dias cobrem a grade inteira sem gerar ocorrências demais.
    /// </summary>
    public const int HorizonDays = 90;

    public static List<CalEvent> ParseIcs(string text, string source)
        => ParseIcs(text, source, DateTime.Now.AddDays(-1), DateTime.Now.AddDays(HorizonDays));

    /// <summary>
    /// Parser ICS com RRULE (DAILY/WEEKLY/MONTHLY/YEARLY, INTERVAL, COUNT, UNTIL, BYDAY),
    /// EXDATE, RECURRENCE-ID (ocorrência remarcada) e TZID. Antes, evento recorrente era
    /// simplesmente ignorado — e o caso mais comum de calendário ICS é justamente a reunião semanal.
    /// </summary>
    public static List<CalEvent> ParseIcs(string text, string source, DateTime from, DateTime to)
    {
        var raws = ParseRaw(text, source);
        // Uma exceção sem DTEND herda a duração do evento mestre (é assim que Google/Outlook gravam).
        foreach (var o in raws.Where(r => r.RecurrenceId.HasValue && r.End == default))
        {
            var master = raws.FirstOrDefault(r => !r.RecurrenceId.HasValue && r.Uid == o.Uid);
            if (master != null) o.End = o.Start + master.Duration;
        }

        var overrides = raws.Where(r => r.RecurrenceId.HasValue).ToList();
        var used = new HashSet<string>();
        var res = new List<CalEvent>();
        foreach (var r in raws.Where(r => !r.RecurrenceId.HasValue))
        {
            if (r.Start == default) continue;
            if (r.Rrule.Length == 0)
            {
                if (r.Start < to && r.Start + r.Duration > from) res.Add(r.ToEvent(r.Start));
                continue;
            }
            res.AddRange(Expand(r, from, to, overrides, used));
        }
        foreach (var o in overrides)
        {
            if (used.Contains(o.Key)) continue;
            if (o.Start < to && o.Start + o.Duration > from) res.Add(o.ToEvent(o.Start));
        }
        return res.OrderBy(e => e.Start).ToList();
    }

    static IEnumerable<CalEvent> Expand(Raw e, DateTime from, DateTime to, List<Raw> overrides, HashSet<string> used)
    {
        var rule = ParseRule(e.Rrule);
        var ex = new HashSet<DateTime>(e.ExDates);
        int n = 0;
        foreach (var occ in Dates(e, rule, from, to))
        {
            if (n++ >= 400) yield break;                    // uma reunião diária não pode virar 90 eventos
            if (ex.Contains(occ)) continue;
            var ov = overrides.FirstOrDefault(o => o.Uid == e.Uid && o.RecurrenceId is DateTime rid && SameMinute(rid, occ));
            if (ov != null)
            {
                used.Add(ov.Key);
                if (ov.Start < to && ov.Start + ov.Duration > from) yield return ov.ToEvent(ov.Start);
                continue;
            }
            yield return e.ToEvent(occ);
        }
    }

    static bool SameMinute(DateTime a, DateTime b) => Math.Abs((a - b).TotalSeconds) < 60;

    static IEnumerable<DateTime> Dates(Raw e, Rule rule, DateTime from, DateTime to)
    {
        var start = e.Start;
        if (rule.Freq == Freq.None) { yield return start; yield break; }
        int interval = Math.Max(1, rule.Interval);
        int count = 0, guard = 0, step = 0;

        // Um evento diário de 2015 não pode custar 4 mil iterações: pula direto para perto da janela.
        // Com COUNT o pulo é inválido (o número de ocorrências conta desde o início).
        if (rule.Count == 0)
        {
            double days = (from.Date - start.Date).TotalDays;
            if (days > 0)
            {
                step = rule.Freq switch
                {
                    Freq.Daily => (int)(days / interval),
                    Freq.Weekly => (int)(days / (7.0 * interval)),
                    Freq.Monthly => Math.Max(0, (from.Year - start.Year) * 12 + from.Month - start.Month) / interval,
                    _ => Math.Max(0, from.Year - start.Year) / interval
                };
                step = Math.Max(0, step - 1);
            }
        }

        while (guard++ < 5000)
        {
            if (rule.Count > 0 && count >= rule.Count) yield break;

            if (rule.Freq == Freq.Weekly)
            {
                var weekStart = start.Date.AddDays((double)step * 7 * interval);
                if (weekStart >= to) yield break;
                for (int d = 0; d < 7; d++)
                {
                    var day = weekStart.AddDays(d);
                    if (day < start.Date) continue;
                    if (rule.ByDay.Count > 0 ? !rule.ByDay.Contains(day.DayOfWeek) : day.DayOfWeek != start.DayOfWeek) continue;
                    if (rule.Count > 0 && count >= rule.Count) yield break;
                    var when = DateTime.SpecifyKind(day + start.TimeOfDay, start.Kind);
                    count++;
                    if (when >= to) yield break;
                    if (rule.Until is DateTime u && when > u) yield break;
                    if (when >= from) yield return when;
                }
                step++;
                continue;
            }

            DateTime occ = rule.Freq switch
            {
                Freq.Daily => start.AddDays((double)step * interval),
                Freq.Monthly => start.AddMonths(step * interval),
                _ => start.AddYears(step * interval)
            };
            step++; count++;
            if (occ >= to) yield break;
            if (rule.Until is DateTime until && occ > until) yield break;
            if (occ >= from) yield return occ;
        }
    }

    static List<Raw> ParseRaw(string text, string source)
    {
        var lines = new List<string>();
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            if ((raw.StartsWith(' ') || raw.StartsWith('\t')) && lines.Count > 0) lines[^1] += raw[1..];
            else lines.Add(raw);
        }
        var res = new List<Raw>();
        Raw? cur = null;
        foreach (var l in lines)
        {
            if (l == "BEGIN:VEVENT") { cur = new Raw { Source = source }; continue; }
            if (l == "END:VEVENT")
            {
                if (cur != null && cur.Start != default) res.Add(cur);
                cur = null; continue;
            }
            if (cur == null) continue;
            int c = l.IndexOf(':');
            if (c < 0) continue;
            var key = l[..c]; var val = l[(c + 1)..];
            switch (key.Split(';')[0].ToUpperInvariant())
            {
                case "UID": cur.Uid = val.Trim(); break;
                case "SUMMARY": cur.Title = Unescape(val); break;
                case "LOCATION": cur.Location = Unescape(val); break;
                case "DTSTART": { var (d, all) = ParseDt(key, val); cur.Start = d; cur.AllDay = all; break; }
                case "DTEND": cur.End = ParseDt(key, val).dt; break;
                case "RRULE": cur.Rrule = val.Trim(); break;
                case "EXDATE":
                    foreach (var part in val.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var (d, _) = ParseDt(key, part.Trim());
                        if (d != default) cur.ExDates.Add(d);
                    }
                    break;
                case "RECURRENCE-ID": cur.RecurrenceId = ParseDt(key, val).dt; break;
            }
        }
        return res;
    }

    static string Unescape(string s) => s.Replace("\\n", " ").Replace("\\,", ",").Replace("\\;", ";").Replace("\\\\", "\\");

    /// <summary>Lê DTSTART/DTEND/EXDATE/RECURRENCE-ID: data pura, UTC (Z) e TZID (IANA ou Windows).</summary>
    static (DateTime dt, bool allDay) ParseDt(string key, string val)
    {
        val = val.Trim();
        bool dateOnly = key.Contains("VALUE=DATE", StringComparison.OrdinalIgnoreCase)
            && !key.Contains("DATE-TIME", StringComparison.OrdinalIgnoreCase)
            || val.Length == 8 && !val.Contains('T');
        if (dateOnly)
            return DateTime.TryParseExact(val[..Math.Min(8, val.Length)], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? (DateTime.SpecifyKind(d, DateTimeKind.Unspecified), true) : (default, false);

        bool utc = val.EndsWith('Z');
        var v = val.TrimEnd('Z');
        if (!DateTime.TryParseExact(v, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return (default, false);
        if (utc) return (DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToLocalTime(), false);

        // TZID: converte da zona do calendário para a zona local. Fuso desconhecido cai para hora local.
        if (Param(key, "TZID") is { Length: > 0 } tzid)
        {
            try
            {
                // O valor é hora de parede na zona do calendário: vira UTC por ela e só então é
                // convertido para a zona local (passar DateTime com Kind=Utc à sobrecarga errada lança).
                var tz = TimeZoneInfo.FindSystemTimeZoneById(tzid);
                var utcValue = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), tz);
                return (TimeZoneInfo.ConvertTimeFromUtc(utcValue, TimeZoneInfo.Local), false);
            }
            catch (Exception e) { Log.Write(e); }
        }
        return (DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), false);
    }

    static string? Param(string key, string name)
    {
        foreach (var p in key.Split(';').Skip(1))
        {
            int eq = p.IndexOf('=');
            if (eq > 0 && p[..eq].Trim().Equals(name, StringComparison.OrdinalIgnoreCase)) return p[(eq + 1)..].Trim('"');
        }
        return null;
    }

    enum Freq { None, Daily, Weekly, Monthly, Yearly }

    sealed class Rule
    {
        public Freq Freq;
        public int Interval = 1;
        public int Count;
        public DateTime? Until;
        public readonly HashSet<DayOfWeek> ByDay = new();
    }

    static Rule ParseRule(string rrule)
    {
        var r = new Rule();
        foreach (var part in rrule.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0) continue;
            string k = part[..eq].Trim().ToUpperInvariant(), v = part[(eq + 1)..].Trim();
            switch (k)
            {
                case "FREQ":
                    r.Freq = v.ToUpperInvariant() switch
                    {
                        "DAILY" => Freq.Daily, "WEEKLY" => Freq.Weekly, "MONTHLY" => Freq.Monthly, "YEARLY" => Freq.Yearly, _ => Freq.None
                    };
                    break;
                case "INTERVAL": if (int.TryParse(v, out var i)) r.Interval = Math.Max(1, i); break;
                case "COUNT": if (int.TryParse(v, out var c)) r.Count = Math.Max(0, c); break;
                case "UNTIL":
                    var (until, _) = ParseDt("UNTIL", v);
                    if (until != default) r.Until = until;
                    break;
                case "BYDAY":
                    foreach (var d in v.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var wd = Weekday(d);
                        if (wd.HasValue) r.ByDay.Add(wd.Value);
                    }
                    break;
            }
        }
        return r;
    }

    static DayOfWeek? Weekday(string s)
    {
        s = s.Trim().ToUpperInvariant();
        if (s.Length > 2) s = s[^2..];                       // "1MO", "-1FR"
        return s switch
        {
            "MO" => DayOfWeek.Monday, "TU" => DayOfWeek.Tuesday, "WE" => DayOfWeek.Wednesday,
            "TH" => DayOfWeek.Thursday, "FR" => DayOfWeek.Friday, "SA" => DayOfWeek.Saturday, "SU" => DayOfWeek.Sunday,
            _ => null
        };
    }

    sealed class Raw
    {
        public string Uid = "", Title = "", Location = "", Source = "", Rrule = "";
        public DateTime Start, End;
        public bool AllDay;
        public DateTime? RecurrenceId;
        public readonly List<DateTime> ExDates = new();
        public string Key => Uid + "|" + (RecurrenceId?.Ticks ?? 0);
        public TimeSpan Duration => End == default || End <= Start ? (AllDay ? TimeSpan.FromDays(1) : TimeSpan.FromHours(1)) : End - Start;
        public CalEvent ToEvent(DateTime start) => new()
        {
            Title = Title, Start = start, End = start + Duration, AllDay = AllDay,
            Location = Location, Source = Source, Uid = Uid, RecurrenceId = RecurrenceId
        };
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
