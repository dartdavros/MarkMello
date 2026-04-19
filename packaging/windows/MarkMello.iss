#define MyAppName "MarkMello"
#define MyAppExeName "MarkMello.exe"
#define MyAppProgId "MarkMello.Markdown"

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif

#ifndef MyPublishDir
  #define MyPublishDir "..\..\publish\win-x64"
#endif

#ifndef MyOutputDir
  #define MyOutputDir "..\dist"
#endif

#ifndef MyArchSuffix
  #define MyArchSuffix "win-x64"
#endif

#ifndef MyOutputBaseName
  #define MyOutputBaseName "MarkMello-setup-win-x64"
#endif

#ifndef MyArchitecturesAllowed
  #define MyArchitecturesAllowed "x64compatible"
#endif

#ifndef MyArchitecturesInstallMode
  #define MyArchitecturesInstallMode "x64compatible"
#endif

#ifndef MyAppId
  #define MyAppId "{{5E8D6758-6EAE-470A-A32D-5F941B1458C8}"
#endif

#ifndef MyReleaseOwner
  #define MyReleaseOwner "dartdavros"
#endif

#ifndef MyReleaseRepo
  #define MyReleaseRepo "MarkMello"
#endif

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=MarkMello contributors
AppPublisherURL=https://github.com/{#MyReleaseOwner}/{#MyReleaseRepo}
AppUpdatesURL=https://github.com/{#MyReleaseOwner}/{#MyReleaseRepo}/releases/latest
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed={#MyArchitecturesAllowed}
ArchitecturesInstallIn64BitMode={#MyArchitecturesInstallMode}
ChangesAssociations=yes
Compression=lzma
SolidCompression=yes
WizardStyle=modern
UsePreviousAppDir=yes
OutputDir={#MyOutputDir}
OutputBaseFilename={#MyOutputBaseName}
UninstallDisplayIcon={app}\{#MyAppExeName}

; Signing is expected to be injected by the release pipeline.
; Example:
; SignTool=signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com /a $f

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Register MarkMello as an available Markdown handler without forcing the default app.
Root: HKCU; Subkey: "Software\Classes\{#MyAppProgId}"; ValueType: string; ValueName: ""; ValueData: "Markdown document"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\{#MyAppProgId}"; ValueType: string; ValueName: "FriendlyTypeName"; ValueData: "Markdown document"
Root: HKCU; Subkey: "Software\Classes\{#MyAppProgId}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\{#MyAppProgId}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\.md\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgId}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#MyAppName}"
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".md"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
