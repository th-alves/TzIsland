namespace TzIsland;

public partial class App : Application
{
    Mutex? _mutex;
    System.Windows.Forms.NotifyIcon? _tray;
    internal bool RestartForUpdate;   // o app está saindo para o Velopack aplicar a atualização

    protected override void OnStartup(StartupEventArgs e)
    {
        // Velopack: precisa ser a primeira coisa do app (trata --velopack-* de instalação/atualização e hooks)
        Velopack.VelopackApp.Build().SetAutoApplyOnStartup(true).Run();
        base.OnStartup(e);

        // Harness de verificação visual (dev): não passa pelo mutex/bandeja/serviços.
        if (Shot.Parse(e.Args) is Shot.Args shot) { RunShot(shot, e.Args); return; }

        var cli = Cli.Parse(e.Args);

        _mutex = new Mutex(true, "TzIsland.SingleInstance", out bool first);
        if (!first)
        {
            if (cli != null) Ipc.Send(cli);   // repassa --notify/--agent para a instância principal
            Shutdown();
            return;
        }
        if (cli is { Type: "agent" }) { Shutdown(); return; }   // hooks não devem iniciar o app sozinhos

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, ex) => { Log.Write(ex.Exception); ex.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => Log.Write(ex.ExceptionObject);

        Settings.Load();
        Settings.Changed += OnSettings;
        Svc.Start();
        var win = new IslandWindow();
        win.Show();
        Ipc.StartServer(m => Dispatcher.Invoke(() => Hub.HandleIpc(m)));
        SetupTray();
        if (Settings.Current.StartWithWindows != Autostart.IsOn()) Autostart.Set(Settings.Current.StartWithWindows);
        if (cli != null) Hub.HandleIpc(cli);
    }

    /// <summary>Renderiza a ilha em PNG no estado pedido e encerra (ver Tools/Shot.cs).</summary>
    void RunShot(Shot.Args shot, string[] args)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Settings.Load();
        int si = Array.IndexOf(args, "--style");
        if (si >= 0 && si + 1 < args.Length)
            Settings.Current.Style = args[si + 1].Equals("floating", StringComparison.OrdinalIgnoreCase) ? IslandStyle.Floating : IslandStyle.Classic;

        // --settings: fotografa a janela de Configurações (útil para conferir abas novas).
        if (args.Contains("--settings"))
        {
            int ti = Array.IndexOf(args, "--tab");
            int tab = ti >= 0 && ti + 1 < args.Length && int.TryParse(args[ti + 1], out var t) ? t : 0;
            var sw = SettingsWindow.ShowSingle();
            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    sw.SelectTabForShot(tab);
                    sw.UpdateLayout();
                    Log.Write("shot: " + Shot.Save((FrameworkElement)sw.Content, sw.Width, sw.Height, shot.Dir, "settings-" + tab, shot.Scale));
                }
                catch (Exception ex) { Log.Write(ex); }
                Shutdown();
            }, DispatcherPriority.ApplicationIdle);
            return;
        }

        var win = new IslandWindow();
        win.Show();
        Dispatcher.InvokeAsync(() =>
        {
            try
            {
                if (shot.Page != null) win.ShotPage(shot.Page);
                win.ApplyShotState(shot.State);
                win.UpdateLayout();
                string suffix = shot.Page != null ? "-" + shot.Page : "";
                Log.Write("shot: " + Shot.Save((FrameworkElement)win.Content, win.Width, win.Height, shot.Dir, shot.State + suffix, shot.Scale));
            }
            catch (Exception ex) { Log.Write(ex); }
            Shutdown();
        }, DispatcherPriority.ApplicationIdle);
    }

    void OnSettings()
    {
        Autostart.Set(Settings.Current.StartWithWindows);
        if (_tray != null) _tray.Visible = Settings.Current.ShowTrayIcon;
    }

    void SetupTray()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Expandir / recolher", null, (_, _) => Dispatcher.Invoke(() => IslandWindow.Instance?.OpenPage("home")));
        menu.Items.Add("Configurações…", null, (_, _) => Dispatcher.Invoke(() => SettingsWindow.ShowSingle().Activate()));
        menu.Items.Add("-");
        menu.Items.Add("Sair", null, (_, _) => Dispatcher.Invoke(Shutdown));
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = AppIcon.Tray(),
            Text = "TzIsland",
            ContextMenuStrip = menu,
            Visible = Settings.Current.ShowTrayIcon
        };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(() => SettingsWindow.ShowSingle().Activate());
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try { _tray?.Dispose(); await Svc.StopAsync(); _mutex?.ReleaseMutex(); } catch { }
        if (RestartForUpdate) Svc.Updates.ApplyAndRestart();   // encerrou de vez → o Velopack aplica e reabre
        base.OnExit(e);
    }
}
