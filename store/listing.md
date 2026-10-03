# Карточка въ Microsoft Store — тексты для Partner Center

Языкъ карточки — русскій (ru-RU), какъ и языкъ пакета.

## Названіе

Пультъ для осушителя Xiaomi

## Краткое описаніе

Неоффиціальный пультъ для осушителя Xiaomi Smart Dehumidifier Lite по локальной сѣти — безъ Mi Home и безъ облака.

## Описаніе

Управляйте осушителемъ Xiaomi Smart Dehumidifier Lite (xiaomi.derh.lite) съ компьютера напрямую по домашней сѣти: программа говоритъ съ осушителемъ безъ приложенія Mi Home и безъ облака Xiaomi.

Что видно:
• влажность въ комнатѣ, окрашенная по медицинскимъ рекомендаціямъ (40…60 % — норма), и температура;
• неисправности (напримѣръ, «бакъ полонъ»), прогрѣвъ, остатокъ просушки и таймера.

Чѣмъ управлять:
• питаніе и режимъ — умный, ночной, сушка бѣлья;
• цѣлевая влажность — ползункомъ со шкалой;
• подсвѣтка (выключатель и яркость), звукъ и блокировка кнопокъ, просушка послѣ выключенія;
• таймеръ выключенія — въ минутахъ или временемъ;
• сбросъ счётчика фильтра.

Командная строка: «Dehumidifier.exe status», «Dehumidifier.exe mode smart humidity 45» — для сценаріевъ и планировщика.

Для работы нужны IP-адресъ осушителя и его токенъ (32 шестнадцатеричныя цифры). Токенъ можно получить изъ своей учётной записи Mi Home сторонней утилитою Xiaomi-cloud-tokens-extractor.

Программа неоффиціальная и не связана съ Xiaomi. Xiaomi — товарный знакъ Xiaomi Inc.

## Что новаго (1.0.0.0)

Первый выпускъ.

## Особенности (по одной на строку)

Влажность и температура въ комнатѣ
Питаніе, режимъ, цѣлевая влажность
Подсвѣтка, звукъ, блокировка кнопокъ
Таймеръ выключенія
Командная строка для сценаріевъ
Работа безъ облака, только локальная сѣть

## Ключевыя слова

осушитель; Xiaomi; dehumidifier; Mi Home; miIO; MIoT; влажность

## Категорія

Утилиты и инструменты

## Политика конфиденціальности

https://github.com/OZ1/Xiaomi-Smart-Dehumidifier-Lite/blob/master/store/privacy.md

## Сайтъ и поддержка

https://github.com/OZ1/Xiaomi-Smart-Dehumidifier-Lite

## Примѣчанія для провѣрки (Notes for certification) — по-англійски

This is an unofficial local-network remote control for the Xiaomi Smart Dehumidifier Lite (model xiaomi.derh.lite).
Without that device on the local network the app cannot connect: after entering an address and pressing "Подключиться" (Connect) it shows "Устройство … не отвѣчаетъ" (device does not respond). All other UI is disabled until a connection succeeds.
runFullTrust: this is a regular .NET WinForms desktop app; it sends UDP packets (miIO protocol, port 54321) only to the device address entered by the user and stores settings in the user profile. No internet servers are contacted.
The UI uses pre-1918 Russian orthography on purpose.
