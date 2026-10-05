<#
.SYNOPSIS
Win32-ресурсъ exe съ локализованными свойствами: Проводникъ → Свойства → Подробно показываетъ блокъ на языкѣ Windows.

.DESCRIPTION
SDK пишетъ одинъ блокъ VERSIONINFO «независимо отъ языка»; здѣсь — по блоку на каждый языкъ изъ описанія для Store
(store/upload/описанія.csv): названіе, краткое описаніе, издатель, товарные знаки. Въ тотъ же .res — значокъ и манифестъ:
компиляторъ не беретъ ихъ отдѣльно, когда задано Win32Resource. Вызывается изъ Dehumidifier.csproj передъ компиляціей;
файлъ перезаписывается, только если измѣнился, чтобы не пересобирать безъ нужды.
#>
param(
	[Parameter(Mandatory)] [string] $Out,
	[Parameter(Mandatory)] [string] $Csv,
	[Parameter(Mandatory)] [string] $Icon,
	[Parameter(Mandatory)] [string] $Manifest,
	[Parameter(Mandatory)] [string] $FileVersion,          # 1.2.3.0
	[Parameter(Mandatory)] [string] $InformationalVersion, # 1.2.3+хешъ
	[Parameter(Mandatory)] [string] $Copyright,            # годъ берётся отсюда
	[string] $Name = 'Dehumidifier')
$ErrorActionPreference = 'Stop'

$Unicode = 1200 # кодовая страница строкъ: UTF-16

# ───── двоичная запись ─────

function Utf16z([string] $s) { [Text.Encoding]::Unicode.GetBytes($s + [char]0) }
function Pad([Collections.Generic.List[byte]] $b) { while ($b.Count % 4) { $b.Add(0) } }

<# Узелъ VERSIONINFO: wLength, wValueLength, wType, szKey, выравниваніе, значеніе, выравниваніе, дѣти (каждое съ 32-битной границы). #>
function Node([string] $key, [byte[]] $value, [int] $valueLength, [int] $type, [byte[][]] $children = @()) {
	$b = [Collections.Generic.List[byte]]::new()
	$b.AddRange([byte[]]::new(6))
	$b.AddRange([byte[]](Utf16z $key)); Pad $b
	if ($value) { $b.AddRange($value) }
	foreach ($c in $children) { Pad $b; $b.AddRange($c) }
	$a = $b.ToArray()
	[BitConverter]::GetBytes([uint16]$a.Length).CopyTo($a, 0)
	[BitConverter]::GetBytes([uint16]$valueLength).CopyTo($a, 2)
	[BitConverter]::GetBytes([uint16]$type).CopyTo($a, 4)
	, $a }
function Text([string] $key, [string] $value) { Node $key (Utf16z $value) ($value.Length + 1) 1 }

<# Одна запись .res: заголовокъ съ числовыми тѵпомъ и именемъ, данныя, выравниваніе. #>
function ResEntry([int] $type, [int] $id, [int] $lang, [byte[]] $data, [int] $flags = 0x0030) {
	$w = [IO.BinaryWriter]::new([IO.MemoryStream]::new())
	$w.Write([uint32]$data.Length); $w.Write([uint32]32)
	$w.Write([uint16]0xFFFF); $w.Write([uint16]$type)
	$w.Write([uint16]0xFFFF); $w.Write([uint16]$id)
	$w.Write([uint32]0); $w.Write([uint16]$flags); $w.Write([uint16]$lang); $w.Write([uint32]0); $w.Write([uint32]0)
	$w.Write($data); while ($w.BaseStream.Length % 4) { $w.Write([byte]0) }
	, $w.BaseStream.ToArray() }

# ───── строки по языкамъ ─────

$rows = Import-Csv $Csv
function Field([string] $name) { $rows | Where-Object Field -EQ $name }
$title = Field 'Title'; $short = Field 'ShortDescription'; $studio = Field 'DevStudio'; $marks = Field 'CopyrightTrademarkInformation'
$year = if ($Copyright -match '\d{4}') { $Matches[0] } else { (Get-Date).Year }
$langs = $title.PSObject.Properties.Name | Where-Object { $_ -notin 'Field', 'ID', 'Type (Тип)', 'default' -and $title.$_ }

<# Строки одного языка: таблица StringFileInfo съ ключомъ «языкъ + кодовая страница». #>
function Table([string] $l, [int] $lcid) {
	Node ('{0:X4}{1:X4}' -f $lcid, $Unicode) $null 0 1 @(
		(Text 'CompanyName'      $studio.$l),
		(Text 'FileDescription'  $title.$l),
		(Text 'FileVersion'      $FileVersion),
		(Text 'InternalName'     $Name),
		(Text 'LegalCopyright'   "© $year $($studio.$l)"),
		(Text 'LegalTrademarks'  $marks.$l),
		(Text 'OriginalFilename' "$Name.exe"),
		(Text 'ProductName'      $title.$l),
		(Text 'ProductVersion'   $InformationalVersion),
		(Text 'Comments'         $short.$l)) }

