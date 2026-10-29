# XD Suite and XD Burner

XD Burner records CDs on Windows. XD Suite manages and launches the individually installable applications. C# / WPF, MIT license.

Install **XD-Brenner-Setup-0.4.1.exe** for the standalone burner, or **XD-Suite-Setup-0.5.0.exe** for the launcher and selectable applications. The suite setup includes XD Burner 0.4.1 and XD Converter 0.5.0; both are selectable. Unchanged applications retain their own version and original installer. Both setups request administrator privileges and offer German and English language selection. New installations use Program Files. Older AppData installations are transferred to Program Files without running an uninstaller, including when an older helper holds the previous folder open. The previous installation remains intact until setup succeeds; personal files are retained. The .NET runtime is included.

Use **Sprache / Language → English / Deutsch** in either app to switch language immediately. Your choice is saved and used by both apps at their next launch. Filenames and user-entered disc names are preserved. Native Windows file pickers follow the operating-system language.

Each app has its own EXE, installer, Start menu shortcut and uninstaller. Removing the suite does not remove other apps. They run independently of the launcher. Keep portable application folders intact, including the suite catalog and update helper.

On startup, the suite checks GitHub for a newer stable suite release and asks whether to download and install it. After verifying size and SHA-256, it exits, runs the same installer against the application folder, and restarts. **The updater never invokes an uninstaller. Settings and independently installed tools are retained.** Self-updates select only the suite component. Cancelling the administrator prompt restarts the existing version. Earlier releases without this feature need one manual installation of the current setup.

XD Burner supports data CDs, MP3/WAV/OGG Vorbis audio CDs, data ISO export/burning and quick/full CD-RW erasure. Rearrange tracks by dragging or with the arrow buttons. Select 0, 1, 2, 3, 5 or 10 seconds between tracks. The capacity display shows the amount free or over capacity; audio uses running time, and data includes actual ISO/Joliet file-system overhead. Without a disc, the display is a labelled standard 80-minute estimate. Windows IMAPI 2 records discs; NAudio, Media Foundation and NVorbis decode audio locally.

Audio uses Disc-at-once for 0/1-second gaps, requiring a compatible drive. Gaps of at least 2 seconds use Track-at-once. Tracks have a four-second minimum; longer gaps add silence. No CD-Text, DVDs/Blu-rays, multisession, copy-protection removal, disc cloning or BIN/CUE support. OGG support covers Vorbis, not Opus. Blank CD-R/CD-RW media are used and finalized. A full audio disc needs about 850 MB of temporary storage. Compilation projects are not persisted yet.

The fixed repository is `ZokkaXDJ9/XD-Suite`. The app uses the latest release's `catalog.json`, with a local fallback when unavailable. Entries specify HTTPS URL, size and SHA-256. Downloads are verified before execution; corrupted files and unsafe redirects are rejected. The suite resolves the current catalog asset through the GitHub release API.

Build on Windows with .NET SDK 9+, the .NET Framework 4.7.2 targeting pack and Inno Setup 7.1.0:

```powershell
.\tools\get-installer-compiler.ps1
.\build-release.ps1
.\tools\test-suite-update.ps1
.\tools\test-setup-components.ps1
.\tools\test-installer.ps1
```

The apps include .NET 8.0.33; update the pinned runtime before future releases. The small update helper uses the .NET Framework provided by Windows 10/11. Output is in `dist/`. Publication is separate from building. Installers are currently unsigned and may trigger SmartScreen. Administrator privileges do not remove that warning. See [signing options](signing.md).

Tests cover real IMAPI ISO creation and hashes, image size, Unicode folders, MP3/WAV/OGG conversion, PCM layout and gaps, cancellation, source-file protection, bilingual WPF text, download validation and update-version selection. The update test runs the actual helper with isolated version 1 and version 2 installers and verifies preserved settings, restart and no uninstaller invocation. Product installer tests require elevation and refuse to overwrite existing installations. **Physical recording, read-back verification on burned media and CD-RW erasure still need a real burner.** Reports and previews are in `artifacts/`.

The source folder, project and executable are named XDBurner. Older versions keep the same installation identity; per-user binaries are relocated to Program Files. Old local builds are under `artifacts/legacy-builds/`; `dist/` contains current installers.

Language preference: `%LOCALAPPDATA%\XD\language.txt`. Suite data: `%LOCALAPPDATA%\XDSuite`. Burner logs: `%LOCALAPPDATA%\XDBurner\Logs`. Licenses: `LICENSE` and `licenses/`.

## XD Converter

Convert audio locally to MP3, WAV, FLAC, OGG/Vorbis, M4A/AAC and Opus. Audio settings default to Automatic; manual overrides are optional under More settings. Source files are retained. Use XD-Konverter-Setup-0.5.0.exe for an independent installation. The stable converter uses Program Files\XDConverter and its own settings in %LOCALAPPDATA%\XDConverter; the local preview remains separate. FFmpeg licensing and the pinned engine source are included.
