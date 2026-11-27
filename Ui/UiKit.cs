using System.Windows.Automation;
using System.Windows.Markup;
using System.Windows.Media.Animation;

namespace TzIsland;

/// <summary>Apple-inspired futuristic design system: refined typography, glassmorphic surfaces, elastic micro-interactions.</summary>
public static class K
{
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    // Manrope embutida (pasta Fonts/, variável 200–800: um arquivo cobre Regular, Medium e SemiBold).
    // Se por algum motivo não carregar, cai para a fonte do Windows.
    static readonly Uri FontBase = new("pack://application:,,,/");
    public static readonly FontFamily Font = new(FontBase, "./Fonts/#Manrope, Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily FontMedium = new(FontBase, "./Fonts/#Manrope, Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily FontSemi = new(FontBase, "./Fonts/#Manrope, Segoe UI Variable Display, Segoe UI");
    public static readonly FontFamily DisplayFont = FontSemi;
    public static readonly FontFamily Mono = new("Cascadia Mono, Consolas, Courier New");

    static SolidColorBrush Mk(string hex)
    {
        var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        b.Freeze();
        return b;
    }

    // ---- Paleta "Cupertino Dark": cinzas neutros da Apple, hierarquia por tom e traço em branco translúcido.
    // Branco puro sobre preto puro "irradia" (o texto parece mais fino e qualquer degrau de pixel aparece):
    // o texto é um branco levemente suavizado sobre um fundo cinza muito escuro, como nos apps nativos.
    public static readonly Brush Fg = Mk("#FFF2F2F5"), Dim = Mk("#FFB4B4BA"), Faint = Mk("#FF8E8E93"),
        Card = Mk("#FF141417"), CardStrong = Mk("#FF232327"), Green = Mk("#FF30D158"),
        Red = Mk("#FFFF453A"), Orange = Mk("#FFFF9F0A"), Clear = Brushes.Transparent,
        Cyan = Mk("#FF64D2FF"), Purple = Mk("#FFBF5AF2"), Neon = Mk("#FF00D4AA");

    // Acentos: azul de sistema em tudo que é interativo; o branco fica reservado para a ação primária.
    public static readonly Brush Violet = Mk("#FFB566FF"), Sky = Mk("#FF64B5FF");

    public static readonly Brush Surface = Elevation(), Stroke = Mk("#14FFFFFF"), StrokeHi = Mk("#2EFFFFFF"),
        // Contorno dos cartões: branco leitoso opaco (1 px). O traço translúcido sumia sobre a superfície escura.
        CardStroke = Mk("#FFECECEC"), CardStrokeHi = Mk("#FFFFFFFF"),
        Accent = Mk("#FF0A84FF"), Label = Mk("#FF9A9AA0"), Muted = Mk("#FF6E6E73"),
        Track = Mk("#FF26262A"), Raised = Mk("#FF1F1F23"), Knob = Mk("#FF5A5A60"),
        White = Mk("#FFFFFFFF"), Ink = Mk("#FF0A0A0B"), Bg = Mk("#FF0F0F11");

    /// <summary>Fundo da ilha no visual Solid Black: cinza bem escuro (nunca #000000, que gera serrilhado óptico no texto).</summary>
    public static readonly Color BgColor = Color.FromRgb(0x0F, 0x0F, 0x11);

    /// <summary>Superfície de cartão: gradiente quase imperceptível de cima para baixo (todo material pega luz).</summary>
    static Brush Elevation()
    {
        var g = new LinearGradientBrush(Color.FromRgb(0x1A, 0x1A, 0x1D), Color.FromRgb(0x14, 0x14, 0x16), 90);
        g.Freeze();
        return g;
    }


    public static SolidColorBrush Hex(string hex) => Mk(hex);

    public static LinearGradientBrush AccentGradient => new(Color.FromRgb(0x0A, 0x84, 0xFF), Color.FromRgb(0x00, 0x71, 0xE3), 0);
    public static LinearGradientBrush VioletGradient => new(Color.FromRgb(0xD0, 0x8C, 0xFF), Color.FromRgb(0xA8, 0x55, 0xF7), 0);

    const string Ns = "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    static Style MakeButtonStyle(bool ghost)
    {
        string hover = ghost
            ? "<Setter TargetName=\"bd\" Property=\"Background\" Value=\"#1AFFFFFF\"/>"
            : "<Setter TargetName=\"bd\" Property=\"Opacity\" Value=\"0.85\"/>";
        string xaml = $$"""
<Style {{Ns}} TargetType="Button">
  <Setter Property="Foreground" Value="White"/>
  <Setter Property="Cursor" Value="Hand"/>
  <Setter Property="Background" Value="Transparent"/>
  <Setter Property="Focusable" Value="False"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="Button">
        <Border x:Name="bd" Background="{TemplateBinding Background}" CornerRadius="{TemplateBinding Tag}" Padding="{TemplateBinding Padding}" RenderTransformOrigin="0.5,0.5">
          <Border.RenderTransform><ScaleTransform x:Name="sc"/></Border.RenderTransform>
          <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
        </Border>
        <!-- O "apertar" saiu do template: era storyboard com To sem From (saltava ao reentrar).
             Agora é Interaction.Press, uma mola com posse que parte do valor atual. -->
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">{{hover}}</Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
""";
        return (Style)XamlReader.Parse(xaml);
    }

    static readonly Style GhostStyle = MakeButtonStyle(true);
    static readonly Style FillStyle = MakeButtonStyle(false);
    public static Style Ghost => GhostStyle;
    public static Style Fill => FillStyle;

    static readonly Style BoxStyle = (Style)XamlReader.Parse($$"""
<Style {{Ns}} TargetType="TextBox">
  <Setter Property="Foreground" Value="White"/>
  <Setter Property="CaretBrush" Value="White"/>
  <Setter Property="SelectionBrush" Value="#55FFFFFF"/>
  <Setter Property="Background" Value="#FF0E0E10"/>
  <Setter Property="Padding" Value="12,8"/>
  <Setter Property="FontSize" Value="12"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="TextBox">
        <Grid>
          <Border Background="{TemplateBinding Background}" CornerRadius="12" BorderBrush="#FF1D1D20" BorderThickness="1"/>
          <ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}" VerticalScrollBarVisibility="Hidden" HorizontalScrollBarVisibility="Hidden"/>
          <TextBlock x:Name="hint" Text="{TemplateBinding Tag}" Foreground="#4DFFFFFF" Margin="{TemplateBinding Padding}" IsHitTestVisible="False" Visibility="Collapsed" FontSize="{TemplateBinding FontSize}" VerticalAlignment="Center"/>
        </Grid>
        <ControlTemplate.Triggers>
          <Trigger Property="Text" Value=""><Setter TargetName="hint" Property="Visibility" Value="Visible"/></Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
""");

    // Campo numérico compacto (usado ao lado dos sliders das Configurações): o texto fica centralizado na vertical,
    // sem depender de padding, para nunca ser cortado nem desalinhado.
    static readonly Style NumBoxStyle = (Style)XamlReader.Parse($$"""
<Style {{Ns}} TargetType="TextBox">
  <Setter Property="Foreground" Value="White"/>
  <Setter Property="CaretBrush" Value="White"/>
  <Setter Property="SelectionBrush" Value="#55FFFFFF"/>
  <Setter Property="Background" Value="#FF0E0E10"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="TextBox">
        <Border x:Name="bd" Background="{TemplateBinding Background}" CornerRadius="10" BorderBrush="#FF1D1D20" BorderThickness="1" Padding="8,0">
          <ScrollViewer x:Name="PART_ContentHost" VerticalAlignment="Center" Focusable="False" VerticalScrollBarVisibility="Hidden" HorizontalScrollBarVisibility="Hidden"/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="bd" Property="BorderBrush" Value="#FF4A4A50"/></Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
""");

    public static TextBox NumBox(string text = "")
        => new() { Style = NumBoxStyle, Text = text, Width = 72, Height = 30, FontFamily = Mono, FontSize = 12, TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };

    // ---------- constructors ----------
    public static TextBlock T(string text, double size = 12, Brush? fg = null, bool bold = false) => new()
    {
        Text = text, FontSize = size, Foreground = fg ?? Fg,
        // Manrope é geométrica: no peso Regular o texto pequeno sai serrilhado em escala de cinza no Windows.
        // Por isso o texto comum vai em Medium; títulos e números em SemiBold.
        FontFamily = bold ? FontSemi : (size >= 20 ? FontMedium : Font),
        FontWeight = bold ? FontWeights.SemiBold : (size >= 20 ? FontWeights.Normal : FontWeights.Medium), TextTrimming = TextTrimming.CharacterEllipsis
    };

    /// <summary>Números grandes (timers, relógio): Manrope SemiBold com algarismos tabulares, que não "pulam" ao mudar.</summary>
    public static TextBlock Num(string text, double size, Brush? fg = null)
    {
        var t = T(text, size, fg ?? Fg, true);
        t.TextTrimming = TextTrimming.None;
        System.Windows.Documents.Typography.SetNumeralAlignment(t, FontNumeralAlignment.Tabular);
        return t;
    }

    /// <summary>Espaçamento entre letras simulado com "hair space" (usado só onde o tracking é intencional).</summary>
    public static string Spaced(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length * 2);
        for (int i = 0; i < text.Length; i++) { sb.Append(text[i]); if (i < text.Length - 1 && text[i] != ' ' && text[i + 1] != ' ') sb.Append('\u200A'); }
        return sb.ToString();
    }

