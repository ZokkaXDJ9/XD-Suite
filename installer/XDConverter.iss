#define Root AddBackslash(SourcePath) + "..\"
#define AppVersion RemoveFileExt(GetFileVersion(Root + "dist\XDConverter\XDConverter.exe"))
#define ProgramId "{3D477D64-7020-48F0-A621-66CF2958722B}"
#define ProgramFolder "XDConverter"
#define ProgramExe "XDConverter.exe"
[Setup]
AppId={{3D477D64-7020-48F0-A621-66CF2958722B}
AppName={cm:ConverterName}
AppVersion={#AppVersion}
AppPublisher=XD Suite
DefaultDirName={code:DefaultInstallDir}
UsePreviousAppDir=no
DefaultGroupName=XD Suite
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
ShowLanguageDialog=yes
AppMutex=Local\XDConverter.App
OutputDir={#Root}dist
OutputBaseFilename=XD-Konverter-Setup-{#AppVersion}
SetupIconFile={#Root}XDConverter\Assets\XDConverter.ico
WizardStyle=modern
Compression=lzma2/normal
SolidCompression=yes
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\XDConverter.exe
LicenseFile={#Root}LICENSE
[Languages]
Name: german; MessagesFile: compiler:Languages\German.isl
Name: english; MessagesFile: compiler:Default.isl
[Tasks]
Name: desktopicon; Description: {cm:CreateDesktopIcon}; GroupDescription: {cm:AdditionalIcons}; Flags: checkedonce
[CustomMessages]
german.ConverterName=XD Konverter
english.ConverterName=XD Converter
german.CopyInstallError=Die vorhandene Installation konnte nicht übernommen werden.%nPfad: %1%n%2%nDie bisherige Installation bleibt erhalten.
english.CopyInstallError=The existing installation could not be transferred.%nPath: %1%n%2%nThe previous installation has been retained.
german.MoveInstallError=Der bisherige Programmordner konnte nicht verschoben werden. Bitte das Programm schließen und erneut versuchen.
english.MoveInstallError=The previous application folder could not be moved. Close the application and try again.
[Files]
Source: "{#Root}dist\XDConverter\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{autoprograms}\{cm:ConverterName}"; Filename: "{app}\XDConverter.exe"; WorkingDir: "{app}"; IconFilename: "{app}\XDConverter.exe"; IconIndex: 0
Name: "{autodesktop}\{cm:ConverterName}"; Filename: "{app}\XDConverter.exe"; WorkingDir: "{app}"; Tasks: desktopicon; IconFilename: "{app}\XDConverter.exe"; IconIndex: 0
[Run]
Filename: "{app}\XDConverter.exe"; Parameters: "{code:ApplicationLanguage}"; Description: "{cm:LaunchProgram,{cm:ConverterName}}"; Flags: nowait postinstall skipifsilent
#include "UpgradePaths.iss"
[Code]
procedure NotifyShellItem(EventId: Integer; Flags: Cardinal; const Path: String; Unused: NativeInt);
external 'SHChangeNotify@shell32.dll stdcall';
function ApplicationLanguage(Param: String): String;
begin
  if ActiveLanguage = 'english' then Result := '--english' else Result := '--german';
end;
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssDone then
  begin
    MigrateRegistration;
    NotifyShellItem($2000, $0005, ExpandConstant('{app}\XDConverter.exe'), 0);
    NotifyShellItem($2000, $0005, ExpandConstant('{autoprograms}\{cm:ConverterName}.lnk'), 0);
  end;
end;
