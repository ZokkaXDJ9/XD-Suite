#define Root AddBackslash(SourcePath) + "..\..\"
#define Version "0.5.3"
[Setup]
AppId={{2A0CA07B-E61C-49EE-BDC2-18AEEAD30C88}
AppName={cm:ProgramName}
AppVersion={#Version}-Beta
AppPublisher=XD Suite
DefaultDirName={autopf}\XD Suite\XDConverterBeta
DefaultGroupName=XD Suite\Beta
DisableProgramGroupPage=yes
PrivilegesRequired=admin
UsePreviousAppDir=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
ShowLanguageDialog=yes
OutputDir={#Root}artifacts\beta
OutputBaseFilename=XD-Konverter-Setup-{#Version}-Beta
SetupIconFile={#Root}XDConverter\Assets\XDConverter.ico
UninstallDisplayIcon={app}\XDConverter.Beta.exe
AppMutex=Local\XDConverter.Beta.App
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
german.ProgramName=XD Konverter Beta
english.ProgramName=XD Converter Beta
[Files]
Source: "{#Root}artifacts\beta\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{autoprograms}\XD Suite\Beta\{cm:ProgramName}"; Filename: "{app}\XDConverter.Beta.exe"; WorkingDir: "{app}"
[Run]
Filename: "{app}\XDConverter.Beta.exe"; Parameters: "{code:ApplicationLanguage}"; Description: "{cm:LaunchProgram,{cm:ProgramName}}"; Flags: nowait postinstall skipifsilent
[Code]
function ApplicationLanguage(Param: String): String;
begin
  if ActiveLanguage = 'english' then Result := '--english' else Result := '--german';
end;
