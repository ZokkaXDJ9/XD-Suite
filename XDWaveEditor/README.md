# XD Wave Editor – lokale Vorschau

Eigenständiger Wave-Editor für Mono und Stereo, Deutsch und Englisch. Teststand **0.5.1-preview+wave-editor.1**, passend zum aktuellen Arbeitsstand der Suite. Kein Eintrag im Live-Katalog, kein GitHub Release und keine Änderungen an Suite, Brenner oder Konverter.

- MP3, WAV, FLAC, OGG/Vorbis, M4A/AAC, Opus, WMA und AIFF öffnen, auch per Drag-and-drop.
- Wellenform mit Cursor, Bereichsauswahl, Zoom und horizontalem Scrollen.
- Ausschneiden, Kopieren und Einfügen innerhalb der geöffneten Datei; Auswahl löschen oder nur die Auswahl behalten; Rückgängig und Wiederholen. Ctrl+X/C/V, Entf, Ctrl+Z/Y.
- Wiedergabe ab Cursor oder innerhalb der Auswahl; Leertaste pausiert, Escape stoppt.
- Export als neue WAV-Datei (24 Bit), MP3 (VBR), FLAC (24 Bit), OGG, M4A oder Opus. Die Originaldatei kann nicht überschrieben werden. Metadaten und Cover werden noch nicht in Exporte übernommen.

Die Audiodaten liegen während der Bearbeitung als temporäre PCM-Datei auf dem Datenträger. Schnitte und Rückgängig verwenden Schnittlisten. Für die Wiedergabe wird eine temporäre 16-Bit-WAV-Datei erzeugt. Es gibt noch keine Effekte, Aufnahmen, Mehrspurprojekte oder speicherbaren Bearbeitungsprojekte. An Schnittgrenzen gibt es noch keine automatische Überblendung.

## Lokal bauen

```powershell
.\XDWaveEditor\tools\build-preview.ps1
.\XDWaveEditor\tools\test-preview-installer.ps1
```

Die geprüfte, festgelegte FFmpeg-Engine des Konverters wird aus dem Archiv wiederverwendet. Der Build benötigt .NET SDK 9+, Windows und den bereits vorhandenen Inno-Setup-Compiler. `-ToolsRoot` kann einen vorhandenen `.tools`-Ordner angeben. Binärdateien, Engine-Lizenzen und Quellverweise werden zusammen ausgeliefert. Vorschau-Setup unter `artifacts/wave-editor-preview/`, eigenständige EXE unter `app/`. Das Setup verwendet Administratorrechte, Programme\XDWaveEditorPreview, eine eigene Installationskennung und eine deutsche/englische Sprachauswahl.

## English

Standalone mono/stereo wave editor preview with live German/English switching. Select, cut, copy and paste audio within the open file, delete regions, keep only a selected range, undo/redo, zoom and listen. Export to a new WAV, MP3, FLAC, OGG, M4A or Opus file. Source files are protected. Metadata, crossfades, effects and saved editing projects are not supported yet. This preview is isolated from the stable catalog and releases.
