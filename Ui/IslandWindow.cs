using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Shapes = System.Windows.Shapes;

namespace TzIsland;

/// <summary>
/// Dynamic Island window: ultra-fluid Apple-inspired floating capsule with spring physics,
/// glassmorphism, responsive micro-animations, and enterprise-grade modular organization.
/// </summary>
public sealed class IslandWindow : Window
{
    public static IslandWindow? Instance { get; private set; }

    enum St { Collapsed, Banner, Expanded }
    St _st = St.Collapsed;
    IntPtr _hwnd;
    HwndSource? _src;

    // Visual tree
    readonly Grid _root = new();
    readonly Border _island = new();
    readonly Grid _layers = new();
    readonly Image _backdrop = new() { Stretch = Stretch.Fill, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
    readonly Shapes.Rectangle _tint = new(), _sheen = new() { IsHitTestVisible = false };
    readonly Shapes.Path _outline = new() { IsHitTestVisible = false, StrokeThickness = 0.8 };
    readonly Grid _mini = new(), _banner = new(), _exp = new();
    readonly Grid _pageHost = new();
    // Barra de seções vertical, na lateral direita (minimalista: só ícones, uma "lente" desliza atrás do ativo).
    const double LensW = 34, LensH = 28;
    readonly Grid _navHost = new();                 // coluna inteira: [lista rolável] + divisor + engrenagem
    readonly Grid _navTrack = new();                // conteúdo rolável: lente atrás dos ícones
    ScrollViewer _navScroll = null!;
    readonly StackPanel _nav = new() { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
    readonly StackPanel _navFoot = new() { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
    readonly Border _navDot = new()
    {
        Width = LensW,
        Height = LensH,
        CornerRadius = new CornerRadius(9),
        Background = K.Hex("#FF2B2B2F"),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Top,
        RenderTransform = new TranslateTransform()
    };
    readonly ScaleTransform _expScale = new(1, 1);
    readonly TranslateTransform _expShift = new();

    // Mini (collapsed pill)
    // Pílula recolhida: apenas um LED à direita que pulsa (laranja = ocioso, verde = ativo).
    // O brilho é um gradiente radial (barato); efeitos de sombra reprocessados a cada frame deixavam a animação travada.
    sealed class Led : Grid
    {
        readonly Shapes.Ellipse _glow, _core;
        readonly ScaleTransform _sc = new(1, 1);

        public Led(Color c)
        {
            Width = 26; Height = 26; IsHitTestVisible = false;
            RenderTransformOrigin = new Point(0.5, 0.5); RenderTransform = _sc;
            var inner = Color.FromArgb(170, c.R, c.G, c.B); var edge = Color.FromArgb(0, c.R, c.G, c.B);
            _glow = new Shapes.Ellipse { Width = 26, Height = 26, Fill = new RadialGradientBrush(inner, edge) };
            _core = new Shapes.Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(c) };
            Children.Add(_glow); Children.Add(_core);
        }

        public void Pulse(double halfMs)
        {
            var sine = new SineEase { EasingMode = EasingMode.EaseInOut };
            DoubleAnimation A(double from, double to) => new(from, to, TimeSpan.FromMilliseconds(halfMs)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = sine };
            _sc.BeginAnimation(ScaleTransform.ScaleXProperty, A(0.78, 1.1));
            _sc.BeginAnimation(ScaleTransform.ScaleYProperty, A(0.78, 1.1));
            _glow.BeginAnimation(UIElement.OpacityProperty, A(0.3, 1));
            _core.BeginAnimation(UIElement.OpacityProperty, A(0.65, 1));
        }
    }

    readonly Led _ledIdle = new(Color.FromRgb(0xFF, 0x9F, 0x0A)), _ledActive = new(Color.FromRgb(0x30, 0xD1, 0x58));
    // Recolhida com atividade ao vivo: mídia (ícone + espectro) e/ou timer em andamento (ícone + tempo).
    enum Live { None, Media, Timer, Both, Download, Agenda }
    readonly Grid _miniLive = new() { Opacity = 0, IsHitTestVisible = false };
    readonly StackPanel _liveL = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) };
    readonly StackPanel _liveR = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 18, 0) };
    readonly Border _mIcon = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly Border _tIcon = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly EqBars _miniEq = new(5) { Fill = Brushes.White, BarWidth = 2.6, Gap = 2.2, Height = 15, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _tText = K.Num("", 12.5, Brushes.White), _tExtra = K.T("", 10, K.Muted);
    Live _live = Live.None, _liveShown = Live.None; bool _liveVideo, _liveBreak; LiveKind _liveKind;
    readonly Grid _ledHost = new() { Width = 26, Height = 26, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0), IsHitTestVisible = false };
    bool? _ledOn;
    bool _ledPulsing;

    // Download ao vivo: linha fina no fundo da pílula (indeterminada, pois o navegador não informa o tamanho final)
    readonly Grid _dlBar = new() { Height = 3, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(24, 0, 24, 4), Opacity = 0, IsHitTestVisible = false };
    readonly Border _dlTrack = new() { CornerRadius = new CornerRadius(1.5), Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)) };
    readonly Border _dlFill = new() { CornerRadius = new CornerRadius(1.5), Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
    readonly TranslateTransform _dlShift = new();
    bool _dlRunning, _dlFinishing;
    readonly TextBlock _aTitle = K.T("", 12.5, Brushes.White, true), _aTime = K.Num("", 12.5, K.Orange);
    readonly Border _aIcon = new() { VerticalAlignment = VerticalAlignment.Center };
    string _agendaKey = ""; bool _agendaSoon;

    // HUD de gestos (volume / play / faixa) dentro da pílula recolhida
    readonly Grid _hud = new() { Opacity = 0, IsHitTestVisible = false, Background = new SolidColorBrush(Color.FromArgb(244, 0, 0, 0)) };
    readonly Grid _hudBar = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
    readonly TextBlock _hudLabel = K.T("", 12, Brushes.White, true);
    readonly Border _dIcon = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _dText = K.Num("", 12, Brushes.White);
    readonly SolidColorBrush _accent = new(Colors.White);
    Color _accentTarget = Colors.White;
    double _hudFrac;
    readonly Border _hudIcon = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 10, 0) };
    readonly Border _hudTrack = new() { Height = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), VerticalAlignment = VerticalAlignment.Center };
    readonly Border _hudFill = new() { Height = 4, CornerRadius = new CornerRadius(2), Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _hudText = K.Num("", 11.5, Brushes.White);
    readonly DispatcherTimer _hudT = new() { Interval = TimeSpan.FromMilliseconds(1100) };
    bool _swiped;
    readonly TranslateTransform _miniShift = new();

    // Banner (notification pulse)
    readonly TextBlock _bTitle = K.T("", 13, K.Fg, true), _bText = K.T("", 12, K.Dim), _bIcon = K.T("", 14, Brushes.White, true);
    readonly Border _bBadge = new() { Width = 36, Height = 36, CornerRadius = new CornerRadius(18), Background = K.CardStrong };

    // Motion & spring dynamics
    readonly MotionLoop _loop;
    readonly Spring _w, _h, _r, _miniO, _bannerO, _expO, _expS, _expY, _pg, _dotX;

    readonly Dictionary<string, IPage> _pages = new();
    readonly Dictionary<string, Button> _navBtns = new();
    readonly Dictionary<string, SolidColorBrush> _navBrush = new();
    readonly Dictionary<string, ScaleTransform> _navScale = new();
    readonly Dictionary<string, double> _navCenter = new();
    double _dir = 1;
    readonly TextBlock _badge = new()
    {
        FontFamily = K.Font, FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = Brushes.White,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.None,
        LineStackingStrategy = LineStackingStrategy.BlockLineHeight, LineHeight = 11
    };
    readonly Border _badgeBox = new()
    {
        Background = K.Red,
        BorderBrush = Brushes.Black, BorderThickness = new Thickness(1.5),
        CornerRadius = new CornerRadius(9.5),
        MinWidth = 19,
        Height = 19,
        Visibility = Visibility.Collapsed,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(0, -1, 3, 0),
        IsHitTestVisible = false,
        RenderTransformOrigin = new Point(0.5, 0.5),
        RenderTransform = new ScaleTransform(1, 1),
        Padding = new Thickness(3, 0, 3, 0)
    };
    string? _pageId;
    bool _pageActive;
    FrameworkElement? _incoming, _outgoing;

    readonly DispatcherTimer _hoverT = new(), _collapseT = new(), _bannerT = new(), _clockT = new() { Interval = TimeSpan.FromSeconds(1) }, _fsT = new() { Interval = TimeSpan.FromSeconds(2) };
    DateTime _activity = DateTime.Now;
    Point _downPos; bool _mouseDown;
    bool _noExpand;

    static AppSettings S => Settings.Current;
    bool Floating => S.Style == IslandStyle.Floating;
    double Ear => Floating ? 0 : 11;
    double MiniEar => Floating ? 0 : Math.Min(11, S.CollapsedHeight * 0.3);

    public IslandWindow()
    {
        Instance = this;
        Title = "TzIsland";
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = false;
        Background = Brushes.Transparent; ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        UseLayoutRounding = true; SnapsToDevicePixels = true;

        _loop = new MotionLoop(Frame);
        _w = _loop.Add(new Spring(200)); _h = _loop.Add(new Spring(34)); _r = _loop.Add(new Spring(17));
        _miniO = _loop.Add(new Spring(1)); _bannerO = _loop.Add(new Spring(0)); _expO = _loop.Add(new Spring(0));
        _expS = _loop.Add(new Spring(0.94)); _expY = _loop.Add(new Spring(14));
        _pg = _loop.Add(new Spring(1)); _dotX = _loop.Add(new Spring(0));

        BuildTree();
        ApplySettings();

        SourceInitialized += OnSourceInit;
        Loaded += (_, _) => { Place(); SetState(St.Collapsed, true); RefreshMini(); };
        Settings.Changed += OnSettingsChanged;
        Settings.PreviewAnimation += () => Dispatcher.InvokeAsync(PreviewAnim, DispatcherPriority.Background);
        Hub.Banner += ShowBanner;
        Hub.Inbox.CollectionChanged += (_, _) => UpdateBadge();
        Svc.Media.Changed += RefreshMini; Svc.Battery.Changed += RefreshMini; Svc.Audio.MicChanged += RefreshMini;
        Svc.Focus.Tick += RefreshMini; Svc.Agents.Changed += RefreshMini; Svc.Camera.StateChanged += RefreshMini;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.InvokeAsync(Place);

        _hoverT.Tick += (_, _) => { _hoverT.Stop(); if (S.ExpandOnHover && _island.IsMouseOver) Expand(); };
        _collapseT.Tick += OnCollapseTick;
        _bannerT.Tick += (_, _) => { _bannerT.Stop(); if (_st == St.Banner && !_island.IsMouseOver) SetState(St.Collapsed); };
        _hudT.Tick += (_, _) => { _hudT.Stop(); FadeHud(false); };
        Svc.Downloads.Changed += () => Dispatcher.InvokeAsync(RefreshMini);
        Svc.Calendar.Changed += () => Dispatcher.InvokeAsync(RefreshMini);
        Hub.HeldChanged += () => Dispatcher.InvokeAsync(RefreshMini);
        Svc.Media.Changed += ApplyAccent;
        _clockT.Tick += (_, _) => { if (_st == St.Collapsed) RefreshMini(); };
        _clockT.Start();
        _fsT.Tick += (_, _) => CheckFullscreen();
        _fsT.Start();
    }

    // ---------------- UI construction ----------------
    void BuildTree()
    {
        Content = _root;
        _root.Children.Add(_island);
        _island.HorizontalAlignment = HorizontalAlignment.Center;
        _island.VerticalAlignment = VerticalAlignment.Top;
        _island.AllowDrop = true;
        _island.Child = _layers;
        _island.Background = null;

        _backdrop.Effect = new BlurEffect { Radius = 30, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Performance };
        _layers.Children.Add(_backdrop);
        _layers.Children.Add(_tint);
        _layers.Children.Add(_sheen);
        _layers.Children.Add(_mini);
        _layers.Children.Add(_banner);
        _layers.Children.Add(_exp);
        _layers.Children.Add(_outline);

        // Mini: somente o LED, centralizado
        _mini.HorizontalAlignment = HorizontalAlignment.Center; _mini.VerticalAlignment = VerticalAlignment.Top;
        _ledActive.Opacity = 0;
        _ledHost.Children.Add(_ledIdle); _ledHost.Children.Add(_ledActive);
        _mini.Children.Add(_ledHost);
        _tText.VerticalAlignment = VerticalAlignment.Center; _tExtra.VerticalAlignment = VerticalAlignment.Center; _tExtra.Margin = new Thickness(4, 1, 0, 0);
        _miniLive.Children.Add(_liveL); _miniLive.Children.Add(_liveR);
        _mini.Children.Add(_miniLive);
        _mini.RenderTransform = _miniShift;
        // barra de download
        _dlFill.RenderTransform = _dlShift;
        _dlBar.Children.Add(_dlTrack); _dlBar.Children.Add(_dlFill);
        K.RoundClip(_dlBar, 1.5);
        _mini.Children.Add(_dlBar);
        // agenda recolhida (usa _liveL / _liveR via LayoutLive)
        _aTitle.VerticalAlignment = VerticalAlignment.Center; _aTitle.Margin = new Thickness(7, 0, 0, 0);
        _aTime.VerticalAlignment = VerticalAlignment.Center;
        // HUD
        _hud.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _hud.ColumnDefinitions.Add(new ColumnDefinition { Width = K.GL("*") });
        _hud.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _hud.Children.Add(_hudIcon);
        _hudBar.Children.Add(_hudTrack); _hudBar.Children.Add(_hudFill);
        _hudLabel.VerticalAlignment = VerticalAlignment.Center; _hudLabel.Visibility = Visibility.Collapsed; _hudLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        _hudBar.Children.Add(_hudLabel);
        _hudBar.SizeChanged += (_, _) => UpdateHudFill();
        _hud.Children.Add(_hudBar.At(col: 1));
        _hudText.Margin = new Thickness(0, 0, 18, 0);
        _hud.Children.Add(_hudText.At(col: 2).Vc());
        _miniEq.Fill = _accent;
        _mini.Children.Add(_hud);

        // Banner: [glowing badge] [title & text]
        _banner.HorizontalAlignment = HorizontalAlignment.Center; _banner.VerticalAlignment = VerticalAlignment.Top; _banner.Opacity = 0;
        _banner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _banner.ColumnDefinitions.Add(new ColumnDefinition { Width = K.GL("*") });
        _bIcon.HorizontalAlignment = HorizontalAlignment.Center; _bIcon.VerticalAlignment = VerticalAlignment.Center;
        _bBadge.Child = _bIcon; _bBadge.Margin = new Thickness(16, 0, 0, 0); _bBadge.VerticalAlignment = VerticalAlignment.Center;
        _banner.Children.Add(_bBadge);
        var bs = new StackPanel { Margin = new Thickness(12, 0, 20, 0), VerticalAlignment = VerticalAlignment.Center };
        bs.Children.Add(_bTitle); bs.Children.Add(_bText);
        _banner.Children.Add(bs.At(col: 1));

        // Expanded: [page host] + [barra de seções vertical à direita]
        _exp.HorizontalAlignment = HorizontalAlignment.Center; _exp.VerticalAlignment = VerticalAlignment.Top; _exp.Opacity = 0;
        _exp.RenderTransformOrigin = new Point(0.5, 0);
        _exp.RenderTransform = new TransformGroup { Children = { _expScale, _expShift } };
        _exp.ColumnDefinitions.Add(new ColumnDefinition { Width = K.GL("*") });
        _exp.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _exp.Children.Add(_pageHost);

        _navTrack.Children.Add(_navDot);     // a "lente" fica atrás dos ícones
        _navTrack.Children.Add(_nav);
        _navScroll = K.Scroll(_navTrack);
        _navScroll.SizeChanged += (_, _) => UpdateNavFade();
        _navScroll.ScrollChanged += (_, _) => UpdateNavFade();
        _navHost.RowDefinitions.Add(new RowDefinition { Height = K.GL("*") });
        _navHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _navHost.Width = LensW + 4;
        _navHost.Children.Add(_navScroll);
        _navHost.Children.Add(_navFoot.At(row: 1));
        _exp.Children.Add(_navHost.At(col: 1));

        // Interaction events
        _island.MouseEnter += (_, _) => { _collapseT.Stop(); if (_st == St.Collapsed && S.ExpandOnHover && !_noExpand) { _hoverT.Interval = TimeSpan.FromMilliseconds(S.HoverDelayMs); _hoverT.Start(); } };
        _island.MouseWheel += OnIslandWheel;
        _island.MouseDown += OnIslandMiddle;
        _island.MouseLeave += (_, _) => { _hoverT.Stop(); if (_st == St.Expanded) ArmCollapse(); };
        _island.PreviewMouseMove += (_, _) => _activity = DateTime.Now;
        _island.PreviewMouseDown += OnPreviewDown;
        _island.MouseLeftButtonDown += (_, e) => { _mouseDown = true; _downPos = e.GetPosition(this); };
        _island.MouseMove += OnIslandMove;
        _island.MouseLeftButtonUp += OnIslandUp;
        _island.DragEnter += (_, e) => { if (_noExpand) return; _collapseT.Stop(); Expand(); ShowPage("files"); e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        _island.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        _island.DragLeave += (_, _) => ArmCollapse();
        _island.Drop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) foreach (var p in paths) Svc.Files.Add(p);
            e.Handled = true;
        };

        var menu = new ContextMenu();
        var mCfg = new MenuItem { Header = "Configurações…" }; mCfg.Click += (_, _) => OpenSettings();
        var mTog = new MenuItem { Header = "Expandir / recolher" }; mTog.Click += (_, _) => Toggle();
        var mExit = new MenuItem { Header = "Sair do TzIsland" }; mExit.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(mTog); menu.Items.Add(mCfg); menu.Items.Add(new Separator()); menu.Items.Add(mExit);
        _island.ContextMenu = menu;

        // Futuristic Apple gradient border glow
        _outline.Stroke = new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(48, 255, 255, 255), 0),
                new GradientStop(Color.FromArgb(10, 255, 255, 255), 0.55),
                new GradientStop(Color.FromArgb(26, 255, 255, 255), 1)
            }, 90);
        _outline.StrokeThickness = 0.8;
        _badgeBox.Child = _badge;
    }

    // ---------------- Window / Placement ----------------
    void OnSourceInit(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        int ex = Native.GetWindowLong(_hwnd, Native.GWL_EXSTYLE);
        Native.SetWindowLong(_hwnd, Native.GWL_EXSTYLE, ex | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);

        _src = HwndSource.FromHwnd(_hwnd);
        if (_src?.CompositionTarget != null) _src.CompositionTarget.BackgroundColor = Colors.Transparent;
        var m = new Native.MARGINS { L = -1, R = -1, T = -1, B = -1 };
        Native.DwmExtendFrameIntoClientArea(_hwnd, ref m);
        int none = unchecked((int)0xFFFFFFFE), noRound = 1;
        Native.DwmSetWindowAttribute(_hwnd, 34, ref none, 4);      // No Windows 11 border
        Native.DwmSetWindowAttribute(_hwnd, 33, ref noRound, 4);   // No Windows 11 system rounding
        _src?.AddHook(WndProc);
        RegisterHotkeys();
        try { Native.AddClipboardFormatListener(_hwnd); } catch (Exception clipErr) { Log.Write(clipErr); }
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY)
        {
            handled = true;
            if (w.ToInt32() == 1) Toggle();
            else if (w.ToInt32() == 2) { OpenPage("dictation"); _ = Svc.Dictation.ToggleAsync(); }
        }
        else if (msg == Native.WM_CLIPBOARDUPDATE) Svc.Clipboard.OnSystemUpdate();
        return IntPtr.Zero;
    }

    void RegisterHotkeys()
    {
        if (_hwnd == IntPtr.Zero) return;
        Native.UnregisterHotKey(_hwnd, 1); Native.UnregisterHotKey(_hwnd, 2);
        if (Native.TryParseHotkey(S.ToggleHotkey, out var m1, out var k1)) Native.RegisterHotKey(_hwnd, 1, m1 | 0x4000, k1);
        if (S.IsOn("dictation") && Native.TryParseHotkey(S.DictationHotkey, out var m2, out var k2)) Native.RegisterHotKey(_hwnd, 2, m2 | 0x4000, k2);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi) { base.OnDpiChanged(oldDpi, newDpi); Place(); }

    void Place()
    {
        if (_hwnd == IntPtr.Zero) return;
        var screens = System.Windows.Forms.Screen.AllScreens;
        var sc = screens[Math.Clamp(S.Monitor, 0, screens.Length - 1)];
        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        int wpx = (int)Math.Round(Width * dpi);
        int x, y = sc.Bounds.Top;
        int pad = (int)(16 * dpi);
        switch (S.Anchor)
        {
            case IslandAnchor.TopLeft: x = sc.Bounds.Left + pad; break;
            case IslandAnchor.TopRight: x = sc.Bounds.Right - wpx - pad; break;
            case IslandAnchor.Custom: x = (int)S.CustomX - wpx / 2; y = (int)S.CustomY; break;
            default: x = sc.Bounds.Left + (sc.Bounds.Width - wpx) / 2; break;
        }
        Native.SetWindowPos(_hwnd, IntPtr.Zero, x, y, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
        UpdateRegion();
    }

    void SaveDraggedPosition()
    {
        if (_hwnd == IntPtr.Zero || !Native.GetWindowRect(_hwnd, out var rc)) return;
        S.Anchor = IslandAnchor.Custom;
        S.CustomX = (rc.Left + rc.Right) / 2.0; S.CustomY = rc.Top;
        var screens = System.Windows.Forms.Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
            if (screens[i].Bounds.Contains((int)S.CustomX, rc.Top + 2)) { S.Monitor = i; break; }
        Settings.Apply();
    }

    int _rgnL = -1, _rgnT = -1, _rgnR = -1, _rgnB = -1;

    void UpdateRegion()
    {
        if (_hwnd == IntPtr.Zero) return;
        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        double w = _w.Value, h = _h.Value, left = (Width - w) / 2, top = _island.Margin.Top;
        int l = (int)Math.Floor(left * dpi), t = (int)Math.Floor(top * dpi), r = (int)Math.Ceiling((left + w) * dpi), b = (int)Math.Ceiling((top + h) * dpi);
        if (l == _rgnL && t == _rgnT && r == _rgnR && b == _rgnB) return;   // nada mudou: evita recriar a região a cada frame
        _rgnL = l; _rgnT = t; _rgnR = r; _rgnB = b;
        Native.SetWindowRgn(_hwnd, Native.CreateRectRgn(l, t, r, b), false);
    }

    // ---------------- Settings synchronization ----------------
    IslandAnim _lastAnim = Settings.Current.IslandAnim;
    DispatcherTimer? _previewT;

    /// <summary>Abre a ilha sozinha e fecha depois de um instante, para mostrar o estilo de animação escolhido.</summary>
    void PreviewAnim()
    {
        if (_noExpand || !S.Animations) return;
        _previewT?.Stop();
        if (_st != St.Expanded) Expand();
        _previewT = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2200) };
        _previewT.Tick += (_, _) => { _previewT!.Stop(); if (!_island.IsMouseOver && _st == St.Expanded) Collapse(); };
        _previewT.Start();
    }

    void OnSettingsChanged()
    {
        if (S.IslandAnim != _lastAnim) { _lastAnim = S.IslandAnim; Dispatcher.InvokeAsync(PreviewAnim, DispatcherPriority.Background); }
        ApplySettings();
        Place();
        RegisterHotkeys();
        SetState(_st, true);
        RefreshMini();
    }

    void ApplySettings()
    {
        double bannerW = Math.Max(S.CollapsedWidth + 180, 400), bannerH = 76;
        Width = Math.Max(S.ExpandedWidth, bannerW) + 120;
        Height = Math.Max(S.ExpandedHeight, bannerH) + 12 + 76;
        _island.Margin = new Thickness(0, Floating ? 10 : 0, 0, 0);
        // Clássico: os "ombros" côncavos comem ~11 px de cada lado do corpo da ilha; o conteúdo precisa respeitar isso
        // (antes os ícones ficavam colados na borda). No modo flutuante nada muda.
        _exp.Width = S.ExpandedWidth - 2 * Ear; _exp.Height = S.ExpandedHeight;
        _mini.Width = S.CollapsedWidth - 2 * MiniEar; _mini.Height = S.CollapsedHeight;
        _banner.Width = bannerW - 2 * Ear; _banner.Height = bannerH;
        _pageHost.Margin = new Thickness(18, 14, 8, Floating ? 14 : 16);
        _navHost.Margin = new Thickness(0, Floating ? 14 : 12, Floating ? 12 : 10, Floating ? 14 : 16);
        _backdrop.Width = S.ExpandedWidth + S.BlurAmount * 2; _backdrop.Height = S.ExpandedHeight + S.BlurAmount; _backdrop.Margin = new Thickness(0, 0, 0, 0);
        ((BlurEffect)_backdrop.Effect).Radius = Math.Max(0.1, S.BlurAmount);
        ApplyLook();
        BuildNav();
        if (S.Fullscreen != FullscreenMode.Hide) Visibility = Visibility.Visible;
    }

    void ApplyLook()
    {
        double a = Math.Clamp(S.Opacity, 0.3, 1);
        _backdrop.Visibility = S.Look == IslandLook.SolidBlack || S.BlurAmount <= 0 ? Visibility.Collapsed : Visibility.Visible;
        switch (S.Look)
        {
            case IslandLook.SolidBlack:
                _tint.Fill = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                _sheen.Visibility = Visibility.Collapsed;
                break;
            case IslandLook.DarkGlass:
                _tint.Fill = new SolidColorBrush(Color.FromArgb((byte)(255 * (0.35 + 0.55 * a)), 8, 8, 9));
                _sheen.Fill = new LinearGradientBrush(Color.FromArgb(18, 255, 255, 255), Color.FromArgb(0, 0, 0, 0), 90);
                _sheen.VerticalAlignment = VerticalAlignment.Top; _sheen.Height = 50; _sheen.Visibility = Visibility.Visible;
                break;
            default:
                _tint.Fill = new LinearGradientBrush(
                    Color.FromArgb((byte)(255 * (0.18 + 0.30 * a)), 22, 22, 24),
                    Color.FromArgb((byte)(255 * (0.32 + 0.50 * a)), 6, 6, 7), 90);
                _sheen.Fill = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(26, 255, 255, 255), 0),
                        new GradientStop(Color.FromArgb(10, 255, 255, 255), 0.4),
                        new GradientStop(Color.FromArgb(0, 255, 255, 255), 1)
                    }, 90);
                _sheen.VerticalAlignment = VerticalAlignment.Top; _sheen.Height = 60; _sheen.Visibility = Visibility.Visible;
                break;
        }
    }

    // Ícones de linha da barra (mesmo traço fino em todos). Cada id vem de Modules.All.
    static readonly Dictionary<string, string> NavIcon = new()
    {
        ["home"] = Ico.Path("home"), ["media"] = Ico.Path("music"), ["focus"] = Ico.Path("timer"), ["ambience"] = Ico.Path("waves"),
        ["inbox"] = Ico.Path("mail"), ["productivity"] = Ico.Path("pen"), ["system"] = Ico.Path("monitor"), ["files"] = Ico.Path("folder"),
        ["clipboard"] = Ico.Path("clipboard"), ["mixer"] = Ico.Path("sliders"), ["mirror"] = Ico.Path("camera"), ["dictation"] = Ico.Path("mic")
    };

    void BuildNav()
    {
        _nav.Children.Clear(); _navBtns.Clear(); _navBrush.Clear(); _navScale.Clear(); _navCenter.Clear();
        var order = Modules.Order().ToList();     // ordem escolhida pelo usuário (Configurações > Módulos)
        foreach (var m in Modules.All.Where(m => S.IsOn(m.Id)).OrderBy(m => order.IndexOf(m.Id)))
        {
            var id = m.Id;
            // Botão do MESMO tamanho/raio da lente: o realce de hover coincide com ela e não vaza nos cantos.
            var b = K.IconBtn(m.Glyph, m.Title, () => ShowPage(id), 15, LensW, LensH);
            b.Tag = new CornerRadius(9); b.Margin = new Thickness(0, 1, 0, 1);
            var ink = new SolidColorBrush(((SolidColorBrush)K.Label).Color);
            var sc = new ScaleTransform(1, 1);
            if (id == "ai") { var sp = Brand.Sparkle(16, ink); sp.Fill = ink; b.Content = sp; }   // faísca de IA no lugar do texto "AI"
            else if (NavIcon.TryGetValue(id, out var ico)) b.Content = new IconHost(ico, 16, ink, 1.7);
            else if (b.Content is TextBlock tb) tb.Foreground = ink;
            if (b.Content is FrameworkElement ce) { ce.RenderTransformOrigin = new Point(0.5, 0.5); ce.RenderTransform = sc; }
            _navBrush[id] = ink; _navScale[id] = sc;
            _navBtns[id] = b;
            if (id == "inbox")
            {
                (_badgeBox.Parent as Panel)?.Children.Remove(_badgeBox);
                var host = new Grid(); host.Children.Add(b); host.Children.Add(_badgeBox);
                _nav.Children.Add(host);
            }
            else _nav.Children.Add(b);
        }
        _navFoot.Children.Clear();
        _navFoot.Children.Add(new Border { Width = 16, Height = 1, Background = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)), Margin = new Thickness(0, 5, 0, 5) });
        var gear = K.IconBtn("\uE713", "Configurações", OpenSettings, 14, LensW, LensH, K.Label);
        gear.Tag = new CornerRadius(9);
        gear.Content = Ico.Make("settings", 16, K.Label, 1.7);
        _navFoot.Children.Add(gear);
        if (_pageId == null || !_navBtns.ContainsKey(_pageId)) _pageId = _navBtns.Keys.FirstOrDefault();
        UpdateBadge();
        Dispatcher.InvokeAsync(MoveDot, DispatcherPriority.Loaded);
    }

    /// <summary>Quando há mais seções do que cabem na altura, as bordas da barra esmaecem (indicando que dá para rolar).</summary>
    void UpdateNavFade()
    {
        if (_navScroll == null) return;
        double h = _navScroll.ActualHeight, off = _navScroll.VerticalOffset, max = _navScroll.ScrollableHeight;
        if (h <= 0 || max <= 0.5) { _navScroll.OpacityMask = null; return; }
        double f = Math.Min(0.14, 22 / h);
        var top = off > 0.5 ? 0.0 : 1.0; var bot = off < max - 0.5 ? 0.0 : 1.0;
        _navScroll.OpacityMask = new LinearGradientBrush(new GradientStopCollection
        {
            new GradientStop(Color.FromArgb((byte)(255 * top), 0, 0, 0), 0),
            new GradientStop(Colors.Black, f),
            new GradientStop(Colors.Black, 1 - f),
            new GradientStop(Color.FromArgb((byte)(255 * bot), 0, 0, 0), 1)
        }, 90);
    }

    void UpdateBadge()
    {
        int n = Hub.Inbox.Count;
        string txt = n > 99 ? "99+" : n.ToString();
        bool changed = _badge.Text != txt;
        _badge.Text = txt;
        _badgeBox.Visibility = n > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (changed && n > 0 && S.Animations)
        {
            var pop = new DoubleAnimation(1.45, 1, TimeSpan.FromMilliseconds(420)) { EasingFunction = new BackEase { Amplitude = 0.9, EasingMode = EasingMode.EaseOut } };
            var sc = (ScaleTransform)_badgeBox.RenderTransform;
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, pop.Clone());
        }
    }

    void OpenSettings() { var w = SettingsWindow.ShowSingle(); w.Activate(); }

    // ---------------- Pages ----------------
    IPage? Make(string id) => id switch
    {
        "home" => new HomePage(), "media" => new MediaPage(), "focus" => new FocusPage(), "ambience" => new AmbiencePage(), "inbox" => new InboxPage(), "productivity" => new ProductivityPage(), "clipboard" => new ClipboardPage(), "mixer" => new MixerPage(),
        "system" => new SystemPage(), "files" => new FilesPage(), "ai" => new AiPage(), "mirror" => new MirrorPage(), "dictation" => new DictationPage(),
        _ => null
    };

    IPage? GetPage(string id)
    {
        if (_pages.TryGetValue(id, out var p)) return p;
        var np = Make(id);
        if (np == null) return null;
        np.View.RenderTransform = new TranslateTransform();
        _pages[id] = np;
        return np;
    }

    void RevealNav()
    {
        int i = 0;
        foreach (var el in _nav.Children.OfType<FrameworkElement>().Concat(_navFoot.Children.OfType<FrameworkElement>())) Motion.Reveal(el, 0.12 + (i++) * 0.025, 10, 0.55);
    }

    public void ShowPage(string id, bool animate = true)
    {
        if (!S.IsOn(id) || GetPage(id) is not IPage next) return;
        if (_pageId == id && _pageHost.Children.Contains(next.View)) { MoveDot(); return; }
        FinishTransition();
        next.View.BeginAnimation(UIElement.OpacityProperty, null);
        next.View.Visibility = Visibility.Visible;
        IPage? prev = _pageId != null && _pages.TryGetValue(_pageId, out var pp) && _pageHost.Children.Contains(pp.View) ? pp : null;
        if (prev != null && _pageActive) prev.Hidden();
        var order = _navBtns.Keys.ToList();
        _dir = prev != null && order.IndexOf(id) < order.IndexOf(_pageId ?? id) ? -1 : 1;
        _pageId = id;
        _pageHost.Children.Add(next.View);
        if (_pageActive) next.Shown();
        if (prev != null && animate && S.Animations)
        {
            _outgoing = prev.View; _incoming = next.View;
            prev.View.BeginAnimation(UIElement.OpacityProperty, null);   // garante que a página que sai realmente possa sumir
            prev.View.Opacity = 1;
            next.View.Opacity = 0;
            _pg.Snap(0); _pg.To(1, 0, 300, 19);
            Motion.RevealPage(next, 0.16, 0.05);
        }
        else if (prev != null) _pageHost.Children.Remove(prev.View);
        MoveDot();
        _loop.Kick();
    }

    void FinishTransition()
    {
        if (_incoming != null) { _incoming.Opacity = 1; ((TranslateTransform)_incoming.RenderTransform).Y = 0; }
        if (_outgoing != null) { _pageHost.Children.Remove(_outgoing); _outgoing.Visibility = Visibility.Visible; _outgoing.Opacity = 1; ((TranslateTransform)_outgoing.RenderTransform).Y = 0; }
        _incoming = _outgoing = null;
    }

    public void OpenPage(string id) { ShowPage(id, false); Expand(); }

    void MoveDot()
    {
        if (_pageId == null || !_navBtns.TryGetValue(_pageId, out var b)) return;
        _nav.UpdateLayout();
        try
        {
            var p = b.TranslatePoint(new Point(0, b.ActualHeight / 2), _navTrack);
            double y = p.Y - LensH / 2;
            foreach (var kv in _navBtns)
                _navCenter[kv.Key] = kv.Value.TranslatePoint(new Point(0, kv.Value.ActualHeight / 2), _navTrack).Y;
            // mola um pouco mais macia (menos "borracha"): chega rápido, passa de leve e assenta
            if (S.Animations) _dotX.To(y, 0, 380, 21); else _dotX.Snap(y);
            b.BringIntoView(new Rect(0, -LensH, b.ActualWidth, b.ActualHeight + LensH * 2));   // ativo sempre visível quando a barra rola
            _loop.Kick();
        }
        catch { }
    }

    // ---------------- State transitions ----------------
    double CollapsedWidthNow => S.CollapsedWidth + _live switch { Live.Both => 84, Live.Agenda => 110, Live.Download => 62, _ => 0 };

    (double w, double h, double r) Target(St s) => s switch
    {
        St.Expanded => (S.ExpandedWidth, S.ExpandedHeight, Floating ? 36 : 28),
        St.Banner => (Math.Max(S.CollapsedWidth + 180, 400), 76, Floating ? 38 : 26),
        _ => (CollapsedWidthNow, S.CollapsedHeight, Floating ? S.CollapsedHeight / 2 : Math.Min(20, S.CollapsedHeight * 0.55))
    };

    void SetState(St s, bool snap = false)
    {
        if (_st == s && !snap) return;
        var prev = _st; _st = s;
        if (!snap && prev != s) { if (s == St.Expanded) SoundFx.Open(); else if (prev == St.Expanded) SoundFx.Close(); }
        var (w, h, r) = Target(s);
        bool exp = s == St.Expanded;
        bool instantOut = s == St.Collapsed && prev == St.Expanded && S.CollapseSpeed == CollapseMode.Instant;
        bool fastOut = !exp && S.CollapseSpeed == CollapseMode.Fast;
        bool anim = S.Animations && !snap && !instantOut;

        if (exp && prev != St.Expanded) CaptureBackdrop();
        if (!exp && prev == St.Expanded && _pageActive) { _pageActive = false; CurrentPage()?.Hidden(); }
        if (exp && prev != St.Expanded)
        {
            if (_pageId == null) _pageId = _navBtns.Keys.FirstOrDefault();
            if (_pageId != null)
            {
                FinishTransition();
                if (GetPage(_pageId) is IPage cur)
                {
                    foreach (var c in _pageHost.Children.OfType<FrameworkElement>().Where(c => c != cur.View).ToList()) _pageHost.Children.Remove(c);
                    if (!_pageHost.Children.Contains(cur.View)) _pageHost.Children.Add(cur.View);
                    _pageActive = true; cur.Shown();
                    cur.View.BeginAnimation(UIElement.OpacityProperty, null); cur.View.Opacity = 1;
                    if (anim) { var rp = AnimProfiles.Get(S.IslandAnim); Motion.RevealPage(cur, rp.RevealBase, rp.RevealStep, rp.RevealY, rp.RevealScale); RevealNav(); }
                }
            }
            MoveDot();
        }
        _miniEq.Running = (_live == Live.Media || _live == Live.Both) && s == St.Collapsed;

        var P = AnimProfiles.Get(S.IslandAnim);
        if (anim)
        {
            if (exp)
            {
                // Expandir: a forma "se abre" com larguras e alturas em molas diferentes (efeito de morph);
                // o conteúdo entra um instante depois, com escala + subida, e os cartões chegam em cascata.
                // Os números de cada estilo (Fluida, Elástica, Cristal, Precisa, Bolha) ficam em AnimProfiles.
                if (_expO.Value < 0.05) { _expS.Snap(P.SStart); _expY.Snap(P.YStart); }   // ponto de partida do estilo (conteúdo ainda invisível)
                _w.To(w, 0, P.WK, P.WC); _h.To(h, P.HDelay, P.HK, P.HC); _r.To(r, P.RDelay, P.RK, P.RC);
                _expO.To(1, P.ODelay, P.OK, P.OC); _expS.To(1, P.SDelay, P.SK, P.SC); _expY.To(0, P.YDelay, P.YK, P.YC);
                _miniO.To(0, 0, 420, 38); _bannerO.To(0, 0, 420, 38);
            }
            else
            {
                // Recolher: o conteúdo some primeiro (rápido) e só então a forma encolhe — nada fica espremido.
                bool banner = s == St.Banner;
                double k = banner ? 360 : fastOut ? Math.Min(P.CK * 2.35, 1400) : P.CK, c = banner ? 24 : fastOut ? P.CC * 1.65 : P.CC;
                double shapeDelay = (s == St.Collapsed && !fastOut) ? P.CShapeDelay : 0;
                _w.To(w, shapeDelay, k, c); _h.To(h, shapeDelay, k, c); _r.To(r, shapeDelay, k, c);
                _expO.To(0, 0, fastOut ? 900 : P.COK, fastOut ? 60 : P.COC);
                _expS.To(P.SOut, 0, fastOut ? 600 : P.CSK, fastOut ? 44 : P.CSC);
                _expY.To(P.YOut, 0, 420, 34);
                _miniO.To(s == St.Collapsed ? 1 : 0, s == St.Collapsed ? (fastOut ? 0.02 : P.MiniDelay) : 0, 420, 38);
                _bannerO.To(s == St.Banner ? 1 : 0, s == St.Banner ? 0.10 : 0, 420, 38);
            }
        }
        else
        {
            _w.Snap(w); _h.Snap(h); _r.Snap(r);
            _miniO.Snap(s == St.Collapsed ? 1 : 0); _bannerO.Snap(s == St.Banner ? 1 : 0); _expO.Snap(exp ? 1 : 0);
            _expS.Snap(exp ? 1 : P.SOut); _expY.Snap(exp ? 0 : P.YStart);
        }
        _exp.IsHitTestVisible = exp;
        _mini.IsHitTestVisible = s == St.Collapsed;
        Frame();
        _loop.Kick();
        if (exp) { _activity = DateTime.Now; }
        else { _bannerT.Stop(); }
    }

    IPage? CurrentPage() => _pageId != null && _pages.TryGetValue(_pageId, out var p) ? p : null;

    void Expand() { if (_noExpand) return; SetState(St.Expanded); }
    void Collapse() => SetState(St.Collapsed);
    void Toggle() { if (_st == St.Expanded) Collapse(); else Expand(); }

    // ---------------- Animation frame step ----------------
    static void Soft(UIElement e)
    {
        double r = (1 - e.Opacity) * 14;
        if (e.Opacity <= 0.01 || r < 0.3) { if (e.Effect != null) e.Effect = null; return; }
        if (e.Effect is BlurEffect b) b.Radius = r;
        else e.Effect = new BlurEffect { Radius = r, RenderingBias = RenderingBias.Performance };
    }

    void Frame()
    {
        double w = _w.Value, h = _h.Value;
        double rb = Math.Clamp(_r.Value, 0, Math.Min(w, h) / 2);
        _island.Width = Math.Max(1, w); _island.Height = Math.Max(1, h);
        if (Floating)
        {
            var g = Geo.Round(w, h, rb, rb);
            _island.Clip = g; _outline.Data = g;
        }
        else
        {
            // Clássico: topo reto colado na borda da tela, ombros côncavos e base arredondada (silhueta de notch).
            double ear = Math.Min(11, Math.Max(0, h * 0.3));
            var (fill, line) = NotchGeo.Make(w, h, ear, rb);
            _island.Clip = fill; _outline.Data = line;
        }
        _mini.Opacity = Math.Clamp(_miniO.Value, 0, 1);
        _banner.Opacity = Math.Clamp(_bannerO.Value, 0, 1);
        _exp.Opacity = Math.Clamp(_expO.Value, 0, 1);
        Soft(_mini); Soft(_banner);                      // só nos elementos pequenos; blur no painel grande engasgava a animação
        if (_exp.Effect != null) _exp.Effect = null;
        _expScale.ScaleX = _expScale.ScaleY = _expS.Value;
        _expShift.Y = _expY.Value;
        double stretch = Math.Min(Math.Abs(_dotX.Velocity) * 0.014, 14);   // squash & stretch da lente (vertical)
        _navDot.Height = LensH + stretch;
        _navDot.Width = LensW - Math.Min(stretch * 0.2, 4);
        ((TranslateTransform)_navDot.RenderTransform).Y = _dotX.Value - stretch / 2;
        // Cada ícone acende e cresce conforme a lente passa por ele (tudo sincronizado com o movimento da lente).
        double lensC = _dotX.Value + LensH / 2;
        var off = ((SolidColorBrush)K.Label).Color; var on = ((SolidColorBrush)K.White).Color;
        foreach (var kv in _navCenter)
        {
            double near = Math.Clamp(1 - Math.Abs(lensC - kv.Value) / (LensH * 0.85), 0, 1);
            if (_navBrush.TryGetValue(kv.Key, out var br)) br.Color = FK.Lerp(off, on, near);
            if (_navScale.TryGetValue(kv.Key, out var sc)) sc.ScaleX = sc.ScaleY = 1 + 0.09 * near;
        }
        _mini.Width = Math.Max(1, Math.Min(w, CollapsedWidthNow) - 2 * MiniEar);

        if (_incoming != null)
        {
            // Troca em sequência: a página antiga sai primeiro (0 → 30%) e só então a nova entra (22% → 100%),
            // assim nunca há duas páginas visíveis ao mesmo tempo (sem "ghosting").
            double t = _pg.Value;
            double tin = Math.Clamp((t - 0.3) / 0.7, 0, 1), tout = Math.Clamp(t / 0.3, 0, 1);
            _incoming.Opacity = tin;
            ((TranslateTransform)_incoming.RenderTransform).Y = (1 - t) * 26 * _dir;
            if (_outgoing != null)
            {
                _outgoing.Opacity = 1 - tout;
                if (tout >= 1) _outgoing.Visibility = Visibility.Hidden;
                ((TranslateTransform)_outgoing.RenderTransform).Y = -tout * 18 * _dir;
            }
            if (_pg.Settled) FinishTransition();
        }
        UpdateRegion();
    }

    // ---------------- Real backdrop blur capture ----------------
    void CaptureBackdrop()
    {
        if (S.Look == IslandLook.SolidBlack || S.BlurAmount <= 0 || _hwnd == IntPtr.Zero) return;
        try
        {
            double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
            if (!Native.GetWindowRect(_hwnd, out var rc)) return;
            double padX = S.BlurAmount, padY = S.BlurAmount;
            int wpx = (int)((S.ExpandedWidth + padX * 2) * dpi), hpx = (int)((S.ExpandedHeight + padY) * dpi);
            int x = rc.Left + (int)((rc.Right - rc.Left - wpx) / 2.0), y = rc.Top + (int)(_island.Margin.Top * dpi);
            using var bmp = new System.Drawing.Bitmap(wpx, hpx);
            using (var gr = System.Drawing.Graphics.FromImage(bmp))
                gr.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(wpx, hpx));
            IntPtr hb = bmp.GetHbitmap();
            try
            {
                var src = Imaging.CreateBitmapSourceFromHBitmap(hb, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                _backdrop.Source = src;
            }
            finally { Native.DeleteObject(hb); }
        }
        catch (Exception e) { Log.Write(e); }
    }

    // ---------------- Interaction ----------------
    void OnPreviewDown(object sender, MouseButtonEventArgs e)
    {
        _activity = DateTime.Now;
        if (e.OriginalSource is DependencyObject d && FindParent<System.Windows.Controls.Primitives.TextBoxBase>(d) != null) EnableKeyboard();
    }

    static T? FindParent<T>(DependencyObject d) where T : DependencyObject
    {
        while (d != null) { if (d is T t) return t; d = VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d); }
        return null;
    }

    public void EnableKeyboard()
    {
        int ex = Native.GetWindowLong(_hwnd, Native.GWL_EXSTYLE);
        Native.SetWindowLong(_hwnd, Native.GWL_EXSTYLE, ex & ~Native.WS_EX_NOACTIVATE);
        Activate();
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        int ex = Native.GetWindowLong(_hwnd, Native.GWL_EXSTYLE);
        Native.SetWindowLong(_hwnd, Native.GWL_EXSTYLE, ex | Native.WS_EX_NOACTIVATE);
        if (_st == St.Expanded) ArmCollapse();
    }

    bool GestureOk => S.CollapsedGestures && _st == St.Collapsed && !_noExpand;
    static bool CtrlDown => Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

    void OnIslandMove(object sender, MouseEventArgs e)
    {
        // Ilha recolhida com mídia: arrastar para os lados troca de faixa (segure Ctrl para reposicionar a ilha).
        if (_mouseDown && e.LeftButton == MouseButtonState.Pressed && GestureOk && Svc.Media.HasSession && !CtrlDown)
        {
            var d = e.GetPosition(this) - _downPos;
            if (Math.Abs(d.X) >= 34 && Math.Abs(d.X) > Math.Abs(d.Y) * 1.4) { _mouseDown = false; _swiped = true; DoSwipe(d.X < 0); return; }
            if (d.Length >= 7) return;      // não deixa o "arrastar para mover" começar antes de o gesto poder acontecer
        }
        if (!_mouseDown || e.LeftButton != MouseButtonState.Pressed || !S.AllowDrag) return;
        if ((e.GetPosition(this) - _downPos).Length < 7) return;
        _mouseDown = false;
        try { DragMove(); } catch { }
        SaveDraggedPosition();
    }

    void OnIslandUp(object sender, MouseButtonEventArgs e)
    {
        bool click = _mouseDown; _mouseDown = false;
        if (_swiped) { _swiped = false; return; }
        if (!click || (e.GetPosition(this) - _downPos).Length > 12) return;
        if (_st == St.Collapsed && S.ExpandOnClick) Expand();
        else if (_st == St.Banner)
        {
            _bannerT.Stop();
            if (_bannerItem?.Action == "update") { SetState(St.Collapsed); _ = Svc.Updates.InstallAsync(); }
            else { ShowPage("inbox", false); Expand(); }
        }
    }

    // ---------------- Gestos na ilha recolhida ----------------
    void OnIslandWheel(object sender, MouseWheelEventArgs e)
    {
        if (!GestureOk) return;
        e.Handled = true; _activity = DateTime.Now;
        var a = Svc.Audio;
        if (a.Muted && e.Delta > 0) a.Muted = false;
        float v = Math.Clamp(a.Volume + (float)(e.Delta / 120.0 * 0.03), 0f, 1f);
        a.Volume = v;
        string ico = a.Muted || v <= 0.001f ? "volume-off" : v < 0.5f ? "volume-low" : "volume";
        ShowHud(ico, v, $"{Math.Round(v * 100)}", null);
    }

    void OnIslandMiddle(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || !GestureOk) return;
        e.Handled = true; _activity = DateTime.Now;
        if (!Svc.Media.HasSession) { ShowHud("music", null, "", "Nada tocando"); return; }
        bool willPlay = !Svc.Media.Playing;
        Svc.Media.PlayPause();
        ShowHud(willPlay ? "play" : "pause", null, "", willPlay ? "Reproduzindo" : "Pausado");
    }

    void DoSwipe(bool next)
    {
        if (next) Svc.Media.Next(); else Svc.Media.Prev();
        ShowHud(next ? "skip-forward" : "skip-back", null, "", next ? "Próxima faixa" : "Faixa anterior");
        if (S.Animations)
        {
            var nudge = new DoubleAnimation(next ? -12 : 12, 0, TimeSpan.FromMilliseconds(520)) { EasingFunction = new ElasticEase { Oscillations = 1, Springiness = 5, EasingMode = EasingMode.EaseOut } };
            _miniShift.BeginAnimation(TranslateTransform.XProperty, nudge);
        }
    }

    void ShowHud(string icon, double? frac, string text, string? label)
    {
        _hudIcon.Child = Ico.Make(icon, 15, Brushes.White, 1.9);
        bool bar = frac != null;
        _hudTrack.Visibility = _hudFill.Visibility = bar ? Visibility.Visible : Visibility.Collapsed;
        _hudLabel.Visibility = bar ? Visibility.Collapsed : Visibility.Visible;
        _hudLabel.Text = label ?? ""; _hudText.Text = text;
        _hudFill.Background = _accent;
        _hudFrac = frac ?? 0; UpdateHudFill();
        FadeHud(true);
        _hudT.Stop(); _hudT.Start();
    }

    void UpdateHudFill() => _hudFill.Width = Math.Max(4, _hudBar.ActualWidth * Math.Clamp(_hudFrac, 0, 1));

    void FadeHud(bool on)
    {
        _hud.IsHitTestVisible = false;
        double ms = S.Animations ? (on ? 140 : 320) : 1;
        _hud.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(on ? 1 : 0, TimeSpan.FromMilliseconds(ms)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
    }

    // ---------------- Cor dinâmica da capa ----------------
    void ApplyAccent()
    {
        var c = S.DynamicCoverColor ? Svc.Media.Accent : Colors.White;
        if (c == _accentTarget) return;
        _accentTarget = c;
        if (S.Animations) _accent.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(c, TimeSpan.FromMilliseconds(600)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        else { _accent.BeginAnimation(SolidColorBrush.ColorProperty, null); _accent.Color = c; }
    }

    void ArmCollapse()
    {
        _collapseT.Stop();
        if (S.CollapseDelayMs <= 0 && !KeepOpen && !_island.IsMouseOver) { Collapse(); return; }
        _collapseT.Interval = TimeSpan.FromMilliseconds(Math.Max(30, S.CollapseDelayMs));
        _collapseT.Start();
    }

    bool KeepOpen => CurrentPage()?.KeepOpen == true || IsKeyboardFocusWithin && Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase
                     || _island.ContextMenu?.IsOpen == true || Mouse.Captured != null;

    void OnCollapseTick(object? s, EventArgs e)
    {
        _collapseT.Stop();
        if (_st != St.Expanded) return;
        if (KeepOpen) { ArmCollapse(); return; }
        if (_island.IsMouseOver)
        {
            // mouse parado em cima: só recolhe por inatividade, se isso estiver ligado
            double lim = S.IdleCollapseSeconds;
            if (lim > 0 && (DateTime.Now - _activity).TotalSeconds >= lim) { Collapse(); return; }
            _collapseT.Interval = TimeSpan.FromSeconds(1); _collapseT.Start();
            return;
        }
        Collapse();
    }

    // ---------------- Banner alert ----------------
    InboxItem? _bannerItem;
    void ShowBanner(InboxItem it)
    {
        _bannerItem = it;
        _bTitle.Text = it.Title; _bText.Text = it.Text.Length > 0 ? it.Text : it.Source;
        _bIcon.Text = it.Source.Length > 0 ? it.Source[..1].ToUpperInvariant() : "!";
        var brand = Brand.Match(it.Source);
        if (brand != null)
        {
            var (bg, fg) = Brand.Colors(brand);
            _bBadge.Child = Brand.Logo(brand, 20, fg);
            _bBadge.Background = bg;
        }
        else { _bBadge.Child = _bIcon; _bBadge.Background = it.Important ? K.Orange : K.CardStrong; }
        if (_st == St.Expanded || Visibility != Visibility.Visible) return;
        SetState(St.Banner);
        _bannerT.Interval = TimeSpan.FromSeconds(it.Important ? S.BannerSeconds + 3 : S.BannerSeconds);
        _bannerT.Stop(); _bannerT.Start();
    }

    // ---------------- Pílula recolhida: LED pulsante ----------------
    new bool IsActive()
    {
        if (S.IsOn("focus") && Svc.Focus.AnyRunning) return true;
        if (S.IsOn("media") && Svc.Media.Playing && Svc.Media.Title.Length > 0) return true;
        if (S.IsOn("ai") && (Svc.Agents.Attention != null || Svc.Agents.Working != null)) return true;
        if (Svc.Camera.IsOn || Svc.Dictation.Listening) return true;
        if (Svc.Audio.MicInUse && S.MonitorMicrophone) return true;
        return false;
    }

    public void RefreshMini()
    {
        // Os eventos de mídia chegam de tarefas assíncronas sem "await": uma exceção aqui sumiria sem deixar rastro. Por isso o log.
        try { SetLed(IsActive()); UpdateLive(); }
        catch (Exception e) { Log.Write(e); }
    }

    static bool MediaPlaying() => S.IsOn("media") && Svc.Media.HasSession && Svc.Media.Playing && (Svc.Media.Title.Length > 0 || Svc.Media.App.Length > 0);

    /// <summary>Decide o que a ilha recolhida mostra (mídia, timer ou os dois) e reorganiza a faixa quando isso muda.</summary>
    void UpdateLive()
    {
        bool media = MediaPlaying();
        var info = S.IsOn("focus") ? Svc.Focus.Primary() : null;
        bool timer = info != null;
        var ag = AgendaNow();
        var dm = Svc.Downloads;
        bool dl = S.ShowDownloadProgress && S.WatchDownloads && (dm.Active || dm.JustFinished);
        // prioridade: compromisso chegando > mídia/timer > download (que ainda aparece como linha fina embaixo quando há mídia/timer)
        var mode = ag != null ? Live.Agenda : media && timer ? Live.Both : media ? Live.Media : timer ? Live.Timer : dl ? Live.Download : Live.None;
        UpdateAgenda(ag);
        UpdateDownload(dl);

        if (media)
        {
            bool video = Svc.Media.IsVideo;
            if (_mIcon.Child == null || video != _liveVideo) { _liveVideo = video; _mIcon.Child = Ico.Make(video ? "video" : "music", 16, Brushes.White, 1.8); }
        }
        _miniEq.Playing = media;
        if (timer)
        {
            bool brk = info!.Kind == LiveKind.Pomodoro && Svc.Focus.Pomo.Phase != FocusPhase.Focus;
            if (_tIcon.Child == null || info.Kind != _liveKind || brk != _liveBreak)
            {
                _liveKind = info.Kind; _liveBreak = brk;
                _tIcon.Child = Ico.Make(info.Kind == LiveKind.Timer ? "hourglass" : "timer", 15, brk ? K.Green : Brushes.White, 1.8);
            }
            if (_tText.Text != info.Time) _tText.Text = info.Time;
            string ex = info.Extra > 0 ? "+" + info.Extra : "";
            if (_tExtra.Text != ex) _tExtra.Text = ex;
        }

        if (mode != _live)
        {
            var prev = _live; _live = mode;
            Log.Write($"ilha recolhida: {prev} -> {mode} (mídia: sessão={Svc.Media.HasSession} tocando={Svc.Media.Playing} título=\"{Svc.Media.Title}\" app=\"{Svc.Media.App}\"; timer={(info == null ? "nenhum" : info.Kind.ToString())})");
            LayoutLive(mode);
            bool first = _liveShown == Live.None && prev == Live.None && _ledOn == null;
            double ms = first || !S.Animations ? 1 : 300;
            var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
            if (prev == Live.None || mode == Live.None)
            {
                _miniLive.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(mode == Live.None ? 0 : 1, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease });
                _ledHost.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(mode == Live.None ? 1 : 0, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease });
            }
            else   // trocou de combinação: a faixa some e volta já reorganizada
                _miniLive.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease });
            _liveShown = mode;
            if (_st == St.Collapsed)
            {
                var (w, _, _) = Target(St.Collapsed);
                if (S.Animations) _w.To(w, 0, 380, 24); else _w.Snap(w);
                _loop.Kick();
            }
        }
        _miniEq.Running = (mode == Live.Media || mode == Live.Both) && _st == St.Collapsed;
    }

    // ---------------- Próximo compromisso ----------------
    (CalEvent ev, TimeSpan left)? AgendaNow()
    {
        int m = S.AgendaAlertMinutes;
        if (m <= 0) return null;
        var now = DateTime.Now;
        var ev = Svc.Calendar.Between(now.AddMinutes(-1), now.AddMinutes(m)).Where(e => !e.AllDay && e.Start > now.AddSeconds(-60)).OrderBy(e => e.Start).FirstOrDefault();
        return ev == null ? null : (ev, ev.Start - now);
    }

    void UpdateAgenda((CalEvent ev, TimeSpan left)? ag)
    {
        if (ag == null) { _agendaKey = ""; return; }
        var (ev, left) = ag.Value;
        bool soon = left.TotalMinutes <= 5;
        string key = ev.Title + "|" + ev.Start.Ticks;
        if (key != _agendaKey || soon != _agendaSoon || _aIcon.Child == null)
        {
            _agendaKey = key; _agendaSoon = soon;
            _aIcon.Child = Ico.Make("calendar", 15, soon ? K.Orange : Brushes.White, 1.8);
            _aTitle.Text = Trim(string.IsNullOrWhiteSpace(ev.Title) ? "Compromisso" : ev.Title, 15);
            _aTime.Foreground = soon ? K.Orange : K.Dim;
        }
        _aTime.Text = left.TotalSeconds <= 0 ? "agora" : left.TotalMinutes > 5 ? $"em {Math.Ceiling(left.TotalMinutes)} min" : "em " + K.Fmt(TimeSpan.FromSeconds(Math.Ceiling(left.TotalSeconds)));
    }

    // ---------------- Download ao vivo ----------------
    void UpdateDownload(bool on)
    {
        var dm = Svc.Downloads;
        if (on && dm.Active)
        {
            if (_dIcon.Child == null) _dIcon.Child = Ico.Make("download", 15, Brushes.White, 1.9);
            string t = DownloadMonitor.FormatBytes(dm.TotalBytes) + (dm.TotalSpeed > 1024 ? " · " + DownloadMonitor.FormatSpeed(dm.TotalSpeed) : "");
            if (dm.Count > 1) t += $" +{dm.Count - 1}";
            if (_dText.Text != t) _dText.Text = t;
            if (!_dlRunning) StartDlBar();
        }
        else if (on && dm.JustFinished)
        {
            if (_dlRunning && !_dlFinishing) FinishDlBar();
        }
        else if (_dlRunning || _dlFinishing) StopDlBar();
    }

    double DlWidth => Math.Max(40, _mini.Width - 48);

    void StartDlBar()
    {
        _dlRunning = true; _dlFinishing = false;
        double w = DlWidth, seg = Math.Max(30, w * 0.38);
        _dlFill.BeginAnimation(WidthProperty, null);
        _dlFill.Background = _accent;                     // acompanha a cor da capa quando há música
        _dlFill.Width = seg;
        var sweep = new DoubleAnimation(-seg, w, TimeSpan.FromMilliseconds(1150)) { RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        _dlShift.BeginAnimation(TranslateTransform.XProperty, sweep);
        _dlBar.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(S.Animations ? 260 : 1)));
    }

    /// <summary>O arquivo terminou de verdade: a barra enche até o fim (verde) e some.</summary>
    void FinishDlBar()
    {
        _dlFinishing = true;
        _dlShift.BeginAnimation(TranslateTransform.XProperty, null); _dlShift.X = 0;
        _dlFill.Background = K.Green;
        var fill = new DoubleAnimation(DlWidth, TimeSpan.FromMilliseconds(S.Animations ? 320 : 1)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        _dlFill.BeginAnimation(WidthProperty, fill);
    }

    void StopDlBar()
    {
        _dlRunning = _dlFinishing = false;
        _dlBar.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(S.Animations ? 380 : 1)));
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        t.Tick += (_, _) => { t.Stop(); if (!_dlRunning) { _dlShift.BeginAnimation(TranslateTransform.XProperty, null); _dlFill.BeginAnimation(WidthProperty, null); _dlFill.Width = 0; } };
        t.Start();
    }

    void LayoutLive(Live m)
    {
        _liveL.Children.Clear(); _liveR.Children.Clear();
        bool both = m == Live.Both;
        _tText.Margin = new Thickness(both || m == Live.Timer ? 7 : 0, 0, 0, 0);
        _miniEq.Margin = new Thickness(both ? 8 : 0, 0, 0, 0);
        if (m == Live.Media) { _liveL.Children.Add(_mIcon); _liveR.Children.Add(_miniEq); }
        else if (m == Live.Timer) { _liveL.Children.Add(_tIcon); _liveR.Children.Add(_tText); _liveR.Children.Add(_tExtra); _tText.Margin = new Thickness(0); }
        else if (m == Live.Agenda) { _liveL.Children.Add(_aIcon); _liveL.Children.Add(_aTitle); _liveR.Children.Add(_aTime); }
        else if (m == Live.Download) { _liveL.Children.Add(_dIcon); _liveR.Children.Add(_dText); }
        else if (both) { _liveL.Children.Add(_tIcon); _liveL.Children.Add(_tText); _liveL.Children.Add(_tExtra); _liveR.Children.Add(_mIcon); _liveR.Children.Add(_miniEq); }
    }

    void SetLed(bool active)
    {
        if (!_ledPulsing && S.Animations)
        {
            _ledPulsing = true;
            _ledIdle.Pulse(1700);     // ocioso: respiração lenta
            _ledActive.Pulse(950);    // ativo: um pouco mais viva
        }
        if (_ledOn == active) return;
        bool first = _ledOn == null;
        _ledOn = active;
        if (first || !S.Animations)
        {
            _ledIdle.BeginAnimation(UIElement.OpacityProperty, null); _ledActive.BeginAnimation(UIElement.OpacityProperty, null);
            _ledIdle.Opacity = active ? 0 : 1; _ledActive.Opacity = active ? 1 : 0;
            return;
        }
        var sine = new SineEase { EasingMode = EasingMode.EaseInOut };
        _ledIdle.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(active ? 0 : 1, TimeSpan.FromMilliseconds(550)) { EasingFunction = sine });
        _ledActive.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(active ? 1 : 0, TimeSpan.FromMilliseconds(550)) { EasingFunction = sine });
    }

    static string Trim(string s, int n) => s.Length > n ? s[..n] + "…" : s;

    // ---------------- Fullscreen monitoring ----------------
    static readonly HashSet<string> ScreenshotApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "ScreenClippingHost", "SnippingTool", "ScreenSketch", "ShareX", "Greenshot", "Lightshot", "flameshot",
        "PicPick", "Snagit32", "SnagitCapture", "SnagitEditor", "Screenpresso", "Gyazo", "GyazoReplay", "Monosnap"
    };

    static bool IsScreenshotTool()
    {
        try
        {
            var h = Native.GetForegroundWindow();
            if (h == IntPtr.Zero) return false;
            Native.GetWindowThreadProcessId(h, out uint pid);
            if (pid == 0) return false;
            using var p = System.Diagnostics.Process.GetProcessById((int)pid);
            return ScreenshotApps.Contains(p.ProcessName);
        }
        catch { return false; }
    }

    void CheckFullscreen()
    {
        bool fs = false;
        try { if (Native.SHQueryUserNotificationState(out int st) == 0) fs = st is 2 or 3 or 4; } catch { }
        // A camada de captura de tela (Win+Shift+S, Print Screen, ShareX...) cobre a tela inteira, mas não é um app em tela cheia:
        // a ilha deve continuar visível para sair no print.
        if (fs && IsScreenshotTool()) fs = false;
        switch (S.Fullscreen)
        {
            case FullscreenMode.Hide:
                _noExpand = fs;
                var want = fs ? Visibility.Hidden : Visibility.Visible;
                if (Visibility != want) Visibility = want;
                break;
            case FullscreenMode.NoExpand:
                _noExpand = fs; if (fs && _st == St.Expanded) Collapse(); break;
            default: _noExpand = false; break;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_hwnd != IntPtr.Zero) { Native.UnregisterHotKey(_hwnd, 1); Native.UnregisterHotKey(_hwnd, 2); try { Native.RemoveClipboardFormatListener(_hwnd); } catch { } }
        base.OnClosed(e);
    }
}
