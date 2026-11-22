namespace TzIsland;

/// <summary>Ícones de linha 24×24 (traço fino, pontas arredondadas) para combinar com o estilo das referências.</summary>
public sealed class IconHost : Viewbox
{
    readonly System.Windows.Shapes.Path _p;
    public IconHost(string data, double size, Brush stroke, double thickness)
    {
        Geometry g;
        try { g = Geometry.Parse(data); }
        catch { g = new EllipseGeometry(new Point(12, 12), 7, 7); }     // nunca derruba a interface por causa de um ícone
        _p = new System.Windows.Shapes.Path
        {
            Data = g, Stroke = stroke, StrokeThickness = thickness, StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
        };
        var c = new Canvas { Width = 24, Height = 24 };
        c.Children.Add(_p);
        Child = c; Width = Height = size; IsHitTestVisible = false; Stretch = Stretch.Uniform;
    }
    public Brush Stroke { get => _p.Stroke; set => _p.Stroke = value; }
}

public static class Ico
{
    static readonly Dictionary<string, string> D = new()
    {
        ["rain"] = "M4 14.9 A7 7 0 1 1 15.7 8 h1.8 a4.5 4.5 0 0 1 2.5 8.2 M16 14 v6 M8 14 v6 M12 16 v6",
        ["forest"] = "M17 14 l3 3.3 a1 1 0 0 1 -.7 1.7 H4.7 a1 1 0 0 1 -.7 -1.7 L7 14 h-.3 a1 1 0 0 1 -.7 -1.7 L9 9 h-.2 A1 1 0 0 1 8 7.3 L12 3 l4 4.3 a1 1 0 0 1 -.8 1.7 H15 l3 3.3 a1 1 0 0 1 -.7 1.7 H17 Z M12 22 v-3",
        ["cafe"] = "M10 2 v2 M14 2 v2 M6 2 v2 M16 8 a1 1 0 0 1 1 1 v8 a4 4 0 0 1 -4 4 H7 a4 4 0 0 1 -4 -4 V9 a1 1 0 0 1 1 -1 h14 a4 4 0 1 1 0 8 h-1",
        ["office"] = "M4 4 h16 a2 2 0 0 1 2 2 v12 a2 2 0 0 1 -2 2 H4 a2 2 0 0 1 -2 -2 V6 a2 2 0 0 1 2 -2 z M6 8 h.01 M10 8 h.01 M14 8 h.01 M18 8 h.01 M8 12 h.01 M12 12 h.01 M16 12 h.01 M7 16 h10",
        ["lofi"] = "M3 14 h3 a2 2 0 0 1 2 2 v3 a2 2 0 0 1 -2 2 H5 a2 2 0 0 1 -2 -2 v-7 a9 9 0 0 1 18 0 v7 a2 2 0 0 1 -2 2 h-1 a2 2 0 0 1 -2 -2 v-3 a2 2 0 0 1 2 -2 h3",
        ["fire"] = "M8.5 14.5 A2.5 2.5 0 0 0 11 12 c0 -1.38 -.5 -2 -1 -3 -1.072 -2.143 -.224 -4.054 2 -6 .5 2.5 2 4.9 4 6.5 2 1.6 3 3.5 3 5.5 a7 7 0 1 1 -14 0 c0 -1.153 .433 -2.294 1 -3 a2.5 2.5 0 0 0 2.5 2.5 z",
        ["stream"] = "M7 16.3 c2.2 0 4 -1.83 4 -4.05 0 -1.16 -.57 -2.26 -1.71 -3.19 S7.29 6.75 7 5.3 c-.29 1.45 -1.14 2.84 -2.29 3.76 S3 11.1 3 12.25 c0 2.22 1.8 4.05 4 4.05 z M12.56 6.6 A10.97 10.97 0 0 0 14 3.02 c.5 2.5 2 4.9 4 6.5 s3 3.5 3 5.5 a6.98 6.98 0 0 1 -11.91 4.97",
        ["waves"] = "M2 6 c.6 .5 1.2 1 2.5 1 C7 7 7 5 9.5 5 c2.6 0 2.4 2 5 2 2.5 0 2.5 -2 5 -2 1.3 0 1.9 .5 2.5 1 M2 12 c.6 .5 1.2 1 2.5 1 2.5 0 2.5 -2 5 -2 2.6 0 2.4 2 5 2 2.5 0 2.5 -2 5 -2 1.3 0 1.9 .5 2.5 1 M2 18 c.6 .5 1.2 1 2.5 1 2.5 0 2.5 -2 5 -2 2.6 0 2.4 2 5 2 2.5 0 2.5 -2 5 -2 1.3 0 1.9 .5 2.5 1",
        // ---- seções da barra
        ["home"] = "M15 21 v-8 a1 1 0 0 0 -1 -1 h-4 a1 1 0 0 0 -1 1 v8 M3 10 a2 2 0 0 1 .709 -1.528 l7 -5.999 a2 2 0 0 1 2.582 0 l7 5.999 A2 2 0 0 1 21 10 v9 a2 2 0 0 1 -2 2 H5 a2 2 0 0 1 -2 -2 z",
        ["timer"] = "M10 2 h4 M12 14 l3 -3 M12 22 a8 8 0 1 0 0 -16 a8 8 0 0 0 0 16 z",
        ["hourglass"] = "M5 22 h14 M5 2 h14 M17 22 v-4.172 a2 2 0 0 0 -.586 -1.414 L12 12 l-4.414 4.414 A2 2 0 0 0 7 17.828 V22 M7 2 v4.172 a2 2 0 0 0 .586 1.414 L12 12 l4.414 -4.414 A2 2 0 0 0 17 6.172 V2",
        ["mail"] = "M22 7 l-8.991 5.727 a2 2 0 0 1 -2.009 0 L2 7 M4 4 h16 a2 2 0 0 1 2 2 v12 a2 2 0 0 1 -2 2 H4 a2 2 0 0 1 -2 -2 V6 a2 2 0 0 1 2 -2 z",
        ["pen"] = "M21.174 6.812 a1 1 0 0 0 -3.986 -3.987 L3.842 16.174 a2 2 0 0 0 -.5 .83 l-1.321 4.352 a.5 .5 0 0 0 .623 .622 l4.353 -1.32 a2 2 0 0 0 .83 -.497 z M15 5 l4 4",
        ["monitor"] = "M20 3 H4 a2 2 0 0 0 -2 2 v10 a2 2 0 0 0 2 2 h16 a2 2 0 0 0 2 -2 V5 a2 2 0 0 0 -2 -2 z M8 21 h8 M12 17 v4",
        ["folder"] = "M20 20 a2 2 0 0 0 2 -2 V8 a2 2 0 0 0 -2 -2 h-7.9 a2 2 0 0 1 -1.69 -.9 L9.6 3.9 A2 2 0 0 0 7.93 3 H4 a2 2 0 0 0 -2 2 v13 a2 2 0 0 0 2 2 z",
        ["camera"] = "M13.997 4 a2 2 0 0 1 1.76 1.05 l.486 .9 A2 2 0 0 0 18.003 7 H20 a2 2 0 0 1 2 2 v9 a2 2 0 0 1 -2 2 H4 a2 2 0 0 1 -2 -2 V9 a2 2 0 0 1 2 -2 h1.997 a2 2 0 0 0 1.759 -1.048 l.489 -.904 A2 2 0 0 1 10.004 4 z M12 17 a4 4 0 1 0 0 -8 a4 4 0 0 0 0 8 z",
        ["mic"] = "M12 2 a3 3 0 0 0 -3 3 v7 a3 3 0 0 0 6 0 V5 a3 3 0 0 0 -3 -3 z M19 10 v2 a7 7 0 0 1 -14 0 v-2 M12 19 v3",
        ["settings"] = "M4 6 h10 M18 6 h2 M4 12 h4 M12 12 h8 M4 18 h12 M20 18 h0 M16 4 v4 M10 10 v4 M18 16 v4",
        // ---- Sistema
        ["wifi"] = "M12 20 h.01 M2 8.82 a15 15 0 0 1 20 0 M5 12.859 a10 10 0 0 1 14 0 M8.5 16.429 a5 5 0 0 1 7 0",
        ["wifi-off"] = "M12 20 h.01 M8.5 16.429 a5 5 0 0 1 7 0 M5 12.859 a10 10 0 0 1 5.17 -2.69 M19 12.859 a10 10 0 0 0 -2.007 -1.523 M2 8.82 a15 15 0 0 1 4.177 -2.643 M22 8.82 a15 15 0 0 0 -11.288 -3.764 M2 2 l20 20",
        ["bluetooth"] = "M7 7 l10 10 l-5 5 V2 l5 5 L7 17",
        ["volume"] = "M11 5 L6 9 H2 v6 h4 l5 4 z M15.5 8.5 a5 5 0 0 1 0 7 M18.8 5.2 a9.5 9.5 0 0 1 0 13.6",
        ["volume-low"] = "M11 5 L6 9 H2 v6 h4 l5 4 z M15.5 8.5 a5 5 0 0 1 0 7",
        ["volume-off"] = "M11 5 L6 9 H2 v6 h4 l5 4 z M22 9 l-6 6 M16 9 l6 6",
        ["sun"] = "M12 8 a4 4 0 1 0 0 8 a4 4 0 0 0 0 -8 z M12 2 v2 M12 20 v2 M4.93 4.93 l1.41 1.41 M17.66 17.66 l1.41 1.41 M2 12 h2 M20 12 h2 M6.34 17.66 l-1.41 1.41 M19.07 4.93 l-1.41 1.41",
        ["zap"] = "M13 2 L4 14 h7 l-1 8 l9 -12 h-7 z",
        ["headphones"] = "M3 14 h3 a2 2 0 0 1 2 2 v3 a2 2 0 0 1 -2 2 H5 a2 2 0 0 1 -2 -2 v-7 a9 9 0 0 1 18 0 v7 a2 2 0 0 1 -2 2 h-1 a2 2 0 0 1 -2 -2 v-3 a2 2 0 0 1 2 -2 h3",
        ["contrast"] = "M12 22 a10 10 0 1 0 0 -20 a10 10 0 0 0 0 20 z M12 18 a6 6 0 0 0 0 -12 v12 z",
        ["clock"] = "M12 22 a10 10 0 1 0 0 -20 a10 10 0 0 0 0 20 z M12 6 v6 l4 2",
        ["battery"] = "M4 7 h12 a2 2 0 0 1 2 2 v6 a2 2 0 0 1 -2 2 H4 a2 2 0 0 1 -2 -2 V9 a2 2 0 0 1 2 -2 z M22 11 v2",
        ["file-text"] = "M15 2 H6 a2 2 0 0 0 -2 2 v16 a2 2 0 0 0 2 2 h12 a2 2 0 0 0 2 -2 V7 z M14 2 v4 a2 2 0 0 0 2 2 h4 M10 9 H8 M16 13 H8 M16 17 H8",
        ["cloud"] = "M17.5 19 H9 a7 7 0 1 1 6.71 -9 h1.79 a4.5 4.5 0 1 1 0 9 z",
        ["calendar"] = "M8 2 v4 M16 2 v4 M5 4 h14 a2 2 0 0 1 2 2 v14 a2 2 0 0 1 -2 2 H5 a2 2 0 0 1 -2 -2 V6 a2 2 0 0 1 2 -2 z M3 10 h18",
        ["music"] = "M9 18 V5 l12 -2 v13 M9 18 a3 3 0 1 1 -6 0 a3 3 0 0 1 6 0 z M21 16 a3 3 0 1 1 -6 0 a3 3 0 0 1 6 0 z",
        ["video"] = "M4 5 h16 a2 2 0 0 1 2 2 v10 a2 2 0 0 1 -2 2 H4 a2 2 0 0 1 -2 -2 V7 a2 2 0 0 1 2 -2 z M10 9 l5 3 l-5 3 z",
    };

