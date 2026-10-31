#define ProgramId "{A17BCC67-3916-4833-ABF1-91A9658AF359}"
#define ProgramFolder "XDWaveEditorBeta"
#define ProgramExe "XDWaveEditor.Beta.exe"
#define LegacyProgramFolder "XDWaveEditorPreview"
#define LegacyProgramExe "XDWaveEditor.Preview.exe"
#define Root AddBackslash(SourcePath) + "..\..\"
#define Version "0.5.2"
[Setup]
AppId={{A17BCC67-3916-4833-ABF1-91A9658AF359}
AppName={cm:ProgramName}
AppVersion={#Version}-Beta
AppPublisher=XD Suite
DefaultDirName={code:DefaultInstallDir}
DefaultGroupName=XD Suite\Beta
DisableProgramGroupPage=yes
PrivilegesRequired=admin
UsePreviousAppDir=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
ShowLanguageDialog=yes
OutputDir={#Root}artifacts\beta
OutputBaseFilename=XD-Wave-Editor-Setup-{#Version}-Beta
SetupIconFile={#Root}XDWaveEditor\Assets\XDWaveEditor.ico
UninstallDisplayIcon={app}\XDWaveEditor.Beta.exe
AppMutex=Local\XDWaveEditor.Beta.App
VersionInfoVersion=0.5.2.0
VersionInfoProductVersion=0.5.2.0
VersionInfoProductTextVersion=0.5.2-Beta
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
LicenseFile={#Root}LICENSE
[Languages]
Name: german; MessagesFile: compiler:Languages\German.isl
Name: english; MessagesFile: compiler:Default.isl
[CustomMessages]
german.ProgramName=XD Wave Editor Beta
english.ProgramName=XD Wave Editor Beta
german.CopyInstallError=Die vorhandene Vorschau konnte nicht übernommen werden.%n%1%n%2
english.CopyInstallError=The previous preview could not be transferred.%n%1%n%2
german.MoveInstallError=Der bisherige Programmordner konnte nicht verschoben werden.
english.MoveInstallError=The previous application folder could not be moved.
[Files]
Source: "{#Root}artifacts\beta\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[InstallDelete]
Type: files; Name: "{app}\XDWaveEditor.Preview.exe"
Type: files; Name: "{app}\XDWaveEditor.Preview.exe.config"
Type: files; Name: "{autoprograms}\XD Wave Editor Vorschau.lnk"
Type: files; Name: "{autoprograms}\XD Wave Editor Preview.lnk"
[Icons]
Name: "{autoprograms}\XD Suite\Beta\{cm:ProgramName}"; Filename: "{app}\XDWaveEditor.Beta.exe"; WorkingDir: "{app}"
[Run]
Filename: "{app}\XDWaveEditor.Beta.exe"; Parameters: "{code:ApplicationLanguage}"; Description: "{cm:LaunchProgram,{cm:ProgramName}}"; Flags: nowait postinstall skipifsilent
#include "..\..\installer\UpgradePaths.iss"
[Code]
function ApplicationLanguage(Param: String): String;
begin
  if ActiveLanguage = 'english' then Result := '--english' else Result := '--german';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssDone then MigrateRegistration;
end;
