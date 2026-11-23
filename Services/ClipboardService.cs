namespace TzIsland;

public enum ClipKind { Text, Image, Files }

public sealed class ClipItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ClipKind Kind { get; set; }
    public string Text { get; set; } = "";               // texto, ou caminhos (um por linha) para arquivos
    public string ImagePath { get; set; } = "";          // PNG em %AppData%\TzIsland\clips (só itens fixados são gravados em disco)
    public bool Pinned { get; set; }
    public DateTime Time { get; set; } = DateTime.Now;
    [System.Text.Json.Serialization.JsonIgnore] public BitmapSource? Image { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string Preview => Kind switch
    {
        ClipKind.Image => "Imagem",
        ClipKind.Files => string.Join(", ", Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(p => System.IO.Path.GetFileName(p.TrimEnd('\\', '/')))),
        _ => Text
    };
}

/// <summary>
/// Histórico da área de transferência (texto, imagens e arquivos) com itens fixáveis.
/// Privacidade: o histórico comum fica só na memória (some ao sair); apenas o que você FIXA é gravado em disco.
/// Conteúdo que gerenciadores de senha marcam como "não incluir no histórico" é ignorado.
/// </summary>
public sealed class ClipboardService
{
    const int MaxText = 50_000, MaxImages = 8, MaxPinned = 30;
    readonly JsonStore<List<ClipItem>> _store = new("clipboard");
    public ObservableCollection<ClipItem> Items { get; } = new();
    public event Action? Changed;
    int _suppress;

    static string ClipDir => System.IO.Path.Combine(Settings.DataDir, "clips");
    static Dispatcher Ui => Application.Current.Dispatcher;

    public void Start()
    {
        foreach (var it in _store.Data)
        {
            if (it.Kind == ClipKind.Image) { it.Image = LoadPng(it.ImagePath); if (it.Image == null) continue; }
            it.Pinned = true;
            Items.Add(it);
        }
    }

    public bool Enabled => Settings.Current.IsOn("clipboard");

    // ---------------- captura ----------------
    /// <summary>Chamado quando o Windows avisa que a área de transferência mudou (WM_CLIPBOARDUPDATE).</summary>
    public void OnSystemUpdate()
    {
        if (!Enabled) return;
        if (_suppress > 0) { _suppress--; return; }
        _ = ReadWithRetryAsync();
    }

    async Task ReadWithRetryAsync()
    {
        for (int i = 0; i < 6; i++)
        {
            try { Read(); return; }
            catch (System.Runtime.InteropServices.COMException) { await Task.Delay(60 + i * 40); }   // outro app está com a área aberta
            catch (Exception e) { Log.Write(e); return; }
        }
    }

