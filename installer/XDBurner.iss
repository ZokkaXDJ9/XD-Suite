#define AppVersion "0.3.0"
#define Root AddBackslash(SourcePath) + "..\"
#define ProgramId "{B5AF424C-85D1-47D4-AF62-30A5A15C5094}"
#define ProgramFolder "XDBurner"
#define ProgramExe "XDBurner.exe"

[Setup]
AppId={{B5AF424C-85D1-47D4-AF62-30A5A15C5094}
AppName={cm:BurnerName}
AppVersion={#AppVersion}
AppPublisher=XD Suite
AppComments=CD-Brennprogramm
DefaultDirName={code:DefaultInstallDir}
DefaultGroupName=XD Suite
DisableProgramGroupPage=yes
PrivilegesRequired=admin
UsePreviousAppDir=yes
ShowLanguageDialog=yes
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
VersionInfoVersion=0.3.0.0
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
Name: "{autoprograms}\{cm:BurnerName}"; Filename: "{app}\XDBurner.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\{cm:BurnerName}"; Filename: "{app}\XDBurner.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\XDBurner.exe"; Description: "{cm:LaunchProgram,{cm:BurnerName}}"; Flags: nowait postinstall skipifsilent

[Messages]
german.WelcomeLabel2=Dieses Setup installiert XD Brenner.%n%nFunktionen: Daten-CDs, Audio-CDs, ISO-Dateien und CD-RW-Löschen.%n%nDie Sprache kann später im Programm geändert werden.
english.WelcomeLabel2=This setup installs XD Burner.%n%nFunctions: data CDs, audio CDs, ISO images and CD-RW erasing.%n%nThe language can also be changed in the application.

[CustomMessages]
german.BurnerName=XD Brenner
english.BurnerName=XD Burner

#include "UpgradePaths.iss"
[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then MigrateRegistration;
end;
