#define Root AddBackslash(SourcePath) + "..\..\..\"
#define SuiteVersion RemoveFileExt(GetFileVersion(Root + "artifacts\converter-preview\app\XDConverter.Preview.exe"))
[Setup]
AppId={{D22B76B3-3CED-4977-9E12-86CA4C4D237A}
AppName={cm:ConverterName}
AppVersion={#SuiteVersion}
AppPublisher=XD Suite
DefaultDirName={autopf}\XDConverterPreview
DefaultGroupName={cm:ConverterName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
ShowLanguageDialog=yes
AppMutex=Local\XDConverter.Preview.App
OutputDir={#Root}artifacts\converter-preview
OutputBaseFilename=XD-Konverter-Vorschau-Setup-{#SuiteVersion}
SetupIconFile={#Root}experimental\XDConverter\Assets\XDConverter.ico
WizardStyle=modern
Compression=lzma2/normal
SolidCompression=yes
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\XDConverter.Preview.exe
LicenseFile={#Root}LICENSE
[Languages]
Name: german; MessagesFile: compiler:Languages\German.isl
Name: english; MessagesFile: compiler:Default.isl
[CustomMessages]
german.ConverterName=XD Konverter Vorschau
english.ConverterName=XD Converter Preview
[Files]
Source: "{#Root}artifacts\converter-preview\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{autoprograms}\{cm:ConverterName}"; Filename: "{app}\XDConverter.Preview.exe"; WorkingDir: "{app}"; IconFilename: "{app}\XDConverter.Preview.exe"; IconIndex: 0
[Run]
Filename: "{app}\XDConverter.Preview.exe"; Parameters: "{code:ApplicationLanguage}"; Description: "{cm:LaunchProgram,{cm:ConverterName}}"; Flags: nowait postinstall skipifsilent
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
    NotifyShellItem($2000, $0005, ExpandConstant('{app}\XDConverter.Preview.exe'), 0);
    NotifyShellItem($2000, $0005, ExpandConstant('{autoprograms}\{cm:ConverterName}.lnk'), 0);
  end;
end;
