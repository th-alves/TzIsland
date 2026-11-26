using System.Windows.Automation;
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
    // Grão: textura fixa de 96×96 repetida a 3% — quebra o banding do preto e dá sensação de material.
    static readonly Brush GrainBrush = MakeGrain();
    readonly Shapes.Rectangle _grain = new() { IsHitTestVisible = false, Fill = GrainBrush };
    readonly Grid _mini = new(), _banner = new(), _exp = new();
    readonly Grid _pageHost = new();
    // Barra de seções vertical, na lateral direita (minimalista: só ícones, uma "lente" desliza atrás do ativo).
    const double LensW = 34, LensH = 28;
    readonly Grid _navHost = new();                 // coluna inteira: [lista rolável] + divisor + engrenagem
    readonly Grid _navTrack = new();                // conteúdo rolável: lente atrás dos ícones
    ScrollViewer _navScroll = null!;
    readonly StackPanel _nav = new() { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
    readonly StackPanel _navFoot = new() { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
    // A "lente" anda por deslocamento e estica por ESCALA: nada de Width/Height por quadro (sem passo de layout).
    readonly ScaleTransform _dotScale = new(1, 1);
    readonly TranslateTransform _dotShift = new();
    readonly Border _navDot = new()
    {
        Width = LensW,
        Height = LensH,
        CornerRadius = new CornerRadius(9),
        Background = K.Raised,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Top,
        RenderTransformOrigin = new Point(0.5, 0.5),
        RenderTransform = new TransformGroup()
    };
    readonly ScaleTransform _expScale = new(1, 1);
    readonly TranslateTransform _expShift = new();
    // Geometria reaproveitada no modo Flutuante: sem alocar um StreamGeometry novo por quadro.
    RectangleGeometry? _roundClip;
    // Sombra externa: halo real desenhado FORA da silhueta, dentro da janela transparente.
    // A janela é recortada por SetWindowRgn (ilha + ShadowPad); o resto da janela não existe para o mouse.
    const double ShadowPad = 24, ShadowBlur = 22, ShadowDepth = 5;
    readonly Canvas _shadowStage = new() { IsHitTestVisible = false };
    readonly Border _shadowBox = new()
    {
        IsHitTestVisible = false, Background = Brushes.Black,
        Effect = new DropShadowEffect
        {
            BlurRadius = ShadowBlur, ShadowDepth = ShadowDepth, Direction = 90, Opacity = 0.62, Color = Colors.Black,
            RenderingBias = RenderingBias.Performance
        }
    };
    // Palco dos portadores do matched geometry (Etapa 4).
    readonly Canvas _carry = new() { IsHitTestVisible = false };

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
            double w = 2 * Math.PI / Math.Max(0.2, halfMs * 2 / 1000.0);
            // Respiração no motor: um relógio só, pausável e com dono definido.
            MotionEngine.Wave(_sc, ScaleTransform.ScaleXProperty, t => 0.94 + 0.16 * Math.Sin(w * t));
            MotionEngine.Wave(_sc, ScaleTransform.ScaleYProperty, t => 0.94 + 0.16 * Math.Sin(w * t));
            MotionEngine.Wave(_glow, UIElement.OpacityProperty, t => 0.65 + 0.35 * Math.Sin(w * t));
            MotionEngine.Wave(_core, UIElement.OpacityProperty, t => 0.825 + 0.175 * Math.Sin(w * t));
        }

        /// <summary>Encerra a respiração deixando o LED sólido (sem herdar um meio-ciclo).</summary>
        public void StopPulse()
        {
            MotionEngine.Jump(_sc, ScaleTransform.ScaleXProperty, 1);
            MotionEngine.Jump(_sc, ScaleTransform.ScaleYProperty, 1);
            MotionEngine.Jump(_glow, UIElement.OpacityProperty, 1);
            MotionEngine.Jump(_core, UIElement.OpacityProperty, 1);
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
    MotionHandle? _dlSweep;
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
    readonly Spring _w, _h, _r, _miniO, _bannerO, _expO, _expS, _expY, _dotX;

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
    // Token de transição: a fase visual é independente do estado lógico (R11).
    enum Phase { Idle, Entering, Leaving, Settled }
    Phase _phase = Phase.Settled;

    // ---- Modo de edição dos cartões: atalho com a ilha aberta. Os cartões tremem e podem ser arrastados entre as seções ----
    bool _edit, _dragging, _dropping, _ending;
    CardHolder? _held;
    Point _downPt, _grab, _lastPtr;
    DateTime _lastMove = DateTime.MinValue;
    Dictionary<string, List<string>>? _snap;
    Shapes.Rectangle? _ghost;
    readonly ScaleTransform _gScale = new(1, 1);
    readonly RotateTransform _gRot = new();
    readonly Spring _gx = new(0), _gy = new(0), _gs = new(1), _gr = new(0);
    MotionLoop _dragLoop = null!;
    string? _dwellId;
    readonly HashSet<string> _guestOn = new();   // seções mantidas "ligadas" só porque um cartão delas mora na seção aberta
    readonly Canvas _dragLayer = new() { IsHitTestVisible = false };
    readonly Border _editBar = new()
    {
        Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(0, 7, 0, 0), Background = K.Hex("#F2141416"), BorderBrush = K.StrokeHi, BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(17), Padding = new Thickness(16, 3, 3, 3)
    };
    readonly DispatcherTimer _dwellT = new() { Interval = TimeSpan.FromMilliseconds(420) }, _scrollT = new() { Interval = TimeSpan.FromMilliseconds(16) };

    // Um único tique lento (1 s) alimenta relógio recolhido, medidor de quadro e a checagem de tela cheia:
    // eram três DispatcherTimers acordando a UI em instantes diferentes só para ler o relógio do sistema.
    readonly DispatcherTimer _hoverT = new(), _collapseT = new(), _bannerT = new(), _tickT = new() { Interval = TimeSpan.FromSeconds(1) };
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
        _dotX = _loop.Add(new Spring(0));
        // A lente usa escala + deslocamento (o grupo é montado aqui: inicializador de campo não pode referenciar campos).
        ((TransformGroup)_navDot.RenderTransform).Children.Add(_dotScale);
        ((TransformGroup)_navDot.RenderTransform).Children.Add(_dotShift);

        // Arrastar cartões: o cartão fantasma segue o ponteiro numa mola rápida (com leve atraso) e se inclina com a velocidade.
        _dragLoop = new MotionLoop(DragFrame);
        _dragLoop.Add(_gx); _dragLoop.Add(_gy); _dragLoop.Add(_gs); _dragLoop.Add(_gr);
        _gr.Stiffness = 240; _gr.Damping = 17;

        BuildTree();
        ApplySettings();
        BuildNav();
        CardCatalog.EnsurePage = id => GetPage(id);
        CardCatalog.PickUp += OnCardPick;
        _dwellT.Tick += (_, _) => OnDwell();
        _scrollT.Tick += (_, _) => OnAutoScroll();
        SourceInitialized += OnSourceInit;
        Loaded += (_, _) => { Place(); SetState(St.Collapsed, true); RefreshMini(); };
        // Energia: janela oculta, máquina dormindo ou sessão bloqueada = nenhum quadro de animação.
        IsVisibleChanged += (_, _) => { SyncMotionPause(); if (IsVisible) RefreshMini(); };
        Microsoft.Win32.SystemEvents.PowerModeChanged += (_, e) =>
            Dispatcher.InvokeAsync(() => SetAsleep(e.Mode == Microsoft.Win32.PowerModes.Suspend));
        Microsoft.Win32.SystemEvents.SessionSwitch += (_, e) => Dispatcher.InvokeAsync(() => SetAsleep(
            e.Reason is Microsoft.Win32.SessionSwitchReason.SessionLock
                or Microsoft.Win32.SessionSwitchReason.ConsoleDisconnect
                or Microsoft.Win32.SessionSwitchReason.RemoteDisconnect));
        // Teclado: Esc fecha, Ctrl+K abre a paleta, Tab/setas trocam de página na barra lateral.
        PreviewKeyDown += (_, e) => OnWindowKey(e);
        Eco.Changed += () => Dispatcher.InvokeAsync(ApplyEco);
        Settings.ChangedKind += OnSettingsChanged;
        Settings.PreviewAnimation += () => Dispatcher.InvokeAsync(PreviewAnim, DispatcherPriority.Background);
        Hub.Banner += ShowBanner;
        Hub.Inbox.CollectionChanged += (_, _) => UpdateBadge();
        Svc.Media.Changed += RefreshMini; Svc.Battery.Changed += RefreshMini; Svc.Audio.MicChanged += RefreshMini;
        Svc.Focus.Tick += RefreshMini; Svc.Agents.Changed += RefreshMini; Svc.Camera.StateChanged += RefreshMini;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.InvokeAsync(Place);

        _hoverT.Tick += (_, _) => { _hoverT.Stop(); if (S.ExpandOnHover && _island.IsMouseOver) Expand(); };
        _collapseT.Tick += OnCollapseTick;
        _bannerT.Tick += (_, _) => { _bannerT.Stop(); if (_st == St.Banner && !_island.IsMouseOver) SetState(St.Collapsed); };
        _previewT.Tick += (_, _) => { _previewT.Stop(); if (!_island.IsMouseOver && _st == St.Expanded) Collapse(); };
        _hudT.Tick += (_, _) => { _hudT.Stop(); FadeHud(false); };
        Svc.Downloads.Changed += () => Dispatcher.InvokeAsync(RefreshMini);
        Svc.Calendar.Changed += () => Dispatcher.InvokeAsync(RefreshMini);
        Hub.HeldChanged += () => Dispatcher.InvokeAsync(RefreshMini);
        Svc.Media.Changed += ApplyAccent;
        _tickT.Tick += (_, _) => OnSlowTick();
        _tickT.Start();
    }

    /// <summary>Ruído cinza neutro gerado uma vez: repetido bem pequeno, vira o grão sutil dos materiais escuros.</summary>
    static Brush MakeGrain()
    {
        const int n = 96;
        var px = new byte[n * n * 4];
        var rnd = new Random(7);
        for (int i = 0; i < px.Length; i += 4)
        {
            byte v = (byte)rnd.Next(256);
            px[i] = v; px[i + 1] = v; px[i + 2] = v; px[i + 3] = 255;
        }
        var bmp = new WriteableBitmap(n, n, 96, 96, PixelFormats.Pbgra32, null);
        bmp.WritePixels(new Int32Rect(0, 0, n, n), px, n * 4, 0);
        bmp.Freeze();
        var brush = new ImageBrush(bmp)
        {
            TileMode = TileMode.Tile, Viewport = new Rect(0, 0, n, n), ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None, Opacity = 0.03
        };
        brush.Freeze();
        return brush;
    }

    // ---------------- UI construction ----------------
    void BuildTree()
    {
        Content = _root;
        // A sombra é irmã da ilha (fora do clipe dela) e fica atrás de tudo.
        _shadowStage.Children.Add(_shadowBox);
        _root.Children.Add(_shadowStage);
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
        _layers.Children.Add(_grain);
        _layers.Children.Add(_mini);
        _layers.Children.Add(_banner);
        _layers.Children.Add(_exp);
        _layers.Children.Add(_carry);
        _layers.Children.Add(_outline);
        // Medidor interno (Etapa 0): Ctrl+Shift+M alterna tempo de quadro / consumidores ativos.
        _statsBox.Child = _statsText;
        _layers.Children.Add(_statsBox);

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

        // Barra do modo de edição (aparece no topo, com mola) e camada transparente onde o cartão arrastado flutua
        var barRow = new StackPanel { Orientation = Orientation.Horizontal };
        barRow.Children.Add(K.T("Arraste os cartões para reorganizar", 11, K.Dim).Vc());
        var done = K.TextBtn("Concluído", () => ExitEdit(), true, 11.5);
        done.Padding = new Thickness(13, 5, 13, 5); done.Margin = new Thickness(14, 0, 0, 0);
        barRow.Children.Add(done);
        _editBar.Child = barRow;
        _exp.Children.Add(_editBar.At(colSpan: 2));

        _dragLayer.MouseMove += OnDragMove;
        _dragLayer.MouseLeftButtonUp += (_, e) => { e.Handled = true; EndDrag(false); };
        _dragLayer.LostMouseCapture += (_, _) => { if (_held != null && !_ending) EndDrag(true); };
        _exp.Children.Add(_dragLayer.At(colSpan: 2));
        BuildPalette();
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
        var mTog = new MenuItem { Header = "Expandir / recolher" }; mTog.Click += (_, _) => { if (_st == St.Expanded) Collapse(); else Expand(); };
        var mEdit = new MenuItem { Header = "Editar cartões" }; mEdit.Click += (_, _) => { Expand(); EnterEdit(); };
        var mExit = new MenuItem { Header = "Sair do TzIsland" }; mExit.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(mTog); menu.Items.Add(mEdit); menu.Items.Add(mCfg); menu.Items.Add(new Separator()); menu.Items.Add(mExit);
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
        SuppressFrame();
        _src?.AddHook(WndProc);
        RegisterHotkeys();
        try { Native.AddClipboardFormatListener(_hwnd); } catch (Exception clipErr) { Log.Write(clipErr); }
    }

    /// <summary>Sem moldura e sem arredondamento do Windows: a silhueta é desenhada pelo app.</summary>
    void SuppressFrame()
    {
        if (_hwnd == IntPtr.Zero) return;
        int none = unchecked((int)0xFFFFFFFE), noRound = 1;
        Native.DwmSetWindowAttribute(_hwnd, 34, ref none, 4);      // DWMWA_BORDER_COLOR = nenhuma borda
        Native.DwmSetWindowAttribute(_hwnd, 33, ref noRound, 4);   // DWMWA_WINDOW_CORNER_PREFERENCE = sem arredondar
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY)
        {
            handled = true;
            if (w.ToInt32() == 1) Toggle();
            else if (w.ToInt32() == 2) { OpenPage("dictation"); _ = Svc.Dictation.ToggleAsync(); }
            else if (w.ToInt32() == 3) ToggleStats();
            else if (w.ToInt32() == 4) TogglePalette();
        }
        else if (msg == Native.WM_CLIPBOARDUPDATE) Svc.Clipboard.OnSystemUpdate();
        else if (msg == Native.WM_NCHITTEST)
        {
            // O halo (e o espaço morto da janela) não é área da ilha: o clique precisa atravessar.
            try
            {
                long lp = l.ToInt64();
                int sx = (short)(lp & 0xFFFF), sy = (short)((lp >> 16) & 0xFFFF);
                var p = PointFromScreen(new Point(sx, sy));
                double iw = Math.Max(1, _w.Value), ih = Math.Max(1, _h.Value), il = (Width - iw) / 2, it = _island.Margin.Top;
                if (p.X < il - 1 || p.X > il + iw + 1 || p.Y < it - 1 || p.Y > it + ih + 1)
                {
                    handled = true;
                    return new IntPtr(Native.HTTRANSPARENT);
                }
            }
            catch { }
        }
        return IntPtr.Zero;
    }

    void RegisterHotkeys()
    {
        if (_hwnd == IntPtr.Zero) return;
        Native.UnregisterHotKey(_hwnd, 1); Native.UnregisterHotKey(_hwnd, 2); Native.UnregisterHotKey(_hwnd, 3);
        Native.RegisterHotKey(_hwnd, 3, 0x0002 | 0x0004 | 0x4000, 0x4D);   // Ctrl+Shift+M: medidor de quadro
        SyncPaletteHotkey();
        if (Native.TryParseHotkey(S.ToggleHotkey, out var m1, out var k1)) Native.RegisterHotKey(_hwnd, 1, m1 | 0x4000, k1);
        if (S.IsOn("dictation") && Native.TryParseHotkey(S.DictationHotkey, out var m2, out var k2)) Native.RegisterHotKey(_hwnd, 2, m2 | 0x4000, k2);
    }

    /// <summary>
    /// Ctrl+K só é capturado com a ilha aberta: registrar globalmente roubaria o atalho de editores
    /// e navegadores o tempo todo, numa app que fica sempre ligada.
    /// </summary>
    void SyncPaletteHotkey()
    {
        if (_hwnd == IntPtr.Zero) return;
        Native.UnregisterHotKey(_hwnd, 4);
        if (_st == St.Expanded) Native.RegisterHotKey(_hwnd, 4, 0x0002 | 0x4000, 0x4B);   // Ctrl+K
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

    // A janela PRECISA ser recortada por SetWindowRgn. HTTRANSPARENT no WM_NCHITTEST só repassa o clique para
    // janelas da mesma thread: para o navegador, a área de trabalho ou qualquer outro app, a parte vazia da
    // janela (880x444 px) continuava engolindo todos os cliques. Sem região, essa área fica "morta" na tela.
    // Para não voltar o contorno de 1 px que o DWM desenhava em volta da região, o SuppressFrame é reaplicado
    // sempre que a região muda.
    int _rgnL = -1, _rgnT = -1, _rgnR = -1, _rgnB = -1;

    void UpdateRegion()
    {
        if (_hwnd == IntPtr.Zero) return;
        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        double w = Math.Max(1, _w.Value), h = Math.Max(1, _h.Value), left = (Width - w) / 2, top = _island.Margin.Top;
        // O halo da sombra vive FORA da silhueta: a região cresce junto (senão o DWM corta o degradê).
        int pad = S.WindowShadow && Floating ? (int)Math.Ceiling(ShadowPad * dpi) : 0;
        int l = (int)Math.Floor(left * dpi) - pad, t = (int)Math.Floor(top * dpi) - pad;
        int r = (int)Math.Ceiling((left + w) * dpi) + pad, b = (int)Math.Ceiling((top + h) * dpi) + pad;
        l = Math.Max(0, l); t = Math.Max(0, t);
        r = Math.Min((int)Math.Ceiling(Width * dpi), r); b = Math.Min((int)Math.Ceiling(Height * dpi), b);
        if (r <= l || b <= t) return;                                     // região inválida (meio do overshoot): mantém a anterior
        if (l == _rgnL && t == _rgnT && r == _rgnR && b == _rgnB) return;   // nada mudou: evita recriar a região a cada quadro
        _rgnL = l; _rgnT = t; _rgnR = r; _rgnB = b;
        Native.SetWindowRgn(_hwnd, Native.CreateRectRgn(l, t, r, b), true);   // o sistema passa a ser dono da região
        SuppressFrame();
    }

    // ---------------- Settings synchronization ----------------
    IslandAnim _lastAnim = Settings.Current.IslandAnim;
    MotionIntensity _lastIntensity = Settings.Current.MotionIntensity;
    double _lastEW = Settings.Current.ExpandedWidth, _lastEH = Settings.Current.ExpandedHeight;
    readonly DispatcherTimer _previewT = new() { Interval = TimeSpan.FromMilliseconds(2200) };
    readonly TextBlock _statsText = K.T("", 9.5, K.Muted);
    readonly Border _statsBox = new()
    {
        Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
        Margin = new Thickness(0, 0, 12, 8), Padding = new Thickness(6, 2, 6, 2), CornerRadius = new CornerRadius(7),
        Background = K.Hex("#E6000000"), IsHitTestVisible = false
    };
    bool _statsOn;

    // ---------------- Paleta de comandos (Ctrl+K) ----------------
    // 13 módulos e dezenas de ações dependiam de saber onde clicar. Reaproveita o padrão de busca
    // das Notas e vira a porta de entrada para o controle por teclado e por CLI.
    sealed record PalCmd(string Title, string Hint, Action Run);
    readonly Border _palette = new()
    {
        Visibility = Visibility.Collapsed, Width = 430, HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 26, 0, 0),
        Background = K.Hex("#F5141417"), BorderBrush = K.StrokeHi, BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(16), Padding = new Thickness(12)
    };
    readonly TextBox _palQuery = K.Box("Digite um comando… (Ctrl+K)");
    readonly StackPanel _palRows = new();
    readonly List<PalCmd> _palCmds = new();
    readonly List<PalCmd> _palShown = new();
    int _palSel;

    void BuildPalette()
    {
        _palQuery.FontSize = 13; _palQuery.Height = 34; _palQuery.Margin = new Thickness(0, 0, 0, 8);
        AutomationProperties.SetName(_palQuery, "Buscar comando");
        _palQuery.TextChanged += (_, _) => FilterPalette();
        _palQuery.PreviewKeyDown += PaletteKeys;
        var stack = new StackPanel();
        stack.Children.Add(_palQuery);
        stack.Children.Add(_palRows);
        _palette.Child = stack;
        _exp.Children.Add(_palette.At(colSpan: 2));
    }

    public void TogglePalette()
    {
        if (_palette.Visibility == Visibility.Visible) ClosePalette(); else OpenPalette();
    }

    void OpenPalette()
    {
        if (_st != St.Expanded) Expand();
        EnableKeyboard();
        FillPalette();
        _palQuery.Text = "";
        _palSel = 0;
        FilterPalette();
        _palette.Visibility = Visibility.Visible;
        _collapseT.Stop();
        Dispatcher.InvokeAsync(() => Keyboard.Focus(_palQuery), DispatcherPriority.Input);
    }

    void ClosePalette()
    {
        _palette.Visibility = Visibility.Collapsed;
        _palRows.Children.Clear();
    }

    void FillPalette()
    {
        _palCmds.Clear();
        foreach (var id in Modules.Order().Where(S.IsOn))
        {
            var pid = id;
            _palCmds.Add(new("Ir para " + Modules.Title(pid), "Página", () => ShowPage(pid)));
        }
        _palCmds.Add(new(Svc.Focus.Pomo.Running ? "Pausar Pomodoro" : "Iniciar Pomodoro", "Foco", () => { Svc.Focus.PomoToggle(); ShowPage("focus"); }));
        _palCmds.Add(new("Reiniciar Pomodoro", "Foco", () => Svc.Focus.PomoReset()));
        _palCmds.Add(new("Iniciar temporizador de 25 min", "Foco", () => { Svc.Focus.TimerSet(25); Svc.Focus.Timer.Start(); ShowPage("focus"); }));
        _palCmds.Add(new("Parar temporizador", "Foco", () => Svc.Focus.TimerReset()));
        _palCmds.Add(new(Settings.Current.ManualDnd ? "Desligar Não perturbar" : "Ligar Não perturbar", "Notificações", () =>
        {
            Settings.Current.ManualDnd = !Settings.Current.ManualDnd;
            Settings.Save();
            Hub.EvaluateDnd();
        }));
        _palCmds.Add(new(Svc.Audio.Muted ? "Ativar som do sistema" : "Silenciar o sistema", "Som", () => Svc.Audio.Muted = !Svc.Audio.Muted));
        _palCmds.Add(new("Volume em 40%", "Som", () => Svc.Audio.Volume = 0.4f));
        _palCmds.Add(new("Trocar saída de áudio", "Som", () => ShowPage("mixer")));
        _palCmds.Add(new("Colar o último item fixado", "Área de transferência", () =>
        {
            var it = Svc.Clipboard.Items.FirstOrDefault(x => x.Pinned) ?? Svc.Clipboard.Items.FirstOrDefault();
            if (it == null) return;
            if (Svc.Clipboard.Copy(it)) Hub.Notify("Área de transferência", "Copiado", it.Preview.Length > 60 ? it.Preview[..60] + "…" : it.Preview);
        }));
        _palCmds.Add(new("Editar cartões", "App", () => EnterEdit()));
        _palCmds.Add(new("Verificar atualizações", "App", () => _ = Svc.Updates.CheckAsync(true)));
        _palCmds.Add(new("Abrir Configurações", "App", OpenSettings));
        _palCmds.Add(new("Recolher a ilha", "App", Collapse));
    }

    void FilterPalette()
    {
        string q = _palQuery.Text.Trim();
        _palShown.Clear();
        _palShown.AddRange(_palCmds.Where(c => q.Length == 0
            || c.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
            || c.Hint.Contains(q, StringComparison.OrdinalIgnoreCase)).Take(8));
        if (_palShown.Count == 0) _palSel = -1;
        else _palSel = Math.Clamp(_palSel, 0, _palShown.Count - 1);
        RenderPalette();
    }

    void RenderPalette()
    {
        _palRows.Children.Clear();
        if (_palShown.Count == 0)
        {
            _palRows.Children.Add(K.T("Nenhum comando encontrado", 11.5, K.Muted).M(10, 8, 10, 8));
            return;
        }
        for (int i = 0; i < _palShown.Count; i++)
        {
            int idx = i;
            var cmd = _palShown[i];
            var grid = K.Cols("*", "auto");
            var title = K.T(cmd.Title, 12.5, K.Fg); title.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(title);
            var hint = K.T(cmd.Hint, 10.5, i == _palSel ? K.Accent : K.Muted); hint.VerticalAlignment = VerticalAlignment.Center;
            hint.Margin = new Thickness(10, 0, 2, 0);
            grid.Children.Add(hint.At(col: 1));
            var b = new Button
            {
                Style = K.Ghost, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10, 8, 10, 8), Tag = new CornerRadius(9), Content = grid,
                Background = i == _palSel ? K.Raised : K.Clear, Margin = new Thickness(0, 1, 0, 1)
            };
            AutomationProperties.SetName(b, cmd.Title);
            b.Click += (_, _) => RunPalette(idx);
            _palRows.Children.Add(b);
        }
    }

    void PaletteKeys(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { ClosePalette(); e.Handled = true; }
        else if (e.Key == Key.Down) { if (_palShown.Count > 0) _palSel = Math.Min(_palSel + 1, _palShown.Count - 1); RenderPalette(); e.Handled = true; }
        else if (e.Key == Key.Up) { if (_palShown.Count > 0) _palSel = Math.Max(_palSel - 1, 0); RenderPalette(); e.Handled = true; }
        else if (e.Key == Key.Enter && _palSel >= 0) { RunPalette(_palSel); e.Handled = true; }
    }

    void RunPalette(int i)
    {
        if (i < 0 || i >= _palShown.Count) return;
        var cmd = _palShown[i];
        ClosePalette();
        cmd.Run();
    }

    /// <summary>Esc, Ctrl+K, Tab/setas na barra lateral. Enter/Espaço nos botões seguem o Button nativo.</summary>
    void OnWindowKey(KeyEventArgs e)
    {
        if (_palette.Visibility == Visibility.Visible)
        {
            if (e.Key == Key.Escape) { ClosePalette(); e.Handled = true; }
            return;                                        // o resto é tratado pela caixa de busca
        }
        if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) != 0) { TogglePalette(); e.Handled = true; return; }
        if (e.Key == Key.Escape && _st == St.Expanded) { Collapse(); e.Handled = true; return; }
        if (_st != St.Expanded) return;
        if (e.Key is Key.Tab or Key.Down or Key.Up or Key.PageDown or Key.PageUp)
        {
            bool back = e.Key is Key.Up or Key.PageUp || e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            StepPage(back ? -1 : 1);
            e.Handled = true;
        }
    }

    void StepPage(int delta)
    {
        var ids = Modules.Order().Where(S.IsOn).Where(id => _navBtns.ContainsKey(id)).ToList();
        if (ids.Count == 0 || _pageId == null) return;
        int i = ids.IndexOf(_pageId);
        int j = ((i < 0 ? 0 : i) + delta + ids.Count) % ids.Count;
        EnableKeyboard();
        ShowPage(ids[j]);
        if (_navBtns.TryGetValue(ids[j], out var b)) b.Focus();
    }

    /// <summary>Modo economia: tique mais espaçado, sem espectro/HUD (as animações continuam normais).</summary>
    void ApplyEco()
    {
        _tickT.Interval = TimeSpan.FromSeconds(Eco.Active ? 4 : 1);
        if (!Eco.Active) return;
        _miniEq.Running = false;
        _statsOn = false;
        _statsBox.Visibility = Visibility.Collapsed;
    }

    /// <summary>Abre a ilha sozinha e fecha depois de um instante, para mostrar o estilo de animação escolhido.</summary>
    void PreviewAnim()
    {
        if (_noExpand || !S.Animations) return;
        _previewT.Stop();
        if (_st != St.Expanded) Expand();
        _previewT.Start();
    }

    void OnSettingsChanged(SettingsChange kind)
    {
        // Granular: arrastar um slider de tamanho não reconstrói a barra nem mata animações.
        if ((kind & SettingsChange.Motion) != 0 && (S.IslandAnim != _lastAnim || S.MotionIntensity != _lastIntensity))
        {
            _lastAnim = S.IslandAnim;
            _lastIntensity = S.MotionIntensity;
            Dispatcher.InvokeAsync(PreviewAnim, DispatcherPriority.Background);
        }
        if ((kind & (SettingsChange.Layout | SettingsChange.Look | SettingsChange.Behavior)) != 0) ApplySettings();
        if ((kind & (SettingsChange.Layout | SettingsChange.Behavior)) != 0) Place();
        if ((kind & SettingsChange.Modules) != 0) BuildNav();
        if ((kind & SettingsChange.Behavior) != 0) RegisterHotkeys();
        SetState(_st);          // retarget, nunca snap
        if ((kind & SettingsChange.Layout) != 0) RetargetSize();
        RefreshMini();
    }

    /// <summary>
    /// Largura/altura/raio do corpo da ilha são molas alimentadas só por SetState, que sai cedo quando o estado não muda:
    /// mexer nas dimensões nas Configurações só redimensionava o conteúdo da pílula (o LED). Aqui o corpo recebe o novo alvo,
    /// e ao mudar as dimensões expandidas a ilha abre sozinha como prévia (fecha depois, como no preview de animação).
    /// </summary>
    void RetargetSize()
    {
        bool expChanged = S.ExpandedWidth != _lastEW || S.ExpandedHeight != _lastEH;
        _lastEW = S.ExpandedWidth; _lastEH = S.ExpandedHeight;
        bool opened = false;
        if (expChanged && !_noExpand && _st == St.Collapsed) { Expand(); opened = true; }   // SetState já aponta para o tamanho novo, com o perfil de animação escolhido
        if (!opened)
        {
            var (w, h, r) = Target(_st);
            if (S.Animations) { _w.To(w, 0, 620, 46); _h.To(h, 0, 620, 46); _r.To(r, 0, 620, 46); }
            else { _w.Snap(w); _h.Snap(h); _r.Snap(r); }
        }
        Frame();
        _loop.Kick();
        if (expChanged && _st == St.Expanded) { _previewT.Stop(); _previewT.Start(); }
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
        if (S.Fullscreen != FullscreenMode.Hide) Visibility = Visibility.Visible;
        SyncMotionPause();
        _shadowStage.Visibility = S.WindowShadow ? Visibility.Visible : Visibility.Collapsed;
        // Trocar o formato com a ilha parada não gera quadro nenhum (SetState sai cedo quando o estado é o mesmo),
        // e o recorte/borda ficavam com a silhueta do estilo anterior: um contorno de notch sobrando em volta da pílula.
        // Uma passada de geometria aqui reaplica clip, borda e região na hora.
        Frame();
    }

    void ApplyLook()
    {
        // 1% = vidro quase invisível, 100% = vidro sólido. O fundo desfocado (captura da tela) e o tint escalam juntos com o valor;
        // antes a captura ficava sempre a 100% e o mínimo era 30%, então o vidro nunca parecia transparente.
        double a = Math.Clamp(S.Opacity, 0.01, 1);
        _backdrop.Opacity = a;
        _backdrop.Visibility = S.Look == IslandLook.SolidBlack || S.BlurAmount <= 0 ? Visibility.Collapsed : Visibility.Visible;
        switch (S.Look)
        {
            case IslandLook.SolidBlack:
                _tint.Fill = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                _sheen.Visibility = Visibility.Collapsed;
                break;
            case IslandLook.DarkGlass:
                _tint.Fill = new SolidColorBrush(Color.FromArgb((byte)Math.Round(255 * 0.90 * a), 8, 8, 9));
                _sheen.Fill = new LinearGradientBrush(Color.FromArgb(18, 255, 255, 255), Color.FromArgb(0, 0, 0, 0), 90);
                _sheen.VerticalAlignment = VerticalAlignment.Top; _sheen.Height = 50; _sheen.Visibility = Visibility.Visible;
                break;
            default:
                _tint.Fill = new LinearGradientBrush(
                    Color.FromArgb((byte)Math.Round(255 * 0.48 * a), 22, 22, 24),
                    Color.FromArgb((byte)Math.Round(255 * 0.82 * a), 6, 6, 7), 90);
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
            var sc = (ScaleTransform)_badgeBox.RenderTransform;
            MotionEngine.Jump(sc, ScaleTransform.ScaleXProperty, 1.45);
            MotionEngine.Jump(sc, ScaleTransform.ScaleYProperty, 1.45);
            MotionEngine.To(sc, ScaleTransform.ScaleXProperty, 1, Tz.Springs.Sheet);
            MotionEngine.To(sc, ScaleTransform.ScaleYProperty, 1, Tz.Springs.Sheet);
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

    /// <summary>
    /// Etapa 4 — matched geometry: o ícone/LED da pílula não pisca e some; ele viaja até o herói da página
    /// (capa da mídia, anel do Foco) e o herói aparece na chegada. A geometria vem do layout real; se algo
    /// não puder ser medido, nada acontece e a transição segue exatamente como antes.
    /// </summary>
    void CarryFromMini(IPage page)
    {
        if (page is not PageBase pb || pb.Hero is not FrameworkElement hero) return;
        if (_miniLive.Opacity < 0.4) return;                 // a pílula precisa estar visível: é dela que se parte
        FrameworkElement? from = _live switch
        {
            Live.Media or Live.Both => _mIcon,
            Live.Timer => _tText,
            _ => null
        };
        if (from is null || from.ActualWidth < 1 || from.ActualHeight < 1) return;
        Matched.Carry(_carry, from, hero, K.CardStrong, 15, Tz.Delays.PageIn + 0.09, round: _live != Live.Timer);
    }

    void RevealNav()
    {
        int i = 0;
        // Sem deslocamento vertical: mover os ícones em Y invalidava os centros que a lente usa (C7).
        foreach (var el in _nav.Children.OfType<FrameworkElement>().Concat(_navFoot.Children.OfType<FrameworkElement>())) Motion.Reveal(el, 0.12 + (i++) * 0.025, 0, 0.9);
    }

    public void ShowPage(string id, bool animate = true)
    {
        if (!S.IsOn(id) || GetPage(id) is not IPage next) return;
        if (_pageId == id && _pageHost.Children.Contains(next.View)) { MoveDot(); return; }
        next.View.Visibility = Visibility.Visible;
        IPage? prev = _pageId != null && _pages.TryGetValue(_pageId, out var pp) && _pageHost.Children.Contains(pp.View) ? pp : null;
        if (prev != null && _pageActive) prev.Hidden();
        var order = _navBtns.Keys.ToList();
        _dir = prev != null && order.IndexOf(id) < order.IndexOf(_pageId ?? id) ? -1 : 1;
        _pageId = id;
        _pageHost.Children.Add(next.View);
        // se esta página já estava ligada como "convidada" (um cartão dela mora na página anterior), não liga duas vezes
        if (_pageActive && !_guestOn.Remove(id)) next.Shown();

        bool anim = animate && S.Animations;
        if (prev != null && prev.View != next.View)
        {
            if (anim) SlideOut(prev.View);
            else _pageHost.Children.Remove(prev.View);
        }
        if (anim)
        {
            // A saída e a entrada são tracks independentes: interromper no meio retargeta, sem pular opacidade.
            MotionEngine.Jump(next.View, UIElement.OpacityProperty, 0);
            Motion.SetSlide(next.View, Tz.Dist.EnterY * _dir);
            MotionEngine.To(next.View, UIElement.OpacityProperty, 1, Tz.Springs.Fade, Tz.Delays.PageIn);
            Motion.SlideTo(next.View, 0, Tz.Delays.PageIn);
            Motion.RevealPage(next, 0.16, Tz.Delays.Stagger);
        }
        else Motion.Rest(next.View, 1, 0);
        MoveDot();
        SyncGuests();
        _loop.Kick();
    }

    /// <summary>A página que sai desliza e é removida quando a track assenta (sem corte no meio do caminho).</summary>
    void SlideOut(FrameworkElement view)
    {
        MotionEngine.To(view, UIElement.OpacityProperty, 0, new SpringSpec(760, 52, 0.002), 0, () =>
        {
            if (_pageHost.Children.Contains(view)) _pageHost.Children.Remove(view);
            Motion.Rest(view, 1, 0);
        });
        Motion.SlideTo(view, -Tz.Dist.ExitY * _dir, 0);
    }

    void FinishTransition()
    {
        // Não força mais o fim de uma transição em voo: cada página é encerrada no valor visual atual.
        var cur = CurrentPage()?.View;
        foreach (var c in _pageHost.Children.OfType<FrameworkElement>().ToList())
            if (c != cur)
            {
                MotionEngine.Stop(c, UIElement.OpacityProperty);
                _pageHost.Children.Remove(c);
            }
    }

    public void OpenPage(string id) { ShowPage(id, false); Expand(); }

    void MoveDot()
    {
        if (_pageId == null || !_navBtns.TryGetValue(_pageId, out var b)) return;
        _nav.UpdateLayout();
        try
        {
            // Posição só de layout (sem RenderTransform): medir no meio de uma animação deixava a lente fora do lugar.
            // A entrada dos ícones agora não desloca em Y (só fade + escala), então os centros continuam válidos.
            double navY = VisualTreeHelper.GetOffset(_nav).Y;
            // soma os deslocamentos até a barra: o botão do Inbox fica dentro de um Grid (junto com o contador),
            // e o offset dele em relação a esse Grid é 0 — a lente ia parar no topo, em cima do Início.
            double CenterOf(FrameworkElement el)
            {
                double y = 0;
                for (DependencyObject d = el; d != null && d != _nav; d = VisualTreeHelper.GetParent(d))
                    if (d is Visual v) y += VisualTreeHelper.GetOffset(v).Y;
                return navY + y + el.ActualHeight / 2;
            }
            double y = CenterOf(b) - LensH / 2;
            foreach (var kv in _navBtns) _navCenter[kv.Key] = CenterOf(kv.Value);
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
        _carry.Children.Clear();          // portadores de uma transição anterior não sobrevivem a uma nova
        var prev = _st; _st = s;
        _phase = s == St.Expanded ? Phase.Entering : Phase.Leaving;
        if (s != St.Expanded && _palette.Visibility == Visibility.Visible) ClosePalette();
        SyncPaletteHotkey();
        SyncMotionPause();                                      // janela oculta não precisa de quadros
        if (!snap && prev != s) { if (s == St.Expanded) SoundFx.Open(); else if (prev == St.Expanded) SoundFx.Close(); }
        var (w, h, r) = Target(s);
        bool exp = s == St.Expanded;
        bool instantOut = s == St.Collapsed && prev == St.Expanded && S.CollapseSpeed == CollapseMode.Instant;
        bool fastOut = !exp && S.CollapseSpeed == CollapseMode.Fast;
        bool anim = S.Animations && !snap && !instantOut;

        if (exp && prev != St.Expanded) CaptureBackdrop();
        if (!exp && prev == St.Expanded) { if (_edit) ExitEdit(); HideGuests(); }
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
                    Motion.Rest(cur.View, 1, 0);
                    if (anim)
                    {
                        var rp = AnimProfiles.Get(S.IslandAnim);
                        Motion.RevealPage(cur, rp.RevealBase, rp.RevealStep, rp.RevealY, rp.RevealScale);
                        RevealNav();
                        CarryFromMini(cur);
                    }
                }
            }
            MoveDot();
            SyncGuests();
        }
        _miniEq.Running = (_live == Live.Media || _live == Live.Both) && s == St.Collapsed && IsVisible;

        var P = AnimProfiles.Get(S.IslandAnim);
        if (anim)
        {
            if (exp)
            {
                // Expandir: a forma "se abre" com larguras e alturas em molas diferentes (efeito de morph);
                // o conteúdo entra um instante depois, com escala + subida, e os cartões chegam em cascata.
                // Os números de cada estilo (Fluida, Elástica, Cristal, Precisa, Bolha) ficam em AnimProfiles.
                // Só pré-posiciona quando o conteúdo está REALMENTE invisível: reabrir no meio do fechamento não salta mais.
                if (_expO.Value <= 0.001) { _expS.Snap(P.SStart); _expY.Snap(P.YStart); }
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
        // Respiração do LED só existe enquanto a pílula está visível (o tique lento cuida do relógio).
        if (s != St.Collapsed)
        {
            _ledIdle.StopPulse(); _ledActive.StopPulse();
            _ledPulsing = false;
        }
        Frame();
        _loop.Kick();
        if (exp) _activity = DateTime.Now;
        else _bannerT.Stop();
        if (!anim) _phase = Phase.Settled;
    }

    // ---------------- Energia ----------------
    bool _asleep;

    /// <summary>Um único lugar decide se o relógio de animação roda: sono/sessão bloqueada ou janela oculta.</summary>
    void SyncMotionPause() => MotionHost.Paused = _asleep || Visibility != Visibility.Visible;

    void SetAsleep(bool asleep)
    {
        if (_asleep == asleep) return;
        _asleep = asleep;
        SyncMotionPause();
        if (asleep) { _tickT.Stop(); _miniEq.Running = false; }
        else { _tickT.Start(); if (_st == St.Collapsed) RefreshMini(); CheckFullscreen(); }
    }

    // ---------------- Verificação visual (--shot) ----------------
    /// <summary>Aplica um estado sem animar (modo --shot: o PNG precisa da geometria final).</summary>
    public void ApplyShotState(string state)
    {
        if (state == "expanded") SetState(St.Expanded, true);
        else if (state == "banner") SetState(St.Banner, true);
        else SetState(St.Collapsed, true);
    }

    /// <summary>Abre uma página sem animar (modo --shot).</summary>
    public void ShotPage(string id) => ShowPage(id, false);

    IPage? CurrentPage() => _pageId != null && _pages.TryGetValue(_pageId, out var p) ? p : null;

    void Expand() { if (_noExpand) return; SetState(St.Expanded); }
    void Collapse() => SetState(St.Collapsed);
    // Atalho (Ctrl+Alt+Espaço): com a ilha fechada, abre; com ela aberta, liga/desliga o modo de edição dos cartões.
    void Toggle() { if (_st == St.Expanded) ToggleEdit(); else Expand(); }

    // ================= Modo de edição dos cartões =================
    void ToggleEdit() { if (_edit) ExitEdit(); else EnterEdit(); }

    void EnterEdit()
    {
        if (_edit || _st != St.Expanded) return;
        _edit = true; _activity = DateTime.Now; _collapseT.Stop();
        CardCatalog.SetEditing(true);
        _editBar.Visibility = Visibility.Visible;
        if (S.Animations) Motion.Reveal(_editBar, 0, -14, 0.88);
        else MotionEngine.Jump(_editBar, UIElement.OpacityProperty, 1);
    }

    void ExitEdit()
    {
        if (!_edit) return;
        if (_held != null) EndDrag(true);
        _edit = false;
        CardCatalog.SetEditing(false);
        Settings.Save();
        if (S.Animations)
            MotionEngine.To(_editBar, UIElement.OpacityProperty, 0, Tz.Springs.Exit, 0,
                () => { if (!_edit) _editBar.Visibility = Visibility.Collapsed; });
        else { MotionEngine.Jump(_editBar, UIElement.OpacityProperty, 0); _editBar.Visibility = Visibility.Collapsed; }
        if (_st == St.Expanded && !_island.IsMouseOver) ArmCollapse();
    }

    /// <summary>Seções cujos cartões estão na seção aberta precisam continuar "ligadas" (volume, brilho, relógio...).</summary>
    void SyncGuests()
    {
        var want = new HashSet<string>();
        if (_st == St.Expanded && _pageActive && CurrentPage() is ICardPage cp)
            foreach (var c in CardCatalog.For(cp.PageId))
                if (c.Owner != cp.PageId) want.Add(c.Owner);

        foreach (var id in _guestOn.ToList())
            if (!want.Contains(id))
            {
                _guestOn.Remove(id);
                if (!(id == _pageId && _pageActive) && _pages.TryGetValue(id, out var gp)) gp.Hidden();
            }
        foreach (var id in want)
            if (id != _pageId && _guestOn.Add(id) && _pages.TryGetValue(id, out var gp)) gp.Shown();
    }

    void HideGuests()
    {
        foreach (var id in _guestOn.ToList())
            if (_pages.TryGetValue(id, out var gp)) gp.Hidden();
        _guestOn.Clear();
    }

    void RelayoutAllBoards()
    {
        foreach (var p in _pages.Values.OfType<ICardPage>())
            p.Board.Layout(CardCatalog.For(p.PageId), true, true);
    }

    // ---- Pegar o cartão ----
    void OnCardPick(CardHolder h, MouseButtonEventArgs e)
    {
        if (!_edit || _held != null || _ending) return;
        _held = h; _dragging = false; _dropping = false;
        _downPt = e.GetPosition(_dragLayer); _lastPtr = _downPt; _grab = e.GetPosition(h);
        _dragLayer.Background = Brushes.Transparent;
        _dragLayer.IsHitTestVisible = true;
        _dragLayer.CaptureMouse();
    }

    void OnDragMove(object sender, MouseEventArgs e)
    {
        if (_held == null || _dropping) return;
        var p = e.GetPosition(_dragLayer);
        _lastPtr = p; _activity = DateTime.Now;
        if (!_dragging)
        {
            if ((p - _downPt).Length < 5) return;      // pequeno tremor do mouse não conta como arrastar
            BeginDrag();
        }
        _gx.To(p.X - _grab.X, 0, 820, 52); _gy.To(p.Y - _grab.Y, 0, 820, 52);
        _dragLoop.Kick();
        UpdateDropTarget(p);
    }

    void BeginDrag()
    {
        var h = _held!;
        _dragging = true;
        _snap = CardCatalog.Snapshot();
        var org = h.TranslatePoint(new Point(0, 0), _dragLayer);
        _ghost = new Shapes.Rectangle
        {
            Width = Math.Max(1, h.ActualWidth), Height = Math.Max(1, h.ActualHeight), RadiusX = 18, RadiusY = 18,
            Fill = new VisualBrush(h.Card) { Stretch = Stretch.Fill },       // cópia ao vivo do cartão
            Stroke = K.StrokeHi, StrokeThickness = 1, IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new TransformGroup { Children = { _gScale, _gRot } }
        };
        Canvas.SetLeft(_ghost, org.X); Canvas.SetTop(_ghost, org.Y);
        _dragLayer.Children.Add(_ghost);
        _gx.Snap(org.X); _gy.Snap(org.Y); _gr.Snap(0); _gs.Snap(1);
        _gs.To(1.05, 0, 320, 15);                       // "pop" elástico ao levantar
        h.SetPlaceholder(true);
        _scrollT.Start();
        _dragLoop.Kick();
    }

    void DragFrame()
    {
        if (_ghost == null) return;
        Canvas.SetLeft(_ghost, _gx.Value); Canvas.SetTop(_ghost, _gy.Value);
        // inclina conforme a velocidade horizontal; ao pousar, volta a zero
        _gr.Target = _dropping ? 0 : Math.Clamp(_gx.Velocity * 0.0055, -7, 7);
        _gScale.ScaleX = _gScale.ScaleY = _gs.Value;
        _gRot.Angle = _gr.Value;
        if (_dropping && _gx.Settled && _gy.Settled && _gs.Settled && _gr.Settled) FinishDrop();
    }

    // ---- Onde soltar ----
    void UpdateDropTarget(Point p)
    {
        // 1) barra lateral: parar sobre um ícone por um instante troca de seção (o cartão continua na mão)
        var navId = NavIdAt(p);
        if (navId != _dwellId)
        {
            _dwellT.Stop(); ClearNavHighlight();
            _dwellId = navId;
            if (navId != null && navId != _pageId) { HighlightNav(navId); _dwellT.Start(); }
        }
        if (navId != null) return;

        // 2) área da seção aberta
        if (CurrentPage() is not ICardPage page) return;
        var tl = _pageHost.TranslatePoint(new Point(0, 0), _dragLayer);
        if (!new Rect(tl, new Size(_pageHost.ActualWidth, _pageHost.ActualHeight)).Contains(p)) return;
        var bp = _dragLayer.TranslatePoint(p, page.Board.Host);
        int idx = page.Board.InsertIndex(bp, _held);
        if (idx >= 0) MoveHeld(page, idx);
    }

    void MoveHeld(ICardPage page, int idx)
    {
        if ((DateTime.Now - _lastMove).TotalMilliseconds < 110) return;      // dá tempo para a mola anterior assentar
        var id = _held!.Info.Id;
        string cur = CardCatalog.HostOf(id);
        if (cur == page.PageId && CardCatalog.For(page.PageId).FindIndex(c => c.Id == id) == idx) return;
        _lastMove = DateTime.Now;
        CardCatalog.Move(id, page.PageId, idx);
        page.Board.Layout(CardCatalog.For(page.PageId), true, true);
        if (cur != page.PageId && _pages.TryGetValue(cur, out var src) && src is ICardPage sp)
            sp.Board.Layout(CardCatalog.For(sp.PageId), true, true);
        SyncGuests();
    }

    string? NavIdAt(Point p)
    {
        foreach (var kv in _navBtns)
        {
            var b = kv.Value;
            if (b.ActualWidth <= 0) continue;
            Point tl;
            try { tl = b.TranslatePoint(new Point(0, 0), _dragLayer); } catch { continue; }
            if (new Rect(tl.X - 8, tl.Y - 1, b.ActualWidth + 16, b.ActualHeight + 2).Contains(p)) return kv.Key;
        }
        return null;
    }

    // Tile opaco: cinza translúcido clareava demais sobre o vidro claro e o ícone branco sumia.
    void HighlightNav(string id) { if (_navBtns.TryGetValue(id, out var b)) b.Background = K.Raised; }
    void ClearNavHighlight() { foreach (var b in _navBtns.Values) b.ClearValue(Control.BackgroundProperty); }

    void OnDwell()
    {
        _dwellT.Stop();
        if (_held == null || _dwellId == null) return;
        var id = _dwellId;
        ClearNavHighlight();
        _dwellId = null;
        if (id != _pageId) ShowPage(id);
    }

    void OnAutoScroll()
    {
        if (_held == null || !_dragging || _dropping) return;
        if (CurrentPage() is not ICardPage page) return;
        var sv = page.Scroller;
        if (sv == null || sv.ScrollableHeight <= 0.5) return;
        var sp = _dragLayer.TranslatePoint(_lastPtr, sv);
        const double edge = 42;
        double dy = 0;
        if (sp.Y < edge && sp.Y > -24) dy = -(edge - sp.Y) / edge * 14;
        else if (sp.Y > sv.ActualHeight - edge && sp.Y < sv.ActualHeight + 24) dy = (sp.Y - (sv.ActualHeight - edge)) / edge * 14;
        if (Math.Abs(dy) < 0.2) return;
        sv.ScrollToVerticalOffset(Math.Clamp(sv.VerticalOffset + dy, 0, sv.ScrollableHeight));
        UpdateDropTarget(_lastPtr);
    }

    // ---- Soltar ----
    void EndDrag(bool cancel)
    {
        if (_held == null || _ending) return;
        _ending = true;
        _dwellT.Stop(); _scrollT.Stop(); ClearNavHighlight(); _dwellId = null;
        _dragLayer.ReleaseMouseCapture();
        var h = _held;

        if (!_dragging)       // foi só um clique, sem arrastar
        {
            _held = null; _ending = false;
            _dragLayer.IsHitTestVisible = false; _dragLayer.Background = null;
            return;
        }

        if (cancel && _snap != null) { CardCatalog.Restore(_snap); RelayoutAllBoards(); }
        else { CardCatalog.SyncHomeOrder(); Settings.Save(); }
        SyncGuests();

        // o cartão fantasma desliza (com mola) até o espaço reservado e só então o cartão de verdade reaparece
        _dropping = true;
        Point? dest = null;
        if (h.Parent is Grid g && g.IsLoaded && g.IsVisible && CurrentPage() is ICardPage cp && cp.Board.Contains(h))
            dest = cp.Board.OriginIn(h, _dragLayer);
        if (dest == null) { FinishDrop(); return; }
        _gx.To(dest.Value.X, 0, 480, 25); _gy.To(dest.Value.Y, 0, 480, 25); _gs.To(1, 0, 380, 14);
        _dragLoop.Kick();
    }

    void FinishDrop()
    {
        _dropping = false; _dragging = false;
        if (_ghost != null) { _dragLayer.Children.Remove(_ghost); _ghost = null; }
        _dragLayer.IsHitTestVisible = false; _dragLayer.Background = null;
        var h = _held; _held = null; _ending = false; _snap = null;
        if (h == null) return;
        h.SetPlaceholder(false);
        if (S.Animations) h.Pulse();
    }

    // ---------------- Animation frame step ----------------
    // (o antigo Soft aplicava BlurEffect de software a cada quadro — removido: opacidade basta e é barata)
    void Frame()
    {
        // Mola pode ultrapassar zero no fechamento rápido: geometria nunca recebe valor negativo
        // (Math.Clamp(x, 0, negativo) lançava ArgumentException e abortava o resto do quadro).
        double w = Math.Max(1, _w.Value), h = Math.Max(1, _h.Value);
        double rb = Math.Clamp(_r.Value, 0, Math.Min(w, h) / 2);
        _island.Width = w; _island.Height = h;
        if (Floating)
        {
            if (_roundClip == null) _roundClip = new RectangleGeometry();
            // voltando do Clássico o recorte precisa ser reatribuído (senão fica a silhueta do notch)
            if (!ReferenceEquals(_island.Clip, _roundClip)) { _island.Clip = _roundClip; _outline.Data = _roundClip; }
            _roundClip.Rect = new Rect(0, 0, w, h);
            _roundClip.RadiusX = _roundClip.RadiusY = rb;
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
        _expScale.ScaleX = _expScale.ScaleY = _expS.Value;
        _expShift.Y = _expY.Value;
        double stretch = Math.Min(Math.Abs(_dotX.Velocity) * 0.014, 14);   // squash & stretch da lente (vertical)
        _dotScale.ScaleY = (LensH + stretch) / LensH;
        _dotScale.ScaleX = (LensW - Math.Min(stretch * 0.2, 4)) / LensW;
        _dotShift.Y = _dotX.Value - stretch / 2;
        // A borda responde à física (identidade TzIsland): espessa um pouco no movimento, volta ao repouso.
        // (espessura e não opacidade, para o repouso continuar idêntico ao de antes)
        _outline.StrokeThickness = 0.8 + Math.Min(0.35, (Math.Abs(_w.Velocity) + Math.Abs(_h.Velocity)) / 3000);
        // Cada ícone acende e cresce conforme a lente passa por ele (tudo sincronizado com o movimento da lente).
        double lensC = _dotX.Value + LensH / 2;
        var off = ((SolidColorBrush)K.Label).Color; var on = ((SolidColorBrush)K.White).Color;
        foreach (var kv in _navCenter)
        {
            double near = Math.Clamp(1 - Math.Abs(lensC - kv.Value) / (LensH * 0.85), 0, 1);
            if (_navBrush.TryGetValue(kv.Key, out var br)) br.Color = FK.Lerp(off, on, near);
            if (_navScale.TryGetValue(kv.Key, out var sc)) sc.ScaleX = sc.ScaleY = 1 + 0.09 * near;
        }
        double mw = Math.Max(1, Math.Min(w, CollapsedWidthNow) - 2 * MiniEar);
        if (Math.Abs(_mini.Width - mw) > 0.2) _mini.Width = mw;   // só quando muda de verdade: menos layout por quadro
        // A fase visual vira "assentada" quando todas as molas param (é o que os gestos leem).
        if (_phase != Phase.Settled && _w.Settled && _h.Settled && _r.Settled
            && _miniO.Settled && _bannerO.Settled && _expO.Settled && _expS.Settled && _expY.Settled)
            _phase = Phase.Settled;
        // Sombra: mesma silhueta, um halo por baixo. No modo Clássico o topo é reto (só as bases arredondam).
        if (S.WindowShadow)
        {
            // Só no modo Flutuante: no Clássico a silhueta tem ombros côncavos e um halo retangular
            // deixaria preto nos "ears" (a ilha é colada na borda da tela — sombra ali não faz sentido).
            _shadowBox.Opacity = Floating ? (S.Look == IslandLook.SolidBlack ? 0.62 : 0.42) : 0;
            Canvas.SetLeft(_shadowBox, (Width - w) / 2);
            Canvas.SetTop(_shadowBox, _island.Margin.Top);
            _shadowBox.Width = w; _shadowBox.Height = h;
            _shadowBox.CornerRadius = new CornerRadius(rb);
        }
        UpdateRegion();
    }

    // ---------------- Real backdrop blur capture ----------------
    bool _capturing;

    /// <summary>
    /// Captura a tela atrás da ilha para o vidro. A própria ilha (pill, banner ou conteúdo ainda saindo) entrava na captura e
    /// aparecia borrada ao fundo, como "ilhas duplicadas". Agora a janela fica excluída da captura só durante o instante da foto.
    /// </summary>
    async void CaptureBackdrop()
    {
        if (S.Look == IslandLook.SolidBlack || S.BlurAmount <= 0 || _hwnd == IntPtr.Zero || _capturing) return;
        _capturing = true;
        bool excluded = false;
        try
        {
            try { excluded = Native.SetWindowDisplayAffinity(_hwnd, 0x11); } catch { }
            if (excluded) await Task.Delay(60);      // dá tempo ao DWM de aplicar a exclusão antes da cópia
            double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
            if (!Native.GetWindowRect(_hwnd, out var rc)) return;
            double padX = S.BlurAmount, padY = S.BlurAmount;
            int wpx = (int)((S.ExpandedWidth + padX * 2) * dpi), hpx = (int)((S.ExpandedHeight + padY) * dpi);
            int x = rc.Left + (int)((rc.Right - rc.Left - wpx) / 2.0), y = rc.Top + (int)(_island.Margin.Top * dpi);
            // O fundo é borrado e fica a 48–82% de opacidade: metade da resolução é indistinguível em tela
            // e economiza metade da RAM do bitmap, que fica residente enquanto a ilha está aberta.
            using var full = new System.Drawing.Bitmap(Math.Max(1, wpx), Math.Max(1, hpx));
            using (var gr = System.Drawing.Graphics.FromImage(full))
                gr.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(wpx, hpx));
            using var bmp = new System.Drawing.Bitmap(Math.Max(1, wpx / 2), Math.Max(1, hpx / 2));
            using (var gr = System.Drawing.Graphics.FromImage(bmp))
            {
                gr.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                gr.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                gr.DrawImage(full, new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height));
            }
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
        finally
        {
            if (excluded) { try { Native.SetWindowDisplayAffinity(_hwnd, 0); } catch { } }   // volta a aparecer em prints e gravações
            _capturing = false;
        }
    }

    // ---------------- Interaction ----------------
    void OnPreviewDown(object sender, MouseButtonEventArgs e)
    {
        _activity = DateTime.Now;
        if (e.OriginalSource is DependencyObject d && FindParent<System.Windows.Controls.Primitives.TextBoxBase>(d) != null) EnableKeyboard();
    }

    static T? FindParent<T>(DependencyObject d) where T : DependencyObject
    {
        while (d != null)
        {
            if (d is T t) return t;
            // Elementos de texto (Run, Paragraph do RichTextBox) não são Visual: sobe pela árvore lógica.
            d = d is Visual || d is System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d)
                : d is FrameworkContentElement fce ? fce.Parent ?? LogicalTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
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
            MotionEngine.Jump(_miniShift, TranslateTransform.XProperty, next ? -12 : 12);
            MotionEngine.To(_miniShift, TranslateTransform.XProperty, 0, Tz.Springs.Sheet);
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
        if (!S.Animations) MotionEngine.Jump(_hud, UIElement.OpacityProperty, on ? 1 : 0);
        else MotionEngine.To(_hud, UIElement.OpacityProperty, on ? 1 : 0, on ? Tz.Springs.Fade.With(2, 1.4) : Tz.Springs.Fade);
    }

    // ---------------- Cor dinâmica da capa ----------------
    void ApplyAccent()
    {
        var c = S.DynamicCoverColor ? Svc.Media.Accent : Colors.White;
        if (c == _accentTarget) return;
        _accentTarget = c;
        if (S.Animations) MotionEngine.ToColor(_accent, SolidColorBrush.ColorProperty, c, 600);
        else MotionEngine.SetColor(_accent, SolidColorBrush.ColorProperty, c);
    }

    void ArmCollapse()
    {
        _collapseT.Stop();
        if (S.CollapseDelayMs <= 0 && !KeepOpen && !_island.IsMouseOver) { Collapse(); return; }
        _collapseT.Interval = TimeSpan.FromMilliseconds(Math.Max(30, S.CollapseDelayMs));
        _collapseT.Start();
    }

    bool KeepOpen => _edit || CurrentPage()?.KeepOpen == true || IsKeyboardFocusWithin && Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase
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
            if (first || !S.Animations)
            {
                MotionEngine.Jump(_miniLive, UIElement.OpacityProperty, mode == Live.None ? 0 : 1);
                MotionEngine.Jump(_ledHost, UIElement.OpacityProperty, mode == Live.None ? 1 : 0);
            }
            else if (prev == Live.None || mode == Live.None)
            {
                MotionEngine.To(_miniLive, UIElement.OpacityProperty, mode == Live.None ? 0 : 1, Tz.Springs.Settle);
                MotionEngine.To(_ledHost, UIElement.OpacityProperty, mode == Live.None ? 1 : 0, Tz.Springs.Settle);
            }
            else   // trocou de combinação: a faixa some e volta já reorganizada
            {
                MotionEngine.Jump(_miniLive, UIElement.OpacityProperty, 0);
                MotionEngine.To(_miniLive, UIElement.OpacityProperty, 1, Tz.Springs.Fade, 0.05);
            }
            _liveShown = mode;
            // Um único escritor da largura — e nunca durante uma transição em voo (a fase manda).
            if (_st == St.Collapsed && _phase == Phase.Settled)
            {
                var (w, _, _) = Target(St.Collapsed);
                if (S.Animations) _w.To(w, 0, 380, 24); else _w.Snap(w);
                _loop.Kick();
            }
        }
        _miniEq.Running = (mode == Live.Media || mode == Live.Both) && _st == St.Collapsed && IsVisible;
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
        MotionEngine.Jump(_dlFill, WidthProperty, seg);
        _dlFill.Background = _accent;                     // acompanha a cor da capa quando há música
        _dlSweep?.Stop();
        _dlSweep = MotionEngine.Wave(_dlShift, TranslateTransform.XProperty, t =>
        {
            double k = (t * 1000 % 1150) / 1150;           // varredura em laço, sem storyboard infinito
            return -seg + (w + seg) * (0.5 - 0.5 * Math.Cos(Math.PI * k));
        });
        if (S.Animations) MotionEngine.To(_dlBar, UIElement.OpacityProperty, 1, Tz.Springs.Fade);
        else MotionEngine.Jump(_dlBar, UIElement.OpacityProperty, 1);
    }

    /// <summary>O arquivo terminou de verdade: a barra enche até o fim (verde) e some.</summary>
    void FinishDlBar()
    {
        _dlFinishing = true;
        _dlSweep?.Stop(); _dlSweep = null;
        MotionEngine.Jump(_dlShift, TranslateTransform.XProperty, 0);
        _dlFill.Background = K.Green;
        if (S.Animations) MotionEngine.To(_dlFill, WidthProperty, DlWidth, Tz.Springs.Sheet);
        else MotionEngine.Jump(_dlFill, WidthProperty, DlWidth);
    }

    void StopDlBar()
    {
        _dlRunning = _dlFinishing = false;
        _dlSweep?.Stop(); _dlSweep = null;
        MotionEngine.To(_dlBar, UIElement.OpacityProperty, 0, Tz.Springs.Fade);
        MotionEngine.To(_dlFill, WidthProperty, 0, Tz.Springs.Fade, 0.1);
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
        // A respiração só existe com a pílula visível (expandida ela fica oculta e custaria quadros à toa).
        if (!_ledPulsing && S.Animations && _st == St.Collapsed)
        {
            _ledPulsing = true;
            _ledIdle.StopPulse();     // ocioso: ponto neutro e imóvel (em repouso a ilha não respira)
            _ledActive.Pulse(950);    // ativo: um pouco mais viva
        }
        if (_ledOn == active) return;
        bool first = _ledOn == null;
        _ledOn = active;
        if (first || !S.Animations)
        {
            MotionEngine.Jump(_ledIdle, UIElement.OpacityProperty, active ? 0 : 1);
            MotionEngine.Jump(_ledActive, UIElement.OpacityProperty, active ? 1 : 0);
            return;
        }
        MotionEngine.To(_ledIdle, UIElement.OpacityProperty, active ? 0 : 1, Tz.Springs.Settle);
        MotionEngine.To(_ledActive, UIElement.OpacityProperty, active ? 1 : 0, Tz.Springs.Settle);
    }

    int _slowTick;

    /// <summary>Um tique por segundo para tudo que só precisa da ordem dos segundos.</summary>
    void OnSlowTick()
    {
        Perf.Sample();
        if (_st == St.Collapsed) RefreshMini();
        if (_statsOn) UpdateStats();
        if (++_slowTick % 2 == 0) CheckFullscreen();      // tela cheia: a cada 2 s, como antes
    }

    // ---------------- Medidor de quadro (Etapa 0) ----------------
    void ToggleStats()
    {
        _statsOn = !_statsOn;
        _statsBox.Visibility = _statsOn ? Visibility.Visible : Visibility.Collapsed;
        if (_statsOn) UpdateStats();
    }

    void UpdateStats() => _statsText.Text = MotionStats.Line();

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

    static readonly HashSet<string> ShellClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd"
    };

    bool IsForegroundFullscreen()
    {
        try
        {
            var h = Native.GetForegroundWindow();
            if (h == IntPtr.Zero || h == _hwnd) return false;
            var sb = new System.Text.StringBuilder(64);
            Native.GetClassName(h, sb, 64);
            if (ShellClasses.Contains(sb.ToString())) return false;      // área de trabalho e barra de tarefas não contam
            if (!Native.GetWindowRect(h, out var r)) return false;
            var scr = System.Windows.Forms.Screen.FromHandle(h).Bounds;
            var mine = System.Windows.Forms.Screen.FromHandle(_hwnd).Bounds;
            if (scr != mine) return false;                                // tela cheia em outro monitor não esconde a ilha
            return r.Left <= scr.Left && r.Top <= scr.Top && r.Right >= scr.Right && r.Bottom >= scr.Bottom;
        }
        catch { return false; }
    }

    void CheckFullscreen()
    {
        // Antes: SHQueryUserNotificationState com o estado 2 (BUSY), que dispara com o foco na área de trabalho e em outras janelas
        // grandes, fazendo a ilha sumir. Agora só conta tela cheia de verdade: janela em primeiro plano cobrindo o monitor da ilha
        // (ignorando desktop e barra de tarefas) ou os estados D3D/apresentação do Windows.
        // Modo "normal" em tela cheia: não há nada a detectar (economiza enumeração de processos a cada 2 s).
        if (S.Fullscreen == FullscreenMode.Keep) { _noExpand = false; return; }
        bool fs = IsForegroundFullscreen();
        try { if (!fs && Native.SHQueryUserNotificationState(out int st) == 0) fs = st is 3 or 4; } catch { }
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
