using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Media.SpeechRecognition;
using Windows.Storage.Streams;

namespace TzIsland;

/// <summary>
/// Mirror: a câmera só é aberta quando o usuário liga o espelho e é liberada ao desligar/sair da tela.
/// Nada é gravado, salvo em disco ou transmitido — os quadros existem apenas na memória para exibição.
/// </summary>
public sealed class CameraService
{
    MediaCapture? _cap;
    MediaFrameReader? _reader;
    bool _pending;
    public bool IsOn { get; private set; }
    public WriteableBitmap? Frame { get; private set; }
    public event Action? FrameUpdated;
    public event Action? StateChanged;

    public async Task<List<(string Id, string Name)>> ListAsync()
    {
        try
        {
            var d = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            return d.Select(x => (x.Id, x.Name)).ToList();
        }
        catch (Exception e) { Log.Write(e); return new(); }
    }

    /// <returns>null se abriu; senão a mensagem de erro para exibir.</returns>
    public async Task<string?> StartAsync(string? deviceId)
    {
        if (IsOn) return null;
        try
        {
            _cap = new MediaCapture();
            var cfg = new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Video,
                SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu
            };
            if (!string.IsNullOrEmpty(deviceId)) cfg.VideoDeviceId = deviceId;
            await _cap.InitializeAsync(cfg);
            var src = _cap.FrameSources.Values.FirstOrDefault(s => s.Info.SourceKind == MediaFrameSourceKind.Color)
                      ?? throw new InvalidOperationException("A câmera não oferece vídeo em cores.");
            _reader = await _cap.CreateFrameReaderAsync(src, MediaEncodingSubtypes.Bgra8);
            _reader.FrameArrived += OnFrame;
            var st = await _reader.StartAsync();
            if (st != MediaFrameReaderStartStatus.Success) throw new InvalidOperationException("Não foi possível iniciar a câmera (" + st + ").");
            IsOn = true;
            StateChanged?.Invoke();
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            await StopAsync();
            return "Acesso negado. Habilite a câmera para apps de desktop em Configurações do Windows > Privacidade > Câmera.";
        }
        catch (Exception e)
        {
            Log.Write(e);
            await StopAsync();
            return e.Message;
        }
    }

    void OnFrame(MediaFrameReader r, MediaFrameArrivedEventArgs a)
    {
        if (_pending) return;
        using var f = r.TryAcquireLatestFrame();
        var sb = f?.VideoMediaFrame?.SoftwareBitmap;
        if (sb == null) return;
        SoftwareBitmap? conv = null;
        try
        {
            var bmp = sb;
            if (sb.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || sb.BitmapAlphaMode == BitmapAlphaMode.Straight)
            {
                conv = SoftwareBitmap.Convert(sb, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                bmp = conv;
            }
            int w = bmp.PixelWidth, h = bmp.PixelHeight;
            var buf = new Windows.Storage.Streams.Buffer((uint)(w * h * 4));
            bmp.CopyToBuffer(buf);
            var bytes = new byte[buf.Length];
            using (var dr = DataReader.FromBuffer(buf)) dr.ReadBytes(bytes);
            _pending = true;
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (!IsOn) return;
                    if (Frame == null || Frame.PixelWidth != w || Frame.PixelHeight != h)
                        Frame = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
                    Frame.WritePixels(new Int32Rect(0, 0, w, h), bytes, w * 4, 0);
                    FrameUpdated?.Invoke();
                }
                finally { _pending = false; }
            });
        }
        catch (Exception e) { Log.Write(e); }
        finally { conv?.Dispose(); }
    }

    public async Task StopAsync()
    {
        try
        {
            if (_reader != null) { _reader.FrameArrived -= OnFrame; await _reader.StopAsync(); _reader.Dispose(); }
            _cap?.Dispose();
        }
        catch (Exception e) { Log.Write(e); }
        _reader = null; _cap = null; IsOn = false; Frame = null;
        StateChanged?.Invoke();
    }
}

/// <summary>Ditado por voz com o reconhecimento de fala do Windows (idiomas conforme instalados no sistema).</summary>
public sealed class DictationService
{
    SpeechRecognizer? _rec;
    bool _want;
    public bool Listening { get; private set; }
    public string Text { get; private set; } = "";
    public string Hypothesis { get; private set; } = "";
    public string? Error { get; private set; }
    public event Action? Changed;
    static Dispatcher Ui => Application.Current.Dispatcher;

    public async Task StartAsync()
    {
        if (Listening) return;
        _want = true; Error = null;
        try
        {
            _rec = new SpeechRecognizer(new Windows.Globalization.Language(Settings.Current.DictationLanguage));
            _rec.Constraints.Add(new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "dictation"));
            var comp = await _rec.CompileConstraintsAsync();
            if (comp.Status != SpeechRecognitionResultStatus.Success)
                throw new InvalidOperationException("Falha ao preparar o reconhecimento: " + comp.Status);
            _rec.HypothesisGenerated += (s, a) => Ui.InvokeAsync(() => { Hypothesis = a.Hypothesis.Text; Changed?.Invoke(); });
            _rec.ContinuousRecognitionSession.ResultGenerated += (s, a) => Ui.InvokeAsync(() =>
            {
                if (a.Result.Status == SpeechRecognitionResultStatus.Success && !string.IsNullOrWhiteSpace(a.Result.Text))
                    Text += (Text.Length > 0 ? " " : "") + a.Result.Text;
                Hypothesis = ""; Changed?.Invoke();
            });
            _rec.ContinuousRecognitionSession.Completed += (s, a) => Ui.InvokeAsync(async () =>
            {
                Listening = false; Changed?.Invoke();
                if (_want && a.Status == SpeechRecognitionResultStatus.TimeoutExceeded) await StartAsync();
            });
            await _rec.ContinuousRecognitionSession.StartAsync();
            Listening = true;
        }
        catch (Exception e)
        {
            Log.Write(e);
            _want = false; Listening = false;
            Error = (uint)e.HResult == 0x80045509
                ? "Ative o reconhecimento de voz online em Configurações do Windows > Privacidade > Fala."
                : e.Message;
        }
        Changed?.Invoke();
    }

    public async Task StopAsync()
    {
        _want = false;
        try { if (_rec != null && Listening) await _rec.ContinuousRecognitionSession.StopAsync(); _rec?.Dispose(); }
        catch (Exception e) { Log.Write(e); }
        _rec = null; Listening = false; Hypothesis = "";
        Changed?.Invoke();
    }

    public async Task ToggleAsync() { if (Listening) await StopAsync(); else await StartAsync(); }
    public void Clear() { Text = ""; Hypothesis = ""; Changed?.Invoke(); }

    public void PasteToActiveWindow()
    {
        if (string.IsNullOrWhiteSpace(Text)) return;
        try
        {
            Clipboard.SetText(Text);
            Native.keybd_event(0x11, 0, 0, UIntPtr.Zero);       // Ctrl
            Native.keybd_event(0x56, 0, 0, UIntPtr.Zero);       // V
            Native.keybd_event(0x56, 0, 2, UIntPtr.Zero);
            Native.keybd_event(0x11, 0, 2, UIntPtr.Zero);
        }
        catch (Exception e) { Log.Write(e); }
    }
}
