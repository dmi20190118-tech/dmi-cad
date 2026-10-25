[Setup]
AppId=DMI-CAD-Prototype
AppName=DMI CAD
AppVersion=0.4.1
DefaultDirName={localappdata}\Programs\DMI CAD
DefaultGroupName=DMI CAD
PrivilegesRequired=lowest
OutputDir=installer-output
OutputBaseFilename=DMI-CAD-Setup-0.4.1
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\DMI-CAD.exe

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\DMI CAD"; Filename: "{app}\DMI-CAD.exe"
Name: "{autodesktop}\DMI CAD"; Filename: "{app}\DMI-CAD.exe"

[Run]
Filename: "{app}\DMI-CAD.exe"; Description: "Launch DMI CAD"; Flags: nowait postinstall skipifsilent
