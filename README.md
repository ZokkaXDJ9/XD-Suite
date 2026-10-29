# XD Suite

XD Brenner ist ein Windows-Programm zum Brennen von CDs. XD Suite verwaltet und startet die einzeln installierbaren Programme. C# / WPF, MIT-Lizenz. [English documentation](docs/README.en.md).

## Installation und Sprache

- **`dist/XD-Konverter-Setup-0.5.0.exe`** installiert nur XD Konverter.
- **`dist/XD-Brenner-Setup-0.4.1.exe`** installiert nur XD Brenner.
- **`dist/XD-Suite-Setup-0.5.1.exe`** installiert XD Suite und die im Setup ausgewählten Programme. XD Brenner 0.4.1 und XD Konverter 0.5.0 sind enthalten und einzeln abwählbar.

Alle Setups fordern Administratorrechte an und zeigen eine Sprachauswahl für Deutsch und Englisch. Frische Installationen verwenden `%ProgramFiles%\XDBurner` bzw. `XDSuite`. Ältere Installationen unter AppData werden ohne Deinstaller nach Programme übernommen und dort aktualisiert. Auch wenn ein alter Updatehelfer den bisherigen Ordner offen hält, werden vorhandene Dateien übernommen. Die alte Installation bleibt bis zum erfolgreichen Abschluss erhalten; eigene Dateien werden nicht gelöscht. Die benötigte .NET-Laufzeit ist enthalten; auch das auswählbare Brenner-Setup benötigt keinen weiteren Download.

In den Apps wechselt **Sprache / Language → Deutsch / English** die Oberfläche direkt. Die Auswahl wird gespeichert. Die Suite und der Brenner teilen ihre Sprachpräferenz; beim Start des Konverters aus der Suite wird deren aktuelle Sprache übernommen. Dateinamen und eigene Eingaben bleiben erhalten. Windows-Dateiauswahldialoge folgen der Betriebssystemsprache.

In der Suite entfernt **Deinstallieren** das ausgewählte installierte Programm nach einer Rückfrage über dessen eigenen Deinstaller. Ein Abbruch lässt es installiert; danach wird die Anzeige aktualisiert.

Jede App hat eine eigene EXE, einen eigenen Startmenü-Eintrag und einen eigenen Deinstallationseintrag. Sie läuft unabhängig vom Launcher. **Die Deinstallation der Suite entfernt keine einzeln installierten Programme.** Die Programme können auch direkt aus `dist/XDBurner` und `dist/XDSuite` gestartet werden. Die Ordner zusammenhalten; zur Suite gehören auch `catalog.json` und `XDUpdateHelper.exe`.

Die Setups sind derzeit unsigniert. Windows kann deshalb „Unbekannter Herausgeber“ und eine SmartScreen-Warnung anzeigen. Administratorrechte beseitigen diese Warnung nicht. Veröffentlichungswege stehen unter [Signierung und SmartScreen](docs/signing.md).

## Suite und Updates

XD Suite zeigt den Programmkatalog, erkennt installierte Programme und startet sie. Die Ansichten **Alle Programme**, **Installiert**, **Updates** und die Suche verwenden den Katalog. Ein Klick auf **Herunterladen & installieren** lädt das jeweilige Setup von GitHub Releases und prüft Größe und SHA-256 vor der Ausführung. Beschädigte Downloads werden verworfen. Downloads lassen sich abbrechen; eine gestartete Installation wird vollständig abgewartet.

Beim Start prüft XD Suite die neueste stabile GitHub-Version der Suite selbst. Ist sie neuer, fragt ein Nachrichtenfenster nach dem Herunterladen und Installieren. Nach Zustimmung wird das Setup geprüft, die Suite beendet, dasselbe Setup im Programmordner ausgeführt und die Suite wieder gestartet. **Der Updater startet keinen Deinstaller. Einstellungen und einzeln installierte Programme bleiben erhalten.** Das Suite-Update wählt ausschließlich die Suite-Komponente. Wird die Administratorabfrage abgebrochen, startet die vorhandene Suite wieder. Frühere Versionen ohne Updateprüfung müssen einmal manuell mit dem aktuellen Setup aktualisiert werden.

Die fest hinterlegte Quelle ist `ZokkaXDJ9/XD-Suite`. Der Katalog liegt unter:

```text
https://github.com/ZokkaXDJ9/XD-Suite/releases/latest/download/catalog.json
```

Die Suite löst die konkrete Katalogdatei über die GitHub-Release-Schnittstelle auf. Ohne Verbindung bleibt der gebündelte oder zuletzt gültige Katalog verfügbar. Installierte Apps lassen sich weiterhin starten. Der Katalog enthält Sprachtexte, Version, HTTPS-URL, Größe, SHA-256 und Installationskennung, aber keine Shell-Befehle oder Setup-Argumente. Weiterleitungen zu HTTP werden abgewiesen.

## XD Konverter

Eigenständig oder über die Suite verwendbar. Ausgabe als MP3, WAV, FLAC, OGG/Vorbis, M4A/AAC und Opus. Dateien oder Ordner hinzufügen, Zielformat wählen und konvertieren. Alle Audiooptionen stehen auf Automatisch; Detailwerte unter „Weitere Einstellungen“ sind freiwillige Ausnahmen. Originaldateien werden nicht überschrieben. Deutsch und Englisch sind im Menü wählbar.

