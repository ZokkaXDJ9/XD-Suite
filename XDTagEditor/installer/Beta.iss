#define Root AddBackslash(SourcePath) + "..\..\"
#define Version "0.5.2"
[Setup]
AppId={{180FF9A1-26B7-4B1B-A501-9ACD282923F0}
AppName={cm:ProgramName}
AppVersion={#Version}-Beta
AppPublisher=XD Suite
DefaultDirName={autopf}\XD Suite\XDTagEditorBeta
DefaultGroupName=XD Suite\Beta
DisableProgramGroupPage=yes
PrivilegesRequired=admin
UsePreviousAppDir=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
ShowLanguageDialog=yes
OutputDir={#Root}artifacts\beta
OutputBaseFilename=XD-Tag-Editor-Setup-{#Version}-Beta
SetupIconFile={#Root}XDTagEditor\Assets\App.ico
UninstallDisplayIcon={app}\XDTagEditor.Beta.exe
AppMutex=Local\XDTagEditor.Beta.App
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
german.ProgramName=XD Tag-Editor Beta
english.ProgramName=XD Tag Editor Beta
[Files]
Source: "{#Root}artifacts\beta\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{autoprograms}\XD Suite\Beta\{cm:ProgramName}"; Filename: "{app}\XDTagEditor.Beta.exe"; WorkingDir: "{app}"
[Run]
Filename: "{app}\XDTagEditor.Beta.exe"; Parameters: "{code:ApplicationLanguage}"; Description: "{cm:LaunchProgram,{cm:ProgramName}}"; Flags: nowait postinstall skipifsilent
[Code]
function ApplicationLanguage(Param: String): String;
begin
  if ActiveLanguage = 'english' then Result := '--english' else Result := '--german';
end;
