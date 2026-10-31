# Entwicklung und lokale Tests

Die Hauptsuite legt den Versionsstand fest, an dem gearbeitet wird. Nur tatsächlich geänderte Programme übernehmen diesen Stand. Unveränderte Programme behalten ihre Version und ihr bereits veröffentlichtes Setup. Eine Suite-Version löst keinen automatischen Versionssprung anderer Programme aus. Lokale Teststände werden über Build-Kennungen unterschieden.

Aktuell: Suite 0.5.2. Brenner und Konverter erhalten wegen ihrer tatsächlich geänderten Installer ebenfalls 0.5.2. Andere unveränderte Programme behalten ihre Version und ihr ursprüngliches Setup.

| Branch | Zuständigkeit |
| --- | --- |
| `main` | XD Suite, gemeinsame Komponenten und freigegebener Stand der Programme |
| `app/xd-burner` | Entwicklung und Tests von XD Brenner |
| `app/xd-converter` | Entwicklung und Tests von XD Konverter; zunächst nur Audio |

Jeder Programm-Branch enthält das Repository als Arbeitsgrundlage. Änderungen am jeweiligen Programm werden auf seinem Branch entwickelt, gebaut und lokal geprüft. Ein neuer Stand wird erst nach Freigabe nach `main` übernommen und in den Suite-Katalog bzw. ein öffentliches Release aufgenommen.

`tools/publish-release.ps1` verweigert Veröffentlichungen außerhalb von `main`. Ein Push auf einen Programm-Branch erzeugt kein Release und verändert den Live-Katalog nicht. Die vorhandenen Setups werden durch einen Quellcode-Push nicht ersetzt.

Der Audiokonverter liegt unter `XDConverter/` und ist seit Suite 0.5.0 für Katalog und Suite-Setup freigegeben. Er besitzt ein eigenes Setup, Programmsymbol und Einstellungsverzeichnis. Tests auf `app/xd-converter` können weiterhin mit `XDConverter/tools/build-preview.ps1` erfolgen. `PreviewBuild=true` verwendet eine getrennte EXE, Installationskennung, Mutex und Einstellungen; Vorschau-Builds sind kein Teil des veröffentlichten Katalogs.

Branch-Wechsel nur mit geklärtem Arbeitsstand durchführen. Änderungen an der Suite und an gemeinsamen Komponenten werden auf `main` gepflegt; Programm-Branches können diesen freigegebenen Stand anschließend übernehmen.

## Beta-Programme

`app/xd-tag-editor`, `app/xd-cd-ripper` und `app/xd-wave-editor` entwickeln die eigenständigen Beta-Programme. Sie bleiben außerhalb des stabilen Katalogs und Suite-Setups. Versionen entsprechen dem Arbeitsstand der Suite und tragen sichtbar `-Beta`; einzelne Teststände unterscheiden sich zusätzlich durch Build-Metadaten. Eigene EXE, AppId, Mutex und Programmordner verhindern das Ersetzen stabiler Programme.

Im jeweiligen Worktree `tools/build-beta.ps1` unter dem Programmordner ausführen; anschließend `tools/publish-beta.ps1 -Publish`. Der Build prüft die Anwendung, erstellt zweisprachige Ansichten, ein unabhängiges Setup und `artifacts/beta/beta-catalog.json`. Die Veröffentlichung verwendet den jeweiligen Branch-Commit, ein eigenes Tag und ausschließlich eine GitHub-Vorabversion (`prerelease=true`, `make_latest=false`). Nachfolgende Builds erhalten neue Tags und Build-Kennungen. Vorhandene Releases werden nicht überschrieben.

Der Suite-Tab wertet nur veröffentlichte Vorabversionen mit Beta-Katalog aus. Er validiert Branch, Beta-Kennung, getrennte Installationskennung, Größe, SHA-256 und Zugehörigkeit des Setups zum Release. Der stabile Updatekanal verwendet weiterhin ausschließlich die neueste stabile Suite-Version.
