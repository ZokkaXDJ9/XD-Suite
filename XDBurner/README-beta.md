# XD Burner Beta 0.5.3-Beta

Diese Beta verwendet die bestehenden Funktionen des Programms auf einem getrennten Update-Kanal. Sie kann auch neben einer gleich nummerierten stabilen Version installiert werden. Stabile Programme werden weder ersetzt noch neu gebaut. Die Beta erscheint im Beta-Tab und nach der Installation auch unter Installiert.

Installation: `%ProgramFiles%\XD Suite\XDBurnerBeta`. Eigene EXE `XDBurner.Beta.exe`, Installationskennung, Mutex und Einstellungen unter `%LOCALAPPDATA%\XDBurnerBeta`. Deutsch und Englisch werden unterstützt. Weitere Builds desselben Versionsstandes erhalten eine höhere Build-Kennung.

Build: `XDBurner/tools/build-beta.ps1 -ToolsRoot <Suite-Checkout>/.tools`. Manuell testen: Sprache wechseln, Dateien hinzufügen, Ziel und Einstellungen prüfen und anschließend zunächst einen ISO-Export durchführen. Physisches Brennen benötigt ein passendes Laufwerk und einen Rohling.

## English

Standalone beta channel with the existing application features. Stable and beta versions can coexist even at equal numerical versions. Separate executable, installer identity, process mutex, application folder and settings. German and English UI. Beta releases are published from the application branch and do not change the stable catalog or stable installer. Physical disc recording still requires manual hardware testing.
