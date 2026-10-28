# XD Konverter - lokale Audiovorschau

Entwicklung auf `app/xd-converter`. Diese Vorschau gehört nicht zum Live-Katalog oder zum Suite-Setup. Video ist für einen späteren Schritt vorgesehen.

Ausgabe: MP3, WAV (16/24 Bit PCM oder 32 Bit Float), FLAC, OGG/Vorbis, M4A/AAC und Opus. Eingabe unter anderem MP3, WAV, FLAC, OGG, M4A, AAC, Opus, WMA und AIFF. Dateien können einzeln, als Ordner oder per Drag-and-drop hinzugefügt werden. Abtastrate, Kanäle und Qualität sind wählbar. Text-Tags werden optional übernommen; Coverbilder derzeit nicht.

Die Originaldateien werden nicht verändert. Existiert ein Zielname bereits, erhält das neue Ergebnis einen anderen Namen. Ergebnisse werden zuerst temporär geschrieben; Abbruch und Fehler entfernen die unvollständige Datei. Bereits fertige Ergebnisse bleiben erhalten.

## Lokal bauen und testen

Das Programmsymbol liegt als SVG unter `Assets/` vor. `tools/build-icon.ps1` erzeugt daraus das Windows-Icon mit neun Auflösungen und die Grafik für den Fensterkopf. Der Vorschau-Build führt diesen Schritt automatisch aus. EXE, Fenster, Startmenü und Setup verwenden dasselbe Symbol.

Vom Repository-Verzeichnis aus:

```powershell
.\experimental\XDConverter\tools\build-preview.ps1
.\experimental\XDConverter\tools\test-preview-installer.ps1
```

Der Build lädt einen festgelegten [BtbN-LGPL-Build](https://github.com/BtbN/FFmpeg-Builds/releases/tag/autobuild-2026-10-28-19-47), prüft SHA-256 und verwendet FFmpeg/ffprobe als getrennte Prozesse. [FFmpeg nennt BtbN als Anbieter von Windows-Builds](https://ffmpeg.org/download.html). Version, Download und Build-Quellen stehen in `engine.lock.json`. Die Lizenz und die mitgelieferte Dokumentation werden in den Vorschau-Ordner kopiert. Es wird kein öffentlicher Binärdownload für den Konverter veröffentlicht.

- Start ohne Installation: `artifacts/converter-preview/app/XDConverter.Preview.exe`.
- Optionales separates Setup: `artifacts/converter-preview/XD-Konverter-Vorschau-Setup-0.1.1.exe`.
- Testbericht: `artifacts/converter-preview/converter-tests.txt`.
- Prüfung unveränderter Live-Quellen: `artifacts/converter-preview/isolation-check.txt`.
- Isolierter Installer-Test: `artifacts/converter-preview/installer-test.txt` (Elevation nur in der Testkopie ausgeschaltet).

Der vollständige `app`-Ordner einschließlich `engine` muss zusammenbleiben. Der lokale Build enthält die .NET-Laufzeit. Das optionale Vorschau-Setup fordert Administratorrechte an und verwendet `Programme\XDConverterPreview` mit eigener Installationskennung. Einstellungen und Fehlerprotokolle liegen ausschließlich unter `%LOCALAPPDATA%\XDConverterPreview`. Die Sprachwahl ist unabhängig von Suite und Brenner.

Das Programm verwendet keine Online-Konvertierungsdienste und enthält keine Katalog- oder Updateabfragen. Die Vorschau lässt sich parallel zur installierten Suite und zum Brenner verwenden. Ihre Übernahme nach `main` und in ein öffentliches Release erfolgt erst nach Freigabe.

Technische Referenzen: [FFmpeg](https://ffmpeg.org/ffmpeg.html), [ffprobe](https://ffmpeg.org/ffprobe.html). Der C#-Quellcode steht unter der MIT-Lizenz des Repositorys. FFmpeg und seine Bibliotheken haben eigene, im Engine-Ordner enthaltene Lizenzhinweise.
