using namespace System.Drawing
using namespace System.Drawing.Drawing2D
# Значокъ кнопки «Подключенъ къ …» (MainForm): вилка и розетка разведены — щёлкни, чтобы разъединить.
# Чёрный на прозрачномъ; программа уменьшаетъ его до высоты шрифта и краситъ въ цвѣтъ текста. Можно править въ графическомъ редакторѣ.
param([string] $Out = "$PSScriptRoot\PlugsApart.png", [int] $Size = 64)
Add-Type -AssemblyName System.Drawing

$bmp = [Bitmap]::new($Size, $Size, [Imaging.PixelFormat]::Format32bppArgb)
$g = [Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'
$u = $Size / 16   # рисунокъ — въ клѣткахъ 16×16
$cord = [Pen]::new([Color]::Black, [Math]::Max(1, 1.4 * $u)); $cord.StartCap = $cord.EndCap = 'Round'
$body = [SolidBrush]::new([Color]::Black)
$g.TranslateTransform($Size / 2, $Size / 2) # наискосокъ, какъ на привычномъ значкѣ
$g.RotateTransform(-45)
$g.TranslateTransform(-$Size / 2, -$Size / 2)
$g.DrawLine($cord, 0.5 * $u, 8 * $u, 2.5 * $u, 8 * $u)      # вилка: шнуръ,
$g.FillRectangle($body, 2.5 * $u, 5 * $u, 3.5 * $u, 6 * $u) # корпусъ
$g.DrawLine($cord, 6 * $u, 6.5 * $u, 7.5 * $u, 6.5 * $u)    # и два штыря;
$g.DrawLine($cord, 6 * $u, 9.5 * $u, 7.5 * $u, 9.5 * $u)
$g.FillRectangle($body, 10 * $u, 5 * $u, 3.5 * $u, 6 * $u)  # зазоръ — и розетка
$g.DrawLine($cord, 13.5 * $u, 8 * $u, 15.5 * $u, 8 * $u)    # со шнуромъ
$g.Dispose()
$bmp.Save($Out, [Imaging.ImageFormat]::Png)
$bmp.Dispose()
