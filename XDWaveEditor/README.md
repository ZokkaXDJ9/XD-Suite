# XD Wave Editor – lokale Vorschau

Eigenständiger Wave-Editor für Mono und Stereo, Deutsch und Englisch. Teststand **0.5.1-preview+wave-editor.2**, passend zum aktuellen Arbeitsstand der Suite. Kein Eintrag im Live-Katalog, kein GitHub Release und keine Änderungen an Suite, Brenner oder Konverter.

- MP3, WAV, FLAC, OGG/Vorbis, M4A/AAC, Opus, WMA und AIFF öffnen, auch per Drag-and-drop.
- Wellenform mit Cursor, Bereichsauswahl, Zoom und horizontalem Scrollen.
- Ausschneiden, Kopieren und Einfügen innerhalb der geöffneten Datei; Auswahl löschen oder nur die Auswahl behalten; Rückgängig und Wiederholen. Ctrl+X/C/V, Entf, Ctrl+Z/Y.
- Wiedergabe ab Cursor oder innerhalb der Auswahl, auf Wunsch als Schleife; Leertaste pausiert, Escape stoppt.
- Genaue Auswahl über Zeitangaben (Ctrl+G). Weitere Audiodateien am Cursor einfügen (Ctrl+I), mit Anpassung von Abtastrate und Kanalzahl.
- Effekte auf die Auswahl oder die gesamte Datei: Lautstärke, Spitzenpegel-Normalisierung mit optionaler DC-Entfernung, Ein-/Ausblenden, Stummschalten, Rückwärtslauf, Polarität umkehren, Wiederholen, Stille einfügen, Tempo und Tonhöhe, 3-Band-EQ, Hoch-/Tiefpass, Rauschminderung und Kompressor. Alle Schritte sind rückgängig machbar. Standardwerte sind vorgegeben; manuelle Änderungen sind optional.
- Export als neue WAV-Datei (24 Bit), MP3 (VBR), FLAC (24 Bit), OGG, M4A oder Opus. Die Originaldatei kann nicht überschrieben werden. Metadaten und Cover werden noch nicht in Exporte übernommen.

Die Audiodaten liegen während der Bearbeitung als temporäre PCM-Datei auf dem Datenträger. Schnitte und Rückgängig verwenden Schnittlisten. Für die Wiedergabe wird eine temporäre 16-Bit-WAV-Datei erzeugt. Effektmaterial bleibt für Rückgängig/Wiederholen im temporären Speicher auf dem Datenträger erhalten und wird beim Schließen entfernt. Übersteuerung wird bei pegeländernden Effekten zunächst verhindert und muss ausdrücklich zugelassen werden. Es gibt noch keine Aufnahmen, Mehrspurprojekte oder speicherbaren Bearbeitungsprojekte. An Schnittgrenzen gibt es noch keine automatische Überblendung.

## Lokal bauen

```powershell
.\XDWaveEditor\tools\build-preview.ps1
.\XDWaveEditor\tools\test-preview-installer.ps1
```

Die geprüfte, festgelegte FFmpeg-Engine des Konverters wird aus dem Archiv wiederverwendet. Der Build benötigt .NET SDK 9+, Windows und den bereits vorhandenen Inno-Setup-Compiler. `-ToolsRoot` kann einen vorhandenen `.tools`-Ordner angeben. Binärdateien, Engine-Lizenzen und Quellverweise werden zusammen ausgeliefert. Vorschau-Setup unter `artifacts/wave-editor-preview-test2/`, eigenständige EXE unter `app/`. Das Setup verwendet Administratorrechte, Programme\XDWaveEditorPreview, eine eigene Installationskennung und eine deutsche/englische Sprachauswahl.

## English

Standalone mono/stereo wave editor preview with live German/English switching. Select, cut, copy and paste audio within the open file, delete regions, keep only a selected range, undo/redo, zoom and listen. Export to a new WAV, MP3, FLAC, OGG, M4A or Opus file. Source files are protected. Effects include gain, linked peak normalization and optional DC removal, fades, mute, reverse, invert, repeat, silence insertion, tempo, pitch, EQ, high/low-pass, FFT noise reduction and compression. Effects support undo/redo. Precise time selection, looped playback and inserting other audio files are included. Metadata, crossfades and saved editing projects are not supported yet. This preview is isolated from the stable catalog and releases.

## Funktionsauswahl und Grenzen

Die Auswahl orientiert sich an den üblichen Editier- und Effektwerkzeugen im [Audacity-Handbuch](https://manual.audacityteam.org/man/index_of_effects_generators_and_analyzers.html). Die verwendeten Filter sind in der [FFmpeg-Dokumentation](https://ffmpeg.org/ffmpeg-filters.html) beschrieben; der Build verwendet die bereits festgelegte Engine und ihre mitgelieferte Hilfe.

Normalisieren bezieht sich auf den Spitzenpegel, nicht auf LUFS. Rauschminderung zielt auf gleichmäßiges Hintergrundrauschen; starke Eingriffe können Artefakte erzeugen. Tempo und Tonhöhe sind getrennt einstellbar, große Änderungen können den Klang beeinflussen. Importierte Originaldateien werden ebenso vor Überschreiben geschützt wie die zuerst geöffnete Datei. Effekte schreiben zunächst temporäres Material und übernehmen es erst nach erfolgreichem Abschluss; Abbruch und ungültige Werte ändern das Dokument nicht.