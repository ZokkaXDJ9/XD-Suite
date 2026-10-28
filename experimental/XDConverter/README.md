# XD Konverter - lokale Audiovorschau

Entwicklung auf `app/xd-converter`. Diese Vorschau gehört nicht zum Live-Katalog oder zum Suite-Setup. Video ist für einen späteren Schritt vorgesehen.

Ausgabe: MP3, WAV, FLAC, OGG/Vorbis, M4A/AAC und Opus. Eingabe unter anderem MP3, WAV, FLAC, OGG, M4A, AAC, Opus, WMA und AIFF. Dateien können einzeln, als Ordner oder per Drag-and-drop hinzugefügt werden. Text-Tags werden optional übernommen; Coverbilder derzeit nicht.

Alle Audioeinstellungen stehen standardmäßig auf **Automatisch**. Für eine normale Konvertierung reichen Dateien, Zielformat und Zielordner. Die optionalen Detailfelder stehen unter „Weitere Einstellungen“. Auch bei einer manuell gewählten Codierung kann Bitrate bzw. VBR-Qualität automatisch bleiben. „Automatisch verwenden“ setzt die Audioeinstellungen des aktuellen Formats zurück; eine sichtbare Zusammenfassung kennzeichnet manuelle Anpassungen.

Die Automatik wird für jede Quelldatei einzeln aufgelöst. MP3/AAC/Vorbis verwenden VBR mit einer ausgewogenen Qualitätsstufe, Opus VBR mit passender Mono-/Stereo-Zielbitrate. Bei manueller CBR-/ABR-Wahl wird eine zur Abtastrate und Kanalzahl passende Bitrate gewählt. WAV erhält Bittiefe und Zahlenformat einer PCM-Quelle; bei Quellen ohne PCM-Bittiefe werden 16 Bit verwendet. FLAC verwendet automatisch die normale Kompressionsstufe. Abtastrate und Kanäle bleiben soweit mit dem Zielformat vereinbar erhalten. Es findet keine inhaltliche Spracherkennung statt; die automatische Opus-Verwendung ist „Musik / allgemein“.

Beim ersten Laden alter Vorschau-Einstellungen werden die früher fest gespeicherten Standardwerte einmalig in Automatisch übersetzt. Abweichende manuelle Werte, Zielordner, Sprache und Tag-Einstellungen bleiben erhalten. Neue manuelle Entscheidungen bleiben auch dann gespeichert, wenn ihr Zahlenwert dem früheren Standard entspricht. Automatisch ist getrennt von den gültigen manuellen Werten MP3 V0, Vorbis Q−1 und FLAC-Stufe 0 gespeichert.

| Format | Einstellungen |
| --- | --- |
| MP3 | CBR, ABR, VBR V0–V9; 8–320 kbit/s; 8–48 kHz |
| M4A / AAC | Mittlere Zielbitrate oder VBR Q1–Q5; 32–512 kbit/s; 7,35–96 kHz |
| OGG / Vorbis | Mittlere Zielbitrate oder VBR Q−1–Q10; 32–448 kbit/s; VBR bis 192 kHz, Zielbitrate bis 48 kHz |
| Opus | CBR, VBR, begrenztes VBR; 6–512 kbit/s; Encoder-Eingang 8/12/16/24/48 kHz; Musik, Sprache oder geringe Verzögerung |
| WAV | 8/16/24/32 Bit PCM, 32/64 Bit Float; 8–192 kHz |
| FLAC | Kompressionsstufe 0–12; automatische oder 16/24 Bit Bittiefe; 8–192 kHz |

Die verfügbaren Bitraten hängen von Abtastrate und Kanalzahl ab. Automatische Kanalwahl bietet Bitraten an, die auch für Mono geeignet sind; für höhere AAC-/Vorbis-/Opus-Bitraten Stereo ausdrücklich wählen. Automatische Abtastrate wählt einen passenden Encoder-Eingang. Opus wird unabhängig von dieser Eingangsrate als 48 kHz ausgelesen. Die VBR-Skalen sind formatspezifisch: bei MP3 bedeutet V0 die höchste Qualität, bei AAC und Vorbis bedeuten höhere Q-Werte höhere Qualität. Mehrkanalquellen werden für die verlustbehafteten Zielformate bei automatischer Kanalwahl auf Stereo gemischt. WAV und FLAC behalten die Quellkanäle bei.

Jedes Zielformat speichert seine eigenen Einstellungen. Alle FLAC-Kompressionsstufen erhalten dieselben PCM-Samples, solange Bittiefe, Abtastrate und Kanalzahl beibehalten werden. Höhere Abtastraten oder Bittiefen stellen verlorene Quelldetails nicht wieder her. [Encoderoptionen in der FFmpeg-Dokumentation](https://ffmpeg.org/ffmpeg-codecs.html).

Die Produktversion folgt automatisch `XDSuite/XDSuite.csproj` (aktuell 0.4.1). Dieser lokale Teststand trägt die Build-Kennung `automatic-defaults`. Einzelprogramme erhalten keine eigenen Versionssprünge.

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
- Optionales separates Setup: `artifacts/converter-preview/XD-Konverter-Vorschau-Setup-0.4.1.exe`.
- Testbericht: `artifacts/converter-preview/converter-tests.txt`.
- Prüfung unveränderter Live-Quellen: `artifacts/converter-preview/isolation-check.txt`.
- Isolierter Installer-Test: `artifacts/converter-preview/installer-test.txt` (Elevation nur in der Testkopie ausgeschaltet).

Der vollständige `app`-Ordner einschließlich `engine` muss zusammenbleiben. Der lokale Build enthält die .NET-Laufzeit. Das optionale Vorschau-Setup fordert Administratorrechte an und verwendet `Programme\XDConverterPreview` mit eigener Installationskennung. Einstellungen und Fehlerprotokolle liegen ausschließlich unter `%LOCALAPPDATA%\XDConverterPreview`. Die Sprachwahl ist unabhängig von Suite und Brenner.

Das Programm verwendet keine Online-Konvertierungsdienste und enthält keine Katalog- oder Updateabfragen. Die Vorschau lässt sich parallel zur installierten Suite und zum Brenner verwenden. Ihre Übernahme nach `main` und in ein öffentliches Release erfolgt erst nach Freigabe.

Technische Referenzen: [FFmpeg](https://ffmpeg.org/ffmpeg.html), [ffprobe](https://ffmpeg.org/ffprobe.html). Der C#-Quellcode steht unter der MIT-Lizenz des Repositorys. FFmpeg und seine Bibliotheken haben eigene, im Engine-Ordner enthaltene Lizenzhinweise.
