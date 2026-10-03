#Requires -Version 7
<#
.SYNOPSIS
    Снимки окна для Microsoft Store на каждомъ языкѣ карточки: store\screenshots\main-<языкъ>-1920x1080.png.

.DESCRIPTION
    Форма запускается въ этомъ процессѣ съ нужной культурой и подключается къ осушителю
    по адресу и токену изъ свѣжайшаго user.config оконной программы. Заголовокъ и подзаголовокъ
    слѣва — Title и ShortDescription изъ store\описанія.csv. Нужна сборка Release.

.EXAMPLE
    pwsh -STA -File store\screenshots.ps1

.EXAMPLE
    pwsh -STA -Command "& store\screenshots.ps1 -Langs ru, en"
#>
param([string] $Root = (Split-Path $PSScriptRoot), [string[]] $Langs = @('ru', 'sr-cyrl', 'bg', 'mk', 'sk', 'sl', 'hr', 'cs', 'pl', 'en'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class Dwm { public struct RECT { public int L, T, R, B; }
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint f);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
 [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int size); }
'@
$asm = [Reflection.Assembly]::LoadFrom("$Root\bin\Release\net10.0-windows\win-x64\Dehumidifier.dll")
[Windows.Forms.Application]::SetHighDpiMode('SystemAware') | Out-Null
[Windows.Forms.Application]::EnableVisualStyles()

# адресъ и токенъ — изъ свѣжайшаго user.config оконной программы
$cfg = Get-ChildItem "$env:LOCALAPPDATA\Dehumidifier" -Recurse -Filter user.config | Sort-Object LastWriteTime | Select-Object -Last 1
$xml = [xml](Get-Content $cfg.FullName -Raw)
$get = { param($n) ($xml.configuration.userSettings.FirstChild.setting | Where-Object name -eq $n).value }
$st = $asm.GetTypes() | Where-Object Name -eq 'Settings' | Select-Object -First 1
$settings = $st.GetProperty('Default').GetValue($null)
$set = { param($n, $v) $st.GetProperty($n).SetValue($settings, $v) }

$csv = Import-Csv "$Root\store\описанія.csv"
$row = { param($f) $csv | Where-Object Field -eq $f }
$out = New-Item -ItemType Directory -Force "$Root\store\screenshots"

foreach ($lang in $Langs) {
	$culture = [Globalization.CultureInfo]::new(($lang -eq 'sr-cyrl') ? 'sr-Cyrl-RS' : $lang)
	[Threading.Thread]::CurrentThread.CurrentUICulture = $culture
	[Threading.Thread]::CurrentThread.CurrentCulture = $culture
	& $set 'IP' (& $get 'IP'); & $set 'Token' (& $get 'Token')
	$form = [Activator]::CreateInstance(($asm.GetTypes() | Where-Object Name -eq 'MainForm' | Select-Object -First 1), $true)
	$form.StartPosition = 'CenterScreen'
	$form.Show()
	$sw = [Diagnostics.Stopwatch]::StartNew()
	while ($sw.Elapsed.TotalSeconds -lt 6) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 30 }

	$form.ActiveControl = $null # иначе адресъ выдѣленъ
	for ($i = 0; $i -lt 10; $i++) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 30 }
	$hwnd = $form.Handle
	$wr = [Dwm+RECT]::new(); [void][Dwm]::GetWindowRect($hwnd, [ref]$wr)
	$fr = [Dwm+RECT]::new(); [void][Dwm]::DwmGetWindowAttribute($hwnd, 9, [ref]$fr, 16) # DWMWA_EXTENDED_FRAME_BOUNDS
	$full = [Drawing.Bitmap]::new($wr.R - $wr.L, $wr.B - $wr.T); $gs = [Drawing.Graphics]::FromImage($full)
	$dc = $gs.GetHdc(); [void][Dwm]::PrintWindow($hwnd, $dc, 2); $gs.ReleaseHdc($dc)
	$shot = $full.Clone([Drawing.Rectangle]::new($fr.L - $wr.L, $fr.T - $wr.T, $fr.R - $fr.L, $fr.B - $fr.T), $full.PixelFormat)
	$form.Hide(); $form.Dispose()

	$W = 1920; $H = 1080
	$canvas = [Drawing.Bitmap]::new($W, $H); $g = [Drawing.Graphics]::FromImage($canvas)
	$g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.TextRenderingHint = 'ClearTypeGridFit'
	$g.FillRectangle([Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Point]::new(0, 0), [Drawing.Point]::new(0, $H), [Drawing.Color]::FromArgb(232, 240, 247), [Drawing.Color]::FromArgb(206, 222, 236)), 0, 0, $W, $H)
	$k = [Math]::Min(1.15, ($H - 100) / $shot.Height); $sw2 = [int]($shot.Width * $k); $sh = [int]($shot.Height * $k)
	$sx = 1180; $sy = [int](($H - $sh) / 2)
	for ($e = 12; $e -ge 1; $e -= 3) { $g.FillRectangle([Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(14, 0, 0, 0)), $sx - $e + 6, $sy - $e + 10, $sw2 + 2 * $e, $sh + 2 * $e) }
	$g.DrawImage($shot, $sx, $sy, $sw2, $sh)

	$ink = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(30, 40, 52)); $gray = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(80, 92, 105))
	$titleFont = [Drawing.Font]::new('Segoe UI Semibold', 40); $subFont = [Drawing.Font]::new('Segoe UI', 24)
	$title = (& $row 'Title').$lang; $sub = (& $row 'ShortDescription').$lang
	$tw = 960; $th = $g.MeasureString($title, $titleFont, $tw).Height; $subh = $g.MeasureString($sub, $subFont, $tw).Height
	$ty = [int](($H - $th - 30 - $subh) / 2)
	$g.DrawString($title, $titleFont, $ink, [Drawing.RectangleF]::new(120, $ty, $tw, $th + 10))
	$g.DrawString($sub, $subFont, $gray, [Drawing.RectangleF]::new(124, $ty + $th + 30, $tw, $subh + 10))
	$file = Join-Path $out "main-$lang-1920x1080.png"
	$canvas.Save($file, [Drawing.Imaging.ImageFormat]::Png)
	"$lang → $file"
}

# форма при подключеніи сохраняетъ настройки — въ user.config самого pwsh; токенъ тамъ не нуженъ
$own = [Configuration.ConfigurationManager]::OpenExeConfiguration([Configuration.ConfigurationUserLevel]::PerUserRoamingAndLocal).FilePath
if (Test-Path $own) { Remove-Item $own }
