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
        BackupManifest();
        CalendarRecurrence();
        LogRotation();
        Durations();
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

    static void BackupManifest()
    {
        Console.WriteLine("backup: manifest");
        string a = TempDir(), b = TempDir();
        Directory.CreateDirectory(a); Directory.CreateDirectory(b);
        File.WriteAllText(Path.Combine(a, "notes.json"), "n");
        string zip = TempDir() + ".zip";
        Backup.ExportTo(zip, a);

        using (var z = ZipFile.OpenRead(zip))
            Check("todo backup tem manifest", z.GetEntry("manifest.json") != null);
        int n = Backup.ImportFrom(zip, b);
        Check("manifest não entra como dado", n == 1, $"n={n}");
        Check("importação informa o que leu", !string.IsNullOrEmpty(Backup.LastMessage));

        // Esquema mais novo deve recusar tudo (importação parcial silenciosa é pior que nenhuma).
        string newer = TempDir() + ".zip";
        using (var z = ZipFile.Open(newer, ZipArchiveMode.Create))
        {
            using (var w = new StreamWriter(z.CreateEntry("manifest.json").Open()))
                w.Write("{\"Schema\":999,\"App\":\"TzIsland\",\"Version\":\"9\",\"Files\":1,\"Names\":[]}");
            using (var w = new StreamWriter(z.CreateEntry("notes.json").Open())) w.Write("novo");
        }
        string c = TempDir(); Directory.CreateDirectory(c);
        Check("esquema mais novo é recusado", Backup.ImportFrom(newer, c) == 0);
        Check("nada foi sobrescrito", !File.Exists(Path.Combine(c, "notes.json")));
        Directory.Delete(a, true); Directory.Delete(b, true); Directory.Delete(c, true);
        try { File.Delete(zip); File.Delete(newer); } catch { }
    }

    static string Ics(string body) => "BEGIN:VCALENDAR\r\n" + body + "END:VCALENDAR\r\n";

    static void CalendarRecurrence()
    {
        Console.WriteLine("agenda: recorrência e fuso");
        var weekly = Ics("BEGIN:VEVENT\r\nUID:w1\r\nSUMMARY:Semanal\r\nDTSTART:20260302T090000\r\nDTEND:20260302T100000\r\nRRULE:FREQ=WEEKLY;BYDAY=MO,WE,FR\r\nEND:VEVENT\r\n");
        var a = CalendarService.ParseIcs(weekly, "t", new DateTime(2026, 3, 2), new DateTime(2026, 3, 9));
        Check("semanal com BYDAY gera 3 ocorrências", a.Count == 3, $"n={a.Count}");
        Check("horário preservado", a.All(e => e.Start.Hour == 9 && e.End.Hour == 10));

        var counted = Ics("BEGIN:VEVENT\r\nUID:c1\r\nDTSTART:20260302T090000\r\nRRULE:FREQ=DAILY;COUNT=2\r\nEND:VEVENT\r\n");
        Check("COUNT limita as ocorrências", CalendarService.ParseIcs(counted, "t", new DateTime(2026, 3, 1), new DateTime(2026, 3, 20)).Count == 2);

        var withEx = Ics("BEGIN:VEVENT\r\nUID:x1\r\nDTSTART:20260302T090000\r\nRRULE:FREQ=WEEKLY;BYDAY=MO,WE,FR\r\nEXDATE:20260304T090000\r\nEND:VEVENT\r\n");
        Check("EXDATE remove a ocorrência", CalendarService.ParseIcs(withEx, "t", new DateTime(2026, 3, 2), new DateTime(2026, 3, 7)).Count == 2);

        var moved = Ics("BEGIN:VEVENT\r\nUID:r1\r\nDTSTART:20260302T090000\r\nDTEND:20260302T093000\r\nRRULE:FREQ=WEEKLY\r\nEND:VEVENT\r\n"
            + "BEGIN:VEVENT\r\nUID:r1\r\nRECURRENCE-ID:20260309T090000\r\nDTSTART:20260309T140000\r\nDTEND:20260309T143000\r\nEND:VEVENT\r\n");
        var m = CalendarService.ParseIcs(moved, "t", new DateTime(2026, 3, 2), new DateTime(2026, 3, 10));
        Check("remarcação substitui a ocorrência", m.Count == 2, $"n={m.Count}");
        Check("remarcação muda o horário", m.Any(e => e.Start.Hour == 14 && e.Start.Day == 9));

        bool iana = true;
        try { TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); } catch { iana = false; }
        if (iana)
        {
            var tz = Ics("BEGIN:VEVENT\r\nUID:z1\r\nDTSTART;TZID=America/Sao_Paulo:20260101T090000\r\nEND:VEVENT\r\n");
            var e = CalendarService.ParseIcs(tz, "t", new DateTime(2025, 12, 31), new DateTime(2026, 1, 3));
            var expected = TimeZoneInfo.ConvertTimeFromUtc(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc), TimeZoneInfo.Local);
            Check("TZID converte para o fuso local", e.Count == 1 && Math.Abs((e[0].Start - expected).TotalMinutes) < 1, e.Count == 1 ? e[0].Start.ToString("s") : "sem evento");
        }
        else Console.WriteLine("  skip TZID (runtime sem fusos IANA)");
    }

    static void LogRotation()
    {
        Console.WriteLine("log: rotação");
        string dir = TempDir(); Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "log.txt");
        File.WriteAllText(path, "a");
        Log.Rotate(path);
        File.WriteAllText(path, "b");
        Log.Rotate(path);
        Check("vira log.1.txt", File.Exists(path + ".1"));
        Check("mantém no máximo dois arquivos girados", File.Exists(path + ".2") && !File.Exists(path + ".3"));
        Check("o arquivo atual foi movido, não apagado", !File.Exists(path));
        Directory.Delete(dir, true);
    }

    static void Durations()
    {
        Console.WriteLine("CLI: durações");
        bool ok;
        ok = Cli.TryDuration("25m", out var a); Check("25m", ok && a == 1500, $"{a}");
        ok = Cli.TryDuration("1h30m", out var b); Check("1h30m", ok && b == 5400, $"{b}");
        ok = Cli.TryDuration("90s", out var c); Check("90s", ok && c == 90, $"{c}");
        ok = Cli.TryDuration("45", out var d); Check("45 (minutos)", ok && d == 2700, $"{d}");
        Check("texto inválido", !Cli.TryDuration("abc", out _));
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
