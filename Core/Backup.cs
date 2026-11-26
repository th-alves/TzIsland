using System.IO.Compression;
using System.Text;

namespace TzIsland;

/// <summary>
/// Backup dos dados locais (ajustes, notas, tarefas, água, clipboard fixado) em um .zip.
/// Três lacunas fechadas: um backup automático diário (ninguém lembra de exportar), um manifest
/// com a versão do esquema (importar um backup antigo num formato novo precisa avisar, não falhar
/// no meio) e a recusa de entradas com caminho relativo (zip é entrada não confiável).
/// </summary>
public static class Backup
{
    /// <summary>Versão do FORMATO do backup. Suba junto quando o formato dos arquivos mudar.</summary>
    public const int SchemaVersion = 2;
    const string ManifestName = "manifest.json";
    const int KeepAuto = 7;

    public sealed record Manifest(int Schema, string App, string Version, DateTime CreatedUtc, int Files, string[] Names);

    /// <summary>Resultado da última importação (nulo = nunca importou). A UI mostra isso ao usuário.</summary>
    public static string? LastMessage { get; private set; }

    /// <summary>Exporta com diálogo. Devolve o caminho salvo ("" se cancelado ou falhou).</summary>
    public static string Export()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exportar dados do TzIsland",
            Filter = "Backup do TzIsland (*.zip)|*.zip",
            FileName = $"TzIsland-dados-{DateTime.Now:yyyy-MM-dd}.zip"
        };
        if (dlg.ShowDialog() != true) return "";
        try { ExportTo(dlg.FileName); return dlg.FileName; }
        catch (Exception e) { Log.Write(e); LastMessage = "Falha ao exportar: " + e.Message; return ""; }
    }

    static bool Skipped(string name) =>
        name.Equals("log.txt", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("log.", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("bench.txt", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase);

    public static void ExportTo(string zipPath, string? dataDir = null)
    {
        string dir = dataDir ?? Settings.DataDir;
        if (File.Exists(zipPath)) File.Delete(zipPath);
        var files = Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir).Select(Path.GetFileName).Where(n => n != null && !Skipped(n!)).Select(n => n!).ToList()
            : new List<string>();

        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        // Manifest primeiro: um leitor sabe o que esperar antes de extrair qualquer coisa.
        var manifest = new Manifest(SchemaVersion, "TzIsland", Version(), DateTime.UtcNow, files.Count, files.ToArray());
        using (var w = new StreamWriter(zip.CreateEntry(ManifestName, CompressionLevel.Optimal).Open(), Encoding.UTF8))
            w.Write(JsonSerializer.Serialize(manifest, Settings.JsonOpt));

        foreach (var name in files)
            zip.CreateEntryFromFile(Path.Combine(dir, name), name, CompressionLevel.Optimal);
    }

    static string Version()
    {
        try { return typeof(Backup).Assembly.GetName().Version?.ToString(3) ?? "?"; }
        catch { return "?"; }
    }

    /// <summary>Importa com diálogo. Devolve quantos arquivos entraram (0 se cancelado/falhou).</summary>
    public static int Import()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Importar dados do TzIsland", Filter = "Backup do TzIsland (*.zip)|*.zip" };
        if (dlg.ShowDialog() != true) return 0;
        try { return ImportFrom(dlg.FileName); }
        catch (Exception e) { Log.Write(e); LastMessage = "Falha ao importar: " + e.Message; return 0; }
    }

    /// <summary>
    /// Extrai apenas arquivos soltos na raiz da pasta de dados: nada de subpastas, nada de "..".
    /// Lê o manifest antes: esquema mais novo que este app → recusa tudo (importação parcial
    /// silenciosa é pior que nenhuma importação).
    /// </summary>
    public static int ImportFrom(string zipPath, string? dataDir = null)
    {
        string dir = dataDir ?? Settings.DataDir;
        using var zip = ZipFile.OpenRead(zipPath);

        var manifestEntry = zip.GetEntry(ManifestName);
        if (manifestEntry != null)
        {
            try
            {
                using var r = new StreamReader(manifestEntry.Open(), Encoding.UTF8);
                var m = JsonSerializer.Deserialize<Manifest>(r.ReadToEnd(), Settings.JsonOpt);
                if (m != null && m.Schema > SchemaVersion)
                {
                    LastMessage = $"Este backup é do esquema {m.Schema}, mais novo que o suportado ({SchemaVersion}). Atualize o TzIsland antes de importar.";
                    return 0;
                }
                if (m != null) LastMessage = $"Backup de {m.CreatedUtc.ToLocalTime():dd/MM/yyyy HH:mm} (esquema {m.Schema}, {m.Files} arquivo(s)).";
            }
            catch (Exception e) { Log.Write(e); }
        }
        else LastMessage = "Backup sem manifest (formato antigo): importando o que for possível.";

        Directory.CreateDirectory(dir);
        int n = 0;
        foreach (var entry in zip.Entries)
        {
            string name = entry.Name;
            if (string.IsNullOrEmpty(name)) continue;
            if (name.Equals(ManifestName, StringComparison.OrdinalIgnoreCase)) continue;
            if (entry.FullName != name) continue;                 // subpasta/segmento extra: ignora
            if (name.Contains("..", StringComparison.Ordinal)) continue;
            string target = Path.GetFullPath(Path.Combine(dir, name));
            if (!target.StartsWith(Path.GetFullPath(dir) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            entry.ExtractToFile(target, true);
            n++;
        }
        LastMessage = (LastMessage is { Length: > 0 } msg ? msg + " " : "") + $"{n} arquivo(s) importado(s).";
        return n;
    }

    /// <summary>
    /// Um backup por dia em %AppData%\TzIsland\backups, mantendo os 7 últimos. Roda no início do app;
    /// se já existe o do dia, não faz nada. Devolve o caminho criado (ou nulo).
    /// </summary>
    public static string? AutoBackupIfDue()
    {
        if (!Settings.Current.AutoBackup) return null;
        try
        {
            string dir = Path.Combine(Settings.DataDir, "backups");
            Directory.CreateDirectory(dir);
            string target = Path.Combine(dir, $"auto-{DateTime.Now:yyyyMMdd}.zip");
            if (File.Exists(target)) return null;
            ExportTo(target);
            foreach (var old in Directory.GetFiles(dir, "auto-*.zip").OrderByDescending(f => f).Skip(KeepAuto))
                try { File.Delete(old); } catch { }
            return target;
        }
        catch (Exception e) { Log.Write(e); return null; }
    }
}
