using System.Reflection;
using System.Text;

namespace TzIsland;

/// <summary>
/// Observabilidade local: as exceções do app viravam só um txt que ninguém lê, e um erro engolido pelo
/// handler global podia degradar a ilha em silêncio (foi assim que um Math.Clamp abortava o quadro).
/// Aqui elas viram contadores agrupados por assinatura, últimos casos e um relatório copiável.
/// </summary>
public static class Diagnostics
{
    public sealed record Entry(DateTime At, string Signature, string Detail);

    const int Cap = 40;
    static readonly List<Entry> _items = new();
    static readonly Dictionary<string, int> _counts = new();
    static readonly object _lock = new();
    static readonly DateTime _start = DateTime.Now;

    public static long Errors { get; private set; }
    public static TimeSpan Uptime => DateTime.Now - _start;

    public static void Record(Exception e)
    {
        try
        {
            string sig = $"{e.GetType().Name}: {Short(e.Message)}";
            lock (_lock)
            {
                Errors++;
                _counts[sig] = _counts.TryGetValue(sig, out var n) ? n + 1 : 1;
                _items.Insert(0, new Entry(DateTime.Now, sig, e.ToString()));
                if (_items.Count > Cap) _items.RemoveAt(Cap);
            }
        }
        catch { /* diagnóstico nunca pode derrubar o app */ }
    }

    static string Short(string s) => s.Length <= 110 ? s : s[..110] + "…";

    /// <summary>Assinaturas mais frequentes (mais casos primeiro).</summary>
    public static IReadOnlyList<(string Signature, int Count)> Top(int max = 8)
    {
        lock (_lock) return _counts.OrderByDescending(kv => kv.Value).Take(max).Select(kv => (kv.Key, kv.Value)).ToList();
    }

    public static IReadOnlyList<Entry> Recent(int max = 8)
    {
        lock (_lock) return _items.Take(max).ToList();
    }

    public static void Clear()
    {
        lock (_lock) { _items.Clear(); _counts.Clear(); Errors = 0; }
    }

    /// <summary>Texto pronto para colar numa issue (sem caminhos do perfil do usuário).</summary>
    public static string Report()
    {
        var sb = new StringBuilder();
        var s = Settings.Current;
        sb.AppendLine($"TzIsland {Version()} · Windows {Environment.OSVersion.Version} · .NET {Environment.Version}");
        sb.AppendLine($"sessão há {Uptime:h\\:mm\\:ss} · erros {Errors}");
        sb.AppendLine($"estilo {s.Style}/{s.Look} · animação {s.IslandAnim}/{s.MotionIntensity} · sombra {s.WindowShadow}");
        sb.AppendLine($"tamanhos {s.ExpandedWidth:0}x{s.ExpandedHeight:0} / {s.CollapsedWidth:0}x{s.CollapsedHeight:0} · módulos {Modules.All.Count(m => s.IsOn(m.Id))}/{Modules.All.Length}");
        sb.AppendLine(MotionStats.Line());
        sb.AppendLine(Perf.Line());
        sb.AppendLine($"tracks ativas {MotionEngine.ActiveCount}" + (Eco.Active ? $" · economia ligada ({Eco.Reason})" : ""));
        sb.AppendLine();
        sb.AppendLine("— erros agrupados —");
        var top = Top(10);
        if (top.Count == 0) sb.AppendLine("nenhum");
        foreach (var (sig, n) in top) sb.AppendLine($"{n,3}x {sig}");
        var recent = Recent(3);
        if (recent.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("— últimos detalhes —");
            foreach (var e in recent) sb.AppendLine($"[{e.At:HH:mm:ss}] {e.Detail.Split('\n').FirstOrDefault()}");
        }
        return sb.ToString();
    }

    static string Version()
    {
        try { return Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "?"; }
        catch { return "?"; }
    }
}
