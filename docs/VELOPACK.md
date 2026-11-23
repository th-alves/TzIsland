# Velopack — empacotar, instalar e atualizar o TzIsland

O app usa o **Velopack** para instalar e se atualizar (substitui o Inno Setup).

## O que já está no código
- `App.xaml.cs`: `VelopackApp.Build().SetAutoApplyOnStartup(true).Run()` é a **primeira** linha do `OnStartup`.
- `Services/UpdateService.cs`: usa `UpdateManager` + `GithubSource` (`https://github.com/th-alves/TzIsland`).
  - verifica a cada N horas (Configurações > Atualizações), baixa o pacote em segundo plano e avisa na ilha;
  - "Atualizar agora" encerra o app e o `Update.exe` do Velopack aplica e reabre;
  - `SetAutoApplyOnStartup(true)` aplica automaticamente o pacote baixado na próxima abertura.
- Em `dotnet run` (sem instalação Velopack) as atualizações ficam desativadas com o motivo na tela.

## 1. Ferramenta
    dotnet tool install vpk --tool-path tools      # uma vez; fica em tools\vpk.exe

## 2. Gerar um release
A versão vem do `TzIsland.csproj` (`<Version>`). Publique e empacote:

    dotnet publish -c Release -o publish
    tools\vpk.exe pack -u TzIsland -v 2.4.0 -p publish -e TzIsland.exe -c win -o releases

Saída em `releases\`: `TzIsland-win-Setup.exe` (instalador), `TzIsland-2.4.0-full.nupkg`,
`RELEASES-win` e `releases.win.json` (o feed que o app lê).

### Atalho: `release.cmd` / `release.ps1`
Faz os passos 2, 3 e 4 de uma vez (atualiza a versão no `.csproj`, publica, empacota e envia ao GitHub).
No cmd, dentro da pasta do projeto:

    release 2.4.1
    release 2.4.1 -Notes "corrige o áudio"

(o `release.cmd` só chama o `release.ps1`, que também pode ser executado direto)

    powershell -NoProfile -ExecutionPolicy Bypass -File release.ps1 2.4.1 -Notes "corrige o áudio"

Opções: `-NoPublish` (reaproveita `publish\`), `-NoUpload` (só empacota).
Precisa de `GH_TOKEN` (ou `GITHUB_TOKEN`) no ambiente, com permissão *Contents: read and write*.

## 3. Instalar
Rode `releases\TzIsland-win-Setup.exe`. Ele instala em `%LocalAppData%\TzIsland` (sem pedir admin),
cria atalhos e registra a desinstalação. **Não use mais o `installer.iss`.**

## 4. Publicar a atualização
Envie para o Release do GitHub (tag `vX.Y.Z`) **todos** os arquivos de `releases\`:

    gh release create v2.4.1 releases\* --title "TzIsland 2.4.1" --notes "..."

Próxima versão = aumentar `<Version>` no `.csproj`, repetir o passo 2 e publicar o Release novo.
Repositório privado: defina `TZISLAND_GH_TOKEN` (token com leitura de Releases) antes de abrir o app.

## 5. Testar o fluxo de atualização
Instale a 2.4.0 pelo Setup, publique um Release 2.4.1 e use Configurações > Atualizações > "Verificar agora"
(ou espere o intervalo). O app baixa, avisa na ilha e aplica em "Atualizar agora" ou ao reabrir.

## Desinstalar
Configurações do Windows > Aplicativos > TzIsland (o Velopack remove a instalação; `%AppData%\TzIsland`
com suas configurações permanece — apague à mão se quiser).
