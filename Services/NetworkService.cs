using Windows.Devices.Radios;
using Windows.Networking.Connectivity;

namespace TzIsland;

/// <summary>Wi-Fi: estado do rádio (ligar/desligar), rede conectada e intensidade do sinal.</summary>
public sealed class NetworkService
{
    Radio? _wifi;

    /// <summary>true = ligado, false = desligado, null = este PC não tem rádio Wi-Fi (ou o acesso foi negado).</summary>
    public bool? WifiOn;
    public bool Connected;
    public string Ssid = "";
    public int Bars;                     // 0…5

    public async Task RefreshAsync()
    {
        try
        {
            await Radio.RequestAccessAsync();
            var radios = await Radio.GetRadiosAsync();
            _wifi = radios.FirstOrDefault(r => r.Kind == RadioKind.WiFi);
            WifiOn = _wifi == null ? null : _wifi.State == RadioState.On;
        }
        catch (Exception e) { Log.Write(e); WifiOn = null; }

        try
        {
            var p = NetworkInformation.GetInternetConnectionProfile();
            Connected = p != null;
            Bars = 0; Ssid = "";
            if (p != null)
            {
                Ssid = p.IsWlanConnectionProfile ? (p.WlanConnectionProfileDetails.GetConnectedSsid() ?? p.ProfileName) : p.ProfileName;
                var bars = p.GetSignalBars();
                Bars = bars.HasValue ? bars.Value : (p.IsWlanConnectionProfile ? 3 : 5);
            }
        }
        catch (Exception e) { Log.Write(e); Connected = false; Ssid = ""; Bars = 0; }
    }

    public async Task<bool> SetWifiAsync(bool on)
    {
        try
        {
            if (_wifi == null) return false;
            var r = await _wifi.SetStateAsync(on ? RadioState.On : RadioState.Off);
            WifiOn = _wifi.State == RadioState.On;
            return r == RadioAccessStatus.Allowed;
        }
        catch (Exception e) { Log.Write(e); return false; }
    }
}
