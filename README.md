# XD Suite · XD Brenner / XD Burner

XD Brenner ist ein Windows-Programm zum Brennen von CDs. XD Suite verwaltet und startet die einzeln installierbaren Programme. C# / WPF, MIT-Lizenz. [English documentation](docs/README.en.md).

## Installation und Sprache

- **`dist/XD-Brenner-Setup-0.3.0.exe`** installiert nur XD Brenner.
- **`dist/XD-Suite-Setup-0.3.0.exe`** installiert XD Suite und die im Setup ausgewählten Programme. XD Brenner ist enthalten und einzeln abwählbar.

Beide Setups fordern Administratorrechte an und zeigen eine Sprachauswahl für Deutsch und Englisch. Frische Installationen verwenden `%ProgramFiles%\XDBurner` bzw. `XDSuite`. Vorhandene Installationen werden im bisherigen Ordner aktualisiert. Die benötigte .NET-Laufzeit ist enthalten; auch das auswählbare Brenner-Setup benötigt keinen weiteren Download.

In beiden Apps wechselt **Sprache / Language → Deutsch / English** die Oberfläche direkt. Die Auswahl wird gespeichert und von beiden Apps beim nächsten Start übernommen. Dateinamen und eigene Eingaben bleiben erhalten. Windows-Dateiauswahldialoge folgen der Betriebssystemsprache.

Jede App hat eine eigene EXE, einen eigenen Startmenü-Eintrag und einen eigenen Deinstallationseintrag. Sie läuft unabhängig vom Launcher. **Die Deinstallation der Suite entfernt keine einzeln installierten Programme.** Die Programme können auch direkt aus `dist/XDBurner` und `dist/XDSuite` gestartet werden. Die Ordner zusammenhalten; zur Suite gehören auch `catalog.json` und `XDUpdateHelper.exe`.

Die Setups sind derzeit unsigniert. Windows kann deshalb „Unbekannter Herausgeber“ und eine SmartScreen-Warnung anzeigen. Administratorrechte beseitigen diese Warnung nicht. Veröffentlichungswege stehen unter [Signierung und SmartScreen](docs/signing.md).

## Suite und Updates

XD Suite zeigt den Programmkatalog, erkennt installierte Programme und startet sie. Die Ansichten **Alle Programme**, **Installiert**, **Updates** und die Suche verwenden den Katalog. Ein Klick auf **Herunterladen & installieren** lädt das jeweilige Setup von GitHub Releases und prüft Größe und SHA-256 vor der Ausführung. Beschädigte Downloads werden verworfen. Downloads lassen sich abbrechen; eine gestartete Installation wird vollständig abgewartet.

Beim Start prüft XD Suite die neueste stabile GitHub-Version der Suite selbst. Ist sie neuer, fragt ein Nachrichtenfenster nach dem Herunterladen und Installieren. Nach Zustimmung wird das Setup geprüft, die Suite beendet, dasselbe Setup im bisherigen Ordner ausgeführt und die Suite wieder gestartet. **Der Updater startet keinen Deinstaller. Einstellungen und einzeln installierte Programme bleiben erhalten.** Das Suite-Update wählt ausschließlich die Suite-Komponente. Wird die Administratorabfrage abgebrochen, startet die vorhandene Suite wieder. Frühere Versionen ohne Updateprüfung müssen einmal manuell mit dem aktuellen Setup aktualisiert werden.

Vorgesehene Quelle ist `ZokkaXDJ9/XD-Suite`. Der Katalog liegt unter:

```text
https://github.com/ZokkaXDJ9/XD-Suite/releases/latest/download/catalog.json
```

Unter **Downloadquelle** lässt sich ein anderes öffentliches Repository als `Besitzer/Repository` einstellen. Ohne Verbindung bleibt der gebündelte oder zuletzt gültige Katalog verfügbar. Installierte Apps lassen sich weiterhin starten. Der Katalog enthält Sprachtexte, Version, HTTPS-URL, Größe, SHA-256 und Installationskennung, aber keine Shell-Befehle oder Setup-Argumente. Weiterleitungen zu HTTP werden abgewiesen.

## XD Brenner

| Bereich | Funktion |
| --- | --- |
| Daten-CD | Dateien und Ordner, Drag-and-drop, ISO 9660 + Joliet, Kapazität einschließlich Dateisystem, freier bzw. überschrittener Platz |
| Audio-CD | MP3/WAV/OGG (Vorbis), Reihenfolge per Ziehen oder ↑/↓, Pausen von 2/3/5/10 Sekunden, Laufzeit und Überschreitung, bis zu 99 Titel |
| ISO brennen | Daten-ISO auf leere CD-R/CD-RW schreiben |
| ISO speichern | Daten-Zusammenstellung auch ohne Laufwerk als ISO exportieren |
| CD-RW löschen | Schnell oder vollständig, mit Rückfrage |
| Optionen | Automatische oder langsamste Geschwindigkeit, vollständige IMAPI-Datenprüfung für Daten/ISO, optional auswerfen |

Windows IMAPI 2 übernimmt das Brennen. NAudio 2.3.0 und Windows Media Foundation lesen MP3/WAV; NVorbis 0.10.5 liest OGG/Vorbis. Audio wird lokal nach 44,1 kHz / 16 Bit / Stereo umgewandelt.

