param(
  [string]$Src = "G:\AIgame\jam\logs\shots11b\01_start.png",
  [string]$Out = "G:\AIgame\jam\docs\images\cover.png",
  [int]$X = 0, [int]$Y = 34, [int]$W = 1278, [int]$H = 786,
  [int]$OutW = 1600, [int]$OutH = 900,
  [switch]$Title
)
Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::new($Src)
if ($X + $W -gt $bmp.Width -or $Y + $H -gt $bmp.Height) { throw "crop ($X,$Y,$W,$H) out of bounds $($bmp.Width)x$($bmp.Height)" }
$crop = [System.Drawing.Rectangle]::new($X, $Y, $W, $H)
$canvas = [System.Drawing.Bitmap]::new($OutW, $OutH, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$g = [System.Drawing.Graphics]::FromImage($canvas)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$scale = [Math]::Max($OutW / $W, $OutH / $H)
$sw = $W * $scale; $sh = $H * $scale
$dest = [System.Drawing.Rectangle]::new([int](($OutW - $sw) / 2), [int](($OutH - $sh) / 2), [int]$sw, [int]$sh)
$g.DrawImage($bmp, $dest, $crop, [System.Drawing.GraphicsUnit]::Pixel)
if ($Title) {
  $shade = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
    [System.Drawing.RectangleF]::new(0, $OutH * 0.45, $OutW, $OutH * 0.55),
    [System.Drawing.Color]::FromArgb(0, 0, 0, 0), [System.Drawing.Color]::FromArgb(215, 4, 4, 8),
    [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $g.FillRectangle($shade, 0, [int]($OutH * 0.45), $OutW, [int]($OutH * 0.55))
  $shade.Dispose()
  $fBig = [System.Drawing.Font]::new("Microsoft YaHei UI", 92, [System.Drawing.FontStyle]::Bold)
  $fSmall = [System.Drawing.Font]::new("Microsoft YaHei UI", 22, [System.Drawing.FontStyle]::Regular)
  $sf = [System.Drawing.StringFormat]::new(); $sf.Alignment = [System.Drawing.StringAlignment]::Center
  $rectB = [System.Drawing.RectangleF]::new(0, ($OutH - 250), $OutW, 170)
  $rectS = [System.Drawing.RectangleF]::new(0, ($OutH - 78), $OutW, 46)
  $g.DrawString("昨日的明天", $fBig, [System.Drawing.Brushes]::White, $rectB, $sf)
  $sub = "8 小时单人作品  ·  Unity 6 + Blender 程序化工件线  ·  中式梦核找异常"
  $g.DrawString($sub, $fSmall, [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(225, 205, 210, 220)), $rectS, $sf)
  $fBig.Dispose(); $fSmall.Dispose(); $sf.Dispose()
}
$g.Dispose(); $bmp.Dispose()
New-Item -ItemType Directory -Force -Path (Split-Path $Out) | Out-Null
$canvas.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$canvas.Dispose()
Write-Output "SAVED $Out ${OutW}x${OutH} from crop ($X,$Y,$W,$H)"
