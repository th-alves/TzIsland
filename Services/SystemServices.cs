using System.Management;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
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

/// <summary>Tipo do aparelho: escolhe o ícone (fone, fone intra, caixa de som, mouse, teclado, controle, celular, relógio…).</summary>
public enum BtKind { Generic, Headphones, Earbuds, Speaker, Mouse, Keyboard, Gamepad, Phone, Watch, Computer, Pen }

public record BtDevice(string Id, string Name, int? Battery, BtKind Kind);

public sealed class BluetoothService
{
    const string BatteryKey = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
    const string ContainerKey = "System.Devices.ContainerId";
    public bool? RadioOn;
    public List<BtDevice> Devices { get; private set; } = new();
    Radio? _radio;
    // Bateria via GATT (BLE) é cara de ler: guarda por 2 minutos por aparelho.
    readonly Dictionary<string, (int? pct, DateTime at)> _gattCache = new();

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

        // 1) Bateria por "container": o Windows publica a % (Hands-Free/AVRCP/HID) num nó filho do aparelho,
        //    não no nó Bluetooth em si. Junta tudo que tem a propriedade, indexado pelo ContainerId.
        var batByContainer = new Dictionary<Guid, int>();
        try
        {
            var all = await DeviceInformation.FindAllAsync("System.Devices.Present:=System.StructuredQueryType.Boolean#True",
                new[] { BatteryKey, ContainerKey }, DeviceInformationKind.Device);
            foreach (var i in all)
            {
                if (!i.Properties.TryGetValue(BatteryKey, out var v) || v == null) continue;
                if (!i.Properties.TryGetValue(ContainerKey, out var c) || c is not Guid g) continue;
                try { int pct = Convert.ToInt32(v); if (pct is >= 0 and <= 100) batByContainer[g] = pct; } catch { }
            }
        }
        catch (Exception e) { Log.Write(e); }

        var list = new List<BtDevice>();
        var seen = new HashSet<string>();

