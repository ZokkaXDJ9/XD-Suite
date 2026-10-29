# Entwicklung und lokale Tests

Die Hauptsuite legt den Versionsstand fest, an dem gearbeitet wird. Nur tatsächlich geänderte Programme übernehmen diesen Stand. Unveränderte Programme behalten ihre Version und ihr bereits veröffentlichtes Setup. Eine Suite-Version löst keinen automatischen Versionssprung anderer Programme aus. Lokale Teststände werden über Build-Kennungen unterschieden.

Aktuell: Suite 0.5.0, Konverter 0.5.0, Brenner unverändert 0.4.1. Der Build verwendet das originale Brenner-Setup samt bisheriger Prüfsumme und Downloadadresse. Bereits aktuelle Tools werden vom Suite-Setup übersprungen.

| Branch | Zuständigkeit |
| --- | --- |
| `main` | XD Suite, gemeinsame Komponenten und freigegebener Stand der Programme |
| `app/xd-burner` | Entwicklung und Tests von XD Brenner |
| `app/xd-converter` | Entwicklung und Tests von XD Konverter; zunächst nur Audio |

Jeder Programm-Branch enthält das Repository als Arbeitsgrundlage. Änderungen am jeweiligen Programm werden auf seinem Branch entwickelt, gebaut und lokal geprüft. Ein neuer Stand wird erst nach Freigabe nach `main` übernommen und in den Suite-Katalog bzw. ein öffentliches Release aufgenommen.

`tools/publish-release.ps1` verweigert Veröffentlichungen außerhalb von `main`. Ein Push auf einen Programm-Branch erzeugt kein Release und verändert den Live-Katalog nicht. Die vorhandenen Setups werden durch einen Quellcode-Push nicht ersetzt.

Der Audiokonverter liegt unter `XDConverter/` und ist seit Suite 0.5.0 für Katalog und Suite-Setup freigegeben. Er besitzt ein eigenes Setup, Programmsymbol und Einstellungsverzeichnis. Tests auf `app/xd-converter` können weiterhin mit `XDConverter/tools/build-preview.ps1` erfolgen. `PreviewBuild=true` verwendet eine getrennte EXE, Installationskennung, Mutex und Einstellungen; Vorschau-Builds sind kein Teil des veröffentlichten Katalogs.

Branch-Wechsel nur mit geklärtem Arbeitsstand durchführen. Änderungen an der Suite und an gemeinsamen Komponenten werden auf `main` gepflegt; Programm-Branches können diesen freigegebenen Stand anschließend übernehmen.
