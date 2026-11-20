using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace TzIsland;

/// <summary>
/// Lê as notificações do Windows via UserNotificationListener. Esta API exige "identidade de pacote" (MSIX)
/// e a permissão do usuário em Configurações > Privacidade > Notificações. Sem isso, o Windows nega o acesso
/// e o app mostra o motivo em vez de fingir que funciona.
/// </summary>
public sealed class WindowsNotificationService
{
    public string Status { get; private set; } = "Desativado";
    public bool Active { get; private set; }
    UserNotificationListener? _l;
    readonly HashSet<uint> _seen = new();
    DispatcherTimer? _t;
    bool _busy;

    public async Task StartAsync()
    {
        if (!Settings.Current.ListenWindowsNotifications) { Status = "Desativado nas configurações"; return; }
        try
        {
            _l = UserNotificationListener.Current;
            var st = await _l.RequestAccessAsync();
            if (st != UserNotificationListenerAccessStatus.Allowed)
            {
                Status = "Sem permissão do Windows (Configurações > Privacidade > Notificações) ou app sem identidade de pacote (MSIX).";
                return;
            }
            foreach (var n in await _l.GetNotificationsAsync(NotificationKinds.Toast)) _seen.Add(n.Id);
            Active = true; Status = "Ativo";
            _t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            _t.Tick += async (_, _) => await PollAsync();
            _t.Start();
        }
        catch (Exception e)
        {
            Log.Write(e);
            Status = "Indisponível: o Windows só libera esta API para apps empacotados (MSIX). Veja docs/MSIX.md.";
        }
    }

    async Task PollAsync()
    {
        if (_busy || _l == null || !Settings.Current.ListenWindowsNotifications) return;
        _busy = true;
        try
        {
            var list = await _l.GetNotificationsAsync(NotificationKinds.Toast);
            var ids = new HashSet<uint>();
            foreach (var n in list)
            {
                ids.Add(n.Id);
                if (!_seen.Add(n.Id)) continue;
                string app = n.AppInfo?.DisplayInfo?.DisplayName ?? "Windows";
                var texts = n.Notification?.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric)?.GetTextElements();
                string title = texts != null && texts.Count > 0 ? texts[0].Text : app;
                string body = texts != null && texts.Count > 1 ? string.Join(" ", texts.Skip(1).Select(x => x.Text)) : "";
                if (!Settings.Current.NotificationPreview) body = "Nova notificação";
                Hub.Notify(app, title, body);
            }
            _seen.IntersectWith(ids);
        }
        catch (Exception e) { Log.Write(e); }
        finally { _busy = false; }
    }
}
