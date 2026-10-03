<#
.SYNOPSIS
    Управленіе осушителемъ Xiaomi Smart Dehumidifier Lite (xiaomi.derh.lite) по локальной сѣти.

.DESCRIPTION
    Протоколъ miIO / MIoT, UDP 54321. Адресъ и токенъ беретъ изъ параметровъ -Ip/-Token,
    перемѣнной MIIO_TOKEN или изъ настроекъ оконной программы DehumidifierControl (Settings.Default):
    сперва user.config въ %LOCALAPPDATA%, затѣмъ значенія по умолчанію изъ Properties\Settings.settings.

    Команды:
      status                      состояніе (по умолчанію)
      watch [сек]                 показывать состояніе каждыя N секундъ (10)
      on | off                    включить / выключить
      mode smart|sleep|dry        режимъ: умный, ночной, сушка бѣлья
      humidity 40..70             цѣлевая влажность
      light off|dim|bright        подсвѣтка
      sound on|off                звукъ кнопокъ
      lock on|off                 блокировка кнопокъ
      dry-after-off on|off        просушка послѣ выключенія
      timer N | timer off         таймеръ въ минутахъ
      reset-filter                сбросить счётчикъ фильтра
      info                        miIO.info: модель, прошивка, сѣть
      get SIID PIID               прочесть произвольное свойство
      set SIID PIID ЗНАЧЕНІЕ      записать произвольное свойство
      raw МЕТОДЪ [JSON]           сырая команда miIO
      save                        запомнить -Ip и -Token въ user.config программы

.EXAMPLE
    ./derh.ps1 -Token 0123456789abcdef0123456789abcdef save

.EXAMPLE
    ./derh.ps1 humidity 50
#>
#Requires -Version 7

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('status', 'watch', 'on', 'off', 'mode', 'humidity', 'light', 'sound', 'lock', 'dry-after-off',
                 'timer', 'reset-filter', 'info', 'get', 'set', 'raw', 'save')]
    [string] $Command = 'status',

    [Parameter(Position = 1, ValueFromRemainingArguments)]
    [string[]] $Arguments = @(),

    [string] $Ip,
    [string] $Token
)

$ErrorActionPreference = 'Stop'

# ───────────────────────────── настройки программы ─────────────────────────────

$SettingsSection = 'DehumidifierControl.Properties.Settings'

# user.config, который пишетъ Settings.Default; у каждой сборки (Debug, Release) свой — берёмъ свѣжайшій.
function Find-UserConfig {
    Get-ChildItem (Join-Path $env:LOCALAPPDATA '*\DehumidifierControl*\*\user.config') -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
}

function Get-AppSetting([string] $Name) {
    if ($file = Find-UserConfig) {
        $node = ([xml](Get-Content $file.FullName -Raw)).SelectSingleNode("/configuration/userSettings/$SettingsSection/setting[@name='$Name']/value")
        if ($node -and $node.InnerText) { return $node.InnerText }
    }
    $defaults = Join-Path $PSScriptRoot 'Properties\Settings.settings'
    if (Test-Path $defaults) {
        $xml = [xml](Get-Content $defaults -Raw)
        $ns = [Xml.XmlNamespaceManager]::new($xml.NameTable)
        $ns.AddNamespace('s', 'http://schemas.microsoft.com/VisualStudio/2004/01/settings')
        $node = $xml.SelectSingleNode("//s:Setting[@Name='$Name']/s:Value", $ns)
        if ($node -and $node.InnerText) { return $node.InnerText }
    }
    $null
}

function Set-AppSettings([System.Collections.IDictionary] $Values) {
    $file = Find-UserConfig
    if (-not $file) { throw 'user.config ещё нѣтъ: запусти разъ DehumidifierControl и подключись.' }
    $xml = [xml](Get-Content $file.FullName -Raw)
    $section = $xml.SelectSingleNode("/configuration/userSettings/$SettingsSection")
    if (-not $section) { throw "Въ $($file.FullName) нѣтъ раздѣла $SettingsSection." }
    foreach ($name in $Values.Keys) {
        $setting = $section.SelectSingleNode("setting[@name='$name']")
        if (-not $setting) {
            $setting = $xml.CreateElement('setting')
            $setting.SetAttribute('name', $name)
            $setting.SetAttribute('serializeAs', 'String')
            [void] $setting.AppendChild($xml.CreateElement('value'))
            [void] $section.AppendChild($setting)
        }
        $setting.SelectSingleNode('value').InnerText = $Values[$name]
    }
    $xml.Save($file.FullName)
    $file.FullName
}

