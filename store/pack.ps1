#Requires -Version 7
<#
.SYNOPSIS
    Собираетъ пакетъ для Microsoft Store: самодостаточныя сборки въ MSIX, затѣмъ .msixbundle и .msixupload.

.DESCRIPTION
    Значенія Identity — изъ Partner Center → Управленіе продуктомъ → Идентификація продукта.
    Store подписываетъ пакетъ самъ, поэтому здѣсь онъ не подписывается.
    Нуженъ Windows SDK (makeappx.exe). Результатъ — въ store\out\.

.EXAMPLE
    pwsh -File store\pack.ps1 -Name 12345OZone.Pult -Publisher "CN=…" -PublisherDisplayName OZone -Version 1.0.0.0
#>
param(
	[string]   $Name                 = 'F1E3C9D5.Xiaomi',
	[string]   $Publisher            = 'CN=F14992C4-B3AE-485D-9890-2C4616716FD1',
	[string]   $PublisherDisplayName = 'Ољег Зонов',
	[string]   $Version              = '1.0.0.0',
	[string[]] $Architectures        = @('x64', 'arm64')
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$out  = Join-Path $PSScriptRoot 'out'

if ($Version -notmatch '^\d+\.\d+\.\d+\.0$') { throw 'Версія для Store — четыре числа, послѣднее 0: напримѣръ 1.0.0.0.' }
$makeappx = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Directory |
	Where-Object Name -match '^10\.' | Sort-Object { [version]$_.Name } -Descending |
	ForEach-Object { Join-Path $_.FullName 'x64\makeappx.exe' } | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $makeappx) { throw 'Не найденъ makeappx.exe — нуженъ Windows SDK.' }
$makepri = Join-Path (Split-Path $makeappx) 'makepri.exe'

# makeappx и makepri перечисляютъ каждый файлъ: при успѣхѣ въ логъ идутъ только предупрежденія, при ошибкѣ — весь выводъ
function Tool([string] $What, [string] $Exe) {
	$log = & $Exe @args 2>&1
	if ($LASTEXITCODE) { $log | Write-Host; throw "$What не удался." }
	$log | Where-Object { "$_" -match 'warn|error' } | ForEach-Object { Write-Warning "${What}: $_" }
}

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
$assets   = New-Item -ItemType Directory (Join-Path $out 'Assets')
$icons    = New-Item -ItemType Directory (Join-Path $out 'icons')
$packages = New-Item -ItemType Directory (Join-Path $out 'packages')

# ───── значки: тѣмъ же рисункомъ, что Dehumidifier.ico ─────
& (Join-Path $root 'make-icon.ps1') -PreviewDir $icons -Sizes 16, 24, 32, 44, 48, 50, 150, 256
Copy-Item "$icons\icon44.png"  "$assets\Square44x44Logo.png"
Copy-Item "$icons\icon150.png" "$assets\Square150x150Logo.png"
Copy-Item "$icons\icon50.png"  "$assets\StoreLogo.png"
foreach ($n in 16, 24, 32, 48, 256) { # панель задачъ и меню «Пускъ» берутъ значокъ точнаго размѣра
	Copy-Item "$icons\icon$n.png" "$assets\Square44x44Logo.targetsize-$n.png"
	Copy-Item "$icons\icon$n.png" "$assets\Square44x44Logo.targetsize-${n}_altform-unplated.png"
}

