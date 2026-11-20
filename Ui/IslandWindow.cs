using System.Windows.Interop;
using System.Windows.Media.Effects;
using Shapes = System.Windows.Shapes;

namespace TzIsland;

public sealed class IslandWindow : Window
{
    public static IslandWindow? Instance { get; private set; }

    enum St { Collapsed, Banner, Expanded }
    St _st = St.Collapsed;
    IntPtr _hwnd;
    HwndSource? _src;

    // árvore visual
    readonly Grid _root = new();
    readonly Border _island = new();
    readonly Grid _layers = new();
    readonly Image _backdrop = new() { Stretch = Stretch.Fill, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
    readonly Shapes.Rectangle _tint = new(), _sheen = new() { IsHitTestVisible = false };
    readonly Shapes.Path _outline = new() { IsHitTestVisible = false, StrokeThickness = 1 };
    readonly Grid _mini = new(), _banner = new(), _exp = new();
    readonly Grid _pageHost = new();
    readonly Grid _navHost = new();
    readonly StackPanel _nav = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    readonly Border _navDot = new() { Width = 18, Height = 3, CornerRadius = new CornerRadius(2), Background = new LinearGradientBrush(Color.FromArgb(255, 0, 229, 255), Color.FromArgb(255, 0, 122, 255), 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, RenderTransform = new TranslateTransform() };
    readonly ScaleTransform _expScale = new(1, 1);
    readonly TranslateTransform _expShift = new();

    // mini
    readonly Image _miniCover = new() { Width = 20, Height = 20, Stretch = Stretch.UniformToFill };
    readonly Border _miniCoverBox = new() { Width = 20, Height = 20, CornerRadius = new CornerRadius(6), ClipToBounds = true, Visibility = Visibility.Collapsed };
    readonly TextBlock _miniGlyph = K.Glyph("", 13), _miniText = K.T("", 12, K.Fg), _miniRight = K.T("", 11, K.Dim);
    readonly Shapes.Ellipse _miniDot = new() { Width = 8, Height = 8, Visibility = Visibility.Collapsed };

    // banner
    readonly TextBlock _bTitle = K.T("", 13, K.Fg, true), _bText = K.T("", 12, K.Dim), _bIcon = K.T("", 14, Brushes.Black, true);
    readonly Border _bBadge = new() { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = K.Fg };

    // movimento
    readonly MotionLoop _loop;
    readonly Spring _w, _h, _r, _miniO, _bannerO, _expO, _expS, _expY, _pg, _dotX;

    readonly Dictionary<string, IPage> _pages = new();
    readonly Dictionary<string, Button> _navBtns = new();
    readonly TextBlock _badge = K.T("", 9, Brushes.White, true);
    readonly Border _badgeBox = new() { Background = K.Red, CornerRadius = new CornerRadius(7), MinWidth = 14, Height = 14, Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false, Padding = new Thickness(3, 0, 3, 0) };
    string? _pageId;
    bool _pageActive;
    FrameworkElement? _incoming, _outgoing;

    readonly DispatcherTimer _hoverT = new(), _collapseT = new(), _bannerT = new(), _clockT = new() { Interval = TimeSpan.FromSeconds(1) }, _fsT = new() { Interval = TimeSpan.FromSeconds(2) };
    DateTime _activity = DateTime.Now;
    Point _downPos; bool _mouseDown;
    bool _noExpand, _hotkeysOn;
    bool _clickExpanded;

    static AppSettings S => Settings.Current;
    bool Floating => true; // pílula flutuante, como a Dynamic Island

    public IslandWindow()
    {
        Instance = this;
        Title = "TzIsland";
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = false;
        Background = Brushes.Transparent; ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        UseLayoutRounding = true; SnapsToDevicePixels = true;

        _loop = new MotionLoop(Frame);
        _w = _loop.Add(new Spring(200)); _h = _loop.Add(new Spring(34)); _r = _loop.Add(new Spring(16));
        _miniO = _loop.Add(new Spring(1)); _bannerO = _loop.Add(new Spring(0)); _expO = _loop.Add(new Spring(0));
        _expS = _loop.Add(new Spring(0.88)); _expY = _loop.Add(new Spring(10));
        _pg = _loop.Add(new Spring(1)); _dotX = _loop.Add(new Spring(0));

        BuildTree();
        ApplySettings();

        SourceInitialized += OnSourceInit;
        Loaded += (_, _) => { Place(); SetState(St.Collapsed, true); RefreshMini(); };
        Settings.Changed += OnSettingsChanged;
        Hub.Banner += ShowBanner;
        Hub.Inbox.CollectionChanged += (_, _) => UpdateBadge();
        Svc.Media.Changed += RefreshMini; Svc.Battery.Changed += RefreshMini; Svc.Audio.MicChanged += RefreshMini;
        Svc.Focus.Tick += RefreshMini; Svc.Agents.Changed += RefreshMini; Svc.Camera.StateChanged += RefreshMini;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.InvokeAsync(Place);

        _hoverT.Tick += (_, _) => { _hoverT.Stop(); if (_island.IsMouseOver) Expand(); };
        _collapseT.Tick += OnCollapseTick;
        _bannerT.Tick += (_, _) => { _bannerT.Stop(); if (_st == St.Banner && !_island.IsMouseOver) SetState(St.Collapsed); };
        _clockT.Tick += (_, _) => { if (_st == St.Collapsed) RefreshMini(); };
        _clockT.Start();
        _fsT.Tick += (_, _) => CheckFullscreen();
        _fsT.Start();
    }

    // ---------------- construção ----------------
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

        // mini: [capa/ícone] [texto] [direita/ponto]
        _mini.HorizontalAlignment = HorizontalAlignment.Center; _mini.VerticalAlignment = VerticalAlignment.Top;
        _mini.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _mini.ColumnDefinitions.Add(new ColumnDefinition { Width = K.GL("*") });
        _mini.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _miniCoverBox.Child = _miniCover;
        var left = new Grid { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(_miniGlyph); left.Children.Add(_miniCoverBox);
        _mini.Children.Add(left);
        _miniText.HorizontalAlignment = HorizontalAlignment.Center; _miniText.VerticalAlignment = VerticalAlignment.Center; _miniText.Margin = new Thickness(8, 0, 8, 0);
        _mini.Children.Add(_miniText.At(col: 1));
        var right = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(_miniDot.M(0, 0, 5, 0)); right.Children.Add(_miniRight);
        _mini.Children.Add(right.At(col: 2));

        // banner
        _banner.HorizontalAlignment = HorizontalAlignment.Center; _banner.VerticalAlignment = VerticalAlignment.Top; _banner.Opacity = 0;
        _banner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _banner.ColumnDefinitions.Add(new ColumnDefinition { Width = K.GL("*") });
        _bIcon.HorizontalAlignment = HorizontalAlignment.Center; _bIcon.VerticalAlignment = VerticalAlignment.Center;
        _bBadge.Child = _bIcon; _bBadge.Margin = new Thickness(18, 0, 0, 0); _bBadge.VerticalAlignment = VerticalAlignment.Center;
        _banner.Children.Add(_bBadge);
        var bs = new StackPanel { Margin = new Thickness(12, 0, 20, 0), VerticalAlignment = VerticalAlignment.Center };
        bs.Children.Add(_bTitle); bs.Children.Add(_bText);
        _banner.Children.Add(bs.At(col: 1));

        // expandido
        _exp.HorizontalAlignment = HorizontalAlignment.Center; _exp.VerticalAlignment = VerticalAlignment.Top; _exp.Opacity = 0;
        _exp.RenderTransformOrigin = new Point(0.5, 0);
        _exp.RenderTransform = new TransformGroup { Children = { _expScale, _expShift } };
        _exp.RowDefinitions.Add(new RowDefinition { Height = K.GL("*") });
        _exp.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _pageHost.Margin = new Thickness(16, 14, 16, 0);
        _exp.Children.Add(_pageHost);
        _navHost.Margin = new Thickness(16, 4, 16, 8); _navHost.Height = 44;
        _nav.VerticalAlignment = VerticalAlignment.Center;
        _navHost.Children.Add(_nav);
        _navHost.Children.Add(_navDot);
        _exp.Children.Add(_navHost.At(row: 1));

        // eventos de entrada
        _island.MouseEnter += (_, _) => { _collapseT.Stop(); if (_st == St.Collapsed && S.ExpandOnHover && !_noExpand) { _hoverT.Interval = TimeSpan.FromMilliseconds(S.HoverDelayMs); _hoverT.Start(); } };
        _island.MouseLeave += (_, _) => { _hoverT.Stop(); if (_st == St.Expanded) ArmCollapse(); };
        _island.PreviewMouseMove += (_, _) => _activity = DateTime.Now;
        _island.PreviewMouseDown += OnPreviewDown;
        _island.MouseLeftButtonDown += (_, e) => { _mouseDown = true; _downPos = e.GetPosition(this); _clickExpanded = false; };
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

        _outline.Stroke = new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(55, 0, 229, 255), 0),
                new GradientStop(Color.FromArgb(30, 191, 90, 242), 0.5),
                new GradientStop(Color.FromArgb(45, 0, 212, 170), 1)
            }, 90);
        _outline.StrokeThickness = 1.2;
        _badgeBox.Child = _badge;
    }

    // ---------------- janela / posição ----------------
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
        Native.DwmSetWindowAttribute(_hwnd, 34, ref none, 4);      // sem borda (Win11)
        Native.DwmSetWindowAttribute(_hwnd, 33, ref noRound, 4);   // sem cantos do sistema (Win11)
        _src?.AddHook(WndProc);
        RegisterHotkeys();
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY)
        {
            handled = true;
            if (w.ToInt32() == 1) Toggle();
            else if (w.ToInt32() == 2) { OpenPage("dictation"); _ = Svc.Dictation.ToggleAsync(); }
        }
        return IntPtr.Zero;
    }

    void RegisterHotkeys()
    {
        if (_hwnd == IntPtr.Zero) return;
        Native.UnregisterHotKey(_hwnd, 1); Native.UnregisterHotKey(_hwnd, 2);
        if (Native.TryParseHotkey(S.ToggleHotkey, out var m1, out var k1)) Native.RegisterHotKey(_hwnd, 1, m1 | 0x4000, k1);
        if (S.IsOn("dictation") && Native.TryParseHotkey(S.DictationHotkey, out var m2, out var k2)) Native.RegisterHotKey(_hwnd, 2, m2 | 0x4000, k2);
        _hotkeysOn = true;
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

    /// <summary>Restringe a janela ao retângulo da ilha: o resto fica "vazado" e deixa os cliques passarem.</summary>
    void UpdateRegion()
    {
        if (_hwnd == IntPtr.Zero) return;
        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        double w = _w.Value, h = _h.Value, left = (Width - w) / 2, top = _island.Margin.Top;
        int l = (int)Math.Floor(left * dpi), t = (int)Math.Floor(top * dpi), r = (int)Math.Ceiling((left + w) * dpi), b = (int)Math.Ceiling((top + h) * dpi);
        Native.SetWindowRgn(_hwnd, Native.CreateRectRgn(l, t, r, b), false);
    }

    // ---------------- configurações ----------------
    void OnSettingsChanged()
    {
        ApplySettings();
        Place();
        RegisterHotkeys();
        SetState(_st, true);
        RefreshMini();
    }

    void ApplySettings()
    {
        double bannerW = Math.Max(S.CollapsedWidth + 180, 400), bannerH = 76;
        Width = Math.Max(S.ExpandedWidth, bannerW) + 48;
        Height = Math.Max(S.ExpandedHeight, bannerH) + 12 + 40;
        _island.Margin = new Thickness(0, Floating ? 10 : 0, 0, 0);
        _exp.Width = S.ExpandedWidth; _exp.Height = S.ExpandedHeight;
        _mini.Width = S.CollapsedWidth; _mini.Height = S.CollapsedHeight;
        _banner.Width = bannerW; _banner.Height = bannerH;
        _backdrop.Width = S.ExpandedWidth + S.BlurAmount * 2; _backdrop.Height = S.ExpandedHeight + S.BlurAmount; _backdrop.Margin = new Thickness(0, 0, 0, 0);
        ((BlurEffect)_backdrop.Effect).Radius = Math.Max(0.1, S.BlurAmount);
        ApplyLook();
        BuildNav();
        if (S.Fullscreen != FullscreenMode.Hide) Visibility = Visibility.Visible;
        _hotkeysOn = false;
    }

    void ApplyLook()
    {
        double a = Math.Clamp(S.Opacity, 0.3, 1);
        _backdrop.Visibility = S.Look == IslandLook.SolidBlack || S.BlurAmount <= 0 ? Visibility.Collapsed : Visibility.Visible;
        switch (S.Look)
        {
            case IslandLook.SolidBlack:
                _tint.Fill = Brushes.Black; _sheen.Visibility = Visibility.Collapsed; break;
            case IslandLook.DarkGlass:
                _tint.Fill = new SolidColorBrush(Color.FromArgb((byte)(255 * (0.32 + 0.60 * a)), 8, 10, 20));
                _sheen.Fill = new LinearGradientBrush(Color.FromArgb(14, 0, 180, 220), Color.FromArgb(0, 0, 0, 0), 90);
                _sheen.VerticalAlignment = VerticalAlignment.Top; _sheen.Height = 50; _sheen.Visibility = Visibility.Visible; break;
            default:
                _tint.Fill = new LinearGradientBrush(
                    Color.FromArgb((byte)(255 * (0.14 + 0.28 * a)), 30, 40, 60),
                    Color.FromArgb((byte)(255 * (0.28 + 0.48 * a)), 8, 10, 18), 90);
                _sheen.Fill = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(38, 0, 229, 255), 0),
                        new GradientStop(Color.FromArgb(16, 191, 90, 242), 0.4),
                        new GradientStop(Color.FromArgb(0, 255, 255, 255), 1)
                    }, 90);
                _sheen.VerticalAlignment = VerticalAlignment.Top; _sheen.Height = 70; _sheen.Visibility = Visibility.Visible; break;
        }
    }

    void BuildNav()
    {
        _nav.Children.Clear(); _navBtns.Clear();
        foreach (var m in Modules.All.Where(m => S.IsOn(m.Id)))
        {
            var id = m.Id;
            var b = K.IconBtn(m.Glyph, m.Title, () => ShowPage(id), 16, 42, 32);
            _navBtns[id] = b;
            if (id == "inbox")
            {
                (_badgeBox.Parent as Panel)?.Children.Remove(_badgeBox);
                var host = new Grid(); host.Children.Add(b); host.Children.Add(_badgeBox);
                _badgeBox.Margin = new Thickness(0, 0, 0, 0);
                _nav.Children.Add(host);
            }
            else _nav.Children.Add(b);
        }
        _nav.Children.Add(new Border { Width = 1, Height = 18, Background = new SolidColorBrush(Color.FromArgb(40, 0, 229, 255)), Margin = new Thickness(6, 0, 6, 0) });
        _nav.Children.Add(K.IconBtn("\uE713", "Configurações", OpenSettings, 15, 42, 32));
        if (_pageId == null || !_navBtns.ContainsKey(_pageId)) _pageId = _navBtns.Keys.FirstOrDefault();
        UpdateBadge();
        Dispatcher.InvokeAsync(MoveDot, DispatcherPriority.Loaded);
    }

    void UpdateBadge()
    {
        int n = Hub.Inbox.Count;
        _badge.Text = n > 99 ? "99+" : n.ToString();
        _badgeBox.Visibility = n > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    void OpenSettings() { var w = SettingsWindow.ShowSingle(); w.Activate(); }

    // ---------------- páginas ----------------
    IPage? Make(string id) => id switch
    {
        "home" => new HomePage(), "media" => new MediaPage(), "inbox" => new InboxPage(), "productivity" => new ProductivityPage(),
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

    public void ShowPage(string id, bool animate = true)
    {
        if (!S.IsOn(id) || GetPage(id) is not IPage next) return;
        if (_pageId == id && _pageHost.Children.Contains(next.View)) { MoveDot(); return; }
        FinishTransition();
        IPage? prev = _pageId != null && _pages.TryGetValue(_pageId, out var pp) && _pageHost.Children.Contains(pp.View) ? pp : null;
        if (prev != null && _pageActive) prev.Hidden();
        _pageId = id;
        _pageHost.Children.Add(next.View);
        if (_pageActive) next.Shown();
        if (prev != null && animate && S.Animations)
        {
            _outgoing = prev.View; _incoming = next.View;
            next.View.Opacity = 0;
            _pg.Snap(0); _pg.To(1, 0, 320, 30);
        }
        else if (prev != null) _pageHost.Children.Remove(prev.View);
        MoveDot();
        _loop.Kick();
    }

    void FinishTransition()
    {
        if (_incoming != null) { _incoming.Opacity = 1; ((TranslateTransform)_incoming.RenderTransform).X = 0; }
        if (_outgoing != null) { _pageHost.Children.Remove(_outgoing); _outgoing.Opacity = 1; ((TranslateTransform)_outgoing.RenderTransform).X = 0; }
        _incoming = _outgoing = null;
    }

    public void OpenPage(string id) { ShowPage(id, false); Expand(); }

    void MoveDot()
    {
        if (_pageId == null || !_navBtns.TryGetValue(_pageId, out var b)) return;
        _nav.UpdateLayout();
        try
        {
            var p = b.TranslatePoint(new Point(b.ActualWidth / 2, 0), _navHost);
            double x = p.X - _navDot.Width / 2;
            if (S.Animations) _dotX.To(x, 0, 380, 26); else _dotX.Snap(x);
            _loop.Kick();
        }
        catch { }
    }

    // ---------------- estados ----------------
    (double w, double h, double r) Target(St s) => s switch
    {
        St.Expanded => (S.ExpandedWidth, S.ExpandedHeight, Floating ? 34 : 30),
        St.Banner => (Math.Max(S.CollapsedWidth + 180, 400), 76, Floating ? 38 : 26),
        _ => (S.CollapsedWidth, S.CollapsedHeight, Floating ? S.CollapsedHeight / 2 : Math.Min(18, S.CollapsedHeight * 0.55))
    };

    void SetState(St s, bool snap = false)
    {
        if (_st == s && !snap) return;
        var prev = _st; _st = s;
        var (w, h, r) = Target(s);
        bool exp = s == St.Expanded, anim = S.Animations && !snap;

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
                    K.FadeIn(cur.View, 90, 8);
                }
            }
            MoveDot();
        }

        double sSt = exp ? 320 : s == St.Banner ? 340 : 420, sDm = exp ? 24 : s == St.Banner ? 22 : 40;
        double dl = 0;
        if (anim)
        {
            _w.To(w, dl, sSt, sDm); _h.To(h, dl, sSt, sDm); _r.To(r, dl, sSt, sDm);
            _miniO.To(s == St.Collapsed ? 1 : 0, s == St.Collapsed ? 0.14 : 0, 420, 40);
            _bannerO.To(s == St.Banner ? 1 : 0, s == St.Banner ? 0.10 : 0, 420, 40);
            _expO.To(exp ? 1 : 0, exp ? 0.12 : 0, 300, 30);
            _expS.To(exp ? 1 : 0.88, exp ? 0.04 : 0, 300, 24);
            _expY.To(exp ? 0 : 10, exp ? 0.06 : 0, 260, 24);
        }
        else
        {
            _w.Snap(w); _h.Snap(h); _r.Snap(r);
            _miniO.Snap(s == St.Collapsed ? 1 : 0); _bannerO.Snap(s == St.Banner ? 1 : 0); _expO.Snap(exp ? 1 : 0);
            _expS.Snap(exp ? 1 : 0.88); _expY.Snap(exp ? 0 : 10);
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

    // ---------------- quadro de animação ----------------
    /// <summary>Desfoca o conteúdo enquanto ele entra/sai; em repouso remove o efeito (custo zero).</summary>
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
        double rb = Math.Clamp(_r.Value, 0, Math.Min(w, h) / 2), rt = Floating ? rb : 0;
        _island.Width = Math.Max(1, w); _island.Height = Math.Max(1, h);
        var g = Geo.Round(w, h, rt, rb);
        _island.Clip = g; _outline.Data = g;
        _mini.Opacity = Math.Clamp(_miniO.Value, 0, 1);
        _banner.Opacity = Math.Clamp(_bannerO.Value, 0, 1);
        _exp.Opacity = Math.Clamp(_expO.Value, 0, 1);
        Soft(_mini); Soft(_banner); Soft(_exp);
        _expScale.ScaleX = _expScale.ScaleY = _expS.Value;
        _expShift.Y = _expY.Value;
        ((TranslateTransform)_navDot.RenderTransform).X = _dotX.Value;

        if (_incoming != null)
        {
            double t = _pg.Value;
            _incoming.Opacity = Math.Clamp(t, 0, 1);
            ((TranslateTransform)_incoming.RenderTransform).X = (1 - t) * 30;
            if (_outgoing != null)
            {
                _outgoing.Opacity = Math.Clamp(1 - t * 1.6, 0, 1);
                ((TranslateTransform)_outgoing.RenderTransform).X = -t * 30;
            }
            if (_pg.Settled) FinishTransition();
        }
        UpdateRegion();
    }

    // ---------------- vidro (desfoque real do que está atrás) ----------------
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

    // ---------------- interação ----------------
    void OnPreviewDown(object sender, MouseButtonEventArgs e)
    {
        _activity = DateTime.Now;
        if (e.OriginalSource is DependencyObject d && FindParent<TextBox>(d) != null) EnableKeyboard();
    }

    static T? FindParent<T>(DependencyObject d) where T : DependencyObject
    {
        while (d != null) { if (d is T t) return t; d = VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d); }
        return null;
    }

    /// <summary>A janela normalmente não rouba foco; ao editar texto precisamos de foco de teclado.</summary>
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

    void OnIslandMove(object sender, MouseEventArgs e)
    {
        if (!_mouseDown || e.LeftButton != MouseButtonState.Pressed || !S.AllowDrag) return;
        if ((e.GetPosition(this) - _downPos).Length < 7) return;
        _mouseDown = false;
        try { DragMove(); } catch { }
        SaveDraggedPosition();
    }

    void OnIslandUp(object sender, MouseButtonEventArgs e)
    {
        bool click = _mouseDown; _mouseDown = false;
        if (!click) return;
        if (_st == St.Collapsed && S.ExpandOnClick) Expand();
        else if (_st == St.Banner) { _bannerT.Stop(); ShowPage("inbox", false); Expand(); }
    }

    void ArmCollapse()
    {
        _collapseT.Interval = TimeSpan.FromSeconds(Math.Max(0.3, S.AutoCollapseSeconds));
        _collapseT.Stop(); _collapseT.Start();
    }

    bool KeepOpen => CurrentPage()?.KeepOpen == true || IsKeyboardFocusWithin && Keyboard.FocusedElement is TextBox
                     || _island.ContextMenu?.IsOpen == true || Mouse.Captured != null;

    void OnCollapseTick(object? s, EventArgs e)
    {
        _collapseT.Stop();
        if (_st != St.Expanded) return;
        if (KeepOpen) { ArmCollapse(); return; }
        double idle = (DateTime.Now - _activity).TotalSeconds;
        double limit = _island.IsMouseOver ? Math.Max(10, S.AutoCollapseSeconds * 5) : S.AutoCollapseSeconds;
        if (idle < limit) { ArmCollapse(); return; }
        Collapse();
    }

    // ---------------- banner de notificação ----------------
    void ShowBanner(InboxItem it)
    {
        _bTitle.Text = it.Title; _bText.Text = it.Text.Length > 0 ? it.Text : it.Source;
        _bIcon.Text = it.Source.Length > 0 ? it.Source[..1].ToUpperInvariant() : "!";
        _bBadge.Background = it.Important ? K.Orange : K.Fg;
        if (_st == St.Expanded || Visibility != Visibility.Visible) return;
        SetState(St.Banner);
        _bannerT.Interval = TimeSpan.FromSeconds(it.Important ? S.BannerSeconds + 3 : S.BannerSeconds);
        _bannerT.Stop(); _bannerT.Start();
    }

    // ---------------- mini ----------------
    public void RefreshMini()
    {
        var m = Svc.Media; var ag = Svc.Agents; var f = Svc.Focus;
        string text; string? glyph = null; ImageSource? cover = null;
        var attention = S.IsOn("ai") ? ag.Attention : null;
        var working = S.IsOn("ai") ? ag.Working : null;

        if (attention != null) { glyph = "\uE7BA"; text = Trim(attention.Name + " precisa de você", 22); _miniGlyph.Foreground = K.Orange; }
        else if (f.Running) { glyph = "\uE916"; text = f.PhaseLabel + " " + K.Fmt(f.Display); _miniGlyph.Foreground = K.Fg; }
        else if (working != null) { glyph = "\uE945"; text = Trim(working.Name + " trabalhando…", 22); _miniGlyph.Foreground = K.Green; }
        else if (m.Playing && m.Title.Length > 0 && S.IsOn("media")) { cover = m.Cover; glyph = "\uE8D6"; text = Trim(m.Title, 22); _miniGlyph.Foreground = K.Fg; }
        else { text = DateTime.Now.ToString("HH:mm", CultureInfo.CurrentCulture); }

        if (cover != null) { _miniCover.Source = cover; _miniCoverBox.Visibility = Visibility.Visible; _miniGlyph.Visibility = Visibility.Collapsed; }
        else { _miniCoverBox.Visibility = Visibility.Collapsed; _miniGlyph.Visibility = glyph != null ? Visibility.Visible : Visibility.Collapsed; if (glyph != null) _miniGlyph.Text = glyph; }
        _miniText.Text = text;
        _miniRight.Text = Svc.Battery.HasBattery ? Svc.Battery.Percent + "%" : "";

        // indicadores de privacidade (como no iOS): laranja = microfone, verde = câmera
        if (Svc.Camera.IsOn) { _miniDot.Fill = K.Green; _miniDot.Visibility = Visibility.Visible; }
        else if (Svc.Audio.MicInUse && S.MonitorMicrophone) { _miniDot.Fill = K.Orange; _miniDot.Visibility = Visibility.Visible; }
        else _miniDot.Visibility = Visibility.Collapsed;
    }

    static string Trim(string s, int n) => s.Length > n ? s[..n] + "…" : s;

    // ---------------- tela cheia ----------------
    void CheckFullscreen()
    {
        bool fs = false;
        try { if (Native.SHQueryUserNotificationState(out int st) == 0) fs = st is 2 or 3 or 4; } catch { }
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
        if (_hwnd != IntPtr.Zero) { Native.UnregisterHotKey(_hwnd, 1); Native.UnregisterHotKey(_hwnd, 2); }
        base.OnClosed(e);
    }
}
