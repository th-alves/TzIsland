using System.IO.Compression;

namespace TzIsland;

/// <summary>
/// Backup dos dados locais (ajustes, notas, tarefas, água, clipboard fixado) em um .zip.
/// Tudo vive em %AppData%\TzIsland e não havia nenhuma forma de levar isso para outra máquina.
/// </summary>
public static class Backup
{
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
        catch (Exception e) { Log.Write(e); return ""; }
    }

    public static void ExportTo(string zipPath, string? dataDir = null)
    {
        string dir = dataDir ?? Settings.DataDir;
        if (File.Exists(zipPath)) File.Delete(zipPath);
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        if (!Directory.Exists(dir)) return;
        foreach (var f in Directory.EnumerateFiles(dir))
        {
            string name = Path.GetFileName(f);
            if (name.Equals("log.txt", StringComparison.OrdinalIgnoreCase)) continue;   // diagnóstico fica fora do backup
            zip.CreateEntryFromFile(f, name, CompressionLevel.Optimal);
        }
    }

    /// <summary>Importa com diálogo. Devolve quantos arquivos entraram (0 se cancelado/falhou).</summary>
    public static int Import()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Importar dados do TzIsland", Filter = "Backup do TzIsland (*.zip)|*.zip" };
        if (dlg.ShowDialog() != true) return 0;
        try { return ImportFrom(dlg.FileName); }
        catch (Exception e) { Log.Write(e); return 0; }
    }

    /// <summary>
    /// Extrai apenas arquivos soltos na raiz da pasta de dados: nada de subpastas, nada de "..".
    /// (Um zip é entrada não confiável; extrair caminhos arbitrários permitiria escrever fora da pasta.)
    /// </summary>
    public static int ImportFrom(string zipPath, string? dataDir = null)
    {
        string dir = dataDir ?? Settings.DataDir;
        Directory.CreateDirectory(dir);
        using var zip = ZipFile.OpenRead(zipPath);
        int n = 0;
        foreach (var entry in zip.Entries)
        {
            string name = entry.Name;
            if (string.IsNullOrEmpty(name)) continue;
            if (entry.FullName != name) continue;                 // subpasta/segmento extra: ignora
            if (name.Contains("..", StringComparison.Ordinal)) continue;
            string target = Path.GetFullPath(Path.Combine(dir, name));
            if (!target.StartsWith(Path.GetFullPath(dir) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            entry.ExtractToFile(target, true);
            n++;
        }
        return n;
    }
}
