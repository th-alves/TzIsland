namespace TzIsland;

/// <summary>Ícone oficial do app (Assets/TzIsland.ico, embutido como recurso WPF), para a bandeja e as janelas.</summary>
public static class AppIcon
{
    static readonly Uri Pack = new("pack://application:,,,/Assets/TzIsland.ico");

    /// <summary>Ícone da bandeja no tamanho que o Windows usa nesta tela (16 px a 100%, 24 px a 150%…).</summary>
    public static System.Drawing.Icon Tray()
    {
        try
        {
            var size = System.Windows.Forms.SystemInformation.SmallIconSize;
            using var s = Application.GetResourceStream(Pack)!.Stream;
            return new System.Drawing.Icon(s, size);
        }
        catch (Exception e)
        {
            Log.Write(e);
            return System.Drawing.SystemIcons.Application;
        }
    }

    /// <summary>Ícone das janelas (barra de título e barra de tarefas).</summary>
    public static ImageSource? Window()
    {
        try { return System.Windows.Media.Imaging.BitmapFrame.Create(Pack); }
        catch (Exception e) { Log.Write(e); return null; }
    }
}