    bool Read()
    {
        var data = Clipboard.GetDataObject();
        if (data == null || Excluded(data)) return false;

        if (Clipboard.ContainsFileDropList())
        {
            var l = Clipboard.GetFileDropList().Cast<string>().ToList();
            if (l.Count > 0) { Push(new ClipItem { Kind = ClipKind.Files, Text = string.Join("\n", l) }); return true; }
        }
        if (Clipboard.ContainsText())
        {
            var t = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(t)) return false;
            if (t.Length > MaxText) t = t[..MaxText];
            Push(new ClipItem { Kind = ClipKind.Text, Text = t });
            return true;
        }
        if (Clipboard.ContainsImage())
        {
            var img = Clipboard.GetImage();
            if (img == null) return false;
            if (img.CanFreeze) img.Freeze();
            Push(new ClipItem { Kind = ClipKind.Image, Image = img, Text = $"{img.PixelWidth}×{img.PixelHeight}" });
            return true;
        }
        return false;
    }

    /// <summary>Gerenciadores de senha pedem para o conteúdo não entrar em históricos: respeitamos.</summary>
    static bool Excluded(IDataObject d)
    {
        try
        {
            if (d.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing")) return true;
            if (d.GetDataPresent("CanIncludeInClipboardHistory") && d.GetData("CanIncludeInClipboardHistory") is MemoryStream ms)
            {
                var b = new byte[4]; int n = ms.Read(b, 0, 4);
                if (n == 4 && BitConverter.ToInt32(b, 0) == 0) return true;
            }
        }
        catch { }
        return false;
    }

    static bool Same(ClipItem a, ClipItem b) =>
        a.Kind == b.Kind && (a.Kind == ClipKind.Image ? a.Text == b.Text && SameImage(a.Image, b.Image) : a.Text == b.Text);

    static bool SameImage(BitmapSource? a, BitmapSource? b)
    {
        if (a == null || b == null || a.PixelWidth != b.PixelWidth || a.PixelHeight != b.PixelHeight) return false;
        try
        {   // compara uma amostra de pixels (barato e suficiente para evitar duplicatas)
            int w = Math.Min(8, a.PixelWidth), h = Math.Min(8, a.PixelHeight);
            var ra = new byte[w * h * 4]; var rb = new byte[w * h * 4];
            var fa = new FormatConvertedBitmap(a, PixelFormats.Bgra32, null, 0); var fb = new FormatConvertedBitmap(b, PixelFormats.Bgra32, null, 0);
            fa.CopyPixels(new Int32Rect(0, 0, w, h), ra, w * 4, 0); fb.CopyPixels(new Int32Rect(0, 0, w, h), rb, w * 4, 0);
            return ra.AsSpan().SequenceEqual(rb);
        }
        catch { return false; }
    }

    void Push(ClipItem it)
    {
        Ui.Invoke(() =>
        {
            var dup = Items.FirstOrDefault(x => Same(x, it));
            if (dup != null)
            {
                dup.Time = DateTime.Now;                        // já existe: só sobe para o topo (mantém o "fixado")
                if (!dup.Pinned) { Items.Remove(dup); Items.Insert(FirstUnpinned(), dup); }
            }
            else Items.Insert(FirstUnpinned(), it);
            Trim();
            Changed?.Invoke();
        });
    }

    int FirstUnpinned() { int i = 0; while (i < Items.Count && Items[i].Pinned) i++; return i; }

    void Trim()
    {
        int max = Math.Clamp(Settings.Current.ClipboardMax, 5, 200);
        var loose = Items.Where(x => !x.Pinned).ToList();
        foreach (var x in loose.Skip(max)) Items.Remove(x);
        foreach (var x in Items.Where(x => !x.Pinned && x.Kind == ClipKind.Image).Skip(MaxImages).ToList()) Items.Remove(x);
    }

    // ---------------- ações ----------------
    public void TogglePin(ClipItem it)
    {
        if (!it.Pinned && Items.Count(x => x.Pinned) >= MaxPinned) return;
        it.Pinned = !it.Pinned;
        Items.Remove(it);
        if (it.Pinned) Items.Insert(0, it); else Items.Insert(FirstUnpinned(), it);
        if (it.Pinned && it.Kind == ClipKind.Image) SavePng(it);
        if (!it.Pinned && it.Kind == ClipKind.Image) DeletePng(it);
        Persist(); Changed?.Invoke();
    }

    public void Remove(ClipItem it)
    {
        Items.Remove(it);
        if (it.Kind == ClipKind.Image) DeletePng(it);
        if (it.Pinned) Persist();
        Changed?.Invoke();
    }

    /// <summary>Apaga o histórico, mantendo os itens fixados.</summary>
    public void ClearUnpinned()
    {
        foreach (var x in Items.Where(x => !x.Pinned).ToList()) Items.Remove(x);
        Changed?.Invoke();
    }

    /// <summary>Coloca o item de volta na área de transferência (sem criar uma duplicata no histórico).</summary>
    public bool Copy(ClipItem it)
    {
        try
        {
            _suppress++;
            switch (it.Kind)
            {
                case ClipKind.Text: Clipboard.SetText(it.Text); break;
                case ClipKind.Image: if (it.Image == null) return false; Clipboard.SetImage(it.Image); break;
                case ClipKind.Files:
                    var sc = new System.Collections.Specialized.StringCollection();
                    foreach (var p in it.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries)) sc.Add(p);
                    Clipboard.SetFileDropList(sc); break;
            }
            it.Time = DateTime.Now;
            if (!it.Pinned && Items.Remove(it)) Items.Insert(FirstUnpinned(), it);
            Changed?.Invoke();
            return true;
        }
        catch (Exception e) { if (_suppress > 0) _suppress--; Log.Write(e); return false; }
    }

    // ---------------- disco (somente fixados) ----------------
    void Persist()
    {
        _store.Data.Clear();
        _store.Data.AddRange(Items.Where(x => x.Pinned));
        _store.Save();
    }

    static void SavePng(ClipItem it)
    {
        if (it.Image == null) return;
        try
        {
            Directory.CreateDirectory(ClipDir);
            it.ImagePath = System.IO.Path.Combine(ClipDir, it.Id + ".png");
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(it.Image));
            using var fs = File.Create(it.ImagePath); enc.Save(fs);
        }
        catch (Exception e) { Log.Write(e); }
    }

    static void DeletePng(ClipItem it)
    {
        try { if (it.ImagePath.Length > 0 && File.Exists(it.ImagePath)) File.Delete(it.ImagePath); } catch { }
        it.ImagePath = "";
    }

    static BitmapSource? LoadPng(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var b = new BitmapImage();
            b.BeginInit(); b.CacheOption = BitmapCacheOption.OnLoad; b.UriSource = new Uri(path); b.EndInit(); b.Freeze();
            return b;
        }
        catch { return null; }
    }
}
