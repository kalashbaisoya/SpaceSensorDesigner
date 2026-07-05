; ============================================================================
;  SpaceSensor Designer — Inno Setup script
;  Build the installer with:  installer\build-installer.ps1   (recommended)
;  or manually:               iscc installer\installer.iss
;
;  Expects the app to have been published to PublishDir (see below) first:
;    dotnet publish src\SpaceSensorDesigner.App -c Release -r win-x64 ^
;      -p:PublishSingleFile=true --self-contained true
; ============================================================================

#define AppName        "SpaceSensor Designer"
#define AppVersion      "1.0.0"
#define AppPublisher    "SpaceSensor"
#define AppExeName      "SpaceSensorDesigner.App.exe"
#define DocExt          ".spacedesign"

; Folder that the publish step produced. Overridable from the command line:
;   iscc /DPublishDir="...\publish" installer\installer.iss
#ifndef PublishDir
  #define PublishDir "..\src\SpaceSensorDesigner.App\bin\Release\net8.0-windows\win-x64\publish"
#endif

[Setup]
AppId={{7C3D9A64-5F1E-4C2B-9E77-SPACESENSOR01}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir=dist
OutputBaseFilename=SpaceSensorDesigner-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
DisableProgramGroupPage=yes
SetupIconFile=..\src\SpaceSensorDesigner.App\Assets\appicon.ico
; To produce a signed installer, configure a "signtool" in the Inno IDE (or pass one)
; and uncomment the next two lines:
; SignTool=signtool
; SignedUninstaller=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "associate";   Description: "Associate {#DocExt} project files with {#AppName}"; GroupDescription: "File associations:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}";        Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";  Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; File-type association for .spacedesign (per-user or per-machine via HKA)
Root: HKA; Subkey: "Software\Classes\{#DocExt}"; ValueType: string; ValueData: "SpaceSensorDesigner.Document"; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Classes\SpaceSensorDesigner.Document"; ValueType: string; ValueData: "{#AppName} Project"; Flags: uninsdeletekey; Tasks: associate
Root: HKA; Subkey: "Software\Classes\SpaceSensorDesigner.Document\DefaultIcon"; ValueType: string; ValueData: "{app}\{#AppExeName},0"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\SpaceSensorDesigner.Document\shell\open\command"; ValueType: string; ValueData: """{app}\{#AppExeName}"" ""%1"""; Tasks: associate

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
