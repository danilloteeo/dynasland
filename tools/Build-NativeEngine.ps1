<#
.SYNOPSIS
    Compila o motor nativo (src/NativeEngine, C++20) para build\bin\<Configuration>\DynaIslandEngine.dll e roda os testes.
.DESCRIPTION
    Requer o Visual Studio Build Tools 2022 com a carga "Desenvolvimento para desktop com C++"
    (winget install Microsoft.VisualStudio.2022.BuildTools --override "--add Microsoft.VisualStudio.Workload.VCTools --includeRecommended").
    O CMake e o MSVC vem nessa carga; nao e preciso abrir um Prompt do Desenvolvedor.
#>
param(
    [ValidateSet('Release', 'Debug')][string]$Configuration = 'Release',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Join-Path $root 'src\NativeEngine'
$build = Join-Path $source 'build'

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) { throw 'vswhere nao encontrado: instale o Visual Studio Build Tools 2022 (carga C++).' }
$vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) { throw 'Nenhuma instalacao do Visual Studio com as ferramentas C++ (x64) foi encontrada.' }

$cmake = Join-Path $vs 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
$ctest = Join-Path $vs 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\ctest.exe'
if (-not (Test-Path $cmake)) { throw "CMake nao encontrado em $vs (componente 'C++ CMake tools for Windows')." }

function Invoke-Step([string]$Name, [scriptblock]$Action) {
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "$Name falhou (codigo $LASTEXITCODE)." }
}

Invoke-Step 'Configurar (CMake)' { & $cmake -S $source -B $build -G 'Visual Studio 17 2022' -A x64 }
Invoke-Step "Compilar ($Configuration)" { & $cmake --build $build --config $Configuration --parallel }
if (-not $SkipTests) {
    Invoke-Step 'Testes nativos' { & $ctest --test-dir $build -C $Configuration --output-on-failure }
}

$dll = Join-Path $build "bin\$Configuration\DynaIslandEngine.dll"
if (-not (Test-Path $dll)) { throw "DLL nao gerada: $dll" }
Write-Host "DLL: $dll" -ForegroundColor Green
