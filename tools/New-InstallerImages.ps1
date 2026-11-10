<#
.SYNOPSIS
    Gera as imagens do instalador em installer\assets\ (as mesmas do repositório):
      - wizard.png / wizard@2x.png   lateral das telas de boas-vindas e conclusão (164x314 e 328x628)
      - small.png / small@2x.png     canto do cabeçalho das demais telas (55x55 e 110x110)
    Desenho: fundo preto com brilho verde, a cápsula da ilha com o ponto de atividade e o nome.
#>
param(
    [string]$OutputDir = (Join-Path $PSScriptRoot '..\installer\assets')
)

Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $OutputDir | Out-Null

$black = [System.Drawing.Color]::FromArgb(255, 10, 10, 10)
$green = [System.Drawing.Color]::FromArgb(255, 48, 209, 88)

function New-Canvas([int]$width, [int]$height) {
    $bmp = New-Object System.Drawing.Bitmap $width, $height, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear($black)
    return $bmp, $g
}

# Brilho radial: verde translúcido no centro, sumindo para o preto.
function Add-Glow($g, [single]$cx, [single]$cy, [single]$radius, [int]$alpha) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddEllipse($cx - $radius, $cy - $radius, $radius * 2, $radius * 2)
    $brush = New-Object System.Drawing.Drawing2D.PathGradientBrush $path
    $brush.CenterColor = [System.Drawing.Color]::FromArgb($alpha, $green)
    $brush.SurroundColors = @([System.Drawing.Color]::FromArgb(0, $green))
    $g.FillPath($brush, $path)
    $brush.Dispose(); $path.Dispose()
}

# A cápsula da ilha: preta, com contorno claro e o ponto verde à direita.
function Add-Island($g, [single]$x, [single]$y, [single]$w, [single]$h, [single]$stroke) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($x, $y, $h, $h, 90, 180)
    $path.AddArc($x + $w - $h, $y, $h, $h, 270, 180)
    $path.CloseFigure()
    $fill = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0, 0, 0))
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 70, 70, 74)), $stroke
    $g.FillPath($fill, $path)
    $g.DrawPath($pen, $path)
    $dot = $h * 0.34
    $dotBrush = New-Object System.Drawing.SolidBrush $green
    $g.FillEllipse($dotBrush, $x + $w - $h / 2 - $dot / 2, $y + ($h - $dot) / 2, $dot, $dot)
    $fill.Dispose(); $pen.Dispose(); $dotBrush.Dispose(); $path.Dispose()
}

function Save-Png($bmp, $g, [string]$name) {
    $g.Dispose()
    $path = Join-Path $OutputDir $name
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Gerado: $path"
}

function New-WizardImage([int]$scale, [string]$name) {
    $w = 164 * $scale; $h = 314 * $scale
    $bmp, $g = New-Canvas $w $h
    Add-Glow $g ($w * 0.15) ($h * 0.95) ($w * 1.25) 120
    Add-Glow $g ($w * 0.95) ($h * 0.10) ($w * 0.70) 45
    Add-Island $g ($w * 0.17) ($h * 0.30) ($w * 0.66) ($w * 0.20) ([single](1.2 * $scale))

    $font = New-Object System.Drawing.Font 'Segoe UI Semibold', ([single](15 * $scale)), ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $small = New-Object System.Drawing.Font 'Segoe UI', ([single](9.5 * $scale)), ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $center = New-Object System.Drawing.StringFormat
    $center.Alignment = [System.Drawing.StringAlignment]::Center
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 245, 245, 247))
    $muted = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 150, 150, 156))
    $g.DrawString('Dynasland', $font, $white, (New-Object System.Drawing.RectangleF 0, ($h * 0.47), $w, ($h * 0.1)), $center)
    $g.DrawString('Ilha dinâmica para Windows', $small, $muted, (New-Object System.Drawing.RectangleF 0, ($h * 0.545), $w, ($h * 0.1)), $center)
    $font.Dispose(); $small.Dispose(); $white.Dispose(); $muted.Dispose()
    Save-Png $bmp $g $name
}

function New-SmallImage([int]$scale, [string]$name) {
    $s = 55 * $scale
    $bmp, $g = New-Canvas $s $s
    Add-Glow $g ($s * 0.2) ($s * 0.95) ($s * 0.95) 110
    Add-Island $g ($s * 0.12) ($s * 0.36) ($s * 0.76) ($s * 0.28) ([single](1.0 * $scale))
    Save-Png $bmp $g $name
}

New-WizardImage 1 'wizard.png'
New-WizardImage 2 'wizard@2x.png'
New-SmallImage 1 'small.png'
New-SmallImage 2 'small@2x.png'
