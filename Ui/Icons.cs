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
        ["music"] = "M9 18 V5 l12 -2 v13 M9 18 a3 3 0 1 1 -6 0 a3 3 0 0 1 6 0 z M21 16 a3 3 0 1 1 -6 0 a3 3 0 0 1 6 0 z",
        ["video"] = "M4 5 h16 a2 2 0 0 1 2 2 v10 a2 2 0 0 1 -2 2 H4 a2 2 0 0 1 -2 -2 V7 a2 2 0 0 1 2 -2 z M10 9 l5 3 l-5 3 z",
    };

    public static IconHost Make(string id, double size = 16, Brush? stroke = null, double thickness = 1.7)
        => new(D.TryGetValue(id, out var d) ? d : D["music"], size, stroke ?? K.Fg, thickness);
}