# ───────────────────────────── протоколъ miIO ─────────────────────────────

$Miio = @{ TimeoutMs = 3000; MessageId = Get-Random -Minimum 1 -Maximum 9000; Handshaken = $false }

function Write-BigEndian([byte[]] $Buffer, [int] $Offset, [uint64] $Value, [int] $Bytes) {
    for ($i = $Bytes - 1; $i -ge 0; $i--) {
        $Buffer[$Offset + $i] = [byte]($Value -band 0xff)
        $Value = $Value -shr 8
    }
}

function Read-BigEndian32([byte[]] $Buffer, [int] $Offset) {
    [uint32](([uint64]$Buffer[$Offset] -shl 24) + ([uint64]$Buffer[$Offset + 1] -shl 16) +
             ([uint64]$Buffer[$Offset + 2] -shl 8) + $Buffer[$Offset + 3])
}

function Connect-Miio([string] $Address, [string] $TokenHex) {
    $Miio.Token = [Convert]::FromHexString($TokenHex)
    if ($Miio.Token.Length -ne 16) { throw 'Токенъ долженъ состоять изъ 32 шестнадцатеричныхъ цифръ.' }
    $key = [Security.Cryptography.MD5]::HashData($Miio.Token)
    $Miio.Iv = [Security.Cryptography.MD5]::HashData([byte[]]($key + $Miio.Token))
    $Miio.Aes = [Security.Cryptography.Aes]::Create()
    $Miio.Aes.Key = $key
    $Miio.Address = $Address
    $Miio.Udp = [Net.Sockets.UdpClient]::new()
    $Miio.Udp.Connect([Net.IPAddress]::Parse($Address), 54321)
}

function Receive-Miio([int] $TimeoutMs) {
    if ($TimeoutMs -le 0) { return $null }
    $Miio.Udp.Client.ReceiveTimeout = $TimeoutMs
    $remote = [Net.IPEndPoint]::new([Net.IPAddress]::Any, 0)
    try { , $Miio.Udp.Receive([ref] $remote) }
    catch [Net.Sockets.SocketException] { $null }   # таймаутъ или ICMP «порт недоступенъ»
}

function Invoke-MiioHandshake {
    $hello = [Convert]::FromHexString('21310020' + 'ff' * 28)
    [void] $Miio.Udp.Send($hello, $hello.Length)
    $data = Receive-Miio $Miio.TimeoutMs
    if (-not $data -or $data.Length -lt 32) { throw "Устройство $($Miio.Address) не отвѣчаетъ — провѣрь адресъ и сѣть." }
    $Miio.DeviceId = Read-BigEndian32 $data 8
    $Miio.Stamp = Read-BigEndian32 $data 12
    $Miio.Clock = [Diagnostics.Stopwatch]::StartNew()
    $Miio.Handshaken = $true
}

function New-MiioPacket([byte[]] $Payload) {
    $encrypted = $Miio.Aes.EncryptCbc($Payload, $Miio.Iv, [Security.Cryptography.PaddingMode]::PKCS7)
    $packet = [byte[]]::new(32 + $encrypted.Length)
    Write-BigEndian $packet 0 0x2131 2
    Write-BigEndian $packet 2 $packet.Length 2
    Write-BigEndian $packet 8 $Miio.DeviceId 4
    Write-BigEndian $packet 12 ($Miio.Stamp + [uint64]$Miio.Clock.Elapsed.TotalSeconds) 4
    [Array]::Copy($Miio.Token, 0, $packet, 16, 16)
    [Array]::Copy($encrypted, 0, $packet, 32, $encrypted.Length)
    # контрольная сумма считается съ токеномъ на ея мѣстѣ
    [Array]::Copy([Security.Cryptography.MD5]::HashData($packet), 0, $packet, 16, 16)
    , $packet
}

