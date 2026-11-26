using System.Runtime.InteropServices;

namespace TzIsland;

/// <summary>CPU, RAM e atividade do disco (%) via chamadas nativas leves. Só amostra quando alguém chama Sample().</summary>
public sealed class SysStats
{
    [StructLayout(LayoutKind.Sequential)]
    struct FT { public uint Lo, Hi; public ulong V => ((ulong)Hi << 32) | Lo; }

    [StructLayout(LayoutKind.Sequential)]
    struct MEM { public uint Length, Load; public ulong TotalPhys, AvailPhys, TotalPage, AvailPage, TotalVirt, AvailVirt, AvailExt; }

    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out FT idle, out FT kernel, out FT user);
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MEM m);

    // Atividade do disco (o "Disco %" do Gerenciador de Tarefas): PhysicalDisk % Idle Time via PDH.
    // PdhAddEnglishCounter usa o nome em inglês, então funciona em Windows de qualquer idioma.
    [StructLayout(LayoutKind.Sequential)] struct PDH_VAL { public uint Status; public double Value; }
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] static extern uint PdhOpenQuery(string? src, IntPtr user, out IntPtr q);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] static extern uint PdhAddEnglishCounter(IntPtr q, string path, IntPtr user, out IntPtr c);
    [DllImport("pdh.dll")] static extern uint PdhCollectQueryData(IntPtr q);
    [DllImport("pdh.dll")] static extern uint PdhGetFormattedCounterValue(IntPtr c, uint fmt, IntPtr type, out PDH_VAL v);
    const uint PDH_FMT_DOUBLE = 0x200, PDH_FMT_NOCAP100 = 0x8000;
    IntPtr _pdhQ, _pdhIdle;
    bool _pdhTried, _pdhPrimed;

    /// <summary>true quando Disk é atividade real; false se o contador não existir (aí cai no espaço ocupado).</summary>
    public bool DiskIsActivity { get; private set; }

    bool SampleDiskActivity()
    {
        if (!_pdhTried)
        {
            _pdhTried = true;
            if (PdhOpenQuery(null, IntPtr.Zero, out _pdhQ) != 0) { _pdhQ = IntPtr.Zero; return false; }
            if (PdhAddEnglishCounter(_pdhQ, @"\PhysicalDisk(_Total)\% Idle Time", IntPtr.Zero, out _pdhIdle) != 0) { _pdhIdle = IntPtr.Zero; return false; }
        }
        if (_pdhQ == IntPtr.Zero || _pdhIdle == IntPtr.Zero) return false;
        if (PdhCollectQueryData(_pdhQ) != 0) return false;
        if (!_pdhPrimed) { _pdhPrimed = true; Disk = 0; return true; }   // contador de taxa: a 1ª leitura só serve de base
        if (PdhGetFormattedCounterValue(_pdhIdle, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, IntPtr.Zero, out var v) != 0 || v.Status > 1) return true;
        Disk = Math.Clamp(100.0 - v.Value, 0, 100);
        return true;
    }

    ulong _idle, _total;
    bool _have;

    public double Cpu, Ram, Disk;

    public void Sample()
    {
        try
        {
            if (GetSystemTimes(out var i, out var k, out var u))
            {
                ulong idle = i.V, total = k.V + u.V;            // o tempo de "kernel" já inclui o ocioso
                if (_have && total > _total) Cpu = Math.Clamp(100.0 * (1.0 - (double)(idle - _idle) / (total - _total)), 0, 100);
                _idle = idle; _total = total; _have = true;
            }
            var m = new MEM { Length = (uint)Marshal.SizeOf<MEM>() };
            if (GlobalMemoryStatusEx(ref m)) Ram = m.Load;
            DiskIsActivity = SampleDiskActivity();
            var root = Path.GetPathRoot(Environment.SystemDirectory);
            if (!DiskIsActivity && !string.IsNullOrEmpty(root))
            {
                var d = new DriveInfo(root);
                if (d.IsReady && d.TotalSize > 0) Disk = 100.0 * (d.TotalSize - d.TotalFreeSpace) / d.TotalSize;
            }
        }
        catch (Exception e) { Log.Write(e); }
    }
}
