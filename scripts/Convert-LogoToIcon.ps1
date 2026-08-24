# Convert-LogoToIcon.ps1
#
# Generates a multi-resolution Windows icon (.ico) from a source PNG.
# Windows uses the .ico (not .png) for EXE icons and window icons, and a
# multi-size .ico keeps the icon crisp at 16px (title bar) up to 256px (Explorer).
#
# Usage (from repo root):
#   powershell -ExecutionPolicy Bypass -File scripts\Convert-LogoToIcon.ps1
param(
    [string]$Source = "favicon.png",
    [string]$Output = "assets\DeepFry.ico",
    [int[]]$Sizes = @(16, 24, 32, 48, 64, 128, 256)
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$srcFull = Join-Path $root $Source
$outFull = Join-Path $root $Output

if (-not (Test-Path $srcFull)) {
    throw "Source logo not found: $srcFull"
}

$outDir = Split-Path -Parent $outFull
if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Force $outDir | Out-Null
}

$srcBmp = [System.Drawing.Bitmap]::FromFile($srcFull)

# Render each size to a PNG stream (Vista+ .ico uses PNG-compressed entries).
$pngs = @()
foreach ($size in $Sizes) {
    $bmp = New-Object -TypeName System.Drawing.Bitmap -ArgumentList $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($srcBmp, 0, 0, $size, $size)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , $ms.ToArray()
    $bmp.Dispose()
    $ms.Dispose()
}
$srcBmp.Dispose()

# Assemble the .ico container: ICONDIR header + ICONDIRENTRY table + PNG blobs.
$fs = [System.IO.File]::Open($outFull, [System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0)               # reserved
$bw.Write([UInt16]1)               # type: 1 = icon
$bw.Write([UInt16]$Sizes.Count)    # number of images
$offset = 6 + 16 * $Sizes.Count
for ($i = 0; $i -lt $Sizes.Count; $i++) {
    $s = $Sizes[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }   # 0 means 256
    $bw.Write([Byte]$dim)
    $bw.Write([Byte]$dim)
    $bw.Write([Byte]0)              # palette count
    $bw.Write([Byte]0)              # reserved
    $bw.Write([UInt16]1)            # color planes
    $bw.Write([UInt16]32)           # bits per pixel
    $bw.Write([UInt32]$pngs[$i].Length)
    $bw.Write([UInt32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($png in $pngs) {
    $bw.Write($png)
}
$bw.Flush()
$bw.Close()
$fs.Close()

Write-Host "Generated $outFull ($($Sizes.Count) sizes) from $Source"
