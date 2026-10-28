[Code]
var LegacyPaths: TArrayOfString;

function CopyMigrationFile(ExistingFile, NewFile: String; FailIfExists: Integer): Integer;
external 'CopyFileW@kernel32.dll stdcall';
function CommitMigrationFile(ExistingFile, NewFile: String): Integer;
external 'MoveFileW@kernel32.dll stdcall';

function IsLegacyUninstaller(Name: String): Boolean;
var I: Integer;
begin
  Result := False;
  if (Length(Name) <> 12) or (Lowercase(Copy(Name, 1, 5)) <> 'unins') then Exit;
  for I := 6 to 8 do if (Name[I] < '0') or (Name[I] > '9') then Exit;
  Name := Lowercase(ExtractFileExt(Name));
  Result := (Name = '.exe') or (Name = '.dat') or (Name = '.msg');
end;

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

function CopyLegacyFiles(Source, Target: String; var FailedPath: String; var ErrorCode: Integer): Boolean;
var Search: TFindRec; SourceFile, TargetFile, Staged: String;
begin
  Result := False;
  if not ForceDirectories(Target) then
  begin
    ErrorCode := 0; FailedPath := Target; Exit;
  end;
  if FindFirst(AddBackslash(Source) + '*', Search) then
  try
    repeat
      if (Search.Name <> '.') and (Search.Name <> '..') then
      begin
        SourceFile := AddBackslash(Source) + Search.Name;
        TargetFile := AddBackslash(Target) + Search.Name;
        if IsLegacyUninstaller(Search.Name) then
          Log('Using the new setup uninstaller instead of importing: ' + SourceFile)
        else if (Search.Attributes and $400) <> 0 then
          Log('Leaving link in the previous directory: ' + SourceFile)
        else if (Search.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
        begin
          if not CopyLegacyFiles(SourceFile, TargetFile, FailedPath, ErrorCode) then Exit;
        end
        else if not FileExists(TargetFile) then
        begin
          { Copy to a new file first. A failed copy cannot leave a partial destination
            that a subsequent update would mistake for an existing user file. }
          Staged := TargetFile + '.xd-migration';
          while FileExists(Staged) or DirExists(Staged) do Staged := Staged + 'x';
          if CopyMigrationFile(PathConvertNormalToSuper(SourceFile), PathConvertNormalToSuper(Staged), 1) = 0 then
          begin
            ErrorCode := DLLGetLastError; FailedPath := SourceFile;
            DeleteFile(Staged); Exit;
          end;
          if CommitMigrationFile(PathConvertNormalToSuper(Staged), PathConvertNormalToSuper(TargetFile)) = 0 then
          begin
            ErrorCode := DLLGetLastError; FailedPath := TargetFile;
            DeleteFile(Staged); Exit;
          end;
        end;
      end;
    until not FindNext(Search);
  finally FindClose(Search); end;
  Result := True;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var I, ErrorCode: Integer; Target, FailedPath, Details: String;
begin
  Result := '';
  Target := RemoveBackslashUnlessRoot(ExpandConstant('{app}'));
  for I := 0 to GetArrayLength(LegacyPaths) - 1 do
    if DirExists(LegacyPaths[I]) and (CompareText(LegacyPaths[I], Target) <> 0) then
    begin
      if Pos(Lowercase(AddBackslash(LegacyPaths[I])), Lowercase(AddBackslash(Target))) = 1 then
      begin
        Result := CustomMessage('MoveInstallError');
        Exit;
      end;
      { An older helper may still hold the previous directory as its working directory.
        Copy without renaming it; keep the source intact until the setup succeeds.
        New directories inherit Program Files permissions. Existing destination files
        are retained; the normal setup replaces its own application files afterwards. }
      Log('Copying previous installation: ' + LegacyPaths[I] + ' -> ' + Target);
      if not CopyLegacyFiles(LegacyPaths[I], Target, FailedPath, ErrorCode) then
      begin
        Details := '';
        if ErrorCode <> 0 then Details := SysErrorMessage(ErrorCode);
        Result := FmtMessage(CustomMessage('CopyInstallError'), [FailedPath, Details]);
        Log(Result); Exit;
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
      if ((Search.Attributes and FILE_ATTRIBUTE_DIRECTORY) = 0) and IsLegacyUninstaller(Search.Name) then DeleteFile(AddBackslash(Path) + Search.Name);
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