Das eigene Setup heißt `XD-Konverter-Setup-0.5.0.exe`. Binärdateien liegen unter `Programme\XDConverter`, Einstellungen unter `%LOCALAPPDATA%\XDConverter`. Die lokale Vorschau bleibt getrennt. Die Konvertierung verwendet den in `XDConverter/engine.lock.json` festgelegten LGPL-Build von FFmpeg mit mitgelieferten Lizenz- und Quellhinweisen.

Die Suite legt den aktuellen Arbeitsstand fest. Nur geänderte Programme erhalten dessen Versionsnummer. Der Brenner ist in diesem Release unverändert auf 0.4.1; sein bestehendes Setup wird mit identischer Prüfsumme übernommen und bei einer aktuellen Installation übersprungen.

## XD Brenner

| Bereich | Funktion |
| --- | --- |
| Daten-CD | Dateien und Ordner, Drag-and-drop, ISO 9660 + Joliet, Kapazität einschließlich Dateisystem, freier bzw. überschrittener Platz |
| Audio-CD | MP3/WAV/OGG (Vorbis), Reihenfolge per Ziehen oder ↑/↓, Pausen von 0/1/2/3/5/10 Sekunden, Laufzeit und Überschreitung, bis zu 99 Titel |
| ISO brennen | Daten-ISO auf leere CD-R/CD-RW schreiben |
| ISO speichern | Daten-Zusammenstellung auch ohne Laufwerk als ISO exportieren |
| CD-RW löschen | Schnell oder vollständig, mit Rückfrage |
| Optionen | Automatische oder langsamste Geschwindigkeit, vollständige IMAPI-Datenprüfung für Daten/ISO, optional auswerfen |

Windows IMAPI 2 übernimmt das Brennen. NAudio 2.3.0 und Windows Media Foundation lesen MP3/WAV; NVorbis 0.10.5 liest OGG/Vorbis. Audio wird lokal nach 44,1 kHz / 16 Bit / Stereo umgewandelt.

Für 0/1 Sekunde Pause verwendet Audio Disc-at-once; dafür muss das Laufwerk diese Brennart unterstützen. Ab 2 Sekunden wird Track-at-once verwendet. Titel werden auf mindestens vier Sekunden aufgefüllt. Größere gewählte Pausen ergänzen Stille. Mehrkanalton wird abgewiesen. OGG meint Vorbis, nicht Opus. Kein CD-Text, DVD/Blu-ray, Disc-Kopieren, Multisession, Packet Writing, Overburning, BIN/CUE oder Kopierschutzumgehung. Leere CD-R/CD-RW werden beschrieben und abgeschlossen.

Die Datenkapazität wird nach Änderungen mit einem echten IMAPI-Dateisystem berechnet. Ohne Rohling gilt ein gekennzeichneter 80-Minuten-Richtwert. Vor dem Schreiben wird das Image erneut geprüft. ISO-Export bis zur Kapazität einer üblichen 80-Minuten-CD. Verknüpfungen und Cloud-Platzhalter zuerst lokal kopieren. Eine volle Audio-CD benötigt etwa 850 MB temporären Speicher. Zusammenstellungen werden noch nicht als Projektdatei gespeichert.

## Bauen und prüfen

Voraussetzungen: Windows, .NET SDK 9+, .NET Framework 4.7.2 Targeting Pack für den Updatehelfer und Inno Setup 7.1.0. Endnutzer brauchen diese Entwicklungswerkzeuge nicht. Der Helfer nutzt das in Windows 10/11 vorhandene .NET Framework.

```powershell
.\tools\get-installer-compiler.ps1
.\build-release.ps1
.\tools\test-suite-update.ps1
.\tools\test-setup-components.ps1
.\tools\test-converter-installer.ps1
.\tools\test-installer.ps1
```

Der Compiler wird aus dem offiziellen Inno-Release bezogen und über SHA-256 und Authenticode geprüft. Alternativ `$env:ISCC_PATH` setzen. Die Anwendungen enthalten .NET 8.0.33; vor künftigen Releases die gepinnte Laufzeit aktualisieren. NuGet-Versionen stehen in Lockdateien.

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
- `%LOCALAPPDATA%\XDSuite`: Katalog, Setups, Updatehelfer und Fehlerprotokoll.
- `%LOCALAPPDATA%\XDBurner\Logs`: lokale Fehlerprotokolle.

Die Programmbinärdateien gehören nach Programme; Sprachpräferenz, Katalog und Protokolle bleiben als Benutzerdaten unter AppData. Alte lokale Builds liegen unter `artifacts/legacy-builds/`; `dist/` enthält die aktuellen Setups.

## Quellen und Lizenzen

Quellcode unter [MIT](LICENSE). Hinweise zu Abhängigkeiten stehen unter `licenses/`.

- [Windows IMAPI](https://learn.microsoft.com/en-us/windows/win32/imapi/burning-a-disc)
- [IMAPI Track-at-once](https://learn.microsoft.com/en-us/windows/win32/api/imapi2/nf-imapi2-idiscformat2trackatonce-addaudiotrack)
- [NAudio](https://github.com/naudio/NAudio/tree/v2.3.0), MIT.
- [NVorbis](https://github.com/NVorbis/NVorbis), MIT.
- [.NET](https://github.com/dotnet/runtime), MIT und mitgelieferte Drittanbieterhinweise.
- [Inno Setup](https://jrsoftware.org/), Lizenz unter `licenses/Inno-Setup.txt`.
