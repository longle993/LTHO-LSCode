#ifndef PublishDir
  #define PublishDir "..\artifacts\installer-publish"
#endif

[Setup]
AppId=LSCode.WebView
AppName=LSCode WebView
AppVersion=1.0.0
DefaultDirName={localappdata}\Programs\LSCode WebView
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\assets\logo2.ico
UninstallDisplayIcon={app}\LSCode.WebView.exe
OutputDir=..\artifacts\installer
OutputBaseFilename=LSCode-WebView-Setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"
Source: "..\artifacts\installer-tools\MicrosoftEdgeWebview2Setup.exe"; Flags: dontcopy

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked

[Icons]
Name: "{autoprograms}\LSCode WebView"; Filename: "{app}\LSCode.WebView.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\LSCode WebView"; Filename: "{app}\LSCode.WebView.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\LSCode.WebView.exe"; Description: "Launch LSCode WebView"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Code]
function HasWebView2: Boolean;
var
  ClientKey, Version: String;
begin
  ClientKey := 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  Result := (RegQueryStringValue(HKLM32, ClientKey, 'pv', Version) and
    (Version <> '') and (Version <> '0.0.0.0')) or
    (RegQueryStringValue(HKCU, ClientKey, 'pv', Version) and
    (Version <> '') and (Version <> '0.0.0.0'));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
begin
  Result := '';
  if HasWebView2 then
    Exit;

  ExtractTemporaryFile('MicrosoftEdgeWebview2Setup.exe');
  if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install',
    '', SW_HIDE, ewWaitUntilTerminated, ExitCode) or not HasWebView2 then
    Result := 'Could not install Microsoft Edge WebView2 Runtime. Check your Internet connection and retry.';
end;
