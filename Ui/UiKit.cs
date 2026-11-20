using System.Windows.Markup;
using System.Windows.Media.Animation;

namespace TzIsland;

/// <summary>Helpers de interface: cores, estilos (botões com mola, campos escuros) e construtores curtos.</summary>
public static class K
{
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    public static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI");

    static SolidColorBrush Mk(string hex)
    {
        var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        b.Freeze();
        return b;
    }

    public static readonly Brush Fg = Mk("#F0FFFFFF"), Dim = Mk("#99FFFFFF"), Faint = Mk("#44FFFFFF"),
        Card = Mk("#14FFFFFF"), CardStrong = Mk("#22FFFFFF"), Blue = Mk("#FF007AFF"), Green = Mk("#FF32D74B"),
        Red = Mk("#FFFF453A"), Orange = Mk("#FFFF9F0A"), Clear = Brushes.Transparent,
        Cyan = Mk("#FF00E5FF"), Purple = Mk("#FFBF5AF2"), Neon = Mk("#FF00D4AA");

    public static SolidColorBrush Hex(string hex) => Mk(hex);

    public static LinearGradientBrush Accent => new(Color.FromArgb(255, 0, 229, 255), Color.FromArgb(255, 191, 90, 242), 45);
    public static LinearGradientBrush GlowBorder => new(Color.FromArgb(60, 0, 229, 255), Color.FromArgb(20, 191, 90, 242), new Point(0, 0), new Point(1, 1));

    const string Ns = "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    static Style MakeButtonStyle(bool ghost)
    {
        string hover = ghost
            ? "<Setter TargetName=\"bd\" Property=\"Background\" Value=\"#28FFFFFF\"/>"
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
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">{{hover}}</Trigger>
          <Trigger Property="IsPressed" Value="True">
            <Trigger.EnterActions><BeginStoryboard><Storyboard>
              <DoubleAnimation Storyboard.TargetName="sc" Storyboard.TargetProperty="ScaleX" To="0.88" Duration="0:0:0.08"/>
              <DoubleAnimation Storyboard.TargetName="sc" Storyboard.TargetProperty="ScaleY" To="0.88" Duration="0:0:0.08"/>
            </Storyboard></BeginStoryboard></Trigger.EnterActions>
            <Trigger.ExitActions><BeginStoryboard><Storyboard>
              <DoubleAnimation Storyboard.TargetName="sc" Storyboard.TargetProperty="ScaleX" To="1" Duration="0:0:0.4"><DoubleAnimation.EasingFunction><ElasticEase Oscillations="1" Springiness="5" EasingMode="EaseOut"/></DoubleAnimation.EasingFunction></DoubleAnimation>
              <DoubleAnimation Storyboard.TargetName="sc" Storyboard.TargetProperty="ScaleY" To="1" Duration="0:0:0.4"><DoubleAnimation.EasingFunction><ElasticEase Oscillations="1" Springiness="5" EasingMode="EaseOut"/></DoubleAnimation.EasingFunction></DoubleAnimation>
            </Storyboard></BeginStoryboard></Trigger.ExitActions>
          </Trigger>
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

    static readonly Style BoxStyle = (Style)XamlReader.Parse($$"""
<Style {{Ns}} TargetType="TextBox">
  <Setter Property="Foreground" Value="White"/>
  <Setter Property="CaretBrush" Value="White"/>
  <Setter Property="SelectionBrush" Value="#770A84FF"/>
  <Setter Property="Background" Value="#20FFFFFF"/>
  <Setter Property="Padding" Value="10,7"/>
  <Setter Property="FontSize" Value="12"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="TextBox">
        <Grid>
          <Border Background="{TemplateBinding Background}" CornerRadius="10"/>
          <ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}" VerticalScrollBarVisibility="Hidden" HorizontalScrollBarVisibility="Hidden"/>
          <TextBlock x:Name="hint" Text="{TemplateBinding Tag}" Foreground="#5CFFFFFF" Margin="{TemplateBinding Padding}" IsHitTestVisible="False" Visibility="Collapsed" FontSize="{TemplateBinding FontSize}" VerticalAlignment="Center"/>
        </Grid>
        <ControlTemplate.Triggers>
          <Trigger Property="Text" Value=""><Setter TargetName="hint" Property="Visibility" Value="Visible"/></Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
""");

    // ---------- construtores ----------
    public static TextBlock T(string text, double size = 12, Brush? fg = null, bool bold = false) => new()
    {
        Text = text, FontSize = size, Foreground = fg ?? Fg, FontFamily = Font,
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextTrimming = TextTrimming.CharacterEllipsis
    };

    public static TextBlock Glyph(string g, double size = 15, Brush? fg = null) => new()
    {
        Text = g, FontFamily = Icons, FontSize = size, Foreground = fg ?? Fg,
        VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center
    };

    public static Button IconBtn(string glyph, string tip, Action click, double size = 15, double w = 34, double h = 30, Brush? fg = null)
    {
        var b = new Button
        {
            Style = GhostStyle, Width = w, Height = h, ToolTip = tip, Tag = new CornerRadius(h / 2),
            Content = glyph.Length > 1 ? (object)T(glyph, size - 2, fg, true) : Glyph(glyph, size, fg)
        };
        b.Click += (_, _) => click();
        return b;
    }

    public static Button TextBtn(string text, Action click, bool accent = false, double size = 12)
    {
        var b = new Button
        {
            Style = accent ? FillStyle : GhostStyle, Padding = new Thickness(13, 6, 13, 6), Tag = new CornerRadius(10),
            Background = accent ? Accent : Card, Content = T(text, size, Fg, true)
        };
        b.Click += (_, _) => click();
        return b;
    }

    public static TextBox Box(string hint = "", bool multiline = false)
    {
        var t = new TextBox { Style = BoxStyle, Tag = hint, FontFamily = Font };
        if (multiline) { t.AcceptsReturn = true; t.TextWrapping = TextWrapping.Wrap; t.VerticalContentAlignment = VerticalAlignment.Top; }
        return t;
    }

    public static Border CardBox(UIElement? child = null, double radius = 20, Thickness? pad = null) => new()
    {
        Background = Card, CornerRadius = new CornerRadius(radius), Padding = pad ?? new Thickness(12), Child = child,
        BorderBrush = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)), BorderThickness = new Thickness(1)
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