function Read-MiioPacket([byte[]] $Data) {
    if ($Data.Length -le 32 -or $Data[0] -ne 0x21 -or $Data[1] -ne 0x31) { return $null }
    $check = [byte[]] $Data.Clone()
    [Array]::Copy($Miio.Token, 0, $check, 16, 16)
    $sum = [Convert]::ToHexString([Security.Cryptography.MD5]::HashData($check))
    if ($sum -ne [Convert]::ToHexString($Data, 16, 16)) { return $null }
    try {
        $body = [byte[]]::new($Data.Length - 32)
        [Array]::Copy($Data, 32, $body, 0, $body.Length)
        $plain = $Miio.Aes.DecryptCbc($body, $Miio.Iv, [Security.Cryptography.PaddingMode]::PKCS7)
        [Text.Encoding]::UTF8.GetString($plain).TrimEnd([char]0) | ConvertFrom-Json
    }
    catch { $null }
}

function Send-Miio([string] $Method, $Params = @()) {
    if ($null -eq $Params) { $Params = @() }
    for ($attempt = 0; $attempt -lt 3; $attempt++) {
        if (-not $Miio.Handshaken -or $attempt -gt 0) { Invoke-MiioHandshake }
        $id = ++$Miio.MessageId
        $json = ConvertTo-Json -InputObject ([ordered]@{ id = $id; method = $Method; params = $Params }) -Compress -Depth 10
        $packet = New-MiioPacket ([Text.Encoding]::UTF8.GetBytes($json))
        [void] $Miio.Udp.Send($packet, $packet.Length)

        $clock = [Diagnostics.Stopwatch]::StartNew()
        while ($data = Receive-Miio ($Miio.TimeoutMs - $clock.ElapsedMilliseconds)) {
            $response = Read-MiioPacket $data
            if (-not $response -or $response.id -ne $id) { continue }
            if ($response.PSObject.Properties['error']) {
                throw "${Method}: $($response.error | ConvertTo-Json -Compress -Depth 10)"
            }
            return , $response.result
        }
    }
    throw 'Нѣтъ отвѣта на команду — скорѣе всего, токенъ невѣренъ.'
}

# ───────────────────────────── модель xiaomi.derh.lite ─────────────────────────────

$OnOff = { param($v) if ($v) { 'включено' } else { 'выключено' } }
$Faults = 'нѣтъ', 'бакъ полонъ', 'ошибка датчика температуры и влажности', 'ошибка датчика медной трубки',
          'сбой связи', 'пора почистить фильтръ', 'размораживаніе', 'заклинило двигатель',
          'защита отъ перегрузки', 'мало хладагента'
$Modes = [ordered]@{ smart = 0; sleep = 1; dry = 2 }
$ModeNames = 'умный', 'ночной', 'сушка бѣлья'
$LightLevels = [ordered]@{ off = 0; dim = 1; bright = 2 }
$LightNames = 'выключена', 'тусклая', 'яркая'

function Unit([string] $Suffix) { { param($v) "$v$Suffix" }.GetNewClosure() }
function Lookup([string[]] $Names) { { param($v) if ($v -ge 0 -and $v -lt $Names.Count) { $Names[$v] } else { "? ($v)" } }.GetNewClosure() }

