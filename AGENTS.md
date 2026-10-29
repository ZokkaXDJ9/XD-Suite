# XD Suite

- Die Hauptsuite gibt den Versionsstand vor, an dem gearbeitet wird. Nur tatsächlich geänderte Programme übernehmen diesen Stand. Unveränderte Programme behalten ihre bisherige Version und ihr bereits veröffentlichtes Setup; niemals nur für gleiche Versionsnummern neu bauen oder aktualisieren. Lokale Teststände über Build-Kennungen unterscheiden.
- `XDSuite/XDSuite.csproj` enthält die aktuelle Suite-Version. Eine neue Programmversion muss diesem Arbeitsstand entsprechen; ein unverändertes Programm darf ausdrücklich älter bleiben. Oberfläche, Katalog und Installer müssen jeweils die tatsächliche Programmversion anzeigen.
- Die Suite wird auf `main` gepflegt, Programme auf ihren jeweiligen `app/…`-Branches. Den Konverter lokal testen; ohne ausdrückliche Freigabe weder in `main` übernehmen noch im Live-Katalog oder als GitHub Release veröffentlichen.
- Deutsch und Englisch unterstützen. Texte sachlich halten; keine Sprüche oder persönliche Zuschreibung hinzufügen.
