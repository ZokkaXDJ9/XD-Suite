# Entwicklung und lokale Tests

Die Versionsnummer der Hauptsuite ist die gemeinsame Produktversion. Einzelprogramme übernehmen diesen Stand und erhalten keine eigenen Versionssprünge. Erst wenn die Version der Hauptsuite erhöht wird, dürfen sich die Produktversionen der Programme ändern. Lokale Teststände werden über eine Build-Kennung unterschieden; sie erzeugen keine neuen öffentlichen Programmversionen.

Der Konverter liest seine Produktversion beim Build aus `XDSuite/XDSuite.csproj`. Oberfläche und Installer übernehmen diese Nummer aus der gebauten Anwendung. Dadurch bleiben lokale Änderungen automatisch auf dem aktuellen Suite-Stand.

| Branch | Zuständigkeit |
| --- | --- |
| `main` | XD Suite, gemeinsame Komponenten und freigegebener Stand der Programme |
| `app/xd-burner` | Entwicklung und Tests von XD Brenner |
| `app/xd-converter` | Entwicklung und Tests von XD Konverter; zunächst nur Audio |

Jeder Programm-Branch enthält das Repository als Arbeitsgrundlage. Änderungen am jeweiligen Programm werden auf seinem Branch entwickelt, gebaut und lokal geprüft. Ein neuer Stand wird erst nach Freigabe nach `main` übernommen und in den Suite-Katalog bzw. ein öffentliches Release aufgenommen.

`tools/publish-release.ps1` verweigert Veröffentlichungen außerhalb von `main`. Ein Push auf einen Programm-Branch erzeugt kein Release und verändert den Live-Katalog nicht. Die vorhandenen Setups werden durch einen Quellcode-Push nicht ersetzt.

Der Audiokonverter wird unter `experimental/XDConverter/` entwickelt. Seine lokalen Builds liegen unter `artifacts/converter-preview/`. Er verwendet einen eigenen Programmnamen, eigene Einstellungen und einen eigenen Installer. Er ist nicht Bestandteil von `build-release.ps1`, der Suite-Installation oder `catalog/catalog.json`.

Branch-Wechsel nur mit geklärtem Arbeitsstand durchführen. Änderungen an der Suite und an gemeinsamen Komponenten werden auf `main` gepflegt; Programm-Branches können diesen freigegebenen Stand anschließend übernehmen.
