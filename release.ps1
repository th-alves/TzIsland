<#
  release.ps1 — publica uma nova versão do TzIsland com Velopack.

  Uso (na pasta do projeto):
      powershell -NoProfile -ExecutionPolicy Bypass -File release.ps1 2.4.1
      powershell -NoProfile -ExecutionPolicy Bypass -File release.ps1 2.4.1 -Notes "corrige o áudio"

  O que faz:
    1. confere a versão e atualiza <Version> no TzIsland.csproj
    2. dotnet publish -c Release -o publish
    3. tools\vpk.exe pack -> releases\  (instalador, pacote, feed)
    4. cria (ou reaproveita) o Release vX.Y.Z no GitHub e anexa todos os arquivos de releases\

  Requisitos: .NET SDK, tools\vpk.exe (dotnet tool install vpk --tool-path tools)
  e autenticação no GitHub: variável de ambiente GH_TOKEN ou GITHUB_TOKEN
  (token com permissão "Contents: read and write" no repositório).
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Version,

    [string]$Notes = "",

    [string]$Repo = "th-alves/TzIsland",

    [string]$Channel = "win",

    # Pula o dotnet publish (reaproveita a pasta publish\ já gerada)
    [switch]$NoPublish,

    # Só empacota; não envia nada ao GitHub
    [switch]$NoUpload
)

$ErrorActionPreference = "Stop"
Set-Location -LiteralPath $PSScriptRoot

function Fail($msg) { Write-Host "ERRO: $msg" -ForegroundColor Red; exit 1 }
function Step($msg) { Write-Host "`n== $msg" -ForegroundColor Cyan }

# ---------------------------------------------------------------- validação
if ($Version -notmatch '^\d+\.\d+\.\d+$') { Fail "versão inválida '$Version' (use algo como 2.4.1)" }
$tag = "v$Version"

if (-not (Test-Path "TzIsland.csproj")) { Fail "TzIsland.csproj não encontrado — rode o script na pasta do projeto." }
if (-not (Test-Path "tools\vpk.exe")) { Fail "tools\vpk.exe não encontrado. Instale com: dotnet tool install vpk --tool-path tools" }

# ---------------------------------------------------------------- 1. versão no csproj
Step "Atualizando a versão para $Version em TzIsland.csproj"
$csproj = Get-Content -LiteralPath "TzIsland.csproj" -Raw
if ($csproj -match '<Version>([^<]+)</Version>') {
    $atual = $Matches[1]
    if ($atual -eq $Version) { Write-Host "já estava em $Version" }
    else {
        $csproj = $csproj -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"
        Set-Content -LiteralPath "TzIsland.csproj" -Value $csproj -NoNewline -Encoding UTF8
        Write-Host "versão anterior: $atual -> $Version"
    }
} else {
    $csproj = $csproj -replace ('(<TargetFramework>[^<]*</TargetFramework>)'), ('$1' + "`n    " + '<Version>' + $Version + '</Version>')
    Set-Content -LiteralPath "TzIsland.csproj" -Value $csproj -NoNewline -Encoding UTF8
    Write-Host 'tag Version criada'
}

# ---------------------------------------------------------------- 2. publish
if (-not $NoPublish) {
    Step "dotnet publish -c Release -o publish"
    & dotnet publish -c Release -o publish
    if ($LASTEXITCODE -ne 0) { Fail "falha no dotnet publish" }
} else { Write-Host "publish pulado (-NoPublish)" -ForegroundColor Yellow }

if (-not (Test-Path "publish\TzIsland.exe")) { Fail "publish/TzIsland.exe não existe — rode sem -NoPublish." }

# ---------------------------------------------------------------- 3. vpk pack
Step "Empacotando com Velopack ($Version, canal $Channel)"
if (-not (Test-Path "releases")) { New-Item -ItemType Directory -Path "releases" | Out-Null }

