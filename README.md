# XD Suite · XD Brenner / XD Burner

Kostenlose Windows-Programme, die ihren Zweck erfüllen: ohne Konto, Werbung, Trial oder kostenpflichtige Funktionen. C# / WPF, MIT-Lizenz. [English documentation](docs/README.en.md).

## Installieren und Sprache wechseln

Zwei **voneinander unabhängige Installer** für Windows 10/11 (64 Bit):

- **`dist/XD-Brenner-Setup-0.2.1.exe`**: nur XD Brenner installieren. Die App heißt in der englischen Oberfläche **XD Burner**.
- **`dist/XD-Suite-Setup-0.2.1.exe`**: den Launcher installieren. Weitere Programme werden erst auf Wunsch heruntergeladen und installiert.

Jeder Installer enthält die benötigte .NET-Laufzeit. Kein zusätzlicher Download zum Installieren der jeweiligen App, normalerweise keine Administratorrechte. Beide Setups bieten Deutsch und Englisch an.

In **beiden Apps** lässt sich unten links im Menü **Sprache / Language → Deutsch / English** jederzeit die Sprache umschalten. Fenstertexte, Schaltflächen, Dialoge, Hilfetexte und laufende Statusmeldungen wechseln direkt. Die Auswahl wird gespeichert und von beiden Apps beim nächsten Start übernommen. Dateinamen, eigene CD-Namen und andere Eingaben werden nicht übersetzt. Windows-Dateiauswahldialoge verwenden zusätzlich die Sprache des Betriebssystems.

Die Programme können auch direkt aus `dist/XDBurner/XDBurner.exe` beziehungsweise `dist/XDSuite/XDSuite.exe` gestartet werden. Den Ordner bei der Suite zusammenhalten: `catalog.json` gehört dazu.

Die Setups sind noch nicht mit einem eigenen Herausgeberzertifikat signiert. Windows kann deshalb bei heruntergeladenen Dateien einen Herausgeberhinweis anzeigen.

## Die Suite ist optional

XD Suite zeigt einen Programmkatalog, erkennt installierte Programme und startet sie. Die Ansichten **Alle Programme**, **Installiert** und **Updates** sowie die Suche funktionieren für alle Einträge des Katalogs. Neue Programme werden über einen erweiterten Katalog ergänzt; es sind keine Platzhalter-Programme eingebaut.

Ein Klick auf **Herunterladen & installieren** lädt das jeweilige Setup von GitHub Releases, prüft die Dateigröße und die **SHA-256-Prüfsumme** und startet erst dann die Installation. Beschädigte Downloads werden verworfen. Downloads und Katalogabrufe lassen sich abbrechen. Eine bereits gestartete Installation wird vollständig abgewartet.

Jede App hat eine eigene EXE, einen eigenen Startmenü-Eintrag und einen eigenen Deinstallations-Eintrag. Sie benötigt weder den Launcher noch einen Hintergrunddienst. **Die Deinstallation der Suite entfernt keine einzeln installierten Programme.** Es gibt keinen Autostart, kein Benutzerkonto und keine Telemetrie.

Erstes Programm: XD Brenner / XD Burner. Weitere Werkzeuge können nach demselben Muster entstehen.

## XD Brenner

| Bereich | Funktion |
| --- | --- |
| Daten-CD | Dateien und ganze Ordner, Drag-and-drop, ISO 9660 + Joliet für Unicode-Dateinamen, CD abschließen |
| Audio-CD | MP3/WAV, Reihenfolge ändern, 44,1 kHz / 16 Bit / Stereo, bis zu 99 Titel |
| ISO brennen | Daten-ISO auf einen leeren CD-R/CD-RW-Rohling schreiben |
| ISO speichern | Daten-Zusammenstellung auch ohne Brenner als ISO exportieren |
| CD-RW löschen | Schnell oder vollständig, mit Rückfrage |
| Optionen | Automatische oder langsamste verfügbare Geschwindigkeit, vollständige IMAPI-Datenprüfung für Daten/ISO, optional auswerfen |

Windows **IMAPI 2** erledigt die Brennoperationen. **NAudio 2.3.0** und Windows Media Foundation wandeln MP3/WAV lokal um.

Grenzen: nur CD-R/CD-RW; kein DVD/Blu-ray, Disc-Kopieren, Multisession, Packet Writing, Overburning, CD-Text, Gapless oder Kopierschutzumgehung. Audio verwendet Track-at-once mit ungefähr zwei Sekunden Pause und mindestens vier Sekunden pro Titel. Mehrkanalton wird abgewiesen. ISO-Export bis zur Kapazität einer üblichen 80-Minuten-CD; kein BIN/CUE oder reines UDF-/Audio-Abbild. Die Daten-Platzanzeige ist eine Schätzung; vor dem Schreiben wird das echte Image geprüft. Verknüpfungen und Cloud-Platzhalter bitte zuerst lokal kopieren. Für die vollständige Audiovorbereitung werden bei einer vollen CD etwa 850 MB temporärer Speicher benötigt. Zusammenstellungen werden noch nicht als Projektdatei gespeichert.

Quellordner, Projekt und EXE heißen `XDBurner`. Ältere Arbeitsversionen werden über dieselbe Installationskennung aktualisiert; alte EXE und alte Verknüpfungen werden ersetzt. Ältere lokale Builds liegen ausschließlich unter `artifacts/legacy-builds/`; `dist/` enthält die aktuellen Setups.

## GitHub Releases und Katalog

