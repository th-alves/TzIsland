using System.IO;
using System.IO.Compression;

namespace TzIsland.Tests;

/// <summary>
/// Runner de testes sem dependência externa (`dotnet run --project Tests`).
/// Cobre o que é testável sem janela: física das molas, perfis de intensidade, geometria do notch,
/// cascata radial, backup (incluindo zip malicioso) e o registro de diagnóstico.
/// </summary>
static class Program
{
    static int _pass, _fail;

    static void Check(string name, bool ok, string? detail = null)
    {
        if (ok) { _pass++; Console.WriteLine($"  ok   {name}"); }
        else { _fail++; Console.WriteLine($"  FAIL {name}{(detail is null ? "" : " — " + detail)}"); }
    }

    static int Main()
    {
        Springs();
        Intensity();
        Notch();
        Radial();
        BackupRoundTrip();
        BackupRejectsTraversal();
        DiagnosticsGroups();
        Console.WriteLine($"\n{_pass} passaram, {_fail} falharam");
        return _fail == 0 ? 0 : 1;
    }

    static void Springs()
    {
        Console.WriteLine("molas");
        var s = new Spring(0);
        s.To(100, 0, 320, 24);
        for (int i = 0; i < 600; i++) s.Step(1 / 60.0);
        Check("assenta no alvo", s.Settled && Math.Abs(s.Value - 100) < 0.001, $"valor {s.Value:0.###}");

        var o = new Spring(0);
        o.To(100, 0, 320, 8);                      // bem sub-amortecida
        double peak = 0;
        for (int i = 0; i < 900; i++) { o.Step(1 / 60.0); peak = Math.Max(peak, o.Value); }
        Check("passa do ponto e volta", peak > 105 && o.Settled, $"pico {peak:0.#}");

        var d = new Spring(0);
        d.To(1, 0.2, 320, 24);
        for (int i = 0; i < 6; i++) d.Step(1 / 60.0);
        Check("atraso segura o valor", Math.Abs(d.Value) < 0.001, $"valor {d.Value:0.###}");

        var c = new Spring(50);
        c.To(100); c.Step(0.05);
        double mid = c.Value; c.Cancel();
        Check("cancelar preserva o valor visual", Math.Abs(c.Value - mid) < 0.001 && Math.Abs(c.Velocity) < 0.001);
    }

    static void Intensity()
    {
        Console.WriteLine("intensidade");
        var s = Settings.Current;
        var old = s.MotionIntensity;
        s.MotionIntensity = MotionIntensity.Natural; var nat = AnimProfiles.Get(IslandAnim.Fluid);
        s.MotionIntensity = MotionIntensity.Discreet; var dis = AnimProfiles.Get(IslandAnim.Fluid);
        s.MotionIntensity = MotionIntensity.Expressive; var exp = AnimProfiles.Get(IslandAnim.Fluid);
        s.MotionIntensity = old;

        Check("Natural preserva o preset", Math.Abs(nat.YStart - 14) < 1e-9 && Math.Abs(nat.RevealStep - 0.055) < 1e-9);
        Check("Discreto reduz o deslocamento", dis.YStart < nat.YStart, $"{dis.YStart:0.###} < {nat.YStart:0.###}");
        Check("Expressivo amplia o deslocamento", exp.YStart > nat.YStart);
        Check("Discreto aproxima da identidade", Math.Abs(1 - dis.SStart) < Math.Abs(1 - nat.SStart));
        Check("Discreto encurta a cascata", dis.RevealStep < nat.RevealStep);
        Check("Expressivo alonga a cascata", exp.RevealStep > nat.RevealStep);
        Check("estilo inválido cai no Fluid", AnimProfiles.Get((IslandAnim)99).Name == AnimProfiles.Fluid.Name);
    }

