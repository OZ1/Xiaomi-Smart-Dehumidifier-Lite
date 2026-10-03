using namespace System.Drawing
using namespace System.Drawing.Drawing2D
# -Out — файлъ .ico; -PreviewDir — папка для PNG каждаго размѣра (icon<N>.png); -Sizes — какіе размѣры рисовать
param([string] $Out, [string] $PreviewDir, [int[]] $Sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256))
Add-Type -AssemblyName System.Drawing

function RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
	$p = [GraphicsPath]::new(); $d = 2 * $r
	$p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
	$p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
	$p.CloseFigure(); $p
}

function Draw([int] $n) {
	$bmp = [Bitmap]::new($n, $n, [Imaging.PixelFormat]::Format32bppArgb)
	$g = [Graphics]::FromImage($bmp)
	$g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'
	$g.ScaleTransform($n / 256, $n / 256)
	$px = 256 / $n   # одна точка итоговой картинки въ координатахъ 256

	# тѣнь
	if ($n -ge 32) {
		$g.FillEllipse([SolidBrush]::new([Color]::FromArgb(60, 0, 0, 0)), 52, 222, 152, 26)
	}
	# корпусъ
	$body = RoundRect 52 12 152 224 30
	$grad = [LinearGradientBrush]::new([PointF]::new(52, 0), [PointF]::new(204, 0), [Color]::FromArgb(255, 255, 255), [Color]::FromArgb(206, 212, 218))
	$g.FillPath($grad, $body)
	$g.DrawPath([Pen]::new([Color]::FromArgb(110, 120, 130), [Math]::Max(5, 1.2 * $px)), $body)

	# рѣшётка сверху
	$grille = RoundRect 72 30 112 40 10
	$g.FillPath([SolidBrush]::new([Color]::FromArgb(58, 66, 76)), $grille)
	if ($n -ge 48) {
		$slit = [Pen]::new([Color]::FromArgb(140, 150, 160), 4)
		for ($x = 88; $x -le 168; $x += 16) { $g.DrawLine($slit, $x, 38, $x, 62) }
	}

	# окошко бака съ водой
	if ($n -ge 24) {
		$tank = RoundRect 72 160 112 60 12
		$g.FillPath([SolidBrush]::new([Color]::FromArgb(214, 234, 248)), $tank)
		$g.SetClip($tank)
		$g.FillRectangle([SolidBrush]::new([Color]::FromArgb(66, 165, 245)), 72, 190, 112, 40)
		$g.ResetClip()
		$g.DrawPath([Pen]::new([Color]::FromArgb(120, 150, 175), [Math]::Max(3, $px)), $tank)
	}

	# капля: остріе въ (128,0), донышко — кругъ радіуса 28 съ центромъ (128,60)
	$drop = [GraphicsPath]::new()
	$drop.AddBezier(128, 0, 128, 22, 100, 38, 100, 60)
	$drop.AddArc(100, 32, 56, 56, 180, -180)
	$drop.AddBezier(156, 60, 156, 38, 128, 22, 128, 0)
	$drop.CloseFigure()
	$m = [Matrix]::new()
	if ($n -ge 24) { $m.Translate(0, 66) } else { $m.Translate(128, 56); $m.Scale(1.55, 1.55); $m.Translate(-128, 0) }
	$drop.Transform($m)
	$g.FillPath([SolidBrush]::new([Color]::FromArgb(30, 136, 229)), $drop)
	if ($n -ge 32) {
		$g.FillEllipse([SolidBrush]::new([Color]::FromArgb(160, 255, 255, 255)), 111, 118, 11, 20)
	}
	$g.Dispose(); $bmp
}

$sizes = $Sizes
$images = foreach ($n in $sizes) {
	$bmp = Draw $n
	if ($PreviewDir) { $bmp.Save((Join-Path $PreviewDir "icon$n.png")) }
	$ms = [IO.MemoryStream]::new(); $bmp.Save($ms, [Imaging.ImageFormat]::Png); $bmp.Dispose()
	, $ms.ToArray()
}

if (-not $Out) { return } # только PNG — напримѣръ, значки для Store

# ICO: заголовокъ, каталогъ, PNG-образы
$ico = [IO.MemoryStream]::new(); $w = [IO.BinaryWriter]::new($ico)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
	$n = $sizes[$i]; $b = if ($n -ge 256) { 0 } else { $n }
	$w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
	$w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
	$offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Flush(); [IO.File]::WriteAllBytes($Out, $ico.ToArray())