$Props = [ordered]@{
    power           = @{ Siid = 2; Piid = 1; Title = 'Питаніе';                    Show = $OnOff }
    fault           = @{ Siid = 2; Piid = 2; Title = 'Неисправность';              Show = (Lookup $Faults) }
    mode            = @{ Siid = 2; Piid = 3; Title = 'Режимъ';                     Show = (Lookup $ModeNames) }
    target_humidity = @{ Siid = 2; Piid = 5; Title = 'Цѣлевая влажность';          Show = (Unit ' %') }
    humidity        = @{ Siid = 3; Piid = 1; Title = 'Влажность въ комнатѣ';       Show = (Unit ' %') }
    temperature     = @{ Siid = 3; Piid = 2; Title = 'Температура';                Show = (Unit ' °C') }
    alarm           = @{ Siid = 4; Piid = 1; Title = 'Звукъ';                      Show = $OnOff }
    light           = @{ Siid = 5; Piid = 1; Title = 'Подсвѣтка';                  Show = $OnOff }
    light_mode      = @{ Siid = 5; Piid = 2; Title = 'Яркость подсвѣтки';          Show = (Lookup $LightNames) }
    child_lock      = @{ Siid = 6; Piid = 1; Title = 'Блокировка кнопокъ';         Show = $OnOff }
    dry_after_off   = @{ Siid = 7; Piid = 1; Title = 'Просушка послѣ выключенія';  Show = $OnOff }
    dry_left        = @{ Siid = 7; Piid = 2; Title = 'Просушки осталось';          Show = (Unit ' секундъ') }
    warming_up      = @{ Siid = 7; Piid = 3; Title = 'Прогрѣвъ';                   Show = $OnOff }
    delay           = @{ Siid = 8; Piid = 1; Title = 'Таймеръ';                    Show = $OnOff }
    delay_time      = @{ Siid = 8; Piid = 2; Title = 'Таймеръ, длительность';      Show = (Unit ' минутъ') }
    delay_remain    = @{ Siid = 8; Piid = 3; Title = 'Таймеръ, осталось';          Show = (Unit ' минутъ') }
}

function Get-DehumidifierState {
    $names = @($Props.Keys)
    $values = @{}
    for ($i = 0; $i -lt $names.Count; $i += 15) {   # устройства не любятъ длинныхъ запросовъ
        $request = @($names[$i..([Math]::Min($i + 14, $names.Count - 1))] |
            ForEach-Object { [ordered]@{ did = $_; siid = $Props[$_].Siid; piid = $Props[$_].Piid } })
        foreach ($r in (Send-Miio 'get_properties' $request)) {
            if ($r.code -eq 0) { $values[$r.did] = $r.value }
        }
    }
    $state = [ordered]@{}
    foreach ($name in $names) {
        $p = $Props[$name]
        $state[$p.Title] = if ($values.ContainsKey($name)) { & $p.Show $values[$name] } else { '—' }
    }
    [pscustomobject] $state
}

function Set-Dehumidifier([System.Collections.IDictionary] $Values) {
    $request = @($Values.GetEnumerator() | ForEach-Object {
        [ordered]@{ did = $_.Key; siid = $Props[$_.Key].Siid; piid = $Props[$_.Key].Piid; value = $_.Value }
    })
    $failed = @(Send-Miio 'set_properties' $request | Where-Object { $_.code -ne 0 })
    if ($failed) { throw "Устройство отклонило команду: $($failed | ConvertTo-Json -Compress)" }
    Write-Host 'Готово.'
}

# ───────────────────────────── разборъ аргументовъ ─────────────────────────────

function Get-Arg([int] $Index, [string] $What) {
    if ($Arguments.Count -le $Index) { throw "Не хватаетъ аргумента: $What." }
    $Arguments[$Index]
}

function ConvertTo-Switch([string] $Text) {
    switch ($Text.ToLowerInvariant()) {
        { $_ -in 'on', '1', 'true', 'yes', 'вкл', 'да' } { return $true }
        { $_ -in 'off', '0', 'false', 'no', 'выкл', 'нет', 'нѣтъ' } { return $false }
    }
    throw "Ожидалось on или off, получено «$Text»."
}

function ConvertTo-Choice([System.Collections.IDictionary] $Choices, [string] $Text) {
    if (-not $Choices.Contains($Text.ToLowerInvariant())) { throw "Ожидалось одно изъ: $($Choices.Keys -join ', ')." }
    $Choices[$Text.ToLowerInvariant()]
}

function ConvertTo-JsonValue([string] $Text) {
    try { ConvertFrom-Json $Text -NoEnumerate } catch { $Text }
}

# ───────────────────────────── главное ─────────────────────────────

if ($MyInvocation.InvocationName -eq '.') { return }   # подключенъ точкой — только функціи