        // 2) Bluetooth clássico (fones, caixas, teclados…)
        try
        {
            var infos = await DeviceInformation.FindAllAsync(
                BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected),
                new[] { BatteryKey, ContainerKey });
            foreach (var i in infos)
            {
                if (string.IsNullOrWhiteSpace(i.Name) || !seen.Add(i.Name)) continue;
                int? bat = ReadBat(i, batByContainer);
                var kind = BtKind.Generic;
                try
                {
                    using var dev = await BluetoothDevice.FromIdAsync(i.Id);
                    if (dev != null) kind = KindFromClass(dev.ClassOfDevice);
                }
                catch { }
                list.Add(new BtDevice(i.Id, i.Name, bat, Refine(kind, i.Name)));
            }
        }
        catch (Exception e) { Log.Write(e); }

        // 3) Bluetooth LE (mouses, relógios, fones novos) — se o Windows não souber a %, lê o serviço de bateria (0x180F).
        try
        {
            var infos = await DeviceInformation.FindAllAsync(
                BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected),
                new[] { BatteryKey, ContainerKey });
            foreach (var i in infos)
            {
                if (string.IsNullOrWhiteSpace(i.Name) || !seen.Add(i.Name)) continue;
                int? bat = ReadBat(i, batByContainer);
                var kind = BtKind.Generic;
                try
                {
                    using var le = await BluetoothLEDevice.FromIdAsync(i.Id);
                    if (le != null)
                    {
                        kind = KindFromAppearance(le.Appearance);
                        bat ??= await GattBatteryAsync(i.Id, le);
                    }
                }
                catch { }
                list.Add(new BtDevice(i.Id, i.Name, bat, Refine(kind, i.Name)));
            }
        }
        catch (Exception e) { Log.Write(e); }

        Devices = list;
    }

    static int? ReadBat(DeviceInformation i, Dictionary<Guid, int> byContainer)
    {
        if (i.Properties.TryGetValue(BatteryKey, out var v) && v != null)
            try { int p = Convert.ToInt32(v); if (p is >= 0 and <= 100) return p; } catch { }
        if (i.Properties.TryGetValue(ContainerKey, out var c) && c is Guid g && byContainer.TryGetValue(g, out var pct)) return pct;
        return null;
    }

    async Task<int?> GattBatteryAsync(string id, BluetoothLEDevice le)
    {
        if (_gattCache.TryGetValue(id, out var c) && DateTime.Now - c.at < TimeSpan.FromMinutes(2)) return c.pct;
        int? pct = null;
        try
        {
            var svcTask = le.GetGattServicesForUuidAsync(GattServiceUuids.Battery, BluetoothCacheMode.Cached).AsTask();
            if (await Task.WhenAny(svcTask, Task.Delay(2500)) == svcTask && svcTask.Result.Status == GattCommunicationStatus.Success)
            {
                foreach (var svc in svcTask.Result.Services)
                {
                    using (svc)
                    {
                        var chs = await svc.GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel, BluetoothCacheMode.Uncached);
                        var ch = chs.Status == GattCommunicationStatus.Success ? chs.Characteristics.FirstOrDefault() : null;
                        if (ch == null) continue;
                        var r = await ch.ReadValueAsync(BluetoothCacheMode.Uncached);
                        if (r.Status == GattCommunicationStatus.Success && r.Value.Length > 0)
                        {
                            using var rd = Windows.Storage.Streams.DataReader.FromBuffer(r.Value);
                            int b = rd.ReadByte(); if (b <= 100) pct = b;
                        }
                        if (pct != null) break;
                    }
                }
            }
        }
        catch { }
        _gattCache[id] = (pct, DateTime.Now);
        return pct;
    }

    static BtKind KindFromClass(BluetoothClassOfDevice? cod)
    {
        if (cod == null) return BtKind.Generic;
        switch (cod.MajorClass)
        {
            case BluetoothMajorClass.AudioVideo:
                return cod.MinorClass switch
                {
                    BluetoothMinorClass.AudioVideoLoudspeaker or BluetoothMinorClass.AudioVideoPortableAudio
                        or BluetoothMinorClass.AudioVideoCarAudio or BluetoothMinorClass.AudioVideoHifiAudioDevice => BtKind.Speaker,
                    BluetoothMinorClass.AudioVideoWearableHeadset or BluetoothMinorClass.AudioVideoHandsFree => BtKind.Earbuds,
                    _ => BtKind.Headphones
                };
            case BluetoothMajorClass.Peripheral:
                int m = (int)cod.MinorClass, hi = (m >> 4) & 3;          // 01 teclado · 10 apontador · 11 combo
                if (hi == 2) return BtKind.Mouse;
                if (hi is 1 or 3) return BtKind.Keyboard;
                return (m & 0x0F) switch { 1 or 2 => BtKind.Gamepad, 5 => BtKind.Pen, _ => BtKind.Generic };
            case BluetoothMajorClass.Phone: return BtKind.Phone;
            case BluetoothMajorClass.Computer: return BtKind.Computer;
            case BluetoothMajorClass.Wearable: return BtKind.Watch;
            case BluetoothMajorClass.Toy: return BtKind.Gamepad;
        }
        return BtKind.Generic;
    }

    static BtKind KindFromAppearance(BluetoothLEAppearance? a)
    {
        if (a == null) return BtKind.Generic;
        var c = a.Category;
        if (c == BluetoothLEAppearanceCategories.HumanInterfaceDevice)
        {
            var s = a.SubCategory;
            if (s == BluetoothLEAppearanceSubcategories.Keyboard) return BtKind.Keyboard;
            if (s == BluetoothLEAppearanceSubcategories.Mouse) return BtKind.Mouse;
            if (s == BluetoothLEAppearanceSubcategories.Gamepad || s == BluetoothLEAppearanceSubcategories.Joystick) return BtKind.Gamepad;
            if (s == BluetoothLEAppearanceSubcategories.DigitizerTablet) return BtKind.Pen;
            return BtKind.Generic;
        }
        if (c == BluetoothLEAppearanceCategories.Watch) return BtKind.Watch;
        if (c == BluetoothLEAppearanceCategories.Phone) return BtKind.Phone;
        if (c == BluetoothLEAppearanceCategories.Computer) return BtKind.Computer;
        if (a.RawValue >> 6 == 0x25) return BtKind.Headphones;   // 0x0940+ = categoria "Audio Sink"/fones (BLE Audio)
        return BtKind.Generic;
    }

    /// <summary>O nome desempata quando a classe é genérica ou imprecisa (ex.: "Buds", "AirPods", "JBL", "Controller").</summary>
    static BtKind Refine(BtKind k, string name)
    {
        string n = name.ToLowerInvariant();
        bool Has(params string[] w) => w.Any(n.Contains);
        if (Has("airpods", "buds", "earbud", "pods", "tws", "freebuds", "wf-", "earfun")) return BtKind.Earbuds;
        if (Has("controller", "gamepad", "xbox", "dualsense", "dualshock", "joy-con", "8bitdo")) return BtKind.Gamepad;
        if (Has("mouse", "mx master", "mx anywhere", "trackpad")) return BtKind.Mouse;
        if (Has("keyboard", "teclado", "keys", "mx keys", "k380")) return BtKind.Keyboard;
        if (Has("watch", "band", "relógio", "relogio")) return BtKind.Watch;
        if (k != BtKind.Generic) return k;
        if (Has("headphone", "headset", "fone", "wh-", "bose", "quietcomfort", "sennheiser", "momentum", "beats", "jabra")) return BtKind.Headphones;
        if (Has("speaker", "jbl", "soundlink", "boom", "flip", "charge", "caixa", "echo", "sonos")) return BtKind.Speaker;
        if (Has("phone", "iphone", "galaxy", "pixel", "xiaomi", "redmi", "motorola", "moto ")) return BtKind.Phone;
        if (Has("pen", "caneta", "stylus")) return BtKind.Pen;
        return k;
    }
}
