using System.Windows.Media.Animation;
using Shapes = System.Windows.Shapes;

namespace TzIsland;

/// <summary>Página que hospeda cartões que o usuário pode arrastar (Início, Sistema…).</summary>
public interface ICardPage : IPage
{
    string PageId { get; }
    CardBoard Board { get; }
    ScrollViewer? Scroller { get; }
}

/// <summary>Cartão arrastável. O Id tem o formato "secao.nome" (ex.: "system.controls"); a seção do Id é a de origem.</summary>
public sealed class MovableCard
{
    public string Id { get; }
    public string Title { get; }
    public string Owner { get; }
    public int ColSpan { get; }
    public int RowSpan { get; }
    public CardHolder Holder { get; }

    public MovableCard(string id, string title, FrameworkElement element, int colSpan = 1, int rowSpan = 1)
    {
        Id = id; Title = title; ColSpan = colSpan; RowSpan = rowSpan;
        Owner = id[..id.IndexOf('.')];
        Holder = new CardHolder(element) { Info = this };
        Holder.SetEditing(CardCatalog.Editing);
        CardCatalog.Register(this);
    }
}

/// <summary>
/// Registro global dos cartões e de onde cada um mora. A posição fica em Settings.CardOrder (seção -> ids em ordem);
/// um cartão sem entrada em nenhuma lista fica na seção de origem.
/// </summary>
public static class CardCatalog
{
    static readonly Dictionary<string, MovableCard> All = new();
    static readonly Dictionary<string, List<string>> Native = new();
    static readonly HashSet<string> Ensuring = new();

    /// <summary>Definido pela ilha: cria a página (se ainda não existir) para que seus cartões fiquem registrados.</summary>
    public static Func<string, object?>? EnsurePage;
    public static event Action<CardHolder, MouseButtonEventArgs>? PickUp;
    public static bool Editing { get; private set; }

    internal static void RaisePick(CardHolder h, MouseButtonEventArgs e) => PickUp?.Invoke(h, e);

    internal static void Register(MovableCard c)
    {
        All[c.Id] = c;
        if (!Native.TryGetValue(c.Owner, out var l)) Native[c.Owner] = l = new List<string>();
        if (!l.Contains(c.Id)) l.Add(c.Id);
    }

    public static void SetEditing(bool on)
    {
        Editing = on;
        foreach (var c in All.Values) c.Holder.SetEditing(on);
    }

    static IEnumerable<string> NativeIds(string page) =>
        page == "home"
            ? HomeWidgets.Order().Select(x => "home." + x)
            : Native.TryGetValue(page, out var l) ? l : Enumerable.Empty<string>();

    /// <summary>Seção onde o cartão está agora.</summary>
    public static string HostOf(string id)
    {
        foreach (var kv in Settings.Current.CardOrder)
            if (kv.Value.Contains(id)) return kv.Key;
        return id[..id.IndexOf('.')];
    }

    static void EnsureOwner(string id)
    {
        var owner = id[..id.IndexOf('.')];
        if (!Ensuring.Add(owner)) return;
        try { EnsurePage?.Invoke(owner); } finally { Ensuring.Remove(owner); }
    }

    /// <summary>Cartões que devem aparecer na seção, na ordem.</summary>
    public static List<MovableCard> For(string pageId)
    {
        var ids = new List<string>();
        if (Settings.Current.CardOrder.TryGetValue(pageId, out var saved)) ids.AddRange(saved);
        foreach (var n in NativeIds(pageId))
            if (!ids.Contains(n) && HostOf(n) == pageId) ids.Add(n);

        var res = new List<MovableCard>();
        foreach (var id in ids.Distinct())
        {
            if (HostOf(id) != pageId) continue;
            if (!All.TryGetValue(id, out var c))
            {
                EnsureOwner(id);
                if (!All.TryGetValue(id, out c)) continue;
            }
            if (id.StartsWith("home.") && !HomeWidgets.IsOn(id[5..])) continue;   // desligado em Configurações > Módulos
            res.Add(c);
        }
        return res;
    }

    /// <summary>Move o cartão para a seção, na posição <paramref name="index"/> da lista dela já sem o próprio cartão.</summary>
    public static void Move(string id, string pageId, int index)
    {
        var order = Settings.Current.CardOrder;
        var visible = For(pageId).Where(c => c.Id != id).Select(c => c.Id).ToList();

        var full = new List<string>();
        if (order.TryGetValue(pageId, out var saved)) full.AddRange(saved.Where(x => x != id));
        foreach (var n in NativeIds(pageId))
            if (n != id && !full.Contains(n) && HostOf(n) == pageId) full.Add(n);

        foreach (var kv in order) kv.Value.Remove(id);

        string? anchor = index < visible.Count ? visible[index] : null;
        int pos = anchor == null ? full.Count : Math.Max(0, full.IndexOf(anchor));
        full.Insert(pos, id);
        order[pageId] = full;
    }

    public static Dictionary<string, List<string>> Snapshot() =>
        Settings.Current.CardOrder.ToDictionary(k => k.Key, v => v.Value.ToList());

    public static void Restore(Dictionary<string, List<string>> snap) => Settings.Current.CardOrder = snap;

    /// <summary>Depois de arrastar no Início: a ordem dos cartões nativos vira a ordem salva em Módulos.</summary>
    public static void SyncHomeOrder()
    {
        if (!Settings.Current.CardOrder.TryGetValue("home", out var l)) return;
        var nat = l.Where(i => i.StartsWith("home.")).Select(i => i[5..]).ToList();
        nat.AddRange((Settings.Current.HomeOrder ?? new List<string>()).Where(x => !nat.Contains(x)));
        Settings.Current.HomeOrder = nat;
    }

    /// <summary>Depois de reordenar em Configurações > Módulos: a lista arrastável do Início acompanha.</summary>
    public static void SyncHomeNatives()
    {
        if (!Settings.Current.CardOrder.TryGetValue("home", out var l)) return;
        var nat = HomeWidgets.Order().Select(x => "home." + x).Where(l.Contains).ToList();
        int k = 0;
        for (int i = 0; i < l.Count && k < nat.Count; i++)
            if (l[i].StartsWith("home.")) l[i] = nat[k++];
    }
}

/// <summary>
/// Grade que posiciona os cartões em fluxo (linha a linha, com colSpan/rowSpan) e anima a reorganização:
/// cada cartão desliza da posição antiga para a nova com uma mola elástica (técnica FLIP).
/// </summary>
public sealed class CardBoard
{
    public Grid Host { get; } = new();
    readonly int _cols;
    readonly Func<int, double>? _rowMin;
    readonly bool _lastRowStar;
    List<MovableCard> _cards = new();

    public IReadOnlyList<MovableCard> Cards => _cards;
    public bool Contains(CardHolder h) => h.Parent == Host;

    public CardBoard(int cols, string[] widths, Func<int, double>? rowMin = null, bool lastRowStar = false)
    {
        _cols = cols; _rowMin = rowMin; _lastRowStar = lastRowStar;
        foreach (var w in widths) Host.ColumnDefinitions.Add(new ColumnDefinition { Width = K.GL(w) });
    }

