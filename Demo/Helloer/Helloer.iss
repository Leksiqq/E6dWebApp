#define AppName "Helloer"
#define AppVersion "1.0.0"
#define AppPublisher "Leksiq"

[Setup]
AppId={{3FB66DE4-2476-49B7-A294-E98AD405D13C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
Compression=lzma
DefaultDirName={autopf32}\{#AppName}
DefaultGroupName={#AppName}
PrivilegesRequired=lowest 
OutputBaseFilename="{#AppName}-{#AppVersion}-setup"
SolidCompression=yes
WizardStyle=modern

[Files]
Source: "bin\Release\net6.0-windows\*"; DestDir: "{app}";

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\Net.Leksi.E6dWebApp.Demo.Helloer.exe"