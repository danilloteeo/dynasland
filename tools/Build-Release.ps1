<#
.SYNOPSIS
    Gera os pacotes de uma versão do Dynasland em publish\:
      - DynaslandSetup-<versão>.exe   instalador (Inno Setup, por usuário, sem administrador)
      - Dynasland-<versão>-portable.zip  executável único, sem instalação
.DESCRIPTION
    A versão vem de <Version> em Directory.Build.props. Requer o .NET 10 SDK e o Inno Setup 6
    (winget install JRSoftware.InnoSetup). Feche o Dynasland antes (os arquivos ficam travados).
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\Build-Release.ps1
#>
param(
    [switch]$SkipTests,
    [switch]$SkipPortable
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$publish = Join-Path $root 'publish'
$project = Join-Path $root 'src\Dynasland.App\Dynasland.App.csproj'

[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props') -Encoding UTF8
$version = $props.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw 'Version não encontrada em Directory.Build.props.' }
Write-Host "Dynasland $version" -ForegroundColor Cyan

function Invoke-Step([string]$name, [scriptblock]$action) {
    Write-Host "==> $name" -ForegroundColor Cyan
    & $action
    if ($LASTEXITCODE -ne 0) { throw "$name falhou (código $LASTEXITCODE)." }
}

if (-not $SkipTests) {
    Invoke-Step 'Testes' { dotnet test (Join-Path $root 'Dynasland.sln') -c Release -nologo -v q }
}

# Instalador: pasta self-contained com ReadyToRun (inicia mais rápido, sem extração para %TEMP%).
$appDir = Join-Path $publish 'Dynasland'
if (Test-Path $appDir) { Remove-Item $appDir -Recurse -Force }
Invoke-Step 'Publicação para o instalador' {
    dotnet publish $project -c Release -r win-x64 --self-contained -nologo -v q `
        -p:PublishReadyToRun=true -p:DebugType=none -o $appDir
}

$iscc = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 não encontrado (winget install JRSoftware.InnoSetup).' }

Invoke-Step 'Instalador' {
    & $iscc /Q "/DAppVersion=$version" "/DSourceDir=$appDir" "/DOutputDir=$publish" (Join-Path $root 'installer\Dynasland.iss')
}

if (-not $SkipPortable) {
    $portableDir = Join-Path $publish 'Dynasland-portable'
    if (Test-Path $portableDir) { Remove-Item $portableDir -Recurse -Force }
    Invoke-Step 'Publicação portátil' {
        dotnet publish $project -c Release -r win-x64 --self-contained -nologo -v q `
            -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:EnableCompressionInSingleFile=true -p:DebugType=none -o $portableDir
    }

    $zip = Join-Path $publish "Dynasland-$version-portable.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $portableDir '*') -DestinationPath $zip
}

Write-Host ''
Get-ChildItem $publish -File | Where-Object { $_.Name -like "*$version*" } |
    ForEach-Object { '{0,-40} {1,8:N1} MB' -f $_.Name, ($_.Length / 1MB) }
