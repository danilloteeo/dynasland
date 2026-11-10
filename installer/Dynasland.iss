; Instalador do Dynasland (Inno Setup 6). Gerado por tools\Build-Release.ps1, que publica o
; aplicativo em publish\Dynasland e chama: ISCC /DAppVersion=<versão> installer\Dynasland.iss
;
; - Instalação por usuário, sem administrador: %LocalAppData%\Programs\Dynasland.
; - Atualizar por cima preserva as configurações (%LocalAppData%\Dynasland nunca é tocado na instalação).
; - O Dynasland em execução é fechado antes de copiar os arquivos (e antes de desinstalar).
; - Visual próprio: tema escuro e imagens de installer\assets (geradas por tools\New-InstallerImages.ps1).
; - Com uma versão já instalada, o assistente entra em modo de atualização: sem perguntas, só
;   "Atualizar". As opções escolhidas na instalação e "Iniciar com o Windows" não são alteradas.
; - /VERYSILENT /RELAUNCH: atualização pedida pelo próprio aplicativo; instala sem janelas e o reabre.

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
DisableWelcomePage=no
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
WizardStyle=modern dark
WizardImageFile=assets\wizard.png,assets\wizard@2x.png
WizardSmallImageFile=assets\small.png,assets\small@2x.png
CloseApplications=no
RestartApplications=no

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Messages]
ptbr.WelcomeLabel1=Bem-vindo ao Dynasland
ptbr.WelcomeLabel2=A ilha dinâmica para o seu Windows: música, notificações, clima, temporizadores e atalhos no topo da tela.%n%nA instalação é só para o seu usuário e não pede permissão de administrador.
ptbr.FinishedHeadingLabel=Tudo pronto
ptbr.FinishedLabel=O Dynasland {#AppVersion} está instalado. Ele fica no topo da tela e na bandeja do sistema.
ptbr.ClickFinish=Clique em Concluir para fechar o instalador.

[CustomMessages]
ptbr.StartupTask=Iniciar o Dynasland com o Windows
ptbr.DeleteDataPrompt=Apagar também as configurações e os registros (logs) do Dynasland?%n%n%1
ptbr.LaunchApp=Abrir o Dynasland
ptbr.UpdateTitle=Atualizar o Dynasland
ptbr.UpdateText=Versão instalada: %1%nNova versão: %2%n%nSuas configurações, históricos e opções de instalação são mantidos. O Dynasland será fechado durante a atualização.
ptbr.ReinstallTitle=Reinstalar o Dynasland
ptbr.ReinstallText=A versão %1 já está instalada.%n%nReinstalar repõe os arquivos do programa. Suas configurações e históricos são mantidos.
ptbr.UpdateButton=&Atualizar
ptbr.ReinstallButton=&Reinstalar
ptbr.UpdateFinishedTitle=Dynasland atualizado
ptbr.UpdateFinishedText=Agora você está usando a versão %1.

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
; Só na primeira instalação: numa atualização vale o que o usuário deixou ligado ou desligado no aplicativo.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExe}"" --inicializacao"; Tasks: startup; Check: not IsUpgrade; Flags: uninsdeletevalue
; A desinstalação sempre remove a entrada, mesmo que tenha sido ligada depois pelo aplicativo.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "{#AppName}"; Flags: uninsdeletevalue dontcreatekey

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent
; Atualização silenciosa pedida pelo aplicativo: reabre ao terminar.
Filename: "{app}\{#AppExe}"; Flags: nowait; Check: RelaunchRequested

[Code]
const
  DataFolder = '{localappdata}\Dynasland';
  { Chave de desinstalação que o Inno Setup cria para o AppId deste instalador. }
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{6C1F3B9E-2D47-4E8A-9B1C-5A7D0E2F4C61}_is1';

var
  InstalledVersion: String;

function InitializeSetup(): Boolean;
begin
  if not RegQueryStringValue(HKCU, UninstallKey, 'DisplayVersion', InstalledVersion) then
    InstalledVersion := '';
  Result := True;
end;

{ Já existe uma versão instalada: o assistente vira "Atualizar" (ou "Reinstalar", se for a mesma). }
function IsUpgrade(): Boolean;
begin
  Result := InstalledVersion <> '';
end;

function IsSameVersion(): Boolean;
begin
  Result := InstalledVersion = '{#AppVersion}';
end;

function RelaunchRequested(): Boolean;
var
  I: Integer;
begin
  Result := False;
  if WizardSilent() then
    for I := 1 to ParamCount do
      if CompareText(ParamStr(I), '/RELAUNCH') = 0 then
        Result := True;
end;

{ Atualização: sem a tela de opções (valem as da instalação) nem a de confirmação. }
function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := IsUpgrade() and ((PageID = wpSelectTasks) or (PageID = wpReady));
end;

procedure InitializeWizard();
begin
  if not IsUpgrade() then
    Exit;

  if IsSameVersion() then
  begin
    WizardForm.WelcomeLabel1.Caption := CustomMessage('ReinstallTitle');
    WizardForm.WelcomeLabel2.Caption := FmtMessage(CustomMessage('ReinstallText'), [InstalledVersion]);
  end
  else
  begin
    WizardForm.WelcomeLabel1.Caption := CustomMessage('UpdateTitle');
    WizardForm.WelcomeLabel2.Caption := FmtMessage(CustomMessage('UpdateText'), [InstalledVersion, '{#AppVersion}']);
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if not IsUpgrade() then
    Exit;

  if CurPageID = wpWelcome then
  begin
    if IsSameVersion() then
      WizardForm.NextButton.Caption := CustomMessage('ReinstallButton')
    else
      WizardForm.NextButton.Caption := CustomMessage('UpdateButton');
  end
  else if (CurPageID = wpFinished) and not IsSameVersion() then
  begin
    WizardForm.FinishedHeadingLabel.Caption := CustomMessage('UpdateFinishedTitle');
    WizardForm.FinishedLabel.Caption := FmtMessage(CustomMessage('UpdateFinishedText'), ['{#AppVersion}']);
  end;
end;

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
