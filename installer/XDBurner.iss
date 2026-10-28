#define AppVersion "0.2.1"
#define Root AddBackslash(SourcePath) + "..\"

[Setup]
AppId={{B5AF424C-85D1-47D4-AF62-30A5A15C5094}
AppName={cm:BurnerName}
AppVersion={#AppVersion}
AppPublisher=XD Suite
AppComments=Einfach CDs brennen. Frei und ohne Umwege.
DefaultDirName={localappdata}\Programs\XDBurner
DefaultGroupName=XD Suite
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
UninstallDisplayName={cm:BurnerName}
UninstallDisplayIcon={app}\XDBurner.exe
OutputDir={#Root}dist
OutputBaseFilename=XD-Brenner-Setup-{#AppVersion}
SetupIconFile={#Root}XDBurner\Assets\XDBurner.ico
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=110
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
AppMutex=Local\XDBurner.App,Local\KlarBrand.App
VersionInfoVersion=0.2.1.0
VersionInfoDescription=XD Burner Installation
VersionInfoProductName=XD Brenner / XD Burner
VersionInfoCopyright=2026 XD Suite contributors
LicenseFile={#Root}LICENSE

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
Source: "{#Root}dist\XDBurner\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
Type: files; Name: "{app}\KlarBrand.exe"
Type: files; Name: "{userprograms}\KlarBrand.lnk"
Type: files; Name: "{userdesktop}\KlarBrand.lnk"
Type: files; Name: "{userprograms}\XD Brenner.lnk"
Type: files; Name: "{userprograms}\XD Burner.lnk"
Type: files; Name: "{userdesktop}\XD Brenner.lnk"
Type: files; Name: "{userdesktop}\XD Burner.lnk"

[Icons]
Name: "{userprograms}\{cm:BurnerName}"; Filename: "{app}\XDBurner.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\{cm:BurnerName}"; Filename: "{app}\XDBurner.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\XDBurner.exe"; Description: "{cm:LaunchProgram,{cm:BurnerName}}"; Flags: nowait postinstall skipifsilent

[Messages]
german.WelcomeLabel2=XD Brenner hilft dir, Daten-CDs und Audio-CDs zu brennen und CD-RWs wiederzuverwenden.%n%nKostenlos, ohne Konto und unabhängig von XD Suite nutzbar. Die Laufzeit ist enthalten.%n%nDeutsch und Englisch lassen sich jederzeit im Menü wechseln.
english.WelcomeLabel2=XD Burner helps you burn data CDs and audio CDs and reuse CD-RWs.%n%nFree, no account required, and works independently of XD Suite. The runtime is included.%n%nSwitch between German and English at any time in the menu.

[CustomMessages]
german.BurnerName=XD Brenner
english.BurnerName=XD Burner
