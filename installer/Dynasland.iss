; Instalador do Dynasland (Inno Setup 6). Gerado por tools\Build-Release.ps1, que publica o
; aplicativo em publish\Dynasland e chama: ISCC /DAppVersion=<versão> installer\Dynasland.iss
;
; - Instalação por usuário, sem administrador: %LocalAppData%\Programs\Dynasland.
; - Atualizar por cima preserva as configurações (%LocalAppData%\Dynasland nunca é tocado na instalação).
; - O Dynasland em execução é fechado antes de copiar os arquivos (e antes de desinstalar).

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish\Dynasland"
#endif
#ifndef OutputDir
  #define OutputDir "..\publish"
#endif

#define AppName "Dynasland"
#define AppExe "Dynasland.exe"
#define AppPublisher "Dynasland"
#define AppUrl "https://github.com/danilloteeo/dynasland"

[Setup]
; AppId fixo: identifica o programa em Configurações › Aplicativos e nas atualizações. Não alterar.
AppId={{6C1F3B9E-2D47-4E8A-9B1C-5A7D0E2F4C61}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=yes
DisableReadyPage=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename=DynaslandSetup-{#AppVersion}
SetupIconFile=..\src\Dynasland.App\Assets\dynasland.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
RestartApplications=no

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
ptbr.StartupTask=Iniciar o Dynasland com o Windows
ptbr.DeleteDataPrompt=Apagar também as configurações e os registros (logs) do Dynasland?%n%n%1
ptbr.LaunchApp=Abrir o Dynasland

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "{cm:StartupTask}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Atualização: remove arquivos da versão anterior que não existem mais na nova.
Type: filesandordirs; Name: "{app}\*"

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Mesma entrada usada pela opção "Iniciar com o Windows" das configurações (StartupRegistration).
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExe}"" --inicializacao"; Tasks: startup; Flags: uninsdeletevalue
; Sem a tarefa, a desinstalação ainda remove a entrada caso o usuário a tenha ligado pelo aplicativo.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "{#AppName}"; Flags: uninsdeletevalue dontcreatekey; Tasks: not startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[Code]
const
  DataFolder = '{localappdata}\Dynasland';

{ Encerra o Dynasland em execução (os arquivos ficariam travados). As configurações são gravadas
  na hora em que mudam, então encerrar não perde nada. }
procedure CloseRunningApp();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  CloseRunningApp();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  CloseRunningApp();
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Folder: String;
begin
  if (CurUninstallStep = usPostUninstall) and not UninstallSilent() then
  begin
    Folder := ExpandConstant(DataFolder);
    if DirExists(Folder) and
       (MsgBox(FmtMessage(CustomMessage('DeleteDataPrompt'), [Folder]), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES) then
    begin
      DelTree(Folder, True, True, True);
    end;
  end;
end;
