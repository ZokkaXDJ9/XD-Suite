# XD Suite and XD Burner

Free, independent Windows apps. No account, advertising, trials, subscriptions or paid features. C# / WPF, MIT license.

Install **XD-Brenner-Setup-0.2.0.exe** for the standalone burner, or **XD-Suite-Setup-0.2.0.exe** for the optional launcher. Both installers contain their own .NET runtime and normally require no administrator privileges. Both support German and English.

Use **Sprache / Language → English / Deutsch** in either app to switch language immediately. Your choice is saved and used by both apps at their next launch. Filenames and user-entered disc names are preserved. Native Windows file pickers also follow the operating-system language.

The suite discovers, launches, downloads, verifies and installs individual apps. Each app has its own EXE, installer, Start menu shortcut and uninstaller. Removing the suite does not remove the other apps. No background service or launcher is required to run them.

XD Burner supports data CDs, MP3/WAV audio CDs, data ISO export/burning and quick/full CD-RW erasure. Recording uses Windows IMAPI 2. Audio is converted locally with NAudio and Windows Media Foundation. Audio is track-at-once with approximately two-second gaps. No CD-Text, gapless audio, DVDs/Blu-rays, multisession, copy-protection removal, disc cloning or BIN/CUE support. Blank CD-R/CD-RW media are used and finalized. Data-size estimates exclude file-system overhead, which is checked before writing. A full audio disc needs about 850 MB of temporary storage. Compilation projects are not persisted yet.

The default release repository is `ZokkaXDJ9/XD-Suite`. The app uses the latest published release's `catalog.json`, with a local fallback when unavailable. Each entry specifies HTTPS download URL, size and SHA-256. Downloads are verified before execution; corrupted files and unsafe redirects are rejected. The catalog accepts no shell commands. Change the repository under **Download source**.

Build on Windows with .NET SDK 9+ and Inno Setup 7.1.0:

```powershell
.\tools\get-installer-compiler.ps1
.\build-release.ps1
.\tools\test-installer.ps1
```

The build includes .NET 8.0.33; update the pinned runtime before future releases. Build output is in `dist/`. Installer creation and public release publication are separate steps. The installers are currently unsigned and may trigger Windows publisher notices.

Tests cover real IMAPI ISO creation and content hashes, Unicode folders, MP3/WAV conversion, PCM layout, cancellation, protection of source files and existing ISO targets, bilingual live WPF text, untouched user data, download validation with injected HTTP responses, and real installation/uninstallation. **Physical recording, read-back verification on burned media and CD-RW erasure still require a real burner.** Test with CD-RW on the target machine first. A real GitHub download also requires a published release. Reports and previews are in `artifacts/`.

The old working title KlarBrand is upgraded using the same installation identity. The source folder retains that name for compatibility with the existing workspace; the project and executable are named XDBurner.

All apps run independently. Shared language preference: `%LOCALAPPDATA%\XD\language.txt`. Suite catalog, downloads and settings: `%LOCALAPPDATA%\XDSuite`. Burner logs: `%LOCALAPPDATA%\XDBurner\Logs`. Licenses: `LICENSE` and `licenses/`.
