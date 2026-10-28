#define Root AddBackslash(SourcePath) + "..\..\..\"
[Setup]
AppId={{D22B76B3-3CED-4977-9E12-86CA4C4D237A}
AppName={cm:ConverterName}
AppVersion=0.1.0
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
OutputBaseFilename=XD-Konverter-Vorschau-Setup-0.1.0
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
Name: "{autoprograms}\{cm:ConverterName}"; Filename: "{app}\XDConverter.Preview.exe"; WorkingDir: "{app}"
[Run]
Filename: "{app}\XDConverter.Preview.exe"; Parameters: "{code:ApplicationLanguage}"; Description: "{cm:LaunchProgram,{cm:ConverterName}}"; Flags: nowait postinstall skipifsilent
[Code]
function ApplicationLanguage(Param: String): String;
begin
  if ActiveLanguage = 'english' then Result := '--english' else Result := '--german';
end;
