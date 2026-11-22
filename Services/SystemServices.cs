using System.Management;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;

namespace TzIsland;

public sealed class BatteryService
{
    public int Percent = -1;
    public bool Charging, HasBattery;
    public event Action? Changed;
    readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromSeconds(30) };
    int _lastNotified = 100;

    public void Start()
    {
        _t.Tick += (_, _) => Poll();
        _t.Start();
        Poll();
    }

    void Poll()
    {
        if (!Native.GetSystemPowerStatus(out var s)) return;
        bool has = (s.BatteryFlag & 128) == 0 && s.BatteryLifePercent != 255;
        int pct = has ? s.BatteryLifePercent : -1;
        bool chg = s.ACLineStatus == 1;
        if (has != HasBattery || pct != Percent || chg != Charging)
        {
            HasBattery = has; Percent = pct; Charging = chg;
            Changed?.Invoke();
        }
        if (has && !chg)
        {
            if (pct <= 10 && _lastNotified > 10) Hub.Notify("Bateria", "Bateria crítica", $"{pct}% restantes. Conecte o carregador.", true);
            else if (pct <= 20 && _lastNotified > 20) Hub.Notify("Bateria", "Bateria baixa", $"{pct}% restantes.", false);
            _lastNotified = pct;
        }
        else _lastNotified = 100;
    }
}

/// <summary>Volume do sistema e detecção de uso do microfone (Core Audio via NAudio).</summary>
public sealed class AudioService
{
    readonly MMDeviceEnumerator _en = new();
    MMDevice? _render;
    public event Action? VolumeChanged;
    public event Action? MicChanged;
    public IReadOnlyList<string> MicApps { get; private set; } = Array.Empty<string>();
    public bool MicInUse => MicApps.Count > 0;
    readonly DispatcherTimer _mic = new() { Interval = TimeSpan.FromSeconds(4) };
    bool _busy;

    public void Start()
    {
        try
        {
            _render = _en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _render.AudioEndpointVolume.OnVolumeNotification += _ => Application.Current.Dispatcher.InvokeAsync(() => VolumeChanged?.Invoke());
        }
        catch (Exception e) { Log.Write(e); }
        _mic.Tick += async (_, _) => await PollMicAsync();
        _mic.Start();
    }

    public float Volume
    {
        get { try { return _render?.AudioEndpointVolume.MasterVolumeLevelScalar ?? 0; } catch { return 0; } }
        set { try { if (_render != null) _render.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(value, 0f, 1f); } catch (Exception e) { Log.Write(e); } }
    }

    public bool Muted
    {
        get { try { return _render?.AudioEndpointVolume.Mute ?? false; } catch { return false; } }
        set { try { if (_render != null) _render.AudioEndpointVolume.Mute = value; } catch (Exception e) { Log.Write(e); } }
    }

    async Task PollMicAsync()
    {
        if (_busy || !Settings.Current.MonitorMicrophone)
        {
            if (!Settings.Current.MonitorMicrophone && MicApps.Count > 0) { MicApps = Array.Empty<string>(); MicChanged?.Invoke(); }
            return;
        }
        _busy = true;
        try
        {
            var apps = await Task.Run(() =>
            {
                var set = new SortedSet<string>();
                try
                {
                    using var en = new MMDeviceEnumerator();
                    foreach (var dev in en.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
                    {
                        var mgr = dev.AudioSessionManager;
                        mgr.RefreshSessions();
                        var ss = mgr.Sessions;
                        for (int i = 0; i < ss.Count; i++)
                        {
                            var s = ss[i];
                            if (s.State != AudioSessionState.AudioSessionStateActive) continue;
                            uint pid = s.GetProcessID;
                            if (pid == 0) continue;
                            try { set.Add(Process.GetProcessById((int)pid).ProcessName); } catch { set.Add("Aplicativo"); }
                        }
                    }
                }
                catch (Exception e) { Log.Write(e); }
                return set.ToList();
            });
            if (!apps.SequenceEqual(MicApps)) { MicApps = apps; MicChanged?.Invoke(); }
        }
        finally { _busy = false; }
    }
}

public sealed class DisplayDevice
{
    public string Name = "";
    public bool IsInternal;
    public IntPtr Handle;
    public uint Min, Max, Cur;
    public bool HasContrast;
    public uint CMin, CMax, CCur;
}

/// <summary>Brilho: tela interna via WMI; monitores externos via DDC/CI (dxva2).</summary>
public sealed class BrightnessService
{
    public List<DisplayDevice> Devices { get; private set; } = new();
    readonly List<Native.PHYSICAL_MONITOR[]> _handles = new();

    public async Task RefreshAsync()
    {
        var list = await Task.Run(() =>
        {
            var l = new List<DisplayDevice>();
            foreach (var h in _handles) { try { Native.DestroyPhysicalMonitors((uint)h.Length, h); } catch { } }
            _handles.Clear();
            try
            {
                using var s = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
                foreach (ManagementObject o in s.Get())
                {
                    l.Add(new DisplayDevice { Name = "Tela interna", IsInternal = true, Min = 0, Max = 100, Cur = Convert.ToUInt32(o["CurrentBrightness"]) });
                    break;
                }
            }
            catch { /* sem painel interno controlável */ }

            var mons = new List<IntPtr>();
            Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (h, _, _, _) => { mons.Add(h); return true; }, IntPtr.Zero);
            foreach (var hm in mons)
            {
                try
                {
                    if (!Native.GetNumberOfPhysicalMonitorsFromHMONITOR(hm, out uint n) || n == 0) continue;
                    var arr = new Native.PHYSICAL_MONITOR[n];
                    if (!Native.GetPhysicalMonitorsFromHMONITOR(hm, n, arr)) continue;
                    _handles.Add(arr);
                    foreach (var pm in arr)
                    {
                        if (!Native.GetMonitorBrightness(pm.hPhysicalMonitor, out uint mn, out uint cur, out uint mx)) continue;
                        var d = new DisplayDevice
                        {
                            Name = string.IsNullOrWhiteSpace(pm.szPhysicalMonitorDescription) ? "Monitor externo" : pm.szPhysicalMonitorDescription,
                            Handle = pm.hPhysicalMonitor, Min = mn, Max = mx, Cur = cur
                        };
                        if (Native.GetMonitorContrast(pm.hPhysicalMonitor, out uint cmn, out uint ccur, out uint cmx))
                        { d.HasContrast = true; d.CMin = cmn; d.CCur = ccur; d.CMax = cmx; }
                        l.Add(d);
                    }
                }
                catch (Exception e) { Log.Write(e); }
            }
            return l;
        });
        Devices = list;
    }

    public void SetBrightness(DisplayDevice d, uint v)
    {
        try
        {
            if (d.IsInternal)
            {
                using var s = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
                foreach (ManagementObject o in s.Get()) o.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)v });
            }
            else Native.SetMonitorBrightness(d.Handle, v);
            d.Cur = v;
        }
        catch (Exception e) { Log.Write(e); }
    }

    public void SetContrast(DisplayDevice d, uint v)
    {
        try { if (d.HasContrast) { Native.SetMonitorContrast(d.Handle, v); d.CCur = v; } }
        catch (Exception e) { Log.Write(e); }
    }
}

public record BtDevice(string Name, int? Battery);

public sealed class BluetoothService
{
    const string BatteryKey = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
    public bool? RadioOn;
    public List<BtDevice> Devices { get; private set; } = new();
    Radio? _radio;

    public async Task<bool> SetAsync(bool on)
    {
        try
        {
            _radio ??= (await Radio.GetRadiosAsync()).FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);
            if (_radio == null) return false;
            var r = await _radio.SetStateAsync(on ? RadioState.On : RadioState.Off);
            RadioOn = _radio.State == RadioState.On;
            return r == RadioAccessStatus.Allowed;
        }
        catch (Exception e) { Log.Write(e); return false; }
    }

    public async Task RefreshAsync()
    {
        try
        {
            await Radio.RequestAccessAsync();
            var radios = await Radio.GetRadiosAsync();
            var bt = radios.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);
            _radio = bt; RadioOn = bt == null ? null : bt.State == RadioState.On;
        }
        catch { RadioOn = null; }

        var list = new List<BtDevice>();
        var seen = new HashSet<string>();
        foreach (var selector in new[]
        {
            BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected),
            BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected)
        })
        {
            try
            {
                var infos = await DeviceInformation.FindAllAsync(selector, new[] { BatteryKey });
                foreach (var i in infos)
                {
                    if (string.IsNullOrWhiteSpace(i.Name) || !seen.Add(i.Name)) continue;
                    int? bat = null;
                    if (i.Properties.TryGetValue(BatteryKey, out var v) && v != null)
                    { try { bat = Convert.ToInt32(v); } catch { } }
                    list.Add(new BtDevice(i.Name, bat));
                }
            }
            catch (Exception e) { Log.Write(e); }
        }
        Devices = list;
    }
}