    /// <summary>Rótulo de cartão: cinza secundário em caixa normal — a Apple não usa caixa alta espaçada.</summary>
    public static TextBlock Cap(string text, double size = 11, Brush? fg = null)
    {
        var t = T(text, Math.Max(size, 10.5), fg ?? Label, true);
        t.TextTrimming = TextTrimming.None;
        return t;
    }

    public static TextBlock Glyph(string g, double size = 15, Brush? fg = null) => new()
    {
        Text = g, FontFamily = Icons, FontSize = size, Foreground = fg ?? Fg,
        VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center
    };

    /// <summary>Anel de foco visível nos alvos só de ícone (Tab/teclado). Antes a ilha era mouse-only.</summary>
    static readonly Style FocusRing = (Style)XamlReader.Parse($$"""
<Style {{Ns}} TargetType="Control">
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="Control">
        <Border BorderBrush="#FF0A84FF" BorderThickness="2" CornerRadius="10" Margin="-3"/>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
""");

    public static Button IconBtn(string glyph, string tip, Action click, double size = 15, double w = 34, double h = 30, Brush? fg = null)
    {
        var b = new Button
        {
            Style = GhostStyle, Width = w, Height = h, ToolTip = tip, Tag = new CornerRadius(h / 2),
            Content = glyph.Length > 1 ? (object)T(glyph, size - 2, fg, true) : Glyph(glyph, size, fg)
        };
        // O template não traz nome acessível: os 13 botões só de ícone da barra eram invisíveis para leitores de tela.
        AutomationProperties.SetName(b, tip);
        AutomationProperties.SetHelpText(b, tip);
        b.Focusable = true;
        b.FocusVisualStyle = FocusRing;
        Interaction.Press(b);
        b.Click += (_, _) => click();
        return b;
    }

