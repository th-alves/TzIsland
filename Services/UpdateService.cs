using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace TzIsland;

/// <summary>
/// Atualizações via Velopack (pacotes gerados por `vpk pack` e publicados como Releases no GitHub).
/// O app baixa o pacote em segundo plano e o aplica ao reiniciar; a troca de arquivos é feita pelo Update.exe
/// do Velopack (não depende mais do Inno Setup). Em modo de desenvolvimento (`dotnet run`) não há instalação.
/// </summary>
public sealed class UpdateService
{
    // Repositório de onde saem os Releases (público; privado funciona com token em TZISLAND_GH_TOKEN)
    const string RepoUrl = "https://github.com/th-alves/TzIsland";
    const string TokenVar = "TZISLAND_GH_TOKEN";

    readonly DispatcherTimer _t = new();
    UpdateManager? _mgr;
    UpdateInfo? _pending;
    bool _busy;

    public static Version Current => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
    public static string CurrentText => $"{Current.Major}.{Current.Minor}.{Math.Max(0, Current.Build)}";

    /// <summary>False em `dotnet run` (sem instalação Velopack).</summary>
    public bool IsInstalled { get; private set; }
    public bool Available { get; private set; }
    public string LatestTag { get; private set; } = "";
    public string PageUrl { get; private set; } = RepoUrl + "/releases";
    public string Notes { get; private set; } = "";
    /// <summary>Texto de estado para a tela de Configurações.</summary>
    public string Status { get; private set; } = "";
    public event Action? Changed;

    public void Start()
    {
        try
        {
            _mgr = new UpdateManager(new GithubSource(RepoUrl, Environment.GetEnvironmentVariable(TokenVar), false));
            IsInstalled = _mgr.IsInstalled;
            if (!IsInstalled) { SetStatus("Modo desenvolvimento (sem instalação Velopack) — atualizações só na versão instalada."); return; }
            if (_mgr.UpdatePendingRestart is { } p) SetStatus($"Atualização {p.Version} baixada: será aplicada ao reiniciar o app.");
        }
        catch (Exception e) { Log.Write("update init: " + e.Message); SetStatus("Velopack não inicializou: " + e.Message); return; }

        _t.Tick += async (_, _) => await CheckAsync(false);
        ApplyInterval();
        // primeira verificação logo depois da abertura (sem competir com a inicialização)
        var first = new DispatcherTimer { Interval = TimeSpan.FromSeconds(25) };
        first.Tick += async (_, _) => { first.Stop(); await CheckAsync(false); };
        first.Start();
    }

    public void ApplyInterval()
    {
        _t.Stop();
        if (!Settings.Current.UpdateCheckEnabled) return;
        _t.Interval = TimeSpan.FromHours(Math.Clamp(Settings.Current.UpdateHours, 1, 72));
        _t.Start();
    }

    public async Task CheckAsync(bool manual)
    {
        if (_busy) return;
        if (!manual && !Settings.Current.UpdateCheckEnabled) return;
        if (_mgr is not { IsInstalled: true } mgr) { if (manual) SetStatus("Modo desenvolvimento (sem instalação Velopack) — nada a atualizar."); return; }

        _busy = true;
        if (manual) SetStatus("Verificando…");
        try
        {
            var info = await mgr.CheckForUpdatesAsync();
            if (info == null) { Available = false; _pending = null; SetStatus($"Você está na versão mais recente ({CurrentText})."); return; }

            var tag = info.TargetFullRelease.Version.ToString();
            Available = true; _pending = info; LatestTag = tag;
            Notes = (info.TargetFullRelease.NotesMarkdown ?? "").Trim();
            if (Notes.Length > 400) Notes = Notes[..400] + "…";
            SetStatus($"Nova versão disponível: {tag} (você está na {CurrentText}).");

            // Baixa já em segundo plano; aplicar é no próximo reinício (ou em "Atualizar agora")
            SetStatus($"Baixando {tag}…");
            await mgr.DownloadUpdatesAsync(info, p => { if (p % 10 == 0) SetStatus($"Baixando {tag}… {p}%"); });
            SetStatus($"Versão {tag} baixada: aplique em \"Atualizar agora\" ou ao fechar o app.");

            if (Settings.Current.UpdateNotifiedTag != tag)
            {
                Settings.Current.UpdateNotifiedTag = tag; Settings.Save();
                Hub.Notify("Atualização", $"Nova versão {tag} disponível", "Toque para atualizar o TzIsland agora", true, bypassDnd: true);
            }
        }
        catch (Exception e) { Log.Write("update: " + e.Message); SetStatus("Não foi possível verificar (sem conexão?)."); }
        finally { _busy = false; }
    }

    void SetStatus(string s) { Status = s; Changed?.Invoke(); }

    /// <summary>Botão "Atualizar agora": encerra o app e deixa o Velopack aplicar e reabrir.</summary>
    public async Task InstallAsync()
    {
        if (_mgr is not { IsInstalled: true }) { OpenPage(); return; }
        if (_pending == null) { await CheckAsync(true); if (_pending == null) return; }

        try
        {
            Hub.Notify("Atualização", $"Aplicando {LatestTag}…", "O TzIsland vai reiniciar sozinho.", false, bypassDnd: true);
            if (Application.Current is App app) app.RestartForUpdate = true;
            Application.Current.Shutdown();
        }
        catch (Exception e)
        {
            Log.Write("update install: " + e.Message);
            Hub.Notify("Atualização", "Falha ao aplicar a atualização", "Abrindo a página do Release.", true, bypassDnd: true);
            OpenPage();
        }
    }

    /// <summary>Chamado no OnExit: a troca de arquivos acontece depois que o app fecha.</summary>
    public void ApplyAndRestart()
    {
        try { _mgr?.ApplyUpdatesAndRestart(_pending?.TargetFullRelease); }
        catch (Exception e) { Log.Write("update apply: " + e.Message); }
    }

    public void OpenPage()
    {
        try { Process.Start(new ProcessStartInfo(PageUrl) { UseShellExecute = true }); } catch (Exception e) { Log.Write(e); }
    }
}
