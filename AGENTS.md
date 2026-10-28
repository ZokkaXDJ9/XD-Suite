# XD Suite

- Die Hauptsuite bestimmt die gemeinsame Produktversion. Einzelprogramme dürfen ihre Versionsnummer nicht eigenständig erhöhen oder dem Suite-Stand vorauseilen. Erst eine Änderung der Hauptsuite-Version erlaubt eine neue Produktversion der Programme. Lokale Teststände über Build-Kennungen unterscheiden.
- `XDSuite/XDSuite.csproj` enthält die aktuelle Suite-Version. Der Konverter übernimmt sie beim Build automatisch; Oberfläche und Installer müssen denselben Stand anzeigen.
- Die Suite wird auf `main` gepflegt, Programme auf ihren jeweiligen `app/…`-Branches. Den Konverter lokal testen; ohne ausdrückliche Freigabe weder in `main` übernehmen noch im Live-Katalog oder als GitHub Release veröffentlichen.
- Deutsch und Englisch unterstützen. Texte sachlich halten; keine Sprüche oder persönliche Zuschreibung hinzufügen.