Vorgesehene Quelle: **`ZokkaXDJ9/XD-Suite`**. Die Suite liest den neuesten veröffentlichten Release-Katalog von:

```text
https://github.com/ZokkaXDJ9/XD-Suite/releases/latest/download/catalog.json
```

Unter **Downloadquelle** kann ein anderes öffentliches Repository als `Besitzer/Repository` eingestellt werden. Fehlt die Internetverbindung, bleibt der gebündelte oder zuletzt gültige Katalog erhalten; installierte Apps lassen sich weiterhin starten. Eine unveröffentlichte oder nicht erreichbare Quelle wird entsprechend angezeigt.

Ein Release enthält `XD-Suite-Setup-0.2.1.exe`, `XD-Brenner-Setup-0.2.1.exe`, `catalog.json` und `SHA256SUMS.txt`. Das Manifest enthält pro Programm Sprachtexte, Version, HTTPS-Downloadadresse, Größe, SHA-256, EXE-Namen und Installationskennung. Es enthält keine frei ausführbaren Shell-Befehle oder Setup-Argumente. Pfade und Metadaten werden vor Verwendung geprüft. Weiterleitungen zu unverschlüsseltem HTTP werden abgewiesen.

`build-release.ps1` erstellt zuerst das Brenner-Setup, trägt dessen tatsächliche Größe und Prüfsumme in den Katalog ein und baut anschließend die Suite mit diesem Katalog. Öffentliche Veröffentlichung und Repository-Erstellung sind ein gesonderter Schritt.

## Bauen und prüfen

Voraussetzungen zum Entwickeln: Windows, .NET SDK 9 oder neuer, Inno Setup 7.1.0 zum Erzeugen der Setups. Endnutzer brauchen diese Werkzeuge nicht.

```powershell
.\tools\get-installer-compiler.ps1  # einmalig, falls kein Compiler vorhanden
.\build-release.ps1
.\tools\test-installer.ps1
```

Der Compiler wird vom offiziellen Inno-GitHub-Release bezogen und vor dem Start über SHA-256 und Authenticode geprüft. Ein vorhandener Compiler kann über `$env:ISCC_PATH` gewählt werden. Für dieses private, kostenlose Projekt wird keine kostenpflichtige Compilerlizenz benötigt.

Beide Anwendungen werden mit .NET **8.0.33** als eigenständige Anwendung veröffentlicht. Die Laufzeitversion vor zukünftigen Releases aktualisieren. NuGet-Versionen sind über Lockdateien festgehalten.

Automatische Prüfungen verwenden echtes IMAPI für ISO-Erstellung, Unicode-/Ordnerstrukturen und SHA-256-Vergleich der Inhalte. Hinzu kommen MP3-/WAV-Decodierung, Resampling, Sektorausrichtung, Abbruch- und Quelldatei-Schutz. Die Suite prüft Katalogvalidierung, Download, Hash, Cache, Abbruch und Weiterleitungen mit kontrollierten HTTP-Antworten. Gemeinsame WPF-Tests schalten Sprache und laufende Meldungen in beide Richtungen um und prüfen, dass gebundene Dateinamen und Nutzereingaben unverändert bleiben. Installerprüfungen testen reale Installation, Erkennung, Start, Deinstallation und Unabhängigkeit in Testordnern; vorhandene Benutzerinstallationen werden dabei nicht überschrieben.

**Hier wurde kein CD-Brenner erkannt. Physisches Brennen, tatsächliches Zurücklesen vom gebrannten Medium, Fortschritt/Abbruch am Gerät und CD-RW-Löschen sind noch nicht am Gerät verifiziert.** Bitte zunächst mit CD-RW am Zielgerät prüfen. Kontrollierte Downloadtests ersetzen außerdem keinen Test eines tatsächlich veröffentlichten GitHub-Downloads. Konkrete Berichte und Ansichten liegen unter `artifacts/`.

## Dateien und Daten

- `XDBurner/`: eigenständiges Projekt `XDBurner.csproj`.
- `XDSuite/`: eigenständiger Launcher.
- `XD.Common/`: gemeinsame Übersetzungen und lokale UI-Helfer, in jede App eingebunden.
- `catalog/`: erweiterbarer, zweisprachiger Programmkatalog.
- `installer/`: zwei voneinander unabhängige Inno-Setup-Skripte.
- `%LOCALAPPDATA%\Programs\XDBurner` und `XDSuite`: Standard-Installationsordner.
- `%LOCALAPPDATA%\XD\language.txt`: gemeinsame Sprachpräferenz, kein laufender Dienst.
- `%LOCALAPPDATA%\XDSuite`: Downloadquelle, Katalog-Cache, geprüfte Setups, Fehlerprotokoll.
- `%LOCALAPPDATA%\XDBurner\Logs`: lokale Fehlerprotokolle des Brenners.

## Quellen und Lizenzen

Quellcode unter [MIT](LICENSE). Abhängigkeiten und Hinweise stehen unter `licenses/`.

- [Windows IMAPI](https://learn.microsoft.com/en-us/windows/win32/imapi/burning-a-disc)
- [IMAPI-Audioformat / Track-at-once](https://learn.microsoft.com/en-us/windows/win32/api/imapi2/nf-imapi2-idiscformat2trackatonce-addaudiotrack)
- [NAudio](https://github.com/naudio/NAudio/tree/v2.3.0), MIT.
- [.NET](https://github.com/dotnet/runtime), MIT und mitgelieferte Drittanbieterhinweise.
- [Inno Setup](https://jrsoftware.org/), Lizenz unter `licenses/Inno-Setup.txt`.
