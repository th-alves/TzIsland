<#
  release.ps1 — publica uma nova versão do TzIsland com Velopack.

  Uso (na pasta do projeto):
      powershell -NoProfile -ExecutionPolicy Bypass -File release.ps1 2.4.1
      powershell -NoProfile -ExecutionPolicy Bypass -File release.ps1 2.4.1 -Notes "corrige o áudio"

    O que faz:
    1. confere a versão e atualiza <Version> no TzIsland.csproj
    2. dotnet publish -c Release -r win-x64 -o publish
    3. tools\vpk.exe pack -> releases\ (instalador, pacotes e feed)
    4. vpk upload github -> publica os arquivos do último empacotamento

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


if (-not (Test-Path "TzIsland.csproj")) {
    Fail "TzIsland.csproj nao encontrado - rode o script na pasta do projeto."
}

if (-not (Test-Path "tools\vpk.exe")) {
    Fail "tools\vpk.exe não encontrado. Instale com: dotnet tool install vpk --tool-path tools"
}

# ---------------------------------------------------------------- 1. versão no csproj
Step "Atualizando a versão para $Version em TzIsland.csproj"

$csprojPath = Join-Path $PSScriptRoot "TzIsland.csproj"
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$csproj = [System.IO.File]::ReadAllText($csprojPath, [System.Text.Encoding]::UTF8)

if ($csproj -match '<Version>([^<]+)</Version>') {
    $atual = $Matches[1]

    if ($atual -eq $Version) {
        Write-Host "já estava em $Version"
    }
    else {
        $csproj = $csproj -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"
        [System.IO.File]::WriteAllText($csprojPath, $csproj, $utf8NoBom)
        Write-Host "versão anterior: $atual -> $Version"
    }
}
else {
    $csproj = $csproj -replace ('(<TargetFramework>[^<]*</TargetFramework>)'), ('$1' + "`n    " + '<Version>' + $Version + '</Version>')
    [System.IO.File]::WriteAllText($csprojPath, $csproj, $utf8NoBom)
    Write-Host 'tag Version criada'
}

# ---------------------------------------------------------------- 2. publish
if (-not $NoPublish) {
    Step "dotnet publish -c Release -r win-x64 -o publish"
    & dotnet publish -c Release -r win-x64 -o publish

    if ($LASTEXITCODE -ne 0) {
        Fail "falha no dotnet publish"
    }
}
else {
    Write-Host "publish pulado (-NoPublish)" -ForegroundColor Yellow
}

if (-not (Test-Path "publish\TzIsland.exe")) {
    Fail "publish/TzIsland.exe nao existe - rode sem -NoPublish."
}


# ---------------------------------------------------------------- 3. vpk pack
Step "Empacotando com Velopack ($Version, canal $Channel)"
if (-not (Test-Path "releases")) { New-Item -ItemType Directory -Path "releases" | Out-Null }

& "tools\vpk.exe" pack `
    -u TzIsland -v $Version -p publish -e TzIsland.exe `
    --runtime win-x64 `
    --packTitle TzIsland --packAuthors Thigas -i Assets\TzIsland.ico `
    -c $Channel -o releases -y
if ($LASTEXITCODE -ne 0) { Fail "falha no vpk pack" }

$manifest = "releases\assets.$Channel.json"

if (-not (Test-Path $manifest)) {
    Fail "manifesto $manifest não encontrado"
}

$assetsPack = @(Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json)

if ($assetsPack.Count -eq 0) {
    Fail "nenhum arquivo encontrado no manifesto"
}

$arquivos = @($assetsPack | ForEach-Object { $_.RelativeFileName })

Write-Host ("gerados neste pack: " + ($arquivos -join ", "))

if ($NoUpload) { Write-Host "`nEmpacotado. Upload pulado (-NoUpload)." -ForegroundColor Yellow; exit 0 }

# ---------------------------------------------------------------- 4. GitHub Release
# ---------------------------------------------------------------- 4. GitHub Release
Step "Publicando o Release $tag em $Repo"

$token = if ($env:GH_TOKEN) {
    $env:GH_TOKEN
} elseif ($env:GITHUB_TOKEN) {
    $env:GITHUB_TOKEN
} else {
    ""
}

if (-not $token) {
    Fail "defina GH_TOKEN (ou GITHUB_TOKEN) com permissão de escrita em Contents."
}

& "tools\vpk.exe" upload github `
    --outputDir "releases" `
    --channel $Channel `
    --repoUrl "https://github.com/$Repo" `
    --token $token `
    --publish `
    --merge `
    --releaseName "TzIsland $Version" `
    --tag $tag `
    --targetCommitish "main"

if ($LASTEXITCODE -ne 0) {
    Fail "falha ao publicar o Release pelo Velopack"
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