# ───── VS_FIXEDFILEINFO ─────

function Four([string] $s) { [Version]((@(($s -replace '[-+].*$', '').Split('.')) + @('0', '0', '0'))[0..3] -join '.') } # 1.2.3-ci → 1.2.3.0
$v = Four $FileVersion
$pv = Four $InformationalVersion
$f = [IO.BinaryWriter]::new([IO.MemoryStream]::new())
$f.Write(0xFEEF04BDu); $f.Write([uint32]0x00010000)
$f.Write([uint32](($v.Major -shl 16) -bor $v.Minor)); $f.Write([uint32](($v.Build -shl 16) -bor $v.Revision))
$f.Write([uint32](($pv.Major -shl 16) -bor $pv.Minor)); $f.Write([uint32](($pv.Build -shl 16) -bor $pv.Revision))
$f.Write([uint32]0x3F); $f.Write([uint32]0)          # маска флаговъ, флаги
$f.Write([uint32]0x00040004)                          # VOS_NT_WINDOWS32
$f.Write([uint32]1); $f.Write([uint32]0)              # VFT_APP
$f.Write([uint32]0); $f.Write([uint32]0)              # дата
$fixed = $f.BaseStream.ToArray()

<# Отдѣльный RT_VERSION на каждый языкъ, помѣченный этимъ языкомъ (какъ LANGUAGE … SUBLANG_NEUTRAL въ .rc), съ однимъ блокомъ внутри:
   загрузчикъ ресурсовъ беретъ ресурсъ на языкѣ Windows — его и показываетъ Проводникъ. Нѣсколько блоковъ въ одномъ ресурсѣ
   Проводникъ не различаетъ — показываетъ первый (провѣрено). Языкъ — нейтральный: ru → 0x0019, sr-Cyrl → 0x6C1A. #>
$versions = foreach ($l in $langs) {
	$lcid = [Globalization.CultureInfo]::GetCultureInfo($l).LCID
	$translation = [byte[]]([BitConverter]::GetBytes([uint16]$lcid) + [BitConverter]::GetBytes([uint16]$Unicode))
	, @($lcid, (Node 'VS_VERSION_INFO' $fixed $fixed.Length 0 @(
		(Node 'StringFileInfo' $null 0 1 @(, (Table $l $lcid))),
		(Node 'VarFileInfo' $null 0 1 @(, (Node 'Translation' $translation $translation.Length 0)))))) }

# ───── значокъ: каждый образъ — RT_ICON, оглавленіе — RT_GROUP_ICON 32512, какъ у компилятора ─────

$ico = [IO.File]::ReadAllBytes($Icon)
$count = [BitConverter]::ToUInt16($ico, 4)
$res = [Collections.Generic.List[byte]]::new()
$res.AddRange([byte[]](ResEntry 0 0 0 ([byte[]]::new(0)) 0))   # пустая первая запись — признакъ 32-битнаго .res
$group = [IO.BinaryWriter]::new([IO.MemoryStream]::new())
$group.Write([uint16]0); $group.Write([uint16]1); $group.Write([uint16]$count)
for ($i = 0; $i -lt $count; $i++) {
	$e = 6 + 16 * $i
	$size = [BitConverter]::ToUInt32($ico, $e + 8); $offset = [BitConverter]::ToUInt32($ico, $e + 12)
	$res.AddRange([byte[]](ResEntry 3 ($i + 1) 0 ([byte[]]$ico[$offset..($offset + $size - 1)]) 0x1010))
	$group.Write($ico, $e, 12); $group.Write([uint16]($i + 1)) }
$res.AddRange([byte[]](ResEntry 14 32512 0 $group.BaseStream.ToArray() 0x1030))

foreach ($v in $versions) { $res.AddRange([byte[]](ResEntry 16 1 $v[0] $v[1])) } # RT_VERSION на каждомъ языкѣ
$res.AddRange([byte[]](ResEntry 24 1 0 ([IO.File]::ReadAllBytes($Manifest)))) # RT_MANIFEST

$bytes = $res.ToArray()
if (-not (Test-Path $Out) -or -not [Collections.StructuralComparisons]::StructuralEqualityComparer.Equals([IO.File]::ReadAllBytes($Out), $bytes)) {
	New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
	[IO.File]::WriteAllBytes($Out, $bytes) }
