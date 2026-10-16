; ETS2Trainer
; Copyright (C) 2026 Despical
; SPDX-License-Identifier: GPL-3.0-or-later
;
; This program is free software: you can redistribute it and/or modify
; it under the terms of the GNU General Public License as published by
; the Free Software Foundation, either version 3 of the License, or
; (at your option) any later version.
;
; This program is distributed in the hope that it will be useful,
; but WITHOUT ANY WARRANTY; without even the implied warranty of
; MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
; GNU General Public License for more details.
;
; You should have received a copy of the GNU General Public License
; along with this program. If not, see <https://www.gnu.org/licenses/>.

#ifndef SourceDir
  #error SourceDir must point to the published portable application.
#endif
#ifndef AppVersion
  #error AppVersion must match the published application.
#endif

[Setup]
AppId={{6CD706AE-948D-41B1-B2BD-159EC654206F}
AppName=ETS2Trainer
AppVersion={#AppVersion}
AppPublisher=Despical
AppPublisherURL=https://github.com/Despical/ETS2Trainer
AppSupportURL=https://github.com/Despical/ETS2Trainer/issues
AppUpdatesURL=https://github.com/Despical/ETS2Trainer/releases/latest
DefaultDirName={localappdata}\Programs\ETS2Trainer
DisableDirPage=no
DisableProgramGroupPage=yes
UsePreviousTasks=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
SetupMutex=Local\ETS2Trainer.Installer
CloseApplications=no
RestartApplications=no
UninstallDisplayIcon={app}\ETS2Trainer.exe
UninstallDisplayName=ETS2Trainer
SetupIconFile=..\src\ETS2Trainer\Assets\Trainer.ico
LicenseFile=..\LICENSE
WizardStyle=modern dynamic windows11
Compression=lzma2
SolidCompression=yes
OutputBaseFilename=ETS2Trainer-{#AppVersion}-Setup-x64
VersionInfoVersion={#AppVersion}
VersionInfoCompany=Despical
VersionInfoDescription=ETS2Trainer Setup

[Languages]
Name: english; MessagesFile: compiler:Default.isl
Name: turkish; MessagesFile: compiler:Languages\Turkish.isl

[Tasks]
Name: desktopicon; Description: "{cm:DesktopShortcut}"; GroupDescription: "{cm:ShortcutsGroup}"; Flags: unchecked
Name: autostart; Description: "{cm:AutoStart}"; GroupDescription: "{cm:GameGroup}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\ETS2Trainer.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\Start Trainer.cmd"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\ETS2Trainer"; Filename: "{app}\ETS2Trainer.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\ETS2Trainer"; Filename: "{app}\ETS2Trainer.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[InstallDelete]
Type: files; Name: "{autodesktop}\ETS2Trainer.lnk"; Tasks: not desktopicon

[Run]
Filename: "{app}\ETS2Trainer.exe"; Parameters: "--after-setup"; Description: "{cm:LaunchTrainer}"; Flags: postinstall nowait skipifsilent

[CustomMessages]
english.DesktopShortcut=Create a desktop shortcut
turkish.DesktopShortcut=Masaüstü kısayolu oluştur
english.ShortcutsGroup=Shortcuts:
turkish.ShortcutsGroup=Kısayollar:
english.AutoStart=Start the trainer automatically when ETS2 launches
turkish.AutoStart=ETS2 açılınca trainer'ı otomatik başlat
english.GameGroup=Game integration (your choice can be changed in Settings):
turkish.GameGroup=Oyun ile birlikte çalışma (Ayarlar'dan değiştirilebilir):
english.LaunchTrainer=Open ETS2Trainer
turkish.LaunchTrainer=ETS2Trainer'ı aç
english.StopFailed=Close the trainer through Settings > Quit trainer, then try again. No process will be force-closed.
turkish.StopFailed=Trainer'ı Ayarlar > Trainer'ı kapat ile kapatıp yeniden dene. İşlem zorla sonlandırılmayacak.
english.ConfigureFailed=The saved startup preference could not be applied. Check that your settings folder is writable and run Setup again.
turkish.ConfigureFailed=Otomatik başlatma tercihi uygulanamadı. Ayar klasörüne yazılabildiğini kontrol edip kurulumu yeniden çalıştır.
english.ReadFailed=Existing preferences could not be read. Open the trainer's settings folder and resolve the settings error before continuing.
turkish.ReadFailed=Mevcut tercihler okunamadı. Devam etmeden önce trainer'ın ayar klasöründeki hatayı düzelt.

[Code]
var
  Prepared: Boolean;
  Configured: Boolean;
  SavedAutoStart: Boolean;
  TasksInitialized: Boolean;

function RunHelper(const FileName, Arguments: String; var ExitCode: Integer): Boolean;
begin
  Result := Exec(FileName, Arguments, ExtractFileDir(FileName), SW_HIDE, ewWaitUntilTerminated, ExitCode);
end;

function HasTaskOverride: Boolean;
var
  I: Integer;
  Arg: String;
begin
  Result := False;
  for I := 1 to ParamCount do begin
    Arg := Uppercase(ParamStr(I));
    if (Pos('/TASKS=', Arg) = 1) or (Pos('/MERGETASKS=', Arg) = 1) then begin
      Result := True;
      Exit;
    end;
  end;
end;

procedure InitializeWizard;
var
  ExitCode: Integer;
begin
  ExtractTemporaryFile('ETS2Trainer.exe');
  if not HasTaskOverride then begin
    if not RunHelper(ExpandConstant('{tmp}\ETS2Trainer.exe'), '--installer-status', ExitCode) then
      RaiseException(CustomMessage('ReadFailed'));
    if ExitCode = 64 then
      SavedAutoStart := True
    else if ExitCode <> 0 then
      RaiseException(CustomMessage('ReadFailed'));
  end;
end;

procedure InitializeTasks;
begin
  if TasksInitialized then Exit;
  TasksInitialized := True;
  if HasTaskOverride then Exit;
  if SavedAutoStart then WizardSelectTasks('autostart') else WizardSelectTasks('!autostart');
  if FileExists(ExpandConstant('{autodesktop}\ETS2Trainer.lnk')) then
    WizardSelectTasks('desktopicon')
  else
    WizardSelectTasks('!desktopicon');
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpSelectTasks then InitializeTasks;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
begin
  Result := '';
  InitializeTasks;
  Prepared := True;
  if not RunHelper(ExpandConstant('{tmp}\ETS2Trainer.exe'), '--installer-stop', ExitCode) then
    Result := CustomMessage('StopFailed')
  else if ExitCode <> 0 then
    Result := CustomMessage('StopFailed');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  AutoStart, Language: String;
  ExitCode: Integer;
begin
  if CurStep = ssPostInstall then begin
    if WizardIsTaskSelected('autostart') then AutoStart := 'on' else AutoStart := 'off';
    if ActiveLanguage = 'turkish' then Language := 'tr' else Language := 'en';
    if not RunHelper(ExpandConstant('{app}\ETS2Trainer.exe'),
      '--installer-configure --auto-start=' + AutoStart + ' --language=' + Language, ExitCode) then
      RaiseException(CustomMessage('ConfigureFailed'));
    if ExitCode <> 0 then RaiseException(CustomMessage('ConfigureFailed'));
    Configured := True;
  end;
end;

procedure DeinitializeSetup;
var
  ExitCode: Integer;
begin
  if Configured then begin
    if WizardIsTaskSelected('autostart') then
      Exec(ExpandConstant('{app}\ETS2Trainer.exe'), '--watch-game --after-setup', ExpandConstant('{app}'), SW_HIDE, ewNoWait, ExitCode);
  end
  else if Prepared then
    Exec(ExpandConstant('{tmp}\ETS2Trainer.exe'), '--installer-resume', ExpandConstant('{tmp}'), SW_HIDE, ewNoWait, ExitCode);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ExitCode: Integer;
begin
  if CurUninstallStep = usUninstall then begin
    if not RunHelper(ExpandConstant('{app}\ETS2Trainer.exe'), '--installer-uninstall', ExitCode) then
      RaiseException(CustomMessage('StopFailed'));
    if ExitCode <> 0 then RaiseException(CustomMessage('StopFailed'));
  end;
end;
