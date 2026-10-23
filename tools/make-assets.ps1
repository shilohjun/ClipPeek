#requires -version 5
<#
    生成 README / 安装包用的素材：
      assets\clippeek.ico   （16/24/32/48/64/128/256，PNG 压缩帧）
      assets\logo.png       （512×512，透明）
      assets\banner.png     （1280×360，README 顶部横幅）

    图标不是手画的：直接调用 ClipPeek.exe 里 Theme.BuildBitmap 渲染，
    保证素材和程序里画出来的图标完全一致。

    用法: powershell -ExecutionPolicy Bypass -File tools\make-assets.ps1
#>

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Definition)
$exe = Join-Path $root 'dist\ClipPeek.exe'
$assets = Join-Path $root 'assets'
if (-not (Test-Path $exe)) { throw "找不到 $exe，先跑 build.ps1" }
if (-not (Test-Path $assets)) { New-Item -ItemType Directory -Path $assets | Out-Null }

# --- 借用程序自己的绘制代码 ---
$assemblyBytes = [System.IO.File]::ReadAllBytes($exe)
$assembly = [Reflection.Assembly]::Load($assemblyBytes)
$theme = $assembly.GetType('ClipPeek.Theme')
if (-not $theme) { throw '程序集里找不到 ClipPeek.Theme' }
$flags = [Reflection.BindingFlags]::Static -bor [Reflection.BindingFlags]::NonPublic
$buildBitmap = $theme.GetMethod('BuildBitmap', $flags)

function New-IconBitmap([int]$size) {
    return $buildBitmap.Invoke($null, @($size, $false))
}

function Get-PngBytes([System.Drawing.Bitmap]$bitmap) {
    $ms = New-Object System.IO.MemoryStream
    $bitmap.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray()
    $ms.Dispose()
    return $bytes
}

# --- ICO（每个尺寸一条 PNG 帧） ---
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = @()
foreach ($size in $sizes) {
    $bmp = New-IconBitmap $size
    $frames += , @{ Size = $size; Bytes = (Get-PngBytes $bmp) }
    $bmp.Dispose()
}

$icoPath = Join-Path $assets 'clippeek.ico'
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
try {
    $bw.Write([UInt16]0)                # reserved
    $bw.Write([UInt16]1)                # type = icon
    $bw.Write([UInt16]$frames.Count)    # count

    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dim = if ($frame.Size -ge 256) { 0 } else { $frame.Size }
        $bw.Write([Byte]$dim)           # width
        $bw.Write([Byte]$dim)           # height
        $bw.Write([Byte]0)              # palette
        $bw.Write([Byte]0)              # reserved
        $bw.Write([UInt16]1)            # planes
        $bw.Write([UInt16]32)           # bit depth
        $bw.Write([UInt32]$frame.Bytes.Length)
        $bw.Write([UInt32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) {
        $payload = [byte[]]$frame.Bytes
        $bw.Write($payload, 0, $payload.Length)
    }
}
finally {
    $bw.Dispose()
    $fs.Dispose()
}

# --- logo.png ---
$logoBmp = New-IconBitmap 512
$logoBmp.Save((Join-Path $assets 'logo.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$logoBmp.Dispose()

# --- banner.png ---
$width = 1280
$height = 360
$banner = New-Object System.Drawing.Bitmap $width, $height
$g = [System.Drawing.Graphics]::FromImage($banner)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

$rect = New-Object System.Drawing.Rectangle 0, 0, $width, $height
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $rect,
    [System.Drawing.Color]::FromArgb(255, 12, 15, 20),
    [System.Drawing.Color]::FromArgb(255, 26, 36, 50),
    20.0)
$g.FillRectangle($brush, $rect)
$brush.Dispose()

# 右侧一层柔和的高光
$glow = New-Object System.Drawing.Drawing2D.GraphicsPath
$glow.AddEllipse(880, -120, 560, 560)
$glowBrush = New-Object System.Drawing.Drawing2D.PathGradientBrush($glow)
$glowBrush.CenterColor = [System.Drawing.Color]::FromArgb(46, 64, 156, 255)
$glowBrush.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 64, 156, 255))
$g.FillPath($glowBrush, $glow)
$glowBrush.Dispose()
$glow.Dispose()

$icon = New-IconBitmap 176
$g.DrawImage($icon, 88, 92, 176, 176)
$icon.Dispose()

$titleFont = New-Object System.Drawing.Font('Segoe UI', 62, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$tagFont = New-Object System.Drawing.Font('Segoe UI', 25, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$chipFont = New-Object System.Drawing.Font('Segoe UI', 19, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$smallFont = New-Object System.Drawing.Font('Segoe UI', 17, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)

$white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 240, 244, 250))
$muted = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 150, 163, 182))
$g.DrawString('ClipPeek', $titleFont, $white, 300, 88)
$g.DrawString('See exactly what you copied.', $tagFont, $muted, 304, 176)

# 一枚绿色对勾，呼应程序里「已复制」气泡的视觉
$checkBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 52, 211, 120))
$g.FillEllipse($checkBrush, 306, 232, 40, 40)
$checkPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 10, 14, 20)), 4.2
$checkPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$checkPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawLine($checkPen, 316, 253, 322, 259)
$g.DrawLine($checkPen, 322, 259, 335, 244)
$checkPen.Dispose()
$g.DrawString('copy confirmation  ·  drag anywhere  ·  history panel', $chipFont, $muted, 364, 240)
$checkBrush.Dispose()

$g.DrawString('Windows 10/11  ·  MIT  ·  github.com/shilohjun/ClipPeek', $smallFont, $muted, 90, 308)

$banner.Save((Join-Path $assets 'banner.png'), [System.Drawing.Imaging.ImageFormat]::Png)

$g.Dispose()
$banner.Dispose()
$titleFont.Dispose(); $tagFont.Dispose(); $chipFont.Dispose(); $smallFont.Dispose()
$white.Dispose(); $muted.Dispose()

Write-Host ("已生成: clippeek.ico ({0} 帧), logo.png, banner.png" -f $frames.Count)
