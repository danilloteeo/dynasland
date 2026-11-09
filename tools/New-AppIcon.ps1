<#
.SYNOPSIS
    Gera src/Dynasland.App/Assets/dynasland.ico (16 a 256 px, PNG embutido, com alfa).
    Desenho: cápsula preta com contorno claro e um ponto de atividade verde.
#>
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\src\Dynasland.App\Assets\dynasland.ico')
)

Add-Type -AssemblyName System.Drawing

function New-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $stroke = [Math]::Max(1.0, $size / 16.0)
    $w = $size - $stroke * 2 - 1
    $h = [Math]::Round($size * 0.5)
    $x = $stroke + 0.5
    $y = ($size - $h) / 2.0
    $r = $h

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($x, $y, $r, $h, 90, 180)
    $path.AddArc($x + $w - $r, $y, $r, $h, 270, 180)
    $path.CloseFigure()

    $fill = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 10, 10, 10))
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 235, 235, 235)), $stroke
    $g.FillPath($fill, $path)
    $g.DrawPath($pen, $path)

    $dot = $h * 0.36
    $dotBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 52, 199, 89))
    $g.FillEllipse($dotBrush, $x + $w - $r / 2 - $dot / 2 - $stroke, $y + ($h - $dot) / 2, $dot, $dot)

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$images = foreach ($s in $sizes) { , (New-IconPng $s) }

$out = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $out
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)

$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $writer.Write([byte]$dim)
    $writer.Write([byte]$dim)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$images[$i].Length)
    $writer.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $writer.Write($img) }
$writer.Flush()

$fullPath = [System.IO.Path]::GetFullPath($OutputPath)
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($fullPath)) | Out-Null
[System.IO.File]::WriteAllBytes($fullPath, $out.ToArray())
Write-Output "Icone gerado: $fullPath"
