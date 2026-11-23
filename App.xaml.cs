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

    void OnSettings()
    {
        Autostart.Set(Settings.Current.StartWithWindows);
        if (_tray != null) _tray.Visible = Settings.Current.ShowTrayIcon;
    }

    void SetupTray()
    {
        using var bmp = new System.Drawing.Bitmap(32, 32);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var b = new System.Drawing.SolidBrush(System.Drawing.Color.White);
            g.FillRectangle(b, 3, 3, 26, 5);
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(3, 3, 26, 26, 0, 180);
            g.FillPath(b, path);
        }
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Expandir / recolher", null, (_, _) => Dispatcher.Invoke(() => IslandWindow.Instance?.OpenPage("home")));
        menu.Items.Add("Configurações…", null, (_, _) => Dispatcher.Invoke(() => SettingsWindow.ShowSingle().Activate()));
        menu.Items.Add("-");
        menu.Items.Add("Sair", null, (_, _) => Dispatcher.Invoke(Shutdown));
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.FromHandle(bmp.GetHicon()),
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
