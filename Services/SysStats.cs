using System.Runtime.InteropServices;

namespace TzIsland;

/// <summary>CPU, RAM e disco (%) via chamadas nativas leves. Só amostra quando alguém chama Sample().</summary>
public sealed class SysStats
{
    [StructLayout(LayoutKind.Sequential)]
    struct FT { public uint Lo, Hi; public ulong V => ((ulong)Hi << 32) | Lo; }

    [StructLayout(LayoutKind.Sequential)]
    struct MEM { public uint Length, Load; public ulong TotalPhys, AvailPhys, TotalPage, AvailPage, TotalVirt, AvailVirt, AvailExt; }

    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out FT idle, out FT kernel, out FT user);
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MEM m);

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
            var root = Path.GetPathRoot(Environment.SystemDirectory);
            if (!string.IsNullOrEmpty(root))
            {
                var d = new DriveInfo(root);
                if (d.IsReady && d.TotalSize > 0) Disk = 100.0 * (d.TotalSize - d.TotalFreeSpace) / d.TotalSize;
            }
        }
        catch (Exception e) { Log.Write(e); }
    }
}
