# Windows SmartScreen und Herausgeber

Die Setups sind derzeit nicht mit Authenticode signiert. Deshalb kann Windows „Unbekannter Herausgeber“ und die SmartScreen-Meldung „Der Computer wurde durch Windows geschützt“ anzeigen. Die Administratoranforderung des Setups ist davon unabhängig.

Microsoft bewertet sowohl die Herausgeberidentität als auch die Reputation einer konkreten Datei. Ein vertrauenswürdiges Codesignaturzertifikat macht den Herausgeber überprüfbar. Auch neu signierte Dateien können zunächst eine SmartScreen-Warnung auslösen. Ein EV-Zertifikat umgeht diese Prüfung nicht automatisch. Selbstsignierte Zertifikate lösen das Problem für fremde Rechner nicht.

Mögliche Wege:

- **Öffentlich vertrauenswürdige Codesignierung**, etwa mit einem eigenen Zertifikat oder Microsoft Artifact Signing. Das erfordert eine Identitätsprüfung und gewöhnlich laufende Kosten. Eine neue Datei startet damit nicht garantiert sofort ohne Warnung.
- **SignPath Foundation** bietet auf Antrag kostenlose Signierung geeigneter Open-Source-Projekte. Die Bedingungen umfassen Lizenz, nachvollziehbaren Buildprozess und überprüfbare Projektreputation. Eine Aufnahme dieses neuen Projekts ist nicht zugesagt. Der sichtbare Zertifikatsinhaber wäre die SignPath Foundation.
- **Microsoft Store**: Laut Microsoft erhalten über den Store verteilte Pakete diese SmartScreen-Downloadwarnung nicht. Dafür wäre eine eigene Store-Veröffentlichung erforderlich.

Es wurde kein Zertifikat gekauft, kein Signierkonto angelegt und kein Antrag an Dritte gesendet. Ein Abschalten von SmartScreen oder Entfernen der Downloadherkunft ist kein Bestandteil der Anwendung oder des Setups.

Quellen, geprüft am 28.10.2026:

- [Microsoft: SmartScreen reputation and signing](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation)
- [SignPath Foundation](https://signpath.org/)
- [SignPath Foundation: Terms and eligibility](https://signpath.org/terms.html)