    public static Button TextBtn(string text, Action click, bool accent = false, double size = 12)
    {
        var b = new Button
        {
            Style = accent ? FillStyle : GhostStyle, Padding = new Thickness(13, 6, 13, 6), Tag = new CornerRadius(10),
            Background = accent ? White : Card, Content = T(text, size, accent ? Ink : Fg, true)
        };
        AutomationProperties.SetName(b, text);
        Interaction.Press(b);
        b.Click += (_, _) => click();
        return b;
    }

    public static TextBox Box(string hint = "", bool multiline = false)
    {
        var t = new TextBox { Style = BoxStyle, Tag = hint, FontFamily = Font };
        if (multiline) { t.AcceptsReturn = true; t.TextWrapping = TextWrapping.Wrap; t.VerticalContentAlignment = VerticalAlignment.Top; }
        return t;
    }

    public static Border CardBox(UIElement? child = null, double radius = 16, Thickness? pad = null) => new()
    {
        Background = Surface, CornerRadius = new CornerRadius(Math.Max(radius, 12)), Padding = pad ?? new Thickness(16, 14, 16, 14), Child = child,
        BorderBrush = CardStroke, BorderThickness = new Thickness(1)
    };

    public static GridLength GL(string s) =>
        s == "auto" ? GridLength.Auto
        : s.EndsWith('*') ? new GridLength(s.Length == 1 ? 1 : double.Parse(s[..^1], CultureInfo.InvariantCulture), GridUnitType.Star)
        : new GridLength(double.Parse(s, CultureInfo.InvariantCulture));

    public static Grid Cols(params string[] w)
    {
        var g = new Grid();
        foreach (var x in w) g.ColumnDefinitions.Add(new ColumnDefinition { Width = GL(x) });
        return g;
    }

