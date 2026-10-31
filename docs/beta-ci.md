# Automatische Branch-Betas / Automatic branch betas

Jeder Push auf einen `app/**`-Branch startet `.github/workflows/beta-release.yml`.
Der Branch muss genau eine passende `beta.json` enthalten. Alle Läufe bleiben erhalten;
ein weiterer Push bricht keinen vorherigen Lauf ab. Manuelles Starten auf einem
Programm-Branch ist ebenfalls möglich.

Die CI baut das Programm, führt seine Funktionsprüfungen aus, rendert Deutsch und
Englisch und installiert/deinstalliert das echte Payload in einem isolierten Testordner.
Erst danach veröffentlicht sie Setup und `beta-catalog.json` als GitHub-Vorabversion.
Uploads werden vor der Veröffentlichung nach Größe und SHA-256 geprüft.

Die sichtbare Version lautet zum Beispiel `0.5.4-Beta`. Die vollständige Kennung
`0.5.4-Beta+burner.1000001001` unterscheidet Pushes und Wiederholungen eines Laufs.
Die Build-Nummer ist `1000000000 + GITHUB_RUN_NUMBER * 1000 + GITHUB_RUN_ATTEMPT`.
Jeder Lauf erhält einen eigenen Release-Tag und Setup-Dateinamen. Stable-Releases
werden dadurch nicht überschrieben. Die Suite wählt die höchste Version und Build-Nummer.

Nur die Beta-Icons verwenden Code-Grün (`#39FF14`) als Hintergrund und das farbige
Programmsymbol: Brenner Orange, Konverter Blau, Tag-Editor Violett, CD-Ripper Gelb
und Wave-Editor Türkis. Die Menüleiste liegt immer oben. Die Stable-Artworks bleiben
eigenständig. Fenster, Menüs und Bedienelemente behalten die normalen App-Farben.

Optische Laufwerke, echte Datenträger und hörbare Wiedergabe müssen manuell geprüft werden.

## English

Every push to `app/**` starts an independent Windows build. A matching `beta.json`
selects the application. Functional checks, bilingual rendering and an isolated
installer round trip must pass before an immutable prerelease is published.
Asset sizes and SHA-256 digests are verified before the draft becomes public.
The visible `X.X.X-Beta` version has an additional numeric build identity so updates
also work without increasing the base version. Parallel runs do not cancel each other.
The suite selects version/build order rather than completion order.

Only beta icons use a code green background with a distinct colored app glyph.
Windows, menus and controls retain the normal application colors.
Menus are horizontal at the top. Stable installers remain separate and unchanged.
Physical media, optical drives and audible playback require manual testing.