    public static void FadeIn(UIElement el, int delayMs = 0, double fromY = 8)
    {
        el.Opacity = 0;
        var tt = new TranslateTransform(0, fromY);
        el.RenderTransform = tt;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var begin = TimeSpan.FromMilliseconds(delayMs);
        el.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)) { BeginTime = begin, EasingFunction = ease });
        tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(fromY, 0, TimeSpan.FromMilliseconds(420)) { BeginTime = begin, EasingFunction = ease });
    }

    public static string Fmt(TimeSpan t) => t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";
    public static string Fmt(double seconds) => Fmt(TimeSpan.FromSeconds(seconds));
}

/// <summary>Rolagem com inércia suave (a roda do mouse anima o deslocamento em vez de pular).</summary>
public static class SmoothScroll
{
    public static readonly DependencyProperty OffsetProperty = DependencyProperty.RegisterAttached(
        "Offset", typeof(double), typeof(SmoothScroll),
        new PropertyMetadata(0.0, (d, e) => ((ScrollViewer)d).ScrollToVerticalOffset((double)e.NewValue)));

    public static ScrollViewer Create(UIElement content)
    {
        var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = content };
        sv.PreviewMouseWheel += (_, e) =>
        {
            e.Handled = true;
            double target = Math.Clamp(sv.VerticalOffset - e.Delta * 0.85, 0, sv.ScrollableHeight);
            sv.BeginAnimation(OffsetProperty, new DoubleAnimation(sv.VerticalOffset, target, TimeSpan.FromMilliseconds(320))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        };
        return sv;
    }
}

/// <summary>Slider em formato de pílula, estilo iOS. Só dispara Changed quando o usuário arrasta.</summary>
public sealed class PillSlider : FrameworkElement
{
    double _v;
    bool _drag;
    public bool ReadOnly { get; set; }
    public event Action<double>? Changed;
    public Brush Fill { get; set; } = new LinearGradientBrush(Color.FromArgb(255, 0, 229, 255), Color.FromArgb(255, 0, 122, 255), 0);

    public PillSlider() { Height = 22; Cursor = Cursors.Hand; }

    public double Value
    {
        get => _v;
        set { value = Math.Clamp(value, 0, 1); if (Math.Abs(value - _v) > 0.0005 && !_drag) { _v = value; InvalidateVisual(); } }
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = 8, y = (ActualHeight - h) / 2;
        dc.DrawRoundedRectangle(K.CardStrong, null, new Rect(0, y, w, h), 4, 4);
        double fw = Math.Max(h, _v * w);
        dc.DrawRoundedRectangle(Fill, null, new Rect(0, y, fw, h), 4, 4);
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

/// <summary>Indicador circular de utilização. Sem valor => anel tracejado e "—" (nunca inventa dado).</summary>
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
        var track = new Pen(K.CardStrong, 5);
        if (_v == null)
        {
            track.DashStyle = new DashStyle(new double[] { 1.2, 1.6 }, 0);
            dc.DrawEllipse(null, track, c, r, r);
        }
        else
        {
            dc.DrawEllipse(null, track, c, r, r);
            double pct = Math.Clamp(_v.Value, 0, 100);
            Brush col = pct >= 95 ? K.Red : pct >= 80 ? K.Orange : K.Cyan;
            var pen = new Pen(col, 5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
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
            new Typeface(K.Font, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), 11, K.Fg, VisualTreeHelper.GetDpi(this).PixelsPerDip);
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
}