    public static string Path(string id) => D.TryGetValue(id, out var d) ? d : D["music"];

    public static IconHost Make(string id, double size = 16, Brush? stroke = null, double thickness = 1.7)
        => new(D.TryGetValue(id, out var d) ? d : D["music"], size, stroke ?? K.Fg, thickness);
}

/// <summary>
/// Ícone de bateria moderno: contorno fino arredondado, preenchimento proporcional à carga (verde carregando, laranja/vermelho
/// quando baixa) e um raio sobre o preenchimento quando está carregando. Desenhado em código, nítido em qualquer tamanho.
/// </summary>
public sealed class BatteryGlyph : FrameworkElement
{
    static readonly Geometry Bolt = Geometry.Parse("M13 2 L5.5 13 H11 L10 22 L18.5 10 H13 Z");
    int _pct = 100; bool _charging;

    public BatteryGlyph() { Width = 34; Height = 18; IsHitTestVisible = false; }

    public void Set(int percent, bool charging)
    {
        percent = Math.Clamp(percent, 0, 100);
        if (percent == _pct && charging == _charging) return;
        _pct = percent; _charging = charging; InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight, nub = Math.Max(2.5, h * 0.16), bw = w - nub - 1, bh = h - 1;
        var edge = new Pen(K.Muted, Math.Max(1.2, h * 0.075)) { LineJoin = PenLineJoin.Round };
        double r = h * 0.3;
        dc.DrawRoundedRectangle(null, edge, new Rect(0.5, 0.5, bw, bh), r, r);
        dc.DrawRoundedRectangle(K.Muted, null, new Rect(bw + 1.5, h * 0.34, nub - 0.5, h * 0.32), nub / 2, nub / 2);

        Brush fill = _charging ? K.Green : _pct <= 15 ? K.Red : _pct <= 30 ? K.Orange : K.White;
        double pad = h * 0.16, iw = (bw - pad * 2) * Math.Max(0.04, _pct / 100.0), ih = bh - pad * 2;
        dc.DrawRoundedRectangle(fill, null, new Rect(0.5 + pad, 0.5 + pad, iw, ih), Math.Max(1.5, r - pad * 0.8), Math.Max(1.5, r - pad * 0.8));

        if (_charging)
        {
            double s = h * 0.7 / 24;
            dc.PushTransform(new TranslateTransform(bw / 2 + 0.5 - 12 * s, h / 2 - 12 * s));
            dc.PushTransform(new ScaleTransform(s, s));
            dc.DrawGeometry(K.Ink, new Pen(Brushes.Transparent, 0), Bolt);
            dc.Pop(); dc.Pop();
        }
    }
}