try {
    $address = if ($Ip) { $Ip } elseif ($saved = Get-AppSetting 'IP') { $saved } else { throw 'Адресъ не заданъ: -Ip или подключись разъ въ оконной программѣ.' }
    $tokenHex = if ($Token) { $Token } elseif ($env:MIIO_TOKEN) { $env:MIIO_TOKEN } else { Get-AppSetting 'Token' }

    if ($Command -eq 'save') {
        if (-not $tokenHex) { throw 'Укажи -Token (и при нуждѣ -Ip).' }
        $path = Set-AppSettings ([ordered]@{ IP = $address; Token = $tokenHex.ToLowerInvariant() })
        Write-Host "Сохранено въ $path"
        return
    }
    if (-not $tokenHex) { throw 'Нуженъ токенъ: -Token, перемѣнная MIIO_TOKEN или «derh.ps1 -Token … save».' }
    Connect-Miio $address $tokenHex

    switch ($Command) {
        'status' { Get-DehumidifierState }
        'watch' {
            $seconds = if ($Arguments) { [int] $Arguments[0] } else { 10 }
            while ($true) {
                Write-Host (Get-Date -Format 'HH:mm:ss')
                Get-DehumidifierState | Format-List | Out-Host
                Start-Sleep -Seconds $seconds
            }
        }
        'on' { Set-Dehumidifier @{ power = $true } }
        'off' { Set-Dehumidifier @{ power = $false } }
        'mode' { Set-Dehumidifier @{ mode = ConvertTo-Choice $Modes (Get-Arg 0 'режимъ smart|sleep|dry') } }
        'humidity' {
            $value = [int] (Get-Arg 0 'влажность 40..70')
            if ($value -lt 40 -or $value -gt 70) { throw 'Цѣлевая влажность — отъ 40 до 70 %.' }
            Set-Dehumidifier @{ target_humidity = $value }
        }
        'light' {
            $level = ConvertTo-Choice $LightLevels (Get-Arg 0 'подсвѣтка off|dim|bright')
            if ($level -eq 0) { Set-Dehumidifier @{ light = $false } }
            else { Set-Dehumidifier ([ordered]@{ light = $true; light_mode = $level }) }
        }
        'sound' { Set-Dehumidifier @{ alarm = ConvertTo-Switch (Get-Arg 0 'on|off') } }
        'lock' { Set-Dehumidifier @{ child_lock = ConvertTo-Switch (Get-Arg 0 'on|off') } }
        'dry-after-off' { Set-Dehumidifier @{ dry_after_off = ConvertTo-Switch (Get-Arg 0 'on|off') } }
        'timer' {
            $text = Get-Arg 0 'минуты или off'
            $minutes = if ($text -eq 'off') { 0 } else { [int] $text }
            if ($minutes -lt 0 -or $minutes -gt 720) { throw 'Таймеръ — отъ 0 до 720 минутъ.' }
            if ($minutes -eq 0) { Set-Dehumidifier @{ delay = $false } }
            else { Set-Dehumidifier ([ordered]@{ delay_time = $minutes; delay = $true }) }
        }
        'reset-filter' {
            Send-Miio 'action' ([ordered]@{ did = 'call-7-3'; siid = 7; aiid = 3; in = @() }) | Out-Null
            Write-Host 'Счётчикъ фильтра сброшенъ.'
        }
        'info' { Send-Miio 'miIO.info' | ConvertTo-Json -Depth 10 }
        'get' {
            $request = @([ordered]@{ did = 'x'; siid = [int](Get-Arg 0 'SIID'); piid = [int](Get-Arg 1 'PIID') })
            Send-Miio 'get_properties' $request | ConvertTo-Json -Compress -Depth 10
        }
        'set' {
            $request = @([ordered]@{
                did = 'x'; siid = [int](Get-Arg 0 'SIID'); piid = [int](Get-Arg 1 'PIID')
                value = ConvertTo-JsonValue (Get-Arg 2 'значеніе')
            })
            Send-Miio 'set_properties' $request | ConvertTo-Json -Compress -Depth 10
        }
        'raw' {
            $params = @()
            if ($Arguments.Count -gt 1) { $params = ConvertFrom-Json $Arguments[1] -NoEnumerate }
            Send-Miio (Get-Arg 0 'методъ') $params | ConvertTo-Json -Depth 10
        }
    }
}
catch {
    Write-Host "Ошибка: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally {
    if ($Miio.Udp) { $Miio.Udp.Dispose() }
    if ($Miio.Aes) { $Miio.Aes.Dispose() }
}
