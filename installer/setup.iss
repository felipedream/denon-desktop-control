[Setup]
AppName=DENON Desktop Control
AppVersion=1.5.0
AppPublisher=Felipe (@felipedream)
AppPublisherURL=https://github.com/felipedream/denon-desktop-control
AppSupportURL=https://t.me/felipedream
DefaultDirName={autopf}\DENON Desktop Control
DefaultGroupName=DENON Desktop Control
UninstallDisplayIcon={app}\DenonDesktopControl.exe
OutputDir=..\installer\out
OutputBaseFilename=DenonDesktopControl-1.5.0-Setup
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
SetupIconFile=..\src\DenonRemote\Assets\app.ico
LicenseFile=..\LICENSE
PrivilegesRequired=lowest

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\DENON Desktop Control"; Filename: "{app}\DenonDesktopControl.exe"
Name: "{group}\Desinstalar"; Filename: "{uninstallexe}"
Name: "{autodesktop}\DENON Desktop Control"; Filename: "{app}\DenonDesktopControl.exe"; Tasks: desktopicon
Name: "{autostartup}\DENON Desktop Control"; Filename: "{app}\DenonDesktopControl.exe"; Tasks: startupicon

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"
Name: "startupicon"; Description: "Iniciar con Windows"; GroupDescription: "Opciones:"

[Run]
Filename: "{app}\DenonDesktopControl.exe"; Description: "Ejecutar DENON Desktop Control"; Flags: nowait postinstall skipifsilent