    static void Notch()
    {
        Console.WriteLine("geometria");
        var (fill, line) = NotchGeo.Make(200, 36, 11, 17);
        var b = fill.Bounds;
        Check("notch cabe na caixa", b.Left >= -0.5 && b.Top >= -0.5 && b.Right <= 200.5 && b.Bottom <= 36.5, b.ToString());
        Check("borda desenhada", line.Bounds.Width > 0 && line.Bounds.Height > 0);
        bool threw = false;
        try { NotchGeo.Make(1, 1, 11, 17); NotchGeo.Make(0, 0, 0, 0); NotchGeo.Make(4, 0.2, 2, 9); }
        catch (Exception e) { threw = true; Console.WriteLine("    " + e.Message); }
        Check("tamanhos degenerados não lançam", !threw);
    }

    static void Radial()
    {
        Console.WriteLine("cascata radial");
        Check("centro não atrasa", Tz.Radial(100, 100, 100, 100) == 0);
        Check("longe satura", Math.Abs(Tz.Radial(1000, 1000, 100, 100) - 0.22) < 1e-9);
        Check("perto atrasa menos", Tz.Radial(110, 100, 100, 100) < Tz.Radial(200, 100, 100, 100));
    }

    static string TempDir() => Path.Combine(Path.GetTempPath(), "tzi-" + Guid.NewGuid().ToString("N"));

    static void BackupRoundTrip()
    {
        Console.WriteLine("backup");
        string a = TempDir(), b = TempDir();
        Directory.CreateDirectory(a); Directory.CreateDirectory(b);
        File.WriteAllText(Path.Combine(a, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(a, "notes.json"), "notas");
        File.WriteAllText(Path.Combine(a, "log.txt"), "fora do backup");
        string zip = TempDir() + ".zip";
        Backup.ExportTo(zip, a);
        int n = Backup.ImportFrom(zip, b);
        Check("exporta 2 arquivos (sem o log)", n == 2, $"n={n}");
        Check("conteúdo preservado", File.ReadAllText(Path.Combine(b, "notes.json")) == "notas");
        Check("log fora do backup", !File.Exists(Path.Combine(b, "log.txt")));
        Directory.Delete(a, true); Directory.Delete(b, true); File.Delete(zip);
    }

    static void BackupRejectsTraversal()
    {
        Console.WriteLine("backup: zip malicioso");
        string target = TempDir();
        Directory.CreateDirectory(target);
        string zip = TempDir() + ".zip";
        bool made = true;
        try
        {
            using var z = ZipFile.Open(zip, ZipArchiveMode.Create);
            using (var w = new StreamWriter(z.CreateEntry("../escaped.txt").Open())) w.Write("x");
            using (var w = new StreamWriter(z.CreateEntry("ok.json").Open())) w.Write("{}");
        }
        catch { made = false; }

        if (!made) { Console.WriteLine("  skip (o runtime recusa criar entrada com \"..\")"); Directory.Delete(target, true); return; }

        int n = Backup.ImportFrom(zip, target);
        bool escaped = File.Exists(Path.Combine(Path.GetDirectoryName(target)!, "escaped.txt"));
        Check("ignora caminho com \"..\"", !escaped);
        Check("importa só o arquivo simples", n == 1, $"n={n}");
        Directory.Delete(target, true);
        try { File.Delete(zip); } catch { }
    }

    static void DiagnosticsGroups()
    {
        Console.WriteLine("diagnóstico");
        Diagnostics.Clear();
        for (int i = 0; i < 3; i++) Diagnostics.Record(new InvalidOperationException("mesma coisa"));
        Diagnostics.Record(new ArgumentException("outra"));
        var top = Diagnostics.Top(5);
        Check("agrupa por assinatura", top.Count == 2, $"grupos {top.Count}");
        Check("conta repetições", top[0].Count == 3, $"n={top[0].Count}");
        Check("total de erros", Diagnostics.Errors == 4);
        Check("relatório cita a exceção", Diagnostics.Report().Contains("InvalidOperationException"));
        Diagnostics.Clear();
    }
}
