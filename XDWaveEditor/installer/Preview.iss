#define Root AddBackslash(SourcePath) + "..\..\"
#define AppVersionText RemoveFileExt(GetFileVersion(Root + "artifacts\wave-editor-preview-test2\app\XDWaveEditor.Preview.exe"))
[Setup]
AppId={{A17BCC67-3916-4833-ABF1-91A9658AF359}
AppName={cm:EditorName}
AppVersion={#AppVersionText}
AppPublisher=XD Suite
DefaultDirName={autopf}\XDWaveEditorPreview
DefaultGroupName={cm:EditorName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
ShowLanguageDialog=yes
AppMutex=Local\XDWaveEditor.Preview.App
OutputDir={#Root}artifacts\wave-editor-preview-test2
OutputBaseFilename=XD-Wave-Editor-Vorschau-Setup-{#AppVersionText}-Test2
SetupIconFile={#Root}XDWaveEditor\Assets\XDWaveEditor.ico
WizardStyle=modern
Compression=lzma2/normal
SolidCompression=yes
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\XDWaveEditor.Preview.exe
LicenseFile={#Root}LICENSE
[Languages]
Name: german; MessagesFile: compiler:Languages\German.isl
Name: english; MessagesFile: compiler:Default.isl
[CustomMessages]
german.EditorName=XD Wave Editor Vorschau
english.EditorName=XD Wave Editor Preview
[Files]
Source: "{#Root}artifacts\wave-editor-preview-test2\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[InstallDelete]
Type: files; Name: "{autoprograms}\XD Wave Editor Vorschau.lnk"
Type: files; Name: "{autoprograms}\XD Wave Editor Preview.lnk"
[Icons]
Name: "{autoprograms}\{cm:EditorName}"; Filename: "{app}\XDWaveEditor.Preview.exe"; WorkingDir: "{app}"; IconFilename: "{app}\XDWaveEditor.Preview.exe"; IconIndex: 0; AppUserModelID: XDSuite.WaveEditor.Preview
[Run]
Filename: "{app}\XDWaveEditor.Preview.exe"; Parameters: "{code:ApplicationLanguage}"; Description: "{cm:LaunchProgram,{cm:EditorName}}"; Flags: nowait postinstall skipifsilent
[Code]
function ApplicationLanguage(Param: String): String;
begin
  if ActiveLanguage = 'english' then Result := '--english' else Result := '--german';
end;
