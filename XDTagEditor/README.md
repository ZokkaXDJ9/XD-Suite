# XD Tag-Editor 0.5.2-Beta

Eigenständiges Testprogramm auf `app/xd-tag-editor`. Kein Eintrag im stabilen Katalog und keine Einbindung in das Suite-Setup. GitHub-Vorabversionen erscheinen ausschließlich im Beta-Tab.

Dateien oder Ordner hinzufügen, Dateien auswählen und rechts Tags bearbeiten. Nur angehakte Felder werden geändert; angehakte leere Felder löschen den bisherigen Wert. Mehrere Interpreten, Albuminterpreten oder Genres mit Semikolon trennen. JPEG-/PNG-Cover hinzufügen oder entfernen. MP3, FLAC, M4A/M4B, OGG/Vorbis, Opus, WAV, AIFF und WMA werden angeboten; die Speichermöglichkeiten hängen vom jeweiligen Format ab. Nicht zuverlässig speicherbare Felder werden vor dem Ersetzen abgewiesen. Die automatisierten Formatprüfungen decken MP3, FLAC, M4A, OGG, Opus und WAV ab; AIFF/WMA benötigen zusätzliche manuelle Prüfung.

Speichern bearbeitet zuerst eine Kopie, liest die Tags zur Kontrolle zurück und ersetzt dann die Datei. Die vorherige Datei bleibt byteidentisch im Unterordner `.xd-tag-backups` neben der Quelldatei erhalten. Außerhalb des Editors geänderte und schreibgeschützte Dateien werden abgewiesen. Bei einer Stapelverarbeitung werden Erfolge und Fehler gezählt; bereits gespeicherte Dateien behalten ihre Sicherung. Keine Audiokonvertierung.

„Aus Dateinamen übernehmen“ erwartet `01 - Interpret - Titel` oder `Interpret - Titel` und zeigt die erkannten Werte vor dem Speichern. Die Umbenennungsvorschau unterstützt `{track}`, `{title}`, `{artist}`, `{album}`, `{disc}` und `{year}`. Namenskonflikte werden vorab abgewiesen.

Installation: `%ProgramFiles%\XD Suite\XDTagEditorBeta`. Eigene Installationskennung, Startmenüeintrag unter XD Suite/Beta und Sprachwahl unter `%LOCALAPPDATA%\XDTagEditorBeta`. Deutsch und Englisch sind direkt umschaltbar. Vor dem Test Sicherungskopien eigener Audiodateien verwenden.

Build: `XDTagEditor/tools/build-beta.ps1 -ToolsRoot <Suite-Checkout>/.tools`. Der vollständige Ordner `artifacts/beta/app` kann ohne Installation gestartet werden. Der Build prüft echte Tag-Rundläufe, unveränderte dekodierte Audiosamples, Cover, Sicherungen, Namenskonflikte und Abbruchpfade; anschließend erzeugt er das unabhängige Beta-Setup und `beta-catalog.json`.

TagLibSharp 2.3.0 wird als separat austauschbare DLL unter LGPL 2.1 ausgeliefert. Lizenz und Quellverweis liegen unter `licenses/`.

## English

Standalone beta tag editor. Add audio files or folders, select files, edit checked fields and save. Checked empty fields clear existing values. Batch edits, JPEG/PNG covers, filename-to-tag import and rename previews are supported. Existing audio is not transcoded. Writes use a verified working copy and retain the previous file in `.xd-tag-backups`. Conflicting filenames, stale snapshots and read-only files are rejected. Format-specific metadata limitations are checked before replacement. Installed separately under Program Files/XD Suite/XDTagEditorBeta with separate language preferences. Stable applications are unaffected.