    public void Layout(IEnumerable<MovableCard> cards, bool animate = false, bool force = false)
    {
        var list = cards.ToList();
        if (!force && list.Count == _cards.Count
            && list.Select(c => c.Id).SequenceEqual(_cards.Select(c => c.Id))
            && list.All(c => c.Holder.Parent == Host)) return;

        animate &= Settings.Current.Animations && Host.IsLoaded;

        // posição visual de cada cartão antes da mudança (layout + deslocamento em andamento)
        var before = new Dictionary<CardHolder, Point>();
        if (animate)
            foreach (var c in _cards)
            {
                var h = c.Holder;
                if (h.Parent != Host) continue;
                var s = System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(h);
                before[h] = new Point(s.Left + h.Shift.X, s.Top + h.Shift.Y);
            }

        Host.Children.Clear();
        foreach (var c in list)
            if (c.Holder.Parent is Panel p && p != Host) p.Children.Remove(c.Holder);

        // empacotamento em fluxo
        var occ = new List<bool[]>();
        bool[] Row(int r) { while (occ.Count <= r) occ.Add(new bool[_cols]); return occ[r]; }
        var cells = new List<(MovableCard c, int r, int col, int cs, int rs)>();
        int cr = 0, cc = 0;
        foreach (var c in list)
        {
            int cs = Math.Clamp(c.ColSpan, 1, _cols), rs = Math.Max(1, c.RowSpan);
            while (true)
            {
                if (cc + cs > _cols) { cr++; cc = 0; continue; }
                bool free = true;
                for (int rr = cr; rr < cr + rs && free; rr++)
                    for (int k = cc; k < cc + cs; k++)
                        if (Row(rr)[k]) { free = false; break; }
                if (free) break;
                cc++;
            }
            for (int rr = cr; rr < cr + rs; rr++)
                for (int k = cc; k < cc + cs; k++) Row(rr)[k] = true;
            cells.Add((c, cr, cc, cs, rs));
            cc += cs;
        }

        int rows = cells.Count == 0 ? 0 : cells.Max(x => x.r + x.rs);
        Host.RowDefinitions.Clear();
        for (int i = 0; i < rows; i++)
            Host.RowDefinitions.Add(new RowDefinition
            {
                Height = _lastRowStar && i == rows - 1 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto,
                MinHeight = _rowMin?.Invoke(i) ?? 0
            });

        foreach (var (c, r, col, cs, rs) in cells)
        {
            var h = c.Holder;
            h.Margin = new Thickness(col == 0 ? 0 : 4, r == 0 ? 0 : 4, col + cs >= _cols ? 0 : 4, 0);
            Grid.SetRow(h, r); Grid.SetColumn(h, col); Grid.SetRowSpan(h, rs); Grid.SetColumnSpan(h, cs);
            Host.Children.Add(h);
        }
        _cards = list;

        if (!animate) { foreach (var c in list) c.Holder.ResetShift(); return; }

        Host.UpdateLayout();
        foreach (var c in list)
        {
            var h = c.Holder;
            var s = System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(h);
            if (before.TryGetValue(h, out var old)) h.FlyFrom(old.X - s.Left, old.Y - s.Top);
            else h.PopIn();
        }
    }

    Rect Slot(CardHolder h) => System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(h);

    /// <summary>Canto superior esquerdo do cartão (já sem animação) em coordenadas de <paramref name="rel"/>.</summary>
    public Point OriginIn(CardHolder h, UIElement rel)
    {
        var s = Slot(h);
        return Host.TranslatePoint(new Point(s.Left + h.Margin.Left, s.Top + h.Margin.Top), rel);
    }

    /// <summary>
    /// Posição de encaixe (na lista sem o cartão que está sendo arrastado) para o ponteiro em coordenadas da grade.
    /// -1 = sem mudança. Sobre o próprio espaço reservado nada muda, o que evita o "vai e volta" ao reorganizar.
    /// </summary>
    public int InsertIndex(Point pt, CardHolder? moving)
    {
        var others = _cards.Where(c => c.Holder != moving).ToList();
        if (moving != null && moving.Parent == Host && Slot(moving).Contains(pt)) return -1;
        for (int i = 0; i < others.Count; i++)
        {
            var s = Slot(others[i].Holder);
            if (s.Contains(pt)) return i + (pt.X > s.Left + s.Width / 2 ? 1 : 0);
        }
        if (others.Count == 0) return 0;
        if (pt.Y > others.Max(o => Slot(o.Holder).Bottom)) return others.Count;
        return -1;
    }
}

/// <summary>
/// Invólucro do cartão: carrega as transformações (escala, deslocamento, inclinação), o modo de edição
/// (camada que captura o mouse + tremida estilo iPhone) e o "espaço reservado" que aparece enquanto o cartão é arrastado.
/// A ordem das transformações (escala, deslocamento, ...) é a que Motion.Reveal espera.
/// </summary>
public sealed class CardHolder : Grid
{
    static readonly Random Rng = new();

    public MovableCard Info { get; internal set; } = null!;
    public FrameworkElement Card { get; }
    public readonly ScaleTransform Scale = new(1, 1);
    public readonly TranslateTransform Shift = new();
    public readonly RotateTransform Tilt = new();

    readonly Border _hit;
    readonly Shapes.Rectangle _dim;
    bool _editing, _held, _jig;

    public CardHolder(FrameworkElement card)
    {
        Card = card;
        RenderTransformOrigin = new Point(0.5, 0.5);
        RenderTransform = new TransformGroup { Children = { Scale, Shift, Tilt } };

        Children.Add(card);

        _dim = new Shapes.Rectangle
        {
            RadiusX = 18, RadiusY = 18, Opacity = 0, IsHitTestVisible = false,
            Fill = K.Hex("#E6050506"),
            Stroke = new SolidColorBrush(Color.FromArgb(110, 255, 255, 255)),
            StrokeThickness = 1.2,
            StrokeDashArray = new DoubleCollection { 4, 4 }
        };
        Children.Add(_dim);

        _hit = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),   // quase invisível, mas capta o mouse
            CornerRadius = new CornerRadius(18),
            BorderBrush = K.Hex("#2EFFFFFF"), BorderThickness = new Thickness(1),
            Cursor = Cursors.SizeAll, Visibility = Visibility.Collapsed
        };
        _hit.MouseLeftButtonDown += (_, e) => { e.Handled = true; CardCatalog.RaisePick(this, e); };
        Children.Add(_hit);

        Loaded += (_, _) => UpdateJiggle();
        Unloaded += (_, _) => StopJiggleNow();
    }

    public void SetEditing(bool on)
    {
        _editing = on;
        _hit.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        UpdateJiggle();
    }

    /// <summary>Espaço tracejado que marca onde o cartão arrastado vai cair.</summary>
    public void SetPlaceholder(bool on)
    {
        _held = on;
        if (Settings.Current.Animations) MotionEngine.To(_dim, OpacityProperty, on ? 1 : 0, Tz.Springs.Fade);
        else MotionEngine.Jump(_dim, OpacityProperty, on ? 1 : 0);
        _hit.IsHitTestVisible = !on;
        UpdateJiggle();
    }

    void UpdateJiggle()
    {
        if (_editing && !_held && IsLoaded && Settings.Current.Animations) StartJiggle(); else StopJiggle();
    }

    MotionHandle? _jiggle;

    void StartJiggle()
    {
        if (_jig) return;
        _jig = true;
        double amp = 0.8 + Rng.NextDouble() * 0.55;
        double period = (100 + Rng.Next(0, 50)) * 2 / 1000.0;
        double phase = Rng.Next(0, 110) / 1000.0;                        // fases diferentes: nunca tremem em uníssono
        _jiggle = MotionEngine.Wave(Tilt, RotateTransform.AngleProperty,
            t => amp * Math.Sin(2 * Math.PI * (t + phase) / period));
    }

    void StopJiggle()
    {
        if (!_jig) return;
        _jig = false;
        _jiggle?.Stop();                                                 // encerra no ângulo atual
        _jiggle = null;
        MotionEngine.To(Tilt, RotateTransform.AngleProperty, 0, Tz.Springs.Exit);
    }

    void StopJiggleNow()
    {
        _jig = false;
        _jiggle?.Stop();
        _jiggle = null;
        MotionEngine.Jump(Tilt, RotateTransform.AngleProperty, 0);
    }

    public void ResetShift()
    {
        MotionEngine.Jump(Shift, TranslateTransform.XProperty, 0);
        MotionEngine.Jump(Shift, TranslateTransform.YProperty, 0);
    }

    /// <summary>Desliza da posição antiga (dx, dy de diferença) até a nova com mola.</summary>
    public void FlyFrom(double dx, double dy)
    {
        ResetShift();
        if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5) return;
        MotionEngine.Jump(Shift, TranslateTransform.XProperty, dx);
        MotionEngine.Jump(Shift, TranslateTransform.YProperty, dy);
        MotionEngine.To(Shift, TranslateTransform.XProperty, 0, Tz.Springs.Sheet);
        MotionEngine.To(Shift, TranslateTransform.YProperty, 0, Tz.Springs.Sheet);
    }

    /// <summary>Entrada de um cartão que acabou de chegar nesta grade.</summary>
    public void PopIn()
    {
        ResetShift();
        MotionEngine.Jump(this, OpacityProperty, 0);
        MotionEngine.To(this, OpacityProperty, 1, Tz.Springs.Fade);
        Pulse(0.88);
    }

    /// <summary>"Pop" ao encaixar o cartão no lugar.</summary>
    public void Pulse(double from = 0.93)
    {
        if (!Settings.Current.Animations) return;
        MotionEngine.Jump(Scale, ScaleTransform.ScaleXProperty, from);
        MotionEngine.Jump(Scale, ScaleTransform.ScaleYProperty, from);
        MotionEngine.To(Scale, ScaleTransform.ScaleXProperty, 1, Tz.Springs.Sheet);
        MotionEngine.To(Scale, ScaleTransform.ScaleYProperty, 1, Tz.Springs.Sheet);
    }
}
