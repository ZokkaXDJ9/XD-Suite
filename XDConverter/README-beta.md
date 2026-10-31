# XD Converter Beta 0.5.3-Beta

Diese Beta verwendet die bestehenden Funktionen des Programms auf einem getrennten Update-Kanal. Sie kann auch neben einer gleich nummerierten stabilen Version installiert werden. Stabile Programme werden weder ersetzt noch neu gebaut. Die Beta erscheint im Beta-Tab und nach der Installation auch unter Installiert.

Installation: `%ProgramFiles%\XD Suite\XDConverterBeta`. Eigene EXE `XDConverter.Beta.exe`, Installationskennung, Mutex und Einstellungen unter `%LOCALAPPDATA%\XDConverterBeta`. Deutsch und Englisch werden unterstützt. Weitere Builds desselben Versionsstandes erhalten eine höhere Build-Kennung.

Build: `XDConverter/tools/build-beta.ps1 -ToolsRoot <Suite-Checkout>/.tools`. Manuell testen: Sprache wechseln, Dateien hinzufügen, Ziel und Einstellungen prüfen und anschließend eine Testdatei konvertieren. Originaldateien bleiben erhalten.

## English

Standalone beta channel with the existing application features. Stable and beta versions can coexist even at equal numerical versions. Separate executable, installer identity, process mutex, application folder and settings. German and English UI. Beta releases are published from the application branch and do not change the stable catalog or stable installer. Conversion preserves source files.