# ───── имя и описаніе на каждомъ языкѣ: Title и ShortDescription изъ описанія.csv → Strings\<языкъ>\Resources.resw ─────
# Имена должны совпадать съ зарезервированными въ Partner Center.
$x = { param($s) [Security.SecurityElement]::Escape($s) }
$strings = New-Item -ItemType Directory (Join-Path $out 'pri\Strings')
$csv = Import-Csv (Join-Path $PSScriptRoot 'описанія.csv')
$title = $csv | Where-Object Field -eq 'Title'; $short = $csv | Where-Object Field -eq 'ShortDescription'
$languages = ([xml](Get-Content (Join-Path $PSScriptRoot 'AppxManifest.xml') -Raw)).Package.Resources.Resource.Language
foreach ($lang in $languages) {
	$appTitle = $title.($lang.ToLowerInvariant()); $desc = $short.($lang.ToLowerInvariant()) # не $name: это $Name
	if (-not $appTitle -or -not $desc) { throw "Въ описанія.csv нѣтъ названія или краткаго описанія для $lang." }
	$dir = New-Item -ItemType Directory (Join-Path $strings $lang)
	[IO.File]::WriteAllText((Join-Path $dir 'Resources.resw'), @"
<?xml version="1.0" encoding="utf-8"?>
<root>
	<resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
	<resheader name="version"><value>2.0</value></resheader>
	<data name="AppDisplayName" xml:space="preserve"><value>$(& $x $appTitle)</value></data>
	<data name="AppDescription" xml:space="preserve"><value>$(& $x $desc)</value></data>
</root>
"@, [Text.UTF8Encoding]::new($false))
}
Copy-Item $assets (Join-Path $out 'pri\Assets') -Recurse
Tool 'makepri createconfig' $makepri createconfig /cf (Join-Path $out 'priconfig.xml') /dq $languages[0] /pv 10.0.0 /o
$priconfig = [xml](Get-Content (Join-Path $out 'priconfig.xml') -Raw) # всѣ языки — въ одинъ resources.pri, безъ пакетовъ ресурсовъ
[void]$priconfig.resources.RemoveChild($priconfig.resources.packaging); $priconfig.Save((Join-Path $out 'priconfig.xml'))

# ───── пакетъ на каждую архитектуру ─────
$template = Get-Content (Join-Path $PSScriptRoot 'AppxManifest.xml') -Raw
foreach ($arch in $Architectures) {
	$layout = Join-Path $out "layout-$arch"
	Write-Host "Сборка $arch…"
	dotnet publish (Join-Path $root 'Dehumidifier.csproj') -c Release -r "win-$arch" --self-contained true `
		-p:PublishSingleFile=false -p:DebugType=embedded -p:Version=$Version -o $layout -nologo -v q
	if ($LASTEXITCODE) { throw "Сборка $arch не удалась." }

	Copy-Item $assets (Join-Path $layout 'Assets') -Recurse
	$manifest = $template.Replace('{Name}', (& $x $Name)).Replace('{Publisher}', (& $x $Publisher)).
		Replace('{PublisherDisplayName}', (& $x $PublisherDisplayName)).Replace('{Version}', $Version).Replace('{Arch}', $arch)
	[IO.File]::WriteAllText((Join-Path $layout 'AppxManifest.xml'), $manifest, [Text.UTF8Encoding]::new($false))

	# resources.pri: строки и значки; индексируется отдѣльная папка, чтобы не трогать папки сборокъ .NET (ru, cs, …)
	Copy-Item (Join-Path $layout 'AppxManifest.xml') (Join-Path $out 'pri') -Force
	Tool "makepri ($arch)" $makepri new /pr (Join-Path $out 'pri') /cf (Join-Path $out 'priconfig.xml') /mn (Join-Path $out 'pri\AppxManifest.xml') `
		/of (Join-Path $layout 'resources.pri') /o

	Tool "makeappx pack ($arch)" $makeappx pack /d $layout /p (Join-Path $packages "Dehumidifier_${Version}_$arch.msix") /o
}

# ───── связка и файлъ для Store ─────
$bundle = Join-Path $out "Dehumidifier_$Version.msixbundle"
Tool 'makeappx bundle' $makeappx bundle /d $packages /p $bundle /bv $Version /o

$upload = Join-Path $out "Dehumidifier_$Version.msixupload" # .msixupload — zip съ .msixbundle
Compress-Archive -Path $bundle -DestinationPath "$upload.zip" -Force
Move-Item "$upload.zip" $upload -Force

Write-Host "Готово: $upload"
