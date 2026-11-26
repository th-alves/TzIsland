using System.IO.Pipes;
using Microsoft.Win32;

namespace TzIsland;

public class IpcMessage
{
    public string Type { get; set; } = "";
    public string? Title { get; set; }
    public string? Text { get; set; }
    public string? Agent { get; set; }
    public string? State { get; set; }
    public double? Progress { get; set; }
    public double? UsageSession { get; set; }
    public double? UsageWeekly { get; set; }
    /// <summary>Segundos (timer) ou fração de 0–1 (volume).</summary>
    public double? Value { get; set; }
    /// <summary>Modo/ação: on|off|toggle, start|pause|stop, play|pause|next|prev, id da página.</summary>
    public string? Mode { get; set; }
}

/// <summary>
/// Linha de comando (usada por scripts, AutoHotkey, Stream Deck e hooks):
///   TzIsland.exe --notify "Título" "Texto"
///   TzIsland.exe --agent claude --state working|needs_input|done|idle [--message "..."] [--progress 40]
///   TzIsland.exe --timer 25m [--start|--pause|--stop]
///   TzIsland.exe --dnd on|off|toggle
///   TzIsland.exe --volume 40|+5|-5   ·   --mute on|off|toggle
///   TzIsland.exe --page media|focus|system|...
///   TzIsland.exe --media play|pause|next|prev|toggle
/// </summary>
public static class Cli
{
    public static IpcMessage? Parse(string[] a)
    {
        string? Val(string k) { int i = Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length ? a[i + 1] : null; }
        bool Has(string k) => Array.IndexOf(a, k) >= 0;
        double? Num(string k) => double.TryParse(Val(k), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;

        int n = Array.IndexOf(a, "--notify");
        if (n >= 0)
            return new IpcMessage
            {
                Type = "notify",
                Title = n + 1 < a.Length ? a[n + 1] : "",
                Text = n + 2 < a.Length && !a[n + 2].StartsWith("--") ? a[n + 2] : ""
            };
        if (a.Contains("--agent"))
            return new IpcMessage
            {
                Type = "agent", Agent = Val("--agent"), State = Val("--state"), Text = Val("--message"),
                Progress = Num("--progress"), UsageSession = Num("--usage-session"), UsageWeekly = Num("--usage-weekly")
            };
        if (Val("--timer") is { } timer)
        {
            if (!TryDuration(timer, out var secs)) return null;
            return new IpcMessage { Type = "timer", Value = secs, Mode = Has("--pause") ? "pause" : Has("--stop") ? "stop" : "start" };
        }
        if (Val("--dnd") is { } dnd)
            return new IpcMessage { Type = "dnd", Mode = dnd.Equals("on", StringComparison.OrdinalIgnoreCase) ? "on" : dnd.Equals("off", StringComparison.OrdinalIgnoreCase) ? "off" : "toggle" };
        if (Val("--volume") is { } vol)
        {
            var s = vol.Trim();
            bool relative = s.StartsWith('+') || s.StartsWith('-');
            if (!double.TryParse(relative ? s[1..] : s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return null;
            // Relativo é resolvido na instância principal (esta não tem o serviço de áudio aberto).
            if (relative) return new IpcMessage { Type = "volume", Value = d / 100, Mode = s.StartsWith('-') ? "down" : "up" };
            if (d > 1.5) d /= 100;                            // 40 = 40%; 0,4 = 40%
            return new IpcMessage { Type = "volume", Value = Math.Clamp(d, 0, 1) };
        }
        if (Val("--mute") is { } mute)
            return new IpcMessage { Type = "mute", Mode = mute.Equals("on", StringComparison.OrdinalIgnoreCase) ? "on" : mute.Equals("off", StringComparison.OrdinalIgnoreCase) ? "off" : "toggle" };
        if (Val("--page") is { } page)
            return new IpcMessage { Type = "page", Mode = page };
        if (Val("--media") is { } media)
            return new IpcMessage { Type = "media", Mode = media };
        return null;
    }

    /// <summary>Aceita 25m, 90s, 1h30m, 45 (minutos) e devolve segundos.</summary>
    public static bool TryDuration(string text, out double seconds)
    {
        seconds = 0;
        var s = text.Trim().ToLowerInvariant();
        if (s.Length == 0) return false;
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var plain)) { seconds = plain * 60; return plain > 0; }
        double total = 0; int i = 0; bool any = false;
        while (i < s.Length)
        {
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
            if (start == i || !double.TryParse(s[start..i], NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return false;
            if (i >= s.Length) return false;
            total += s[i] switch { 'h' => n * 3600, 'm' => n * 60, 's' => n, _ => -1 };
            if (total < 0) return false;
            any = true; i++;
        }
        seconds = total;
        return any && total > 0;
    }
}

public static class Ipc
{
    const string PipeName = "TzIsland.ipc.v2";

    public static void Send(IpcMessage m)
    {
        try
        {
            using var c = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            c.Connect(1500);
            using var w = new StreamWriter(c) { AutoFlush = true };
            w.WriteLine(JsonSerializer.Serialize(m));
        }
        catch { /* instância principal indisponível */ }
    }

    public static void StartServer(Action<IpcMessage> onMessage)
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    using var s = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await s.WaitForConnectionAsync();
                    using var r = new StreamReader(s);
                    var line = await r.ReadLineAsync();
                    if (line == null) continue;
                    var m = JsonSerializer.Deserialize<IpcMessage>(line);
                    if (m != null) onMessage(m);
                }
                catch { await Task.Delay(300); }
            }
        });
    }
}

public static class Autostart
{
    const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "TzIsland";

    public static void Set(bool on)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(Key, true);
            if (k == null) return;
            if (on) k.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
            else k.DeleteValue(ValueName, false);
        }
        catch (Exception e) { Log.Write(e); }
    }

    public static bool IsOn()
    {
        try { using var k = Registry.CurrentUser.OpenSubKey(Key); return k?.GetValue(ValueName) != null; }
        catch { return false; }
    }
}
