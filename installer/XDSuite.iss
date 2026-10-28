#define Root AddBackslash(SourcePath) + "..\"
[Setup]
AppId={{F80CC53F-43F1-4E46-B7F4-61435E75303D}
AppName=XD Suite
AppVersion=0.2.1
AppPublisher=XD Suite
DefaultDirName={localappdata}\Programs\XDSuite
DefaultGroupName=XD Suite
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
UninstallDisplayName=XD Suite
UninstallDisplayIcon={app}\XDSuite.exe
OutputDir={#Root}dist
OutputBaseFilename=XD-Suite-Setup-0.2.1
SetupIconFile={#Root}XDSuite\Assets\XDSuite.ico
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=110
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
AppMutex=Local\XDSuite.App
VersionInfoVersion=0.2.1.0
VersionInfoDescription=XD Suite Installation
VersionInfoProductName=XD Suite
LicenseFile={#Root}LICENSE
[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce
[Files]
Source: "{#Root}dist\XDSuite\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{userprograms}\XD Suite"; Filename: "{app}\XDSuite.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\XD Suite"; Filename: "{app}\XDSuite.exe"; WorkingDir: "{app}"; Tasks: desktopicon
[Run]
Filename: "{app}\XDSuite.exe"; Description: "{cm:LaunchProgram,XD Suite}"; Flags: nowait postinstall skipifsilent
[Messages]
german.WelcomeLabel2=XD Suite ist dein gemeinsamer Werkzeugkasten für kostenlose Programme.%n%nJedes Programm hat einen eigenen Installer und funktioniert auch ohne die Suite. Weitere Programme werden nur auf deinen Wunsch heruntergeladen und installiert.%n%nDeutsch und Englisch lassen sich jederzeit im Menü wechseln.
english.WelcomeLabel2=XD Suite is your home for free, useful apps.%n%nEvery app has its own installer and works without the suite. Additional apps are only downloaded and installed when you choose them.%n%nSwitch between German and English at any time in the menu.
