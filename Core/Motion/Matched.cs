namespace TzIsland;

/// <summary>
/// Matched geometry (Etapa 4): um "portador" atravessa a transição entre duas superfícies.
/// A geometria é medida no layout real (nada de coordenadas fixas) e o movimento é só transformação —
/// nenhuma largura/altura é animada no layout. Se qualquer medida falhar, não faz nada: o pior caso é
/// não haver continuidade, nunca um layout quebrado.
/// </summary>
public static class Matched
{
    /// <summary>Retângulo de <paramref name="el"/> no espaço de <paramref name="space"/> (null se não medível).</summary>
    public static Rect? RectOf(FrameworkElement el, Visual space)
    {
        try
        {
            if (!el.IsVisible || el.ActualWidth < 0.5 || el.ActualHeight < 0.5) return null;
            if (!ReferenceEquals(el, space) && !el.IsAncestorOf(space) && !space.IsAncestorOf(el)) return null;
            var r = el.TransformToAncestor(space).TransformBounds(new Rect(el.RenderSize));
            return r.Width < 1 || r.Height < 1 ? null : r;
        }
        catch { return null; }
    }

    /// <summary>
    /// Leva um portador de <paramref name="from"/> até <paramref name="to"/> e revela o destino na chegada.
    /// Devolve false quando não deu para medir — aí o chamador deixa o destino como estava.
    /// </summary>
    public static bool Carry(Canvas stage, FrameworkElement from, FrameworkElement to, Brush fill,
                             double radius = 14, double revealDelay = 0.15, bool round = false)
    {
        if (stage?.Parent is not Visual space || from == null || to == null) return false;
        if (RectOf(from, space) is not Rect a || RectOf(to, space) is not Rect b) return false;
        if (Math.Abs(a.Width - b.Width) < 2 && Math.Abs(a.Height - b.Height) < 2 && Math.Abs(a.Left - b.Left) < 2 && Math.Abs(a.Top - b.Top) < 2) return false;

        var scale = new ScaleTransform(1, 1);
        var move = new TranslateTransform();
        var group = new TransformGroup();
        group.Children.Add(scale); group.Children.Add(move);
        var carrier = new Border
        {
            Background = fill,
            CornerRadius = round ? new CornerRadius(Math.Min(a.Width, a.Height) / 2) : new CornerRadius(radius),
            Width = a.Width, Height = a.Height,
            RenderTransformOrigin = new Point(0, 0), RenderTransform = group,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(carrier, a.Left); Canvas.SetTop(carrier, a.Top);
        stage.Children.Add(carrier);

        // Chega rápido (o destino aparece quando ele chega) e some num fade curto: cross-fade, nunca corte.
        var s = Tz.Springs.Lift;
        MotionEngine.To(move, TranslateTransform.XProperty, b.Left - a.Left, s);
        MotionEngine.To(move, TranslateTransform.YProperty, b.Top - a.Top, s);
        MotionEngine.To(scale, ScaleTransform.ScaleXProperty, b.Width / a.Width, s);
        MotionEngine.To(scale, ScaleTransform.ScaleYProperty, b.Height / a.Height, s);
        MotionEngine.To(carrier, UIElement.OpacityProperty, 0, Tz.Springs.Fade, revealDelay + 0.10,
                        () => stage.Children.Remove(carrier));

        // Destino entra exatamente na chegada do portador.
        MotionEngine.Jump(to, UIElement.OpacityProperty, 0);
        MotionEngine.To(to, UIElement.OpacityProperty, 1, Tz.Springs.Fade, revealDelay);
        return true;
    }
}
