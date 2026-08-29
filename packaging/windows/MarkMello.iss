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

#ifndef MySetupIconFile
  #define MySetupIconFile ".\markmello-installer.ico"
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
; {autopf} follows the install mode picked in the privileges dialog:
; Program Files for an all-users install, {localappdata}\Programs for a
; per-user one. A hard-coded {localappdata} would keep offering a profile
; directory even after the user chose "install for all users".
DefaultDirName={autopf}\{#MyAppName}
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
SetupIconFile={#MySetupIconFile}
UninstallDisplayIcon={app}\{#MyAppExeName}

; Signing is expected to be injected by the release pipeline.
; Example:
; SignTool=signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com /a $f

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
; Native AOT publish drops a *.pdb next to the binary (the linker's
; debug companion). It is useless to end users and only bloats the
; installer, so exclude all PDBs from the shipped artefact. They stay
; in the build's publish/ directory for symbol uploads if ever needed.
Source: "{#MyPublishDir}\*"; Excludes: "*.pdb"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Register MarkMello as an available Markdown handler without forcing the default app.
; HKA follows the install mode: HKLM for an all-users install, so every account
; on the machine gets the association, and HKCU for a per-user one. Hard-coded
; HKCU would register the handler only for whoever ran the installer.
Root: HKA; Subkey: "Software\Classes\{#MyAppProgId}"; ValueType: string; ValueName: ""; ValueData: "Markdown document"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\{#MyAppProgId}"; ValueType: string; ValueName: "FriendlyTypeName"; ValueData: "Markdown document"
Root: HKA; Subkey: "Software\Classes\{#MyAppProgId}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"
Root: HKA; Subkey: "Software\Classes\{#MyAppProgId}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
Root: HKA; Subkey: "Software\Classes\.md\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgId}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#MyAppName}"
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".md"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