& "tools\vpk.exe" pack `
    -u TzIsland -v $Version -p publish -e TzIsland.exe `
    --packTitle TzIsland --packAuthors Thigas `
    -c $Channel -o releases -y
if ($LASTEXITCODE -ne 0) { Fail "falha no vpk pack" }

$arquivos = Get-ChildItem -LiteralPath "releases" -File |
    Where-Object { $_.Name -match '\.(nupkg|exe|zip|json)$' -or $_.Name -eq 'RELEASES' }
if ($arquivos.Count -eq 0) { Fail "nenhum arquivo gerado em releases" }
Write-Host ("gerados: " + ($arquivos.Name -join ", "))

if ($NoUpload) { Write-Host "`nEmpacotado. Upload pulado (-NoUpload)." -ForegroundColor Yellow; exit 0 }

# ---------------------------------------------------------------- 4. GitHub Release
Step "Publicando o Release $tag em $Repo"
$token = if ($env:GH_TOKEN) { $env:GH_TOKEN } elseif ($env:GITHUB_TOKEN) { $env:GITHUB_TOKEN } else { "" }
if (-not $token) { Fail "defina GH_TOKEN (ou GITHUB_TOKEN) com permissão de escrita em Contents." }

$headers = @{
    Authorization          = "Bearer $token"
    Accept                 = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
    "User-Agent"           = "TzIsland-release"
}
$api = "https://api.github.com/repos/$Repo"

# Release já existe?
$releaseId = $null
try {
    $r = Invoke-RestMethod -Uri "$api/releases/tags/$tag" -Headers $headers -Method Get
    $releaseId = $r.id
    Write-Host ("Release " + $tag + " ja existe (id " + $releaseId + ") - os arquivos serao atualizados")
} catch {
    if ($_.Exception.Response.StatusCode.value__ -ne 404) { Fail "falha ao consultar o Release: $($_.Exception.Message)" }
}

if (-not $releaseId) {
    $body = @{
        tag_name         = $tag
        target_commitish = "main"
        name             = "TzIsland $Version"
        body             = if ($Notes) { $Notes } else { "TzIsland $Version" }
        draft            = $false
        prerelease       = $false
    } | ConvertTo-Json
    try {
        $r = Invoke-RestMethod -Uri "$api/releases" -Headers $headers -Method Post -Body $body -ContentType "application/json"
        $releaseId = $r.id
        Write-Host "Release $tag criado (id $releaseId)"
    } catch { Fail "não foi possível criar o Release: $($_.Exception.Message)" }
}

# Envia/atualiza cada arquivo
foreach ($f in $arquivos) {
    $mb = [string][math]::Round($f.Length / 1MB, 1)
    Write-Host ("  enviando " + $f.Name + " - " + $mb + " MB")
    $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
    try {
        Invoke-RestMethod -Uri ("https://uploads.github.com/repos/" + $Repo + "/releases/" + $releaseId + "/assets?name=" + $f.Name) -Headers $headers -Method Post -Body $bytes -ContentType "application/octet-stream" | Out-Null
    } catch {
        # 422 = já existe um asset com esse nome: remove e reenvia
        if ($_.Exception.Response.StatusCode.value__ -eq 422) {
            $existentes = Invoke-RestMethod -Uri "$api/releases/$releaseId/assets" -Headers $headers -Method Get
            $velho = $existentes | Where-Object { $_.name -eq $f.Name }
            if ($velho) {
                Invoke-RestMethod -Uri "$api/releases/assets/$($velho.id)" -Headers $headers -Method Delete | Out-Null
                Invoke-RestMethod -Uri ("https://uploads.github.com/repos/" + $Repo + "/releases/" + $releaseId + "/assets?name=" + $f.Name) -Headers $headers -Method Post -Body $bytes -ContentType "application/octet-stream" | Out-Null
            } else { Fail "falha ao enviar $($f.Name): $($_.Exception.Message)" }
        } else { Fail "falha ao enviar $($f.Name): $($_.Exception.Message)" }
    }
}

# ---------------------------------------------------------------- 5. conferência
Step "Conferindo o feed publico"
$feed = "https://github.com/$Repo/releases/latest/download/releases.$Channel.json"
try {
    Invoke-WebRequest -Uri $feed -Method Head -MaximumRedirection 5 -UseBasicParsing | Out-Null
    Write-Host "feed OK" -ForegroundColor Green
}
catch { Write-Host "aviso: feed sem resposta por enquanto" -ForegroundColor Yellow }

Write-Host ("Publicado: https://github.com/" + $Repo + "/releases/tag/" + $tag)
