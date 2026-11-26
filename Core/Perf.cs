using System.Runtime.InteropServices;

namespace TzIsland;

/// <summary>
/// Medição de custo em repouso (Etapa 24): CPU médio do processo, RAM privada e p95 de quadro.
/// Antes, "não medido" era a resposta para toda decisão de desempenho — inclusive o teto de quadros
/// que custou fluidez. Aqui os três números ficam disponíveis para o Diagnóstico e para o modo
/// <c>--bench N</c>, que mede sem depender de alguém olhando a tela.
/// </summary>
public static class Perf
{
    static readonly Process P = Process.GetCurrentProcess();
    static TimeSpan _cpuAt;
    static DateTime _at;
    static bool _primed;

    /// <summary>CPU do processo (0–100, média do intervalo entre duas amostras).</summary>
    public static double CpuPercent { get; private set; }
    /// <summary>Memória privada residente (MB).</summary>
    public static double RamMb { get; private set; }
    public static double P95 => MotionStats.Percentile(0.95);

    /// <summary>Marca o início do intervalo de CPU. Chame antes de dormir um tempo.</summary>
    public static void Prime()
    {
        try
        {
            P.Refresh();
            _cpuAt = P.TotalProcessorTime;
            _at = DateTime.UtcNow;
            RamMb = P.PrivateMemorySize64 / 1048576.0;
            _primed = true;
        }
        catch { }
    }

    /// <summary>Fecha o intervalo e atualiza as três medidas. Custa menos que um refresh de WMI.</summary>
    public static void Sample()
    {
        if (!_primed) { Prime(); return; }
        try
        {
            P.Refresh();
            var now = DateTime.UtcNow;
            double s = (now - _at).TotalSeconds;
            if (s > 0.2)
            {
                double used = (P.TotalProcessorTime - _cpuAt).TotalSeconds;
                CpuPercent = Math.Clamp(used / s / Math.Max(1, Environment.ProcessorCount) * 100, 0, 100);
                _cpuAt = P.TotalProcessorTime;
                _at = now;
            }
            RamMb = P.PrivateMemorySize64 / 1048576.0;
        }
        catch { }
    }

    public static string Line() => $"CPU {CpuPercent:0.0}% · RAM {RamMb:0} MB · p95 {P95:0.0} ms";

    /// <summary>
    /// Saída de console para <c>--bench</c>: o app é WinExe, então sem isso o Console.WriteLine
    /// não aparece quando ele é chamado de um terminal.
    /// </summary>
    public static void AttachConsole()
    {
        try
        {
            if (!AttachConsoleNative(-1)) return;
            var w = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            Console.SetOut(w);
        }
        catch { /* sem console: o bench.txt continua sendo gravado */ }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool AttachConsoleNative(int processId);
}