Audio verwendet Track-at-once mit mindestens zwei Sekunden Pause und mindestens vier Sekunden pro Titel. Größere gewählte Pausen ergänzen Stille. Mehrkanalton wird abgewiesen. OGG meint Vorbis, nicht Opus. Kein CD-Text, Gapless, DVD/Blu-ray, Disc-Kopieren, Multisession, Packet Writing, Overburning, BIN/CUE oder Kopierschutzumgehung. Leere CD-R/CD-RW werden beschrieben und abgeschlossen.

Die Datenkapazität wird nach Änderungen mit einem echten IMAPI-Dateisystem berechnet. Ohne Rohling gilt ein gekennzeichneter 80-Minuten-Richtwert. Vor dem Schreiben wird das Image erneut geprüft. ISO-Export bis zur Kapazität einer üblichen 80-Minuten-CD. Verknüpfungen und Cloud-Platzhalter zuerst lokal kopieren. Eine volle Audio-CD benötigt etwa 850 MB temporären Speicher. Zusammenstellungen werden noch nicht als Projektdatei gespeichert.

## Bauen und prüfen

Voraussetzungen: Windows, .NET SDK 9+, .NET Framework 4.7.2 Targeting Pack für den Updatehelfer und Inno Setup 7.1.0. Endnutzer brauchen diese Entwicklungswerkzeuge nicht. Der Helfer nutzt das in Windows 10/11 vorhandene .NET Framework.

```powershell
.\tools\get-installer-compiler.ps1
.\build-release.ps1
.\tools\test-suite-update.ps1
.\tools\test-setup-components.ps1
.\tools\test-installer.ps1
```

Der Compiler wird aus dem offiziellen Inno-Release bezogen und über SHA-256 und Authenticode geprüft. Alternativ `$env:ISCC_PATH` setzen. Beide Anwendungen enthalten .NET 8.0.33; vor künftigen Releases die gepinnte Laufzeit aktualisieren. NuGet-Versionen stehen in Lockdateien.

Der Build erstellt zuerst den Brenner und sein Setup, trägt Größe und Prüfsumme in den Katalog ein und baut die Suite einschließlich Updatehelfer und Brenner-Setup. Ein Release enthält beide Setups, `catalog.json` und `SHA256SUMS.txt`. Veröffentlichung erfolgt separat mit `tools/publish-release.ps1`.

Tests verwenden echtes IMAPI für ISO-Erstellung, Unicode-Strukturen, Größenberechnung und Inhalts-Hashes. Hinzu kommen MP3/WAV/OGG-Decodierung, Resampling, Pausen, Sektorausrichtung, Abbruch und Quelldateischutz. Die Suite prüft Updateversionen, Katalog, Download, Hash, Cache und Weiterleitungen mit kontrollierten HTTP-Antworten. WPF-Tests prüfen Sprachwechsel und unveränderte Nutzereingaben. Der echte Updatehelfer wird mit zwei isolierten Testinstallern geprüft: Versionswechsel im selben Ordner, Neustart, unveränderte Einstellungen und kein Deinstalleraufruf. Der Komponententest verwendet die produktiven Setup-Skripte mit isolierten Kennungen, kleinen Testdateien und ohne Elevation. Er prüft Auswahl, enthaltenes Brenner-Setup, Sprachweitergabe und getrennte Deinstallation. Produkt-Installerprüfungen benötigen Administratorrechte und verweigern den Lauf bei vorhandenen Installationen.

**Hier wurde kein CD-Brenner erkannt. Physisches Brennen, Zurücklesen vom gebrannten Medium, Fortschritt/Abbruch am Gerät und CD-RW-Löschen sind noch nicht am Gerät verifiziert.** Zunächst mit CD-RW am Zielgerät prüfen. Berichte und gerenderte Ansichten liegen unter `artifacts/`.

## Dateien und Daten

- `XDBurner/`: eigenständiger Brenner.
- `XDSuite/`: eigenständiger Launcher.
- `XD.Common/`: gemeinsame Übersetzungen, in jede App eingebunden.
- `XD.UpdateHelper/`: führt das Update nach dem Ende der Suite aus und startet sie wieder.
- `catalog/`: zweisprachiger Programmkatalog.
- `installer/`: unabhängige Setups mit stabilen Installationskennungen.
- `%LOCALAPPDATA%\XD\language.txt`: gemeinsame Sprachpräferenz.
- `%LOCALAPPDATA%\XDSuite`: Downloadquelle, Katalog, Setups, Updatehelfer und Fehlerprotokoll.
- `%LOCALAPPDATA%\XDBurner\Logs`: lokale Fehlerprotokolle.

Ältere Installationen behalten ihren Ordner. Alte lokale Builds liegen unter `artifacts/legacy-builds/`; `dist/` enthält die aktuellen Setups.

## Quellen und Lizenzen

Quellcode unter [MIT](LICENSE). Hinweise zu Abhängigkeiten stehen unter `licenses/`.

- [Windows IMAPI](https://learn.microsoft.com/en-us/windows/win32/imapi/burning-a-disc)
- [IMAPI Track-at-once](https://learn.microsoft.com/en-us/windows/win32/api/imapi2/nf-imapi2-idiscformat2trackatonce-addaudiotrack)
- [NAudio](https://github.com/naudio/NAudio/tree/v2.3.0), MIT.
- [NVorbis](https://github.com/NVorbis/NVorbis), MIT.
- [.NET](https://github.com/dotnet/runtime), MIT und mitgelieferte Drittanbieterhinweise.
- [Inno Setup](https://jrsoftware.org/), Lizenz unter `licenses/Inno-Setup.txt`.
