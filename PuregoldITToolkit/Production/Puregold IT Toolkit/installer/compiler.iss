#ifndef ReleaseVersion
  #define ReleaseVersion "1.1.1"
#endif
#define MyAppName "Puregold IT Toolkit"
#define MyAppExeName "PuregoldITToolkit.exe"

[Setup]
AppId={{5803DFE3-CC91-4755-B019-F0D8B2199D7A}
AppName={#MyAppName}
AppVersion={#ReleaseVersion}
AppPublisher=Puregold Price Club Inc.
VersionInfoVersion={#ReleaseVersion}.0
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
SetupIconFile=..\icon.ico
OutputDir=.
OutputBaseFilename=PITK_Setup_v{#ReleaseVersion}
SolidCompression=yes
Compression=lzma2/normal
WizardStyle=modern
MinVersion=6.1sp1
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Types]
Name: "full"; Description: "All tools"
Name: "custom"; Description: "Choose tools"; Flags: iscustom

[Components]
Name: "ejconsolidator"; Description: "EJ Consolidator"; Types: full
Name: "scpwdreport"; Description: "SC/PWD Reports"; Types: full
Name: "sodchecker"; Description: "SOD Checker"; Types: full
Name: "forms"; Description: "Forms"; Types: full
Name: "pims"; Description: "PIMS Vendor Manager"; Types: full
Name: "servicedesk"; Description: "Service Desk"; Types: full
Name: "purepostool"; Description: "PurePOS Tools"; Types: full
Name: "ftpbrowsertool"; Description: "FTP Browser"; Types: full

[Files]
Source: "..\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PuregoldITToolkit.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinSCP.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinSCPnet.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\icon.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\StoreProfilesDatabase.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "..\defaults\GlobalConfig.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "..\data\stores.csv"; DestDir: "{app}\data"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "..\NDP48-x86-x64-AllOS-ENU.exe"; Flags: dontcopy

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function CheckDotNet48: Boolean;
var
  Release: Cardinal;
begin
  Result := (RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040)) or
    (RegQueryDWordValue(HKLM32, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  if CheckDotNet48 then Exit;
  ExtractTemporaryFile('NDP48-x86-x64-AllOS-ENU.exe');
  if not ShellExec('runas', ExpandConstant('{tmp}\NDP48-x86-x64-AllOS-ENU.exe'), '/passive /norestart', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
    Result := 'Windows could not install Microsoft .NET Framework 4.8. Install it and run setup again.'
  else if (ResultCode <> 0) and (ResultCode <> 3010) and (ResultCode <> 1641) then
    Result := 'Microsoft .NET Framework 4.8 could not be installed. Restart Windows and run setup again.'
  else if not CheckDotNet48 then
    Result := 'Restart Windows to finish installing Microsoft .NET Framework 4.8, then run setup again.'
  else if (ResultCode = 3010) or (ResultCode = 1641) then
    NeedsRestart := True;
end;

function Base64Encode(const Input: AnsiString): String;
var
  I, B: Integer;
  Base64Chars: String;
begin
  Base64Chars := 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';
  Result := '';
  I := 1;
  while I <= Length(Input) do
  begin
    B := Ord(Input[I]) shl 16;
    if I + 1 <= Length(Input) then B := B + (Ord(Input[I + 1]) shl 8);
    if I + 2 <= Length(Input) then B := B + Ord(Input[I + 2]);
    Result := Result + Base64Chars[((B shr 18) and 63) + 1];
    Result := Result + Base64Chars[((B shr 12) and 63) + 1];
    if I + 1 <= Length(Input) then Result := Result + Base64Chars[((B shr 6) and 63) + 1] else Result := Result + '=';
    if I + 2 <= Length(Input) then Result := Result + Base64Chars[(B and 63) + 1] else Result := Result + '=';
    I := I + 3;
  end;
end;

function Feature(const ComponentName, FeatureName: String): String;
begin
  if WizardIsComponentSelected(ComponentName) then Result := '"' + FeatureName + '":true'
  else Result := '"' + FeatureName + '":false';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Json: AnsiString;
begin
  if CurStep <> ssPostInstall then Exit;
  Json := '{' + Feature('ejconsolidator', 'EJConsolidator') + ',' +
    Feature('scpwdreport', 'ScPwdReports') + ',' + Feature('sodchecker', 'SODChecker') + ',' +
    Feature('forms', 'PuregoldForms') + ',' + Feature('pims', 'PIMSManager') + ',' +
    Feature('servicedesk', 'ServiceDeskUtilities') + ',' + Feature('purepostool', 'PureposTool') + ',' +
    Feature('ftpbrowsertool', 'FtpBrowserTool') + '}';
  if not SaveStringToFile(ExpandConstant('{app}\license.dat'), Base64Encode(Json), False) then
    MsgBox('The tool selection could not be saved. Run setup again.', mbError, MB_OK);
end;
