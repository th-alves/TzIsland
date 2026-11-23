using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace TzIsland;

public enum LyricsState { Idle, Loading, Synced, Plain, Instrumental, NotFound, Error }

public sealed record LyricLine(double Time, string Text);

/// <summary>
/// Letras sincronizadas buscadas no LRCLIB (https://lrclib.net) — API gratuita, aberta e sem chave.
/// Só busca quando a página de Mídia pede (e se a opção estiver ligada). Resultados ficam em cache em disco.
/// </summary>
public sealed class LyricsService
{
    static readonly HttpClient Http = CreateHttp();
    static HttpClient CreateHttp()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("TzIsland/2.4 (app pessoal para Windows)");
        return h;
    }

    static string CacheDir => Path.Combine(Settings.DataDir, "lyrics");
    string _key = "";
    int _gen;

    public LyricsState State { get; private set; } = LyricsState.Idle;
    public IReadOnlyList<LyricLine> Lines { get; private set; } = Array.Empty<LyricLine>();
    public string Plain { get; private set; } = "";
    public string ForTitle { get; private set; } = "";
    public event Action? Changed;

    /// <summary>Pede a letra da faixa. Chamadas repetidas para a mesma faixa não refazem a busca.</summary>
    public void Request(string title, string artist, string album, double durationSec)
    {
        if (!Settings.Current.LyricsEnabled || string.IsNullOrWhiteSpace(title)) { Set(LyricsState.Idle, null, "", ""); _key = ""; return; }
        var (t, a) = Clean(title, artist);
        string key = (t + "|" + a).ToLowerInvariant();
        if (key == _key && State != LyricsState.Error) return;
        _key = key; ForTitle = title;
        int gen = ++_gen;
        if (TryCache(key, out var cached)) { Apply(cached!); return; }
        Set(LyricsState.Loading, null, "", "");
        _ = Task.Run(() => Fetch(gen, key, t, a, album, durationSec));
    }

    /// <summary>Índice da linha atual para a posição dada (−1 antes da primeira).</summary>
    public int IndexAt(double pos)
    {
        var l = Lines;
        int lo = 0, hi = l.Count - 1, r = -1;
        while (lo <= hi) { int m = (lo + hi) / 2; if (l[m].Time <= pos) { r = m; lo = m + 1; } else hi = m - 1; }
        return r;
    }

    // ---------------- busca ----------------
    sealed class Dto { public double Duration { get; set; } public bool Instrumental { get; set; } public string? PlainLyrics { get; set; } public string? SyncedLyrics { get; set; } }

    async Task Fetch(int gen, string key, string title, string artist, string album, double dur)
    {
        try
        {
            Dto? hit = null;
            string q = $"track_name={Uri.EscapeDataString(title)}&artist_name={Uri.EscapeDataString(artist)}";
            if (album.Length > 0) q += $"&album_name={Uri.EscapeDataString(album)}";
            if (dur > 0) q += $"&duration={(int)Math.Round(dur)}";
            using (var r = await Http.GetAsync("https://lrclib.net/api/get?" + q))
            {
                if (r.IsSuccessStatusCode) hit = JsonSerializer.Deserialize<Dto>(await r.Content.ReadAsStringAsync(), Json);
                else if ((int)r.StatusCode != 404) throw new HttpRequestException("LRCLIB " + (int)r.StatusCode);
            }
            if (hit == null || (string.IsNullOrWhiteSpace(hit.SyncedLyrics) && string.IsNullOrWhiteSpace(hit.PlainLyrics) && !hit.Instrumental))
            {
                // busca aproximada (título do YouTube costuma vir diferente do cadastro da música)
                string sq = $"track_name={Uri.EscapeDataString(title)}" + (artist.Length > 0 ? $"&artist_name={Uri.EscapeDataString(artist)}" : "");
                var arr = JsonSerializer.Deserialize<List<Dto>>(await Http.GetStringAsync("https://lrclib.net/api/search?" + sq), Json) ?? new();
                hit = arr.Where(x => !string.IsNullOrWhiteSpace(x.SyncedLyrics) || !string.IsNullOrWhiteSpace(x.PlainLyrics) || x.Instrumental)
                         .OrderBy(x => string.IsNullOrWhiteSpace(x.SyncedLyrics) ? 1 : 0)
                         .ThenBy(x => dur > 0 ? Math.Abs(x.Duration - dur) : 0)
                         .FirstOrDefault(x => dur <= 0 || Math.Abs(x.Duration - dur) <= 8);
            }
            var res = hit == null ? new Cached { Kind = "none" }
                    : hit.Instrumental ? new Cached { Kind = "instrumental" }
                    : !string.IsNullOrWhiteSpace(hit.SyncedLyrics) ? new Cached { Kind = "synced", Text = hit.SyncedLyrics }
                    : new Cached { Kind = "plain", Text = hit.PlainLyrics };
            SaveCache(key, res);
            await Application.Current.Dispatcher.InvokeAsync(() => { if (gen == _gen) Apply(res); });
        }
        catch (Exception e)
        {
            Log.Write("letra: " + e.Message);
            await Application.Current.Dispatcher.InvokeAsync(() => { if (gen == _gen) Set(LyricsState.Error, null, "", ""); });
        }
    }

    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    void Apply(Cached c)
    {
        switch (c.Kind)
        {
            case "synced": { var l = ParseLrc(c.Text ?? ""); if (l.Count > 0) Set(LyricsState.Synced, l, "", ""); else Set(LyricsState.Plain, null, StripTimes(c.Text ?? ""), ""); break; }
            case "plain": Set(LyricsState.Plain, null, c.Text ?? "", ""); break;
            case "instrumental": Set(LyricsState.Instrumental, null, "", ""); break;
            default: Set(LyricsState.NotFound, null, "", ""); break;
        }
    }

    void Set(LyricsState s, List<LyricLine>? lines, string plain, string _)
    {
        State = s; Lines = lines ?? (IReadOnlyList<LyricLine>)Array.Empty<LyricLine>(); Plain = plain;
        Changed?.Invoke();
    }

    // ---------------- LRC ----------------
    static readonly Regex Stamp = new(@"\[(\d{1,3}):(\d{2})(?:[.:](\d{1,3}))?\]", RegexOptions.Compiled);
    static readonly Regex Offset = new(@"^\[offset:\s*([+-]?\d+)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static List<LyricLine> ParseLrc(string lrc)
    {
        var res = new List<LyricLine>();
        double off = 0;
        foreach (var raw in lrc.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            var om = Offset.Match(line);
            if (om.Success) { off = int.Parse(om.Groups[1].Value, CultureInfo.InvariantCulture) / 1000.0; continue; }
            var ms = Stamp.Matches(line);
            if (ms.Count == 0) continue;
            string text = Stamp.Replace(line, "").Trim();
            foreach (Match m in ms)
            {
                double t = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 60 + int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                if (m.Groups[3].Success) { var f = m.Groups[3].Value; t += int.Parse(f, CultureInfo.InvariantCulture) / Math.Pow(10, f.Length); }
                res.Add(new LyricLine(Math.Max(0, t - off), text));
            }
        }
        return res.OrderBy(x => x.Time).ToList();
    }

    static string StripTimes(string s) => string.Join("\n", s.Replace("\r", "").Split('\n').Select(l => Stamp.Replace(l, "").Trim()));

    // ---------------- limpeza do título (YouTube etc.) ----------------
    static readonly Regex Junk = new(@"\s*[\(\[][^\)\]]*(official|video|lyrics?|letra|audio|áudio|clipe|visuali[sz]er|hd|4k|remaster|ao vivo|live)[^\)\]]*[\)\]]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static (string title, string artist) Clean(string title, string artist)
    {
        string t = Junk.Replace(title, "").Trim();
        string a = Regex.Replace(artist ?? "", @"\s*-\s*Topic$|VEVO$", "", RegexOptions.IgnoreCase).Trim();
        int dash = t.IndexOf(" - ", StringComparison.Ordinal);
        if (dash > 0 && (a.Length == 0 || t.StartsWith(a, StringComparison.OrdinalIgnoreCase) || a.Length > 0 && dash < t.Length - 3))
        {
            // "Artista - Música" no título: usa essa divisão
            string left = t[..dash].Trim(), right = t[(dash + 3)..].Trim();
            if (a.Length == 0 || left.Contains(a, StringComparison.OrdinalIgnoreCase) || a.Contains(left, StringComparison.OrdinalIgnoreCase)) { a = left; t = right; }
        }
        t = Regex.Replace(t, @"\s*(feat\.?|ft\.?)\s.*$", "", RegexOptions.IgnoreCase).Trim();
        return (t, a);
    }

    // ---------------- cache em disco ----------------
    sealed class Cached { public string Kind { get; set; } = "none"; public string? Text { get; set; } }

    static string CachePath(string key) => Path.Combine(CacheDir, Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(key)))[..16] + ".json");

    static bool TryCache(string key, out Cached? c)
    {
        c = null;
        try
        {
            var p = CachePath(key);
            if (!File.Exists(p)) return false;
            // "não achou" expira em 3 dias (a letra pode ser cadastrada depois)
            c = JsonSerializer.Deserialize<Cached>(File.ReadAllText(p), Json);
            if (c == null) return false;
            if (c.Kind == "none" && (DateTime.Now - File.GetLastWriteTime(p)).TotalDays > 3) return false;
            return true;
        }
        catch { return false; }
    }

    static void SaveCache(string key, Cached c)
    {
        try { Directory.CreateDirectory(CacheDir); File.WriteAllText(CachePath(key), JsonSerializer.Serialize(c)); }
        catch (Exception e) { Log.Write(e); }
    }
}
