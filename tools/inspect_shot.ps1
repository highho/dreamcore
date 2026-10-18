# Inspect a captured frame: crop the game client area, then measure centre vs corner
# luminance. If the URP volume (vignette/grain) is dead, the ratio sits at ~1.0; a real
# vignette pushes corners visibly darker. ASCII only (PowerShell 5.1 codepage trap).
param(
    [Parameter(Mandatory = $true)][string]$Png,
    [int]$X = 0, [int]$Y = 0, [int]$W = 1280, [int]$H = 720,
    [string]$CropOut = "",
    [int]$Step = 4
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$src = [System.Drawing.Image]::FromFile((Resolve-Path $Png).Path)
Write-Host ("source {0}x{1}" -f $src.Width, $src.Height)
$bmp = New-Object System.Drawing.Bitmap $W, $H
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.DrawImage($src, (New-Object System.Drawing.Rectangle 0, 0, $W, $H),
             (New-Object System.Drawing.Rectangle $X, $Y, $W, $H), [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose(); $src.Dispose()

function Lum([int]$px, [int]$py) {
    $c = $bmp.GetPixel($px, $py)
    return (0.2126 * $c.R + 0.7152 * $c.G + 0.0722 * $c.B)
}

$cx = [int]($W / 2); $cy = [int]($H / 2)
$half = [int]([Math]::Min($W, $H) * 0.12)
$center = 0.0; $n = 0
for ($y = $cy - $half; $y -lt $cy + $half; $y += $Step) {
    for ($x = $cx - $half; $x -lt $cx + $half; $x += $Step) { $center += Lum $x $y; $n++ }
}
$center = $center / $n

$cornerTotal = 0.0; $cn = 0
$m = [int]([Math]::Min($W, $H) * 0.06)
foreach ($box in @(@(0, 0), @(($W - $m), 0), @(0, ($H - $m)), @(($W - $m), ($H - $m)))) {
    for ($y = $box[1]; $y -lt $box[1] + $m; $y += $Step) {
        for ($x = $box[0]; $x -lt $box[0] + $m; $x += $Step) { $cornerTotal += Lum $x $y; $cn++ }
    }
}
$corner = $cornerTotal / $cn
Write-Host ("centre L={0:N1}  corner L={1:N1}  corner/centre={2:N3}" -f $center, $corner, ($corner / $center))

# grain proxy: mean |delta| between horizontally adjacent samples in a mid-band strip
$sum = 0.0; $k = 0
for ($y = [int]($H * 0.35); $y -lt [int]($H * 0.65); $y += $Step * 3) {
    $prev = Lum ($cx - $half) $y
    for ($x = $cx - $half + $Step; $x -lt $cx + $half; $x += $Step) {
        $cur = Lum $x $y; $sum += [Math]::Abs($cur - $prev); $prev = $cur; $k++
    }
}
Write-Host ("neighbour |delta| mean={0:N2} over {1} steps" -f ($sum / $k), $k)

if ($CropOut -ne "") {
    $out = [System.IO.Path]::GetFullPath($CropOut)   # avoid Join-Path choking on rooted child paths
    $dir = Split-Path -Parent $out
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $big = New-Object System.Drawing.Bitmap ($W * 2), ($H * 2)
    $g2 = [System.Drawing.Graphics]::FromImage($big)
    $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g2.DrawImage($bmp, 0, 0, $big.Width, $big.Height)
    $g2.Dispose()
    $big.Save($out, [System.Drawing.Imaging.ImageFormat]::Jpeg)
    $big.Dispose()
    Write-Host "crop written: $out"
}
$bmp.Dispose()
