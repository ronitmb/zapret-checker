# Generates app.ico (16..256) and icon.png preview: green shield + lightning bolt
Add-Type -AssemblyName System.Drawing

function New-IconBitmap([int]$size) {
    $bmp = [System.Drawing.Bitmap]::new($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $s = $size / 256.0

    # rounded square background, dark navy gradient
    $r = 52 * $s
    $d = $r * 2
    $bg = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $bg.AddArc(0, 0, $d, $d, 180, 90)
    $bg.AddArc($size - $d, 0, $d, $d, 270, 90)
    $bg.AddArc($size - $d, $size - $d, $d, $d, 0, 90)
    $bg.AddArc(0, $size - $d, $d, $d, 90, 90)
    $bg.CloseFigure()
    $rect = [System.Drawing.Rectangle]::new(0, 0, $size, $size)
    $c1 = [System.Drawing.Color]::FromArgb(255, 24, 46, 74)
    $c2 = [System.Drawing.Color]::FromArgb(255, 7, 18, 34)
    $bgBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new($rect, $c1, $c2, 90.0)
    $g.FillPath($bgBrush, $bg)

    # shield
    $shieldCoords = @(
        @(48, 66), @(128, 46), @(208, 66), @(206, 132),
        @(178, 180), @(128, 214), @(78, 180), @(50, 132)
    )
    $pts = @()
    foreach ($c in $shieldCoords) {
        $pts += [System.Drawing.Point]::new([int]($c[0] * $s), [int]($c[1] * $s))
    }
    $shPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $shPath.AddPolygon($pts)
    $shRect = [System.Drawing.Rectangle]::new(40, 40, 180, 190)
    $g1 = [System.Drawing.Color]::FromArgb(255, 46, 204, 113)
    $g2 = [System.Drawing.Color]::FromArgb(255, 21, 138, 76)
    $shBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new($shRect, $g1, $g2, 90.0)
    $g.FillPath($shBrush, $shPath)
    $shPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 150, 240, 185), [single](6 * $s))
    $g.DrawPath($shPen, $shPath)

    # lightning bolt
    $boltCoords = @(
        @(142, 62), @(94, 140), @(124, 140), @(102, 202), @(170, 112), @(138, 112), @(162, 62)
    )
    $bpts = @()
    foreach ($c in $boltCoords) {
        $bpts += [System.Drawing.Point]::new([int]($c[0] * $s), [int]($c[1] * $s))
    }
    $g.FillPolygon([System.Drawing.Brushes]::Gold, $bpts)
    $boltPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 130, 95, 10), [single](3 * $s))
    $g.DrawPolygon($boltPen, $bpts)

    $g.Dispose()
    return $bmp
}

$sizes = 256, 64, 48, 32, 16
$entries = @()
foreach ($sz in $sizes) {
    $bmp = New-IconBitmap $sz
    $ms = [System.IO.MemoryStream]::new()
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $entries += , @($sz, $ms.ToArray())
    $ms.Dispose()
    $bmp.Dispose()
}

$fs = [System.IO.File]::Create("$PSScriptRoot\app.ico")
$bw = [System.IO.BinaryWriter]::new($fs)
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$entries.Count)
$offset = 6 + 16 * $entries.Count
foreach ($e in $entries) {
    $sz = $e[0]
    $w = 0; if ($sz -lt 256) { $w = $sz }
    $bw.Write([byte]$w)
    $bw.Write([byte]$w)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$e[1].Length)
    $bw.Write([uint32]$offset)
    $offset += $e[1].Length
}
foreach ($e in $entries) { $bw.Write($e[1]) }
$bw.Dispose()
$fs.Dispose()

$prev = New-IconBitmap 256
$prev.Save("$PSScriptRoot\icon.png", [System.Drawing.Imaging.ImageFormat]::Png)
$prev.Dispose()
Write-Host "OK: app.ico + icon.png"
