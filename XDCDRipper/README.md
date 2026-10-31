# XD CD-Ripper 0.5.2-Beta

Eigenständiges Testprogramm auf `app/xd-cd-ripper`. Kein Eintrag im stabilen Katalog und keine Einbindung in das Suite-Setup. GitHub-Vorabversionen erscheinen ausschließlich im Beta-Tab.

Audio-CD einlegen, Laufwerk wählen und „CD einlesen“ anklicken. Titel direkt in der Tabelle bearbeiten, gewünschte Titel anhaken und Interpret, Album, Jahr und Genre eintragen. Als FLAC, WAV oder MP3 (128/192/256/320 kbit/s) in den gewählten Zielordner speichern. Vorhandene Dateien werden durch neue Namen erhalten. Keine Online-Titeldatenbank, keine Hintergrunddownloads und keine Kopierschutzumgehung.

CDDA-Sektoren werden über Windows `IOCTL_CDROM_READ_TOC_EX` und `IOCTL_CDROM_RAW_READ` ausgelesen. Unterstützt werden reine Audio-CDs mit 44,1 kHz, 16 Bit und Stereo. Mixed-Mode-, Daten- und Pre-Emphasis-CDs werden in dieser Beta ausdrücklich abgewiesen. Versteckte Titel im ersten Pregap werden nicht ausgelesen. Während des Auslesens wird das Medium gegen normales Auswerfen gesperrt; die Sperre wird nach Erfolg, Abbruch und Fehler freigegeben.

Standardmäßig werden Audioblöcke zweimal gelesen und verglichen. Bei Fehlern folgen Wiederholungen und ein Rückfall auf einzelne Sektoren. Dauerhafte Fehler brechen den aktuellen Titel ab; Fehler werden nicht durch Stille ersetzt. Der Blockvergleich ist keine AccurateRip-Prüfung und keine Garantie für fehlerfreie oder offsetkorrigierte Archivkopien. Die CD-Identität wird vor dem Lauf und nach jedem Titel erneut geprüft.

Abbruch entfernt die unvollständige WAV-/Encoder-Ausgabe; bereits fertige Titel bleiben erhalten. WAV/FLAC erhalten die gelesenen PCM-Samples, MP3 verwendet verlustbehaftete Kompression. Metadaten werden in einem separaten FFmpeg-Prozess geschrieben.

Installation: `%ProgramFiles%\XD Suite\XDCDRipperBeta`. Eigene Installationskennung, Startmenü unter XD Suite/Beta und eigene Sprachwahl unter `%LOCALAPPDATA%\XDCDRipperBeta`. Deutsch und Englisch sind direkt umschaltbar. Die festgelegte LGPL-FFmpeg-Engine, Lizenz- und Quellhinweise werden mitgeliefert.

Build: `XDCDRipper/tools/build-beta.ps1 -ToolsRoot <Suite-Checkout>/.tools`. Der vollständige Ordner `artifacts/beta/app` ist ohne Installation startbar. Automatische Tests prüfen TOC-Grenzen, letzte Sektoren, WAV-Header, PCM-Inhalt, echte WAV/FLAC/MP3-Codierung, Tags, Namenskollisionen, Abbruch und Lesefehler mit simulierten CD-Sektoren.

**Ein physisches CD-Laufwerk ist auf dem Entwicklungsrechner nicht verfügbar. Echtes Auslesen und hörbare Wiedergabe müssen mit der Beta manuell geprüft werden.** Zunächst eine bekannte, unbeschädigte Audio-CD verwenden: Titelanzahl und Laufzeiten vergleichen, einen Titel als WAV auslesen, Anfang und Ende anhören, danach FLAC/MP3 und Abbruch prüfen. Erst nach dieser Prüfung größere Sammlungen einlesen.

## English

Standalone audio CD ripper beta. Read the disc, select tracks, edit titles and album information, then export to WAV, FLAC or MP3. Existing output files are never overwritten. Audio blocks are read twice by default, retried on errors and compared. Persistent errors stop the current track; unfinished files are removed and completed tracks remain. Audio-only CDs are supported; data/mixed-mode and pre-emphasis discs are rejected. No online metadata, AccurateRip, offset correction or hidden-pregap extraction. Windows device access and real optical-drive behavior still require manual hardware testing. Separate installation under Program Files/XD Suite/XDCDRipperBeta; stable applications are unaffected.

References: [Windows RAW_READ_INFO](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntddcdrm/ns-ntddcdrm-__raw_read_info), [CDROM_READ_TOC_EX](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntddcdrm/ns-ntddcdrm-_cdrom_read_toc_ex).
