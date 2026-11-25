using System.Globalization;
using System.Windows.Media.Imaging;

namespace TzIsland;

/// <summary>
/// Harness de verificação visual (uso de desenvolvimento). Renderiza a ilha em PNG no estado pedido,
/// sem depender de alguém olhando a tela a cada mudança:
///   TzIsland.exe --shot pasta\ [--state collapsed|banner|expanded] [--page media] [--scale 1.5] [--style floating]
/// A janela é criada fora do fluxo normal (sem mutex, sem bandeja, sem serviços) e fecha ao terminar.
/// </summary>
public static class Shot
{
    public readonly record struct Args(string Dir, string State, string? Page, double Scale);

    public static Args? Parse(string[] a)
    {
        int i = Array.IndexOf(a, "--shot");
        if (i < 0 || i + 1 >= a.Length) return null;
        string? Val(string k) { int j = Array.IndexOf(a, k); return j >= 0 && j + 1 < a.Length ? a[j + 1] : null; }
        double scale = double.TryParse(Val("--scale"), NumberStyles.Float, CultureInfo.InvariantCulture, out var sc) && sc > 0 ? sc : 1;
        return new Args(a[i + 1], (Val("--state") ?? "collapsed").ToLowerInvariant(), Val("--page"), Math.Clamp(scale, 0.5, 3));
    }

    /// <summary>
    /// Renderiza sem composição de tela (RenderTargetBitmap). Com scale &gt; 1 o mesmo layout é desenhado
    /// com mais pixels, o que equivale a conferir em 150% de DPI.
    /// </summary>
    public static string Save(FrameworkElement root, double width, double height, string dir, string name, double scale)
    {
        Directory.CreateDirectory(dir);
        int pw = Math.Max(1, (int)Math.Ceiling(width * scale)), ph = Math.Max(1, (int)Math.Ceiling(height * scale));
        var rtb = new RenderTargetBitmap(pw, ph, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        rtb.Render(root);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        string path = Path.Combine(dir, $"{name}@{scale:0.##}x.png");
        using (var fs = File.Create(path)) enc.Save(fs);
        return path;
    }
}
