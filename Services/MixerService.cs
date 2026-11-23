using NAudio.CoreAudioApi;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace TzIsland;

/// <summary>Um app no mixer. Várias sessões do mesmo programa (Chrome abre várias) viram um item só.</summary>
public sealed class MixerApp
{
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
    public ImageSource? Icon { get; init; }
    public bool IsSystem { get; init; }
    public bool Active { get; internal set; }
    public float Volume { get; internal set; }
    public bool Muted { get; internal set; }
    internal readonly List<AudioSessionControl> Sessions = new();
}

public sealed record MixerDevice(string Id, string Name, bool IsDefault);

/// <summary>
/// Mixer de volume (Core Audio via NAudio): volume e mudo de cada app que usa o som, volume geral e troca do
/// dispositivo de saída padrão. Não roda nada em segundo plano: a página chama Refresh/Peaks enquanto está aberta.
/// </summary>
public sealed class MixerService
{
    MMDeviceEnumerator? _en;
    MMDevice? _dev;
    string _devId = "";
    bool _logged;
    readonly Dictionary<int, (string Key, string Name, string? Path)?> _procs = new();
    readonly Dictionary<string, int> _firstSeen = new();
    int _seq;
    static readonly Dictionary<string, ImageSource?> IconCache = new(StringComparer.OrdinalIgnoreCase);

    public string DeviceName { get; private set; } = "";
    public bool HasDevice => _dev != null;
    public IReadOnlyList<MixerApp> Apps { get; private set; } = Array.Empty<MixerApp>();
    public IReadOnlyList<MixerDevice> Devices { get; private set; } = Array.Empty<MixerDevice>();

    MMDeviceEnumerator En => _en ??= new MMDeviceEnumerator();

    /// <summary>Relê o dispositivo padrão e as sessões de áudio (e, se pedido, a lista de saídas).</summary>
    public void Refresh(bool devices)
    {
        try
        {
            var d = En.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (d.ID != _devId) { _dev?.Dispose(); _dev = d; _devId = d.ID; DeviceName = d.FriendlyName; _procs.Clear(); }
            else d.Dispose();
        }
        catch (Exception e)
        {
            _dev = null; _devId = ""; DeviceName = ""; Apps = Array.Empty<MixerApp>();
            if (!_logged) { Log.Write(e); _logged = true; }
            if (devices) RefreshDevices();
            return;
        }

        var groups = new Dictionary<string, MixerApp>();
        var seenPids = new HashSet<int>();
        try
        {
            var mgr = _dev!.AudioSessionManager;
            mgr.RefreshSessions();
            var ss = mgr.Sessions;
            for (int i = 0; i < ss.Count; i++)
            {
                try
                {
                    var s = ss[i];
                    if (s.State == NAudio.CoreAudioApi.Interfaces.AudioSessionState.AudioSessionStateExpired) continue;
                    bool sys = s.IsSystemSoundsSession;
                    string key, name; string? path = null;
                    if (sys) { key = "#system"; name = "Sons do sistema"; }
                    else
                    {
                        int pid = (int)s.GetProcessID;
                        if (pid == 0) continue;
                        seenPids.Add(pid);
                        if (!_procs.TryGetValue(pid, out var info)) { info = ProcInfo(pid); _procs[pid] = info; }
                        if (info == null) continue;
                        (key, name, path) = info.Value;
                    }
                    if (!groups.TryGetValue(key, out var app))
                    {
                        app = new MixerApp { Key = key, Name = name, IsSystem = sys, Icon = sys ? null : IconFor(path) };
                        app.Volume = s.SimpleAudioVolume.Volume; app.Muted = s.SimpleAudioVolume.Mute;
                        groups[key] = app;
                        if (!_firstSeen.ContainsKey(key)) _firstSeen[key] = _seq++;
                    }
                    app.Sessions.Add(s);
                    if (s.State == NAudio.CoreAudioApi.Interfaces.AudioSessionState.AudioSessionStateActive) app.Active = true;
                }
                catch { /* sessão sumiu no meio da leitura */ }
            }
        }
        catch (Exception e) { if (!_logged) { Log.Write(e); _logged = true; } }

        foreach (var pid in _procs.Keys.Where(p => !seenPids.Contains(p)).ToList()) _procs.Remove(pid);   // PIDs são reutilizados
        // Ordem estável (a ordem em que cada app apareceu), para a lista não pular enquanto você mexe; sons do sistema no fim.
        Apps = groups.Values.OrderBy(a => a.IsSystem).ThenBy(a => _firstSeen[a.Key]).ToList();
        if (devices) RefreshDevices();
    }

    void RefreshDevices()
    {
        var list = new List<MixerDevice>();
        try
        {
            foreach (var d in En.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                try { list.Add(new MixerDevice(d.ID, d.FriendlyName, d.ID == _devId)); } catch { }
                finally { d.Dispose(); }
            }
        }
        catch (Exception e) { Log.Write(e); }
        Devices = list.OrderByDescending(d => d.IsDefault).ThenBy(d => d.Name).ToList();
    }

    // ---------------- Volume geral (dispositivo padrão)
    public float MasterVolume
    {
        get { try { return _dev?.AudioEndpointVolume.MasterVolumeLevelScalar ?? 0; } catch { return 0; } }
        set { try { if (_dev != null) _dev.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(value, 0f, 1f); } catch (Exception e) { Log.Write(e); } }
    }

    public bool MasterMuted
    {
        get { try { return _dev?.AudioEndpointVolume.Mute ?? false; } catch { return false; } }
        set { try { if (_dev != null) _dev.AudioEndpointVolume.Mute = value; } catch (Exception e) { Log.Write(e); } }
    }

