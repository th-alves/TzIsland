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
}

/// <summary>
/// Linha de comando (usada por scripts e hooks):
///   TzIsland.exe --notify "Título" "Texto"
///   TzIsland.exe --agent claude --state working|needs_input|done|idle [--message "..."] [--progress 40]
///                [--usage-session 62] [--usage-weekly 30]
/// </summary>
public static class Cli
{
    public static IpcMessage? Parse(string[] a)
    {
        string? Val(string k) { int i = Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length ? a[i + 1] : null; }
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
        return null;
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
