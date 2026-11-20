; Instalador do TzIsland — compile com Inno Setup (https://jrsoftware.org/isinfo.php)
; Antes: dotnet publish -c Release -o publish
[Setup]
AppName=TzIsland
AppVersion=2.0.0
AppPublisher=Thigas
DefaultDirName={autopf}\TzIsland
DefaultGroupName=TzIsland
OutputBaseFilename=TzIsland-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayName=TzIsland
CloseApplications=yes

[Files]
Source: "publish\TzIsland.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "STATUS.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\TzIsland"; Filename: "{app}\TzIsland.exe"
Name: "{group}\Desinstalar TzIsland"; Filename: "{uninstallexe}"
Name: "{userstartup}\TzIsland"; Filename: "{app}\TzIsland.exe"; Tasks: autostart

[Tasks]
Name: "autostart"; Description: "Iniciar o TzIsland com o Windows"; Flags: unchecked

[Run]
Filename: "{app}\TzIsland.exe"; Description: "Abrir o TzIsland"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill"; Parameters: "/F /IM TzIsland.exe"; Flags: runhidden; RunOnceId: "KillTzIsland"

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\TzIsland"
