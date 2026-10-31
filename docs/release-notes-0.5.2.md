# XD Suite 0.5.2

Der neue Tab **Beta-Releases** zeigt veröffentlichte GitHub-Vorabversionen aus den Programm-Branches. Beta-Pakete tragen `0.5.2-Beta`, werden erst auf Wunsch heruntergeladen und nach Größen-/SHA-256-Prüfung über ein eigenes Setup installiert. Sie ersetzen keine stabilen Programme und sind nicht Teil des stabilen Katalogs oder der Suite-Komponenten. Die erste Beta-Auswahl umfasst Tag-Editor, CD-Ripper und Wave-Editor. Der CD-Ripper benötigt noch manuelle Tests am echten Laufwerk.

Alle Anwendungen verwenden künftig eigene Unterordner unter **Programme\XD Suite**. Das Suite-Update verschiebt die Suite; Updates der einzelnen Programme verschieben diese jeweils selbst. Das Suite-Update übernimmt auch bereits installierte Brenner und Konverter aus den alten Standardordnern über deren eigene neue Installer. Nicht installierte Programme werden nur bei Auswahl hinzugefügt. Vorhandene Dateien werden vor der Installation übernommen; ein Fehler lässt die bisherige Installation erhalten. Bereinigung und Deinstallation betreffen die jeweilige Anwendung. AppData-Einstellungen und unabhängige Programme bleiben erhalten. Es wird kein alter Deinstaller aufgerufen.

Brenner und Konverter erhalten in diesem Release tatsächlich geänderte Installer mit Ordner-Migration und daher **0.5.2**. Unveränderte Programme behalten weiterhin ihre bisherige Version und ihr bestehendes Setup.

## English

The new **Beta releases** tab lists published GitHub prereleases from application branches. Beta builds use `0.5.2-Beta`, download only on request and have separate installers, application folders and identities. They do not replace stable applications and are not included in the stable catalog or suite components. Initial beta applications are Tag Editor, CD Ripper and Wave Editor. Physical CD ripping still requires manual hardware testing.

Applications now install into separate folders beneath **Program Files\XD Suite**. Each application's update migrates its own previous installation without invoking an uninstaller. Existing files are transferred before installation, preparation failures retain the old installation, and AppData settings remain intact. Burner and Converter are updated to 0.5.2 because their installers now implement this migration.