    public float MasterPeak { get { try { return _dev?.AudioMeterInformation.MasterPeakValue ?? 0; } catch { return 0; } } }

    // ---------------- Por app
    public void SetVolume(MixerApp a, float v)
    {
        v = Math.Clamp(v, 0f, 1f);
        foreach (var s in a.Sessions) try { s.SimpleAudioVolume.Volume = v; } catch { }
        a.Volume = v;
    }

    public void SetMuted(MixerApp a, bool m)
    {
        foreach (var s in a.Sessions) try { s.SimpleAudioVolume.Mute = m; } catch { }
        a.Muted = m;
    }

    /// <summary>Lê de novo volume/mudo de um app (mudou no mixer do Windows ou no próprio app).</summary>
    public void Reread(MixerApp a)
    {
        var s = a.Sessions.FirstOrDefault();
        if (s == null) return;
        try { a.Volume = s.SimpleAudioVolume.Volume; a.Muted = s.SimpleAudioVolume.Mute; } catch { }
    }

    public float Peak(MixerApp a)
    {
        float p = 0;
        foreach (var s in a.Sessions) try { p = Math.Max(p, s.AudioMeterInformation.MasterPeakValue); } catch { }
        return p;
    }

    // ---------------- Dispositivo padrão (IPolicyConfig: a mesma interface que o Windows e o EarTrumpet usam)
    public bool SetDefaultDevice(string id)
    {
        try
        {
            var pc = (IPolicyConfig)new CPolicyConfigClient();
            try
            {
                for (int role = 0; role <= 2; role++) Marshal.ThrowExceptionForHR(pc.SetDefaultEndpoint(id, role));   // Console, Multimídia, Comunicações
            }
            finally { Marshal.ReleaseComObject(pc); }
            return true;
        }
        catch (Exception e) { Log.Write(e); return false; }
    }

    [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    class CPolicyConfigClient { }

    [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr fmt);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, int def, IntPtr fmt);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr ep, IntPtr mix);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, int def, IntPtr a, IntPtr b);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr a);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, int fx, IntPtr key, IntPtr val);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, int fx, IntPtr key, IntPtr val);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string id, int visible);
    }

    // ---------------- Nome e ícone real do app
    static (string Key, string Name, string? Path)? ProcInfo(int pid)
    {
        string? path = ImagePath(pid);
        string pname;
        try { using var p = Process.GetProcessById(pid); pname = p.ProcessName; }
        catch { if (path == null) return null; pname = System.IO.Path.GetFileNameWithoutExtension(path); }

        string name = pname.Length > 0 ? char.ToUpper(pname[0]) + pname[1..] : "Aplicativo";
        if (path != null)
        {
            try
            {
                var fd = FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim();
                if (!string.IsNullOrWhiteSpace(fd) && fd.Length <= 40) name = fd;
            }
            catch { }
        }
        return (pname.ToLowerInvariant(), name, path);
    }

    static string? ImagePath(int pid)
    {
        IntPtr h = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, pid);
        if (h == IntPtr.Zero) return null;
        try
        {
            var sb = new System.Text.StringBuilder(1024); int len = sb.Capacity;
            return QueryFullProcessImageName(h, 0, sb, ref len) ? sb.ToString() : null;
        }
        finally { CloseHandle(h); }
    }

    static ImageSource? IconFor(string? path)
    {
        if (path == null) return null;
        if (IconCache.TryGetValue(path, out var c)) return c;
        ImageSource? img = null;
        try { img = ShellIcon(path); } catch { }
        if (img == null)
        {
            try
            {
                using var ic = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (ic != null) img = Imaging.CreateBitmapSourceFromHIcon(ic.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            catch { }
        }
        img?.Freeze();
        IconCache[path] = img;
        return img;
    }

    /// <summary>Ícone 48 px do executável, o mesmo do Explorador (lista "extra grande" do shell), com transparência.</summary>
    static ImageSource? ShellIcon(string path)
    {
        var sfi = new SHFILEINFO();
        if (SHGetFileInfo(path, 0, ref sfi, (uint)Marshal.SizeOf<SHFILEINFO>(), 0x4000 /* SHGFI_SYSICONINDEX */) == IntPtr.Zero) return null;
        var iid = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
        if (SHGetImageList(2 /* SHIL_EXTRALARGE */, ref iid, out var list) != 0 || list == null) return null;
        try
        {
            if (list.GetIcon(sfi.iIcon, 1 /* ILD_TRANSPARENT */, out var hicon) != 0 || hicon == IntPtr.Zero) return null;
            try { return Imaging.CreateBitmapSourceFromHIcon(hicon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); }
            finally { DestroyIcon(hicon); }
        }
        finally { Marshal.ReleaseComObject(list); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEINFO
    {
        public IntPtr hIcon; public int iIcon; public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [ComImport, Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IImageList
    {
        [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, out int pi);
        [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, out int pi);
        [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
        [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, out int pi);
        [PreserveSig] int Draw(IntPtr pimldp);
        [PreserveSig] int Remove(int i);
        [PreserveSig] int GetIcon(int i, int flags, out IntPtr picon);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string path, uint attr, ref SHFILEINFO sfi, uint size, uint flags);
    [DllImport("shell32.dll", EntryPoint = "#727")] static extern int SHGetImageList(int list, ref Guid riid, out IImageList ppv);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr h, int flags, System.Text.StringBuilder name, ref int size);
}
