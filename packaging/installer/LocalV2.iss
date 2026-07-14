#ifndef SourceDir
  #error SourceDir must identify the verified Local-only Version 2.0 publish directory.
#endif
#ifndef OutputDir
  #error OutputDir must identify the Local-only Version 2.0 installer artifact directory.
#endif

#define AppName "Dan's RBI Baseball 2026 - Local Only"
#define AppVersion "2.0"
#define AppExeName "DanVille50RBIbaseball.exe"

[Setup]
AppId={{B9271E06-CBA7-499B-A409-FC0861DEB213}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=DanVille50
VersionInfoVersion=2.0.0.0
VersionInfoCompany=DanVille50
VersionInfoDescription=Dan's RBI Baseball 2026 Local-only Version 2.0 Installer
VersionInfoProductName={#AppName}
DefaultDirName={localappdata}\Programs\DanVille50\Dans RBI Baseball 2026 Local Only
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=Dans-RBI-Baseball-2026-Local-2.0-Setup
SetupIconFile=..\..\StandaloneBaseball\Branding\DansRBIBaseball.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
