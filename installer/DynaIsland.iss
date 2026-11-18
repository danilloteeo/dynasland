; Instalador do DynaIsland (Inno Setup 6). Gerado por tools\Build-Release.ps1, que publica o
; aplicativo em publish\DynaIsland e chama: ISCC /DAppVersion=<versão> installer\DynaIsland.iss
;
; - Instalação por usuário, sem administrador: %LocalAppData%\Programs\DynaIsland.
; - Atualizar por cima preserva as configurações (%LocalAppData%\DynaIsland nunca é tocado na instalação).
; - O DynaIsland em execução é fechado antes de copiar os arquivos (e antes de desinstalar).
; - Visual próprio: tema escuro e imagens de installer\assets (geradas por tools\New-InstallerImages.ps1).
; - Com uma versão já instalada, o assistente entra em modo de atualização: sem perguntas, só
;   "Atualizar". As opções escolhidas na instalação e "Iniciar com o Windows" não são alteradas.
; - /VERYSILENT /RELAUNCH: atualização pedida pelo próprio aplicativo; instala sem janelas e o reabre.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish\DynaIsland"
#endif
#ifndef OutputDir
  #define OutputDir "..\publish"
#endif

#define AppName "DynaIsland"
#define AppExe "DynaIsland.exe"
#define AppPublisher "DynaIsland"
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
; Nome antigo mantido: as versões 1.7.x procuram "DynaslandSetup-*.exe" no release para se atualizar.
OutputBaseFilename=DynaslandSetup-{#AppVersion}
SetupIconFile=..\src\DynaIsland.App\Assets\dynaisland.ico
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
ptbr.WelcomeLabel1=Bem-vindo ao DynaIsland
ptbr.WelcomeLabel2=A ilha dinâmica para o seu Windows: música, notificações, clima, temporizadores e atalhos no topo da tela.%n%nA instalação é só para o seu usuário e não pede permissão de administrador.
ptbr.FinishedHeadingLabel=Tudo pronto
ptbr.FinishedLabel=O DynaIsland {#AppVersion} está instalado. Ele fica no topo da tela e na bandeja do sistema.
ptbr.ClickFinish=Clique em Concluir para fechar o instalador.

[CustomMessages]
ptbr.StartupTask=Iniciar o DynaIsland com o Windows
ptbr.DeleteDataPrompt=Apagar também as configurações e os registros (logs) do DynaIsland?%n%n%1
ptbr.LaunchApp=Abrir o DynaIsland
ptbr.UpdateTitle=Atualizar o DynaIsland
ptbr.UpdateText=Versão instalada: %1%nNova versão: %2%n%nSuas configurações, históricos e opções de instalação são mantidos. O DynaIsland será fechado durante a atualização.
ptbr.ReinstallTitle=Reinstalar o DynaIsland
ptbr.ReinstallText=A versão %1 já está instalada.%n%nReinstalar repõe os arquivos do programa. Suas configurações e históricos são mantidos.
ptbr.UpdateButton=&Atualizar
ptbr.ReinstallButton=&Reinstalar
ptbr.UpdateFinishedTitle=DynaIsland atualizado
ptbr.UpdateFinishedText=Agora você está usando a versão %1.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "{cm:StartupTask}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Atualização: remove arquivos da versão anterior que não existem mais na nova.
Type: filesandordirs; Name: "{app}\*"
; Atalhos com o nome antigo (Dynasland), de quando o aplicativo se chamava assim.
Type: files; Name: "{autoprograms}\Dynasland.lnk"
Type: files; Name: "{autodesktop}\Dynasland.lnk"

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
  DataFolder = '{localappdata}\DynaIsland';
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

{ Encerra o DynaIsland em execução (os arquivos ficariam travados). As configurações são gravadas
  na hora em que mudam, então encerrar não perde nada. }
procedure CloseRunningApp();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  { Versões até a 1.7.1 chamavam-se Dynasland.exe. }
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM Dynasland.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500);
end;

{ Quem veio do Dynasland: "Iniciar com o Windows" apontava para Dynasland.exe, que deixa de existir.
  Se estava ligado, passa para o nome e o executável novos; em qualquer caso a entrada antiga sai. }
procedure CurStepChanged(CurStep: TSetupStep);
var
  Old: String;
begin
  if (CurStep = ssPostInstall) and RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Dynasland', Old) then
  begin
    RegWriteStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#AppName}',
      '"' + ExpandConstant('{app}\{#AppExe}') + '" --inicializacao');
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Dynasland');
  end;
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
