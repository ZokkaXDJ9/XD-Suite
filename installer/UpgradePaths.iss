[Code]
function DefaultInstallDir(Param: String): String;
var
  Existing: String;
begin
  Result := ExpandConstant('{autopf}\{#ProgramFolder}');
  if RegQueryStringValue(HKLM, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#ProgramId}_is1', 'InstallLocation', Existing) then
  begin
    if FileExists(AddBackslash(Existing) + '{#ProgramExe}') then Result := RemoveBackslashUnlessRoot(Existing);
  end
  else if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#ProgramId}_is1', 'InstallLocation', Existing) then
  begin
    if FileExists(AddBackslash(Existing) + '{#ProgramExe}') then Result := RemoveBackslashUnlessRoot(Existing);
  end;
end;

procedure MigrateRegistration;
var
  Previous: String;
begin
  { Only remove a redundant per-user registration after the same installation was updated.
    No uninstaller is run and no application data or other programs are removed. }
  if RegKeyExists(HKLM, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#ProgramId}_is1') and
     RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#ProgramId}_is1', 'InstallLocation', Previous) then
    if CompareText(RemoveBackslashUnlessRoot(Previous), RemoveBackslashUnlessRoot(ExpandConstant('{app}'))) = 0 then
      RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#ProgramId}_is1');
end;
