#define Root AddBackslash(SourcePath) + "..\"
#define ProgramId "{F80CC53F-43F1-4E46-B7F4-61435E75303D}"
#define ProgramFolder "XDSuite"
#define ProgramExe "XDSuite.exe"
[Setup]
AppId={{F80CC53F-43F1-4E46-B7F4-61435E75303D}
AppName=XD Suite
AppVersion=0.3.1
AppPublisher=XD Suite
DefaultDirName={code:DefaultInstallDir}
DefaultGroupName=XD Suite
DisableProgramGroupPage=yes
PrivilegesRequired=admin
UsePreviousAppDir=yes
ShowLanguageDialog=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
UninstallDisplayName=XD Suite
UninstallDisplayIcon={app}\XDSuite.exe
OutputDir={#Root}dist
OutputBaseFilename=XD-Suite-Setup-0.3.1
SetupIconFile={#Root}XDSuite\Assets\XDSuite.ico
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=110
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
AppMutex=Local\XDSuite.App
VersionInfoVersion=0.3.1.0
VersionInfoDescription=XD Suite Installation
VersionInfoProductName=XD Suite
LicenseFile={#Root}LICENSE
[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce
[Types]
Name: "full"; Description: "{cm:TypeFull}"
Name: "custom"; Description: "{cm:TypeCustom}"; Flags: iscustom
[Components]
Name: "suite"; Description: "XD Suite"; Types: full custom; Flags: fixed
Name: "burner"; Description: "{cm:BurnerName}"; Types: full
[Files]
Source: "{#Root}dist\XDSuite\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#Root}dist\XD-Brenner-Setup-0.3.1.exe"; DestDir: "{tmp}"; Components: burner; Flags: deleteafterinstall
[Icons]
Name: "{autoprograms}\XD Suite"; Filename: "{app}\XDSuite.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\XD Suite"; Filename: "{app}\XDSuite.exe"; WorkingDir: "{app}"; Tasks: desktopicon
[Run]
Filename: "{app}\XDSuite.exe"; Description: "{cm:LaunchProgram,XD Suite}"; Flags: nowait postinstall skipifsilent
[Messages]
german.WelcomeLabel2=Dieses Setup installiert XD Suite und die ausgewählten Programme.%n%nProgramme können im nächsten Schritt einzeln ausgewählt werden. Bereits vorhandene Installationen werden aktualisiert.%n%nDie Sprache kann später im Programm geändert werden.
english.WelcomeLabel2=This setup installs XD Suite and the selected applications.%n%nSelect the applications to install on the next page. Existing installations will be updated.%n%nThe language can also be changed in the application.
[CustomMessages]
german.TypeFull=Vollständige Installation
english.TypeFull=Full installation
german.TypeCustom=Benutzerdefiniert
english.TypeCustom=Custom installation
german.BurnerName=XD Brenner
english.BurnerName=XD Burner
german.ToolInstallError=XD Brenner konnte nicht installiert werden. Fehlercode: %1
english.ToolInstallError=XD Burner could not be installed. Error code: %1

#include "UpgradePaths.iss"
[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var ExitCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    if WizardIsComponentSelected('burner') then
    begin
      if not Exec(ExpandConstant('{tmp}\XD-Brenner-Setup-0.3.1.exe'), '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LANG=' + ActiveLanguage, '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
        RaiseException(FmtMessage(CustomMessage('ToolInstallError'), [IntToStr(ExitCode)]));
      if (ExitCode <> 0) and (ExitCode <> 3010) then
        RaiseException(FmtMessage(CustomMessage('ToolInstallError'), [IntToStr(ExitCode)]));
    end;
    MigrateRegistration;
  end;
end;
