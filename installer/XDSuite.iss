#define Root AddBackslash(SourcePath) + "..\"
#define ProgramId "{F80CC53F-43F1-4E46-B7F4-61435E75303D}"
#define ProgramFolder "XDSuite"
#define ProgramExe "XDSuite.exe"
[Setup]
AppId={{F80CC53F-43F1-4E46-B7F4-61435E75303D}
AppName=XD Suite
AppVersion=0.5.3
AppPublisher=XD Suite
DefaultDirName={code:DefaultInstallDir}
DefaultGroupName=XD Suite
DisableProgramGroupPage=yes
PrivilegesRequired=admin
UsePreviousAppDir=no
ShowLanguageDialog=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
UninstallDisplayName=XD Suite
UninstallDisplayIcon={app}\XDSuite.exe
OutputDir={#Root}dist
OutputBaseFilename=XD-Suite-Setup-0.5.3
SetupIconFile={#Root}XDSuite\Assets\XDSuite.ico
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=110
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
AppMutex=Local\XDSuite.App
VersionInfoVersion=0.5.3.0
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
Name: "converter"; Description: "{cm:ConverterName}"; Types: full
[Files]
Source: "{#Root}dist\XDSuite\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#Root}dist\XD-Brenner-Setup-0.5.2.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "{#Root}dist\XD-Konverter-Setup-0.5.2.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall
[InstallDelete]
Type: files; Name: "{userprograms}\XD Suite.lnk"
Type: files; Name: "{userdesktop}\XD Suite.lnk"
[Icons]
Name: "{autoprograms}\XD Suite"; Filename: "{app}\XDSuite.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\XD Suite"; Filename: "{app}\XDSuite.exe"; WorkingDir: "{app}"; Tasks: desktopicon
[Run]
Filename: "{app}\XDSuite.exe"; Description: "{cm:LaunchProgram,XD Suite}"; Flags: nowait postinstall skipifsilent
[Messages]
german.WelcomeLabel2=Dieses Setup installiert XD Suite und die ausgewählten Programme.%n%nProgramme können im nächsten Schritt einzeln ausgewählt werden. Bereits vorhandene Installationen werden aktualisiert.%n%nDie Sprache kann später im Programm geändert werden.
english.WelcomeLabel2=This setup installs XD Suite and the selected applications.%n%nSelect the applications to install on the next page. Existing installations will be updated.%n%nThe language can also be changed in the application.
[CustomMessages]
german.CopyInstallError=Die vorhandene Installation konnte nicht übernommen werden.%nPfad: %1%n%2%nDie bisherige Installation bleibt erhalten.
english.CopyInstallError=The existing installation could not be transferred.%nPath: %1%n%2%nThe previous installation has been retained.
german.MoveInstallError=Der bisherige Programmordner konnte nicht nach Programme verschoben werden. Bitte das Programm schließen und erneut versuchen.
english.MoveInstallError=The previous application folder could not be moved to Program Files. Close the application and try again.
german.TypeFull=Vollständige Installation
english.TypeFull=Full installation
german.TypeCustom=Benutzerdefiniert
english.TypeCustom=Custom installation
german.BurnerName=XD Brenner
english.BurnerName=XD Burner
german.ConverterName=XD Konverter
english.ConverterName=XD Converter
german.ToolInstallError=%1 konnte nicht installiert werden. Fehlercode: %2
english.ToolInstallError=%1 could not be installed. Error code: %2

#include "UpgradePaths.iss"
[Code]
function ToolAtLeastInRoot(Root: Integer; Id, ExeName, Required: String): Boolean;
var Path, VersionText: String; InstalledVersion, RequiredVersion: Int64;
begin
  Result := False;
  if not RegQueryStringValue(Root, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\' + Id + '_is1', 'InstallLocation', Path) then Exit;
  if not FileExists(AddBackslash(Path) + ExeName) then Exit;
  if CompareText(ExtractFileDir(RemoveBackslashUnlessRoot(Path)), ExpandConstant('{autopf}\XD Suite')) <> 0 then Exit;
  if not GetPackedVersion(AddBackslash(Path) + ExeName, InstalledVersion) then
  begin
    if not RegQueryStringValue(Root, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\' + Id + '_is1', 'DisplayVersion', VersionText) then Exit;
    if not StrToVersion(VersionText, InstalledVersion) then Exit;
  end;
  if not StrToVersion(Required, RequiredVersion) then Exit;
  Result := ComparePackedVersion(InstalledVersion, RequiredVersion) >= 0;
end;
function ToolAtLeast(Id, ExeName, Required: String): Boolean;
begin
  Result := ToolAtLeastInRoot(HKLM64, Id, ExeName, Required) or ToolAtLeastInRoot(HKLM32, Id, ExeName, Required) or
    ToolAtLeastInRoot(HKCU64, Id, ExeName, Required) or ToolAtLeastInRoot(HKCU32, Id, ExeName, Required);
end;
function ToolNeedsRelocationInRoot(Root: Integer; Id, ExeName: String): Boolean;
var Path, Folder: String;
begin
  Result := False;
  if not RegQueryStringValue(Root, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\' + Id + '_is1', 'InstallLocation', Path) then Exit;
  Path := RemoveBackslashUnlessRoot(Path);
  if not FileExists(AddBackslash(Path) + ExeName) then Exit;
  Folder := ChangeFileExt(ExeName, '');
  Result := (CompareText(Path, ExpandConstant('{autopf}\') + Folder) = 0) or
    ((Pos('\appdata\local\programs\', Lowercase(Path)) > 0) and (CompareText(ExtractFileName(Path), Folder) = 0));
end;
function ToolNeedsRelocation(Id, ExeName: String): Boolean;
begin
  Result := ToolNeedsRelocationInRoot(HKLM64, Id, ExeName) or ToolNeedsRelocationInRoot(HKLM32, Id, ExeName) or
    ToolNeedsRelocationInRoot(HKCU64, Id, ExeName) or ToolNeedsRelocationInRoot(HKCU32, Id, ExeName);
end;
procedure CurStepChanged(CurStep: TSetupStep);
var ExitCode: Integer;
begin
  if CurStep = ssDone then MigrateRegistration;
  if CurStep = ssPostInstall then
  begin
    if (WizardIsComponentSelected('burner') or ToolNeedsRelocation('{B5AF424C-85D1-47D4-AF62-30A5A15C5094}', 'XDBurner.exe')) and not ToolAtLeast('{B5AF424C-85D1-47D4-AF62-30A5A15C5094}', 'XDBurner.exe', '0.5.2') then
    begin
      if not Exec(ExpandConstant('{tmp}\XD-Brenner-Setup-0.5.2.exe'), '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LANG=' + ActiveLanguage, '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
        RaiseException(FmtMessage(CustomMessage('ToolInstallError'), [CustomMessage('BurnerName'), IntToStr(ExitCode)]));
      if (ExitCode <> 0) and (ExitCode <> 3010) then
        RaiseException(FmtMessage(CustomMessage('ToolInstallError'), [CustomMessage('BurnerName'), IntToStr(ExitCode)]));
    end;
    if (WizardIsComponentSelected('converter') or ToolNeedsRelocation('{3D477D64-7020-48F0-A621-66CF2958722B}', 'XDConverter.exe')) and not ToolAtLeast('{3D477D64-7020-48F0-A621-66CF2958722B}', 'XDConverter.exe', '0.5.2') then
    begin
      if not Exec(ExpandConstant('{tmp}\XD-Konverter-Setup-0.5.2.exe'), '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /TASKS="" /LANG=' + ActiveLanguage, '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
        RaiseException(FmtMessage(CustomMessage('ToolInstallError'), [CustomMessage('ConverterName'), IntToStr(ExitCode)]));
      if (ExitCode <> 0) and (ExitCode <> 3010) then
        RaiseException(FmtMessage(CustomMessage('ToolInstallError'), [CustomMessage('ConverterName'), IntToStr(ExitCode)]));
    end;
  end;
end;