    public static Grid Rows(params string[] h)
    {
        var g = new Grid();
        foreach (var x in h) g.RowDefinitions.Add(new RowDefinition { Height = GL(x) });
        return g;
    }

    public static T At<T>(this T el, int row = 0, int col = 0, int rowSpan = 1, int colSpan = 1) where T : UIElement
    {
        Grid.SetRow(el, row); Grid.SetColumn(el, col);
        if (rowSpan > 1) Grid.SetRowSpan(el, rowSpan);
        if (colSpan > 1) Grid.SetColumnSpan(el, colSpan);
        return el;
    }

    public static T M<T>(this T el, double l, double t = 0, double r = 0, double b = 0) where T : FrameworkElement
    { el.Margin = new Thickness(l, t, r, b); return el; }

    public static T Vc<T>(this T el) where T : FrameworkElement { el.VerticalAlignment = VerticalAlignment.Center; return el; }

    public static ScrollViewer Scroll(UIElement content) => SmoothScroll.Create(content);

    /// <summary>
    /// Recorta o elemento com cantos arredondados de verdade. ClipToBounds num Border com CornerRadius recorta em retângulo,
    /// então imagens e fundos "vazam" pelos cantos; este recorte acompanha o tamanho do elemento.
    /// </summary>
    public static T RoundClip<T>(T el, double radius) where T : FrameworkElement
    {
        void Apply()
        {
            if (el.ActualWidth <= 0 || el.ActualHeight <= 0) return;
            var g = new RectangleGeometry(new Rect(0, 0, el.ActualWidth, el.ActualHeight), radius, radius);
            g.Freeze(); el.Clip = g;
        }
        el.SizeChanged += (_, _) => Apply();
        Apply();
        return el;
    }

    public static void FadeIn(UIElement el, int delayMs = 0, double fromY = 10) => Motion.FadeIn(el, delayMs, fromY);

    public static string Fmt(TimeSpan t) => t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";
    public static string Fmt(double seconds) => Fmt(TimeSpan.FromSeconds(seconds));
}

/// <summary>
/// Rolagem suave: cada giro da roda soma ao alvo acumulado (girar rápido anda mais) e a posição o persegue
/// com suavização exponencial a cada frame — sem reiniciar animações.
/// </summary>
public static class SmoothScroll
{
    const double PixelsPerDelta = 1.9;   // 120 de delta ≈ 228 px por "clique"
    const double Smoothing = 15;         // maior = mais responsivo

    /// <summary>Suavização por quadro registrada no relógio único do app (sem assinatura própria).</summary>
    sealed class Sink : IFrameSink
    {
        readonly ScrollViewer _sv;
        double _target, _pos;

        public Sink(ScrollViewer sv) { _sv = sv; }

        public void Home() { _pos = _sv.VerticalOffset; _target = _pos; }

        public void Add(double delta)
        {
            _target = Math.Clamp(_target - delta, 0, _sv.ScrollableHeight);
            MotionHost.Attach(this);
        }

        public bool Step(double dt)
        {
            _target = Math.Clamp(_target, 0, _sv.ScrollableHeight);
            _pos += (_target - _pos) * (1 - Math.Exp(-dt * Smoothing));
            if (Math.Abs(_target - _pos) < 0.35)
            {
                _pos = _target;
                _sv.ScrollToVerticalOffset(_pos);
                return false;
            }
            _sv.ScrollToVerticalOffset(_pos);
            return true;
        }
    }

    public static ScrollViewer Create(UIElement content)
    {
        var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = content };
        var sink = new Sink(sv);

        sv.PreviewMouseWheel += (_, e) =>
        {
            if (sv.ScrollableHeight <= 0.5) return;       // nada para rolar: deixa o evento seguir
            e.Handled = true;
            sink.Home();
            sink.Add(e.Delta * PixelsPerDelta);
        };
        sv.Unloaded += (_, _) => MotionHost.Detach(sink);
        return sv;
    }
}

