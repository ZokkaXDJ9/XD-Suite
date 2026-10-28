[Code]
var LegacyPaths: TArrayOfString;

function IsLegacyDir(Path: String): Boolean;
begin
  Path := RemoveBackslashUnlessRoot(Path);
  Result := (CompareText(ExtractFileName(Path), '{#ProgramFolder}') = 0) and
    (Pos('\appdata\local\programs\', Lowercase(Path)) > 0) and
    (Pos('..', Path) = 0);
end;

function DefaultInstallDir(Param: String): String;
begin
  Result := ExpandConstant('{autopf}\{#ProgramFolder}');
end;

procedure RememberLegacy(Root: Integer);
var Path: String; I: Integer;
begin
  if RegQueryStringValue(Root, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#ProgramId}_is1', 'InstallLocation', Path) and IsLegacyDir(Path) then
  begin
    Path := RemoveBackslashUnlessRoot(Path);
    for I := 0 to GetArrayLength(LegacyPaths) - 1 do
      if CompareText(LegacyPaths[I], Path) = 0 then Exit;
    SetArrayLength(LegacyPaths, GetArrayLength(LegacyPaths) + 1);
    LegacyPaths[GetArrayLength(LegacyPaths) - 1] := Path;
  end;
end;

function InitializeSetup: Boolean;
begin
  RememberLegacy(HKLM64); RememberLegacy(HKLM32);
  RememberLegacy(HKCU64); RememberLegacy(HKCU32);
  Result := True;
end;

procedure InitializeWizard;
begin
  { Also correct /DIR supplied by an older update helper. }
  if IsLegacyDir(WizardDirValue) then WizardForm.DirEdit.Text := DefaultInstallDir('');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var I: Integer; Target: String;
begin
  Result := '';
  Target := RemoveBackslashUnlessRoot(ExpandConstant('{app}'));
  for I := 0 to GetArrayLength(LegacyPaths) - 1 do
    if DirExists(LegacyPaths[I]) and (CompareText(LegacyPaths[I], Target) <> 0) and not DirExists(Target) then
    begin
      { Relocate the existing installation, including its log and any user files.
        The normal setup then updates it. No uninstaller is executed. }
      if not RenameFile(LegacyPaths[I], Target) then
      begin
        Result := CustomMessage('MoveInstallError');
        Exit;
      end;
    end;
end;

procedure RemoveOwnedLegacyFiles(Path: String);
var Search: TFindRec;
begin
  if not IsLegacyDir(Path) then Exit;
  DeleteFile(AddBackslash(Path) + '{#ProgramExe}');
  DeleteFile(AddBackslash(Path) + 'XDUpdateHelper.exe');
  DeleteFile(AddBackslash(Path) + 'XDUpdateHelper.exe.config');
  DeleteFile(AddBackslash(Path) + 'catalog.json');
  DeleteFile(AddBackslash(Path) + 'README.md');
  DeleteFile(AddBackslash(Path) + 'LICENSE');
  if FindFirst(AddBackslash(Path) + 'unins*.*', Search) then
  try
    repeat
      if (Search.Attributes and FILE_ATTRIBUTE_DIRECTORY = 0) and
        ((CompareText(ExtractFileExt(Search.Name), '.exe') = 0) or (CompareText(ExtractFileExt(Search.Name), '.dat') = 0) or (CompareText(ExtractFileExt(Search.Name), '.msg') = 0)) then DeleteFile(AddBackslash(Path) + Search.Name);
    until not FindNext(Search);
  finally FindClose(Search); end;
  if FindFirst(ExpandConstant('{app}\licenses\*'), Search) then
  try
    repeat
      if Search.Attributes and FILE_ATTRIBUTE_DIRECTORY = 0 then DeleteFile(AddBackslash(Path) + 'licenses\' + Search.Name);
    until not FindNext(Search);
  finally FindClose(Search); end;
  RemoveDir(AddBackslash(Path) + 'licenses');
  RemoveDir(Path); { Fails harmlessly if unrelated files remain. }
end;

procedure MigrateRegistration;
var I: Integer; Previous, Target: String;
begin
  Target := RemoveBackslashUnlessRoot(ExpandConstant('{app}'));
  if not FileExists(AddBackslash(Target) + '{#ProgramExe}') then Exit;
  for I := 0 to GetArrayLength(LegacyPaths) - 1 do
    if CompareText(LegacyPaths[I], Target) <> 0 then RemoveOwnedLegacyFiles(LegacyPaths[I]);
  if RegQueryStringValue(HKCU64, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#ProgramId}_is1', 'InstallLocation', Previous) and IsLegacyDir(Previous) then
    RegDeleteKeyIncludingSubkeys(HKCU64, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#ProgramId}_is1');
  if RegQueryStringValue(HKCU32, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#ProgramId}_is1', 'InstallLocation', Previous) and IsLegacyDir(Previous) then
    RegDeleteKeyIncludingSubkeys(HKCU32, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#ProgramId}_is1');
#if ProgramFolder == "XDSuite"
  { Older update helpers restart the old path. A short-lived forwarder bridges
    that first relocation; the new suite removes it after it has exited. }
  if WizardSilent then
    for I := 0 to GetArrayLength(LegacyPaths) - 1 do
      if CompareText(LegacyPaths[I], Target) <> 0 then
      begin
        ForceDirectories(LegacyPaths[I]);
        FileCopy(ExpandConstant('{app}\XDUpdateHelper.exe'), AddBackslash(LegacyPaths[I]) + 'XDSuite.exe', False);
        FileCopy(ExpandConstant('{app}\XDUpdateHelper.exe.config'), AddBackslash(LegacyPaths[I]) + 'XDSuite.exe.config', False);
      end;
#endif
end;