// The published Native AOT executable handles these commands before Avalonia.
// Run under the original user's identity even for an elevated all-users install.
var
  AutomaticChoiceInitialized: Boolean;

function HasNativeTaskOverride: Boolean;
var
  I: Integer;
  Argument: String;
  Tasks: TStringList;
begin
  Result := False;
  for I := 1 to ParamCount do begin
    Argument := Lowercase(ParamStr(I));
    if Pos('/tasks=', Argument) = 1 then
      Result := True;
    if Pos('/mergetasks=', Argument) = 1 then begin
      Tasks := TStringList.Create;
      try
        Tasks.CommaText := Copy(Argument, Length('/mergetasks=') + 1, Length(Argument));
        if (Tasks.IndexOf('autoupdates') >= 0) or (Tasks.IndexOf('!autoupdates') >= 0) then
          Result := True;
      finally
        Tasks.Free;
      end;
    end;
  end;
end;

procedure SelectAutomaticUpdateTask(Enabled: Boolean);
var
  Selected: TStringList;
  Index: Integer;
begin
  Selected := TStringList.Create;
  try
    Selected.CommaText := WizardSelectedTasks(False);
    Index := Selected.IndexOf('autoupdates');
    if Index >= 0 then Selected.Delete(Index);
    if Enabled then Selected.Add('autoupdates');
    WizardSelectTasks(Selected.CommaText);
  finally
    Selected.Free;
  end;
end;

procedure InitializeAutomaticUpdateChoice;
var
  Choice: String;
  ExitCode: Integer;
begin
  if AutomaticChoiceInitialized then Exit;
  Choice := Lowercase(ExpandConstant('{param:AUTOUPDATES|}'));
  if (Choice <> '') and (Choice <> 'on') and (Choice <> 'off') then
    RaiseException('/AUTOUPDATES must be on or off.');
  if Choice <> '' then
    SelectAutomaticUpdateTask(Choice = 'on')
  else if not HasNativeTaskOverride then begin
    ExtractTemporaryFile('{#MyAppExeName}');
    if not ExecAsOriginalUser(ExpandConstant('{tmp}\{#MyAppExeName}'),
      '--get-automatic-updates', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
      RaiseException('Unable to read the automatic update preference.');
    if (ExitCode <> 0) and (ExitCode <> 10) then
      RaiseException('Unable to read the automatic update preference.');
    SelectAutomaticUpdateTask(ExitCode = 0);
  end;
  AutomaticChoiceInitialized := True;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpSelectTasks then InitializeAutomaticUpdateChoice;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  InitializeAutomaticUpdateChoice;
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Argument: String;
  ExitCode: Integer;
begin
  if CurStep <> ssPostInstall then Exit;
  if WizardIsTaskSelected('autoupdates') then Argument := '--automatic-updates=on'
  else Argument := '--automatic-updates=off';
  if not ExecAsOriginalUser(ExpandConstant('{app}\{#MyAppExeName}'), Argument,
    '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
    RaiseException('Unable to save the automatic update preference.');
  if ExitCode <> 0 then
    RaiseException('Unable to save the automatic update preference.');
end;