/// <summary>Pill-shaped slider with gradient fill. Only fires Changed on user drag.</summary>
public sealed class PillSlider : FrameworkElement
{
    double _v;
    bool _drag;
    public bool ReadOnly { get; set; }
    public event Action<double>? Changed;
    Brush _fill = K.AccentGradient;
    public Brush Fill { get => _fill; set { _fill = value; InvalidateVisual(); } }
    bool _showThumb;
    public bool ShowThumb { get => _showThumb; set { _showThumb = value; InvalidateVisual(); } }
    /// <summary>Cor do "bolinha" (por padrão branca); a página de Mídia usa a cor da capa.</summary>
    public Brush ThumbBrush { get; set; } = K.White;

    public PillSlider() { Height = 22; Cursor = Cursors.Hand; }

    public double Value
    {
        get => _v;
        set { value = Math.Clamp(value, 0, 1); if (Math.Abs(value - _v) > 0.0005 && !_drag) { _v = value; InvalidateVisual(); } }
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = 6, y = (ActualHeight - h) / 2;
        dc.DrawRoundedRectangle(K.Track, null, new Rect(0, y, w, h), 3, 3);
        double fw = Math.Max(h, _v * w);
        dc.DrawRoundedRectangle(Fill, null, new Rect(0, y, fw, h), 3, 3);
                    if (_showThumb)
            {
                double lo = Math.Min(4.5, w / 2), hi = Math.Max(lo, w - 4.5);   // controle estreito: sem máximo negativo
                dc.DrawEllipse(ThumbBrush, null, new Point(Math.Clamp(_v * w, lo, hi), ActualHeight / 2), 4.5, 4.5);
            }
    }

    void Set(Point p)
    {
        _v = Math.Clamp(p.X / Math.Max(1, ActualWidth), 0, 1);
        InvalidateVisual();
        Changed?.Invoke(_v);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (ReadOnly) return;
        _drag = true; CaptureMouse(); Set(e.GetPosition(this));
    }

    protected override void OnMouseMove(MouseEventArgs e) { if (_drag) Set(e.GetPosition(this)); }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { if (_drag) { _drag = false; ReleaseMouseCapture(); } e.Handled = true; }
}

/// <summary>Circular usage ring gauge. Null value renders dashed track with "—" (never fabricates data).</summary>
public sealed class RingGauge : FrameworkElement
{
    double? _v;
    public string Caption { get; set; } = "";
    public double? Value { get => _v; set { _v = value; InvalidateVisual(); } }

    public RingGauge() { Width = 54; Height = 54; }

    protected override void OnRender(DrawingContext dc)
    {
        double s = Math.Min(ActualWidth, ActualHeight), r = s / 2 - 4;
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        var track = new Pen(K.CardStrong, 4);
        if (_v == null)
        {
            track.DashStyle = new DashStyle(new double[] { 1.2, 1.6 }, 0);
            dc.DrawEllipse(null, track, c, r, r);
        }
        else
        {
            dc.DrawEllipse(null, track, c, r, r);
            double pct = Math.Clamp(_v.Value, 0, 100);
            Brush col = pct >= 95 ? K.Red : pct >= 80 ? K.Orange : K.White;
            var pen = new Pen(col, 4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            double ang = pct / 100 * 359.9 - 90;
            var start = new Point(c.X, c.Y - r);
            var end = new Point(c.X + r * Math.Cos(ang * Math.PI / 180), c.Y + r * Math.Sin(ang * Math.PI / 180));
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(start, false, false);
                ctx.ArcTo(end, new Size(r, r), 0, pct > 50, SweepDirection.Clockwise, true, false);
            }
            dc.DrawGeometry(null, pen, g);
        }
        string txt = _v == null ? "—" : $"{_v:0}%";
        var ft = new FormattedText(txt, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(K.Font, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), 12, K.Fg, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(ft, new Point(c.X - ft.Width / 2, c.Y - ft.Height / 2));
    }
}

public interface IPage
{
    FrameworkElement View { get; }
    void Shown();
    void Hidden();
    bool KeepOpen { get; }
}

public abstract class PageBase : IPage
{
    public Grid Root { get; } = new();
    public FrameworkElement View => Root;
    public virtual void Shown() { }
    public virtual void Hidden() { }
    public virtual bool KeepOpen => false;
    /// <summary>Elemento que recebe o portador do matched geometry (o LED/tempo recolhido viaja até ele).</summary>
    public virtual FrameworkElement? Hero => null;
    /// <summary>Elementos que entram em cascata quando a página aparece (padrão: cartões e blocos de primeiro nível).</summary>
    public virtual IEnumerable<FrameworkElement> RevealTargets => Motion.Targets(Root);
}
