XD Suite und XD Brenner 0.4.1 für Windows 10/11 (64 Bit).

- Fehlercode 7 beim Update aus AppData behoben. Ein älterer Updatehelfer konnte den bisherigen Ordner als Arbeitsverzeichnis offen halten und dadurch das Verschieben blockieren.
- Das Setup übernimmt die Installation nach Programme, ohne den alten Ordner umbenennen zu müssen. Eigene Dateien und Einstellungen bleiben erhalten. Erst nach erfolgreicher Installation werden die bisherigen Programmdateien bereinigt. Kein Deinstaller wird gestartet.
- Ein unvollständiger Kopierversuch ersetzt keine Zieldatei. Bei einem erneuten Versuch bleiben bereits vorhandene Dateien im Ziel erhalten.
- Der neue Updatehelfer und das Setup starten aus einem unabhängigen Arbeitsverzeichnis. Setupfehler enthalten zusätzlich das Setup-Protokoll.

Geprüft: Der Fehler ließ sich mit gesperrtem altem Arbeitsverzeichnis reproduzieren. Derselbe Test besteht mit der Korrektur. Zusätzlich geprüft: unlesbare Quelldatei, Erhalt der bisherigen Installation nach fehlgeschlagener Vorbereitung, erneuter Versuch, Einstellungen und eigene Dateien, Installationseintrag im neuen Ordner sowie kein Deinstalleraufruf. Die bestehenden Brenner- und Suite-Integrationstests bleiben enthalten.

**Die neue Setup-Datei direkt über die vorhandene Version ausführen. Nicht vorher deinstallieren.** Das Suite-Update kann die neue Version ebenfalls nach Bestätigung installieren.

---

XD Suite and XD Burner 0.4.1 for Windows 10/11 (64-bit).

- Fixed update error 7 when an older helper holds the AppData installation directory as its working directory.
- Transfers the existing installation to Program Files without renaming the locked directory. Settings and personal files are retained. Previous application files are cleaned up only after successful installation; no uninstaller is invoked.
- Copies are staged before being committed. Retries preserve existing destination files.
- New update helpers use an independent working directory and retain an explicit setup log.

Validated with the reproduced directory-lock failure, an unreadable source file, retained old installation, retry, data preservation, updated installation registration and no uninstaller invocation.

Run the new setup over the current version. Do not uninstall first. The installers remain unsigned, so SmartScreen may still warn.
