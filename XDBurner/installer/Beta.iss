#define Root AddBackslash(SourcePath) + "..\..\"
#define Version "0.5.3"
[Setup]
AppId={{E5E8A34F-0A7B-4563-9CA7-2F6F3A19AD12}
AppName={cm:ProgramName}
AppVersion={#Version}-Beta
AppPublisher=XD Suite
DefaultDirName={autopf}\XD Suite\XDBurnerBeta
DefaultGroupName=XD Suite\Beta
DisableProgramGroupPage=yes
PrivilegesRequired=admin
UsePreviousAppDir=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
ShowLanguageDialog=yes
OutputDir={#Root}artifacts\beta
OutputBaseFilename=XD-Brenner-Setup-{#Version}-Beta
SetupIconFile={#Root}XDBurner\Assets\XDBurner.ico
UninstallDisplayIcon={app}\XDBurner.Beta.exe
AppMutex=Local\XDBurner.Beta.App
VersionInfoVersion=0.5.3.0
VersionInfoProductVersion=0.5.3.0
VersionInfoProductTextVersion=0.5.3-Beta
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
german.ProgramName=XD Brenner Beta
english.ProgramName=XD Burner Beta
[Files]
Source: "{#Root}artifacts\beta\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{autoprograms}\XD Suite\Beta\{cm:ProgramName}"; Filename: "{app}\XDBurner.Beta.exe"; WorkingDir: "{app}"
[Run]
Filename: "{app}\XDBurner.Beta.exe"; Parameters: "{code:ApplicationLanguage}"; Description: "{cm:LaunchProgram,{cm:ProgramName}}"; Flags: nowait postinstall skipifsilent
[Code]
function ApplicationLanguage(Param: String): String;
begin
  if ActiveLanguage = 'english' then Result := '--english' else Result := '--german';
end;
