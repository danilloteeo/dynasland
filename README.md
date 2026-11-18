# DynaIsland

Ilha dinâmica (estilo Dynamic Island) para Windows 10 e Windows 11, em C# / .NET 10 / WPF, com um motor nativo
em C++: uma pílula no topo da tela que mostra o que está acontecendo — música, volume, notificações, clima,
cotações — e abre painéis de controle com um clique.

> Versão **1.8.0** (o aplicativo se chamava Dynasland até a 1.7.1). [Guia de uso](docs/GUIA_DE_USO.md) ·
> [Notas da versão](CHANGELOG.md) · [Arquitetura nativa](docs/ARQUITETURA_NATIVA.md)

## Instalar

Baixe em [Releases](https://github.com/danilloteeo/dynasland/releases):

- **`DynaslandSetup-<versão>.exe`** (recomendado): instala só para o seu usuário, sem pedir administrador,
  em `%LocalAppData%\Programs\Dynasland`. Cria atalho no Menu Iniciar (e, se marcado, na Área de Trabalho)
  e oferece "Iniciar com o Windows". Desinstale por Configurações › Aplicativos; a desinstalação pergunta se
  deve apagar também as configurações e os históricos (`%LocalAppData%\DynaIsland`).
- **`DynaIsland-<versão>-portable.zip`**: um único `DynaIsland.exe`, sem instalação (os dados ficam no
  mesmo `%LocalAppData%\DynaIsland`). A primeira abertura é mais lenta (o Windows extrai os arquivos).

> O instalador mantém o nome `DynaslandSetup-*` (e a pasta `Programs\Dynasland`) por compatibilidade: é por esse
> nome que as versões 1.7.x acham a atualização. O aplicativo instalado já aparece como DynaIsland.

O Windows SmartScreen pode avisar na primeira execução (os arquivos não são assinados digitalmente):
clique em **Mais informações › Executar assim mesmo**. O aplicativo exige o
[WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (já vem no Windows 11 e em
Windows 10 atualizados) para a janela de Configurações.

## Atualizar

- **Pelo aplicativo**: quando há versão nova, a ilha avisa; **Atualizar agora** baixa o instalador do Release,
  confere o tamanho e o SHA-256, instala em modo silencioso e reabre o DynaIsland.
- **Manual**: instale a versão nova por cima; o DynaIsland aberto é fechado sozinho e as configurações ficam.
- **Quem vem do Dynasland (1.7.1 ou anterior)**: na primeira execução o DynaIsland **copia** a pasta antiga
  `%LocalAppData%\Dynasland` para `%LocalAppData%\DynaIsland` (banco, históricos, configurações) e mantém
  "Iniciar com o Windows" ligado se estava. A pasta antiga não é alterada; se a cópia falhar, o aplicativo continua
  usando a antiga e tenta de novo na próxima vez. Depois de conferir que está tudo certo, ela pode ser apagada.
- A versão portátil não se atualiza sozinha: baixe o zip novo e substitua o executável.

## O que ele faz

| Módulo | O que aparece | Integração com o Windows |
|---|---|---|
| **Música e mídia** | Capa, título, artista, progresso, anterior/play/próxima; várias mídias viram abas; fila do Spotify ("A seguir"); letra sincronizada (opcional); onda de som na ilha | Global System Media Transport Controls; Spotify Web API opcional (OAuth PKCE com o seu Client ID); LRCLIB |
| **Volume** | Nível, mudo, troca do dispositivo de saída e do microfone, volume por aplicativo | Core Audio (`IPolicyConfig` para o padrão) |
| **Brilho** | Brilho de cada tela | WMI no notebook, DDC/CI em monitores externos |
| **Notificações** | Agrupadas por app, histórico, regras por aplicativo (mostrar, só histórico, bloquear, prioridade, duração) | `UserNotificationListener` |
| **Área de transferência** | Histórico de texto e imagens com busca, favoritos e exclusão | Listener Win32; respeita conteúdo marcado como privado |
| **Clima** | Tempo agora (sensação, umidade, vento, chuva) e próximos dias, na ilha em repouso | [Open-Meteo](https://open-meteo.com) (gratuito, sem chave) |
| **Cotações** | Dólar, euro e bitcoin em reais | AwesomeAPI |
| **Placar NBA** | Jogos do dia | ESPN |
| **Água** | Lembrete a cada hora (8h–22h), copos do dia, meta, "Bebi/Adiar" | Banco local (SQLite) |
| **Caps Lock e Num Lock** | Aviso rápido ao ligar/desligar | Estado do teclado |
| **Relógio** | Hora e data na ilha em repouso e na aberta | — |

O código de outros módulos (Bateria, Downloads, Monitor do sistema, Captura, Bluetooth, Pomodoro, Modo gamer,
Agenda) continua no repositório, mas eles **não estão registrados** nesta versão; veja [Criando um módulo](#criando-um-módulo)
para religar um. Módulos sem suporte no computador aparecem como indisponíveis, com o motivo.

### Atalhos padrão

| Atalho | Ação |
|---|---|
| Ctrl+Alt+D | Abrir/fechar a ilha |
| Ctrl+Alt+H | Mostrar/ocultar a ilha |
| Ctrl+Alt+V | Histórico da área de transferência |
| Ctrl+Alt+M | Microfone: desligar/ligar |
| Ctrl+Alt+G | Registrar um copo de água |

Todos podem ser trocados ou desligados em Configurações › Atalhos, onde outras ações (reproduzir/pausar, brilho,
abrir configurações) também podem receber atalho.

## Uso rápido

- A ilha é uma **pílula preta no topo** (ou embaixo, nas configurações), com clima, hora e a onda da música. **Passar o
  mouse** a amplia e mostra as setas e as bolinhas para trocar de página; **clicar** a abre; clicar fora recolhe.
- A ilha nunca rouba o foco do programa em uso, deixa passar os cliques fora da cápsula e se esconde em
  tela cheia (opcional), com o PC bloqueado ou suspenso. Ela se mantém por cima das outras janelas.
- **Ícone na bandeja**: clique abre as configurações; o menu mostra/oculta a ilha, escolhe o monitor e sai
  ("Sair do DynaIsland"): é o único jeito de encerrar o programa.
- **Configurações** (WebView2) em 11 seções: Geral, Aparência, Animações, Comportamento, Monitores, Módulos,
  Notificações, Privacidade, Atalhos, Armazenamento e Sobre.
- Linha de comando (também funciona com o DynaIsland já aberto): `DynaIsland.exe --configuracoes[=atalhos|privacidade|...]`
  e `--novidades` (mostra as novidades da versão).

Mais detalhes no [guia de uso](docs/GUIA_DE_USO.md).

## Privacidade e dados

Tudo fica neste computador, em `%LocalAppData%\DynaIsland` (banco SQLite `dynaisland.db`, cópia automática
das configurações e logs). Nada é enviado a servidores, exceto: a consulta de versão ao GitHub (se ligada), as
coordenadas da cidade para a previsão do tempo, as cotações e o placar, e — se você ligar — a letra da música
(LRCLIB) e o Spotify. Os históricos têm limites configuráveis e podem ser apagados em Configurações › Privacidade e
› Armazenamento, onde também é possível exportar e importar as configurações.

## Requisitos

- Windows 10 (1809+) ou Windows 11, x64, com o WebView2 Runtime.
- Para compilar: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (fixado em `global.json`, com roll-forward).
- Para compilar o **motor nativo** (opcional para desenvolver; obrigatório para gerar versões): o
  [Visual Studio Build Tools 2022](https://visualstudio.microsoft.com/downloads/) com a carga "Desenvolvimento para
  desktop com C++". Sem ele tudo compila e roda; o aplicativo usa os ganchos em C#.

## Compilar, testar e executar

```powershell
powershell -ExecutionPolicy Bypass -File tools\Build-NativeEngine.ps1   # motor nativo (C++) + testes nativos
dotnet build DynaIsland.sln
dotnet test DynaIsland.sln
dotnet run --project src/DynaIsland.App
```

O primeiro comando gera `src\NativeEngine\build\bin\Release\DynaIslandEngine.dll`, que o `dotnet build` copia para a
saída. Feche o DynaIsland antes de compilar (os DLLs ficam travados enquanto ele roda). Os testes de captura e
gravação reais da tela só rodam com `DYNASLAND_CAPTURE_TESTS=1`. Em Debug, `--demo=agua|volume|notificacao|musica|atividade|cotacoes|placar`
mostra um estado da ilha sem precisar provocá-lo.

## Gerar uma versão (instalador e portátil)

Requer o [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`) e o Build Tools do C++.

```powershell
powershell -ExecutionPolicy Bypass -File tools\Build-Release.ps1
```

O script lê a versão de `<Version>` em `Directory.Build.props`, compila o motor nativo, roda os testes e gera em
`publish\` o instalador (`installer\DynaIsland.iss`, publicação self-contained com ReadyToRun) e o zip portátil
(executável único, com a DLL embutida). Para publicar, crie um Release `v<versão>` no GitHub com os dois arquivos
(o instalador sai como `DynaslandSetup-<versão>.exe`, ver [Instalar](#instalar)): a verificação de atualizações
compara a versão instalada com o Release mais recente do repositório configurado em `DynaIslandUpdateRepository`.

## Arquitetura

```
src/
  DynaIsland.Core/            Regras puras, sem Windows/WPF: atividades e IslandCoordinator (máquina de
                             estados da ilha), eventos, contratos dos serviços, módulos, atalhos, layout
  DynaIsland.Infrastructure/  Windows e dados: SQLite (migrações, repositórios, recuperação, migração da pasta
                             antiga), Core Audio, DDC/CI, GSMTC, notificações, área de transferência, clima,
                             Spotify, atualizações, instância única, ponte com o motor nativo
  DynaIsland.Modules/         Um módulo por pasta (lógica + view models + DataTemplates da ilha)
  DynaIsland.App/             WPF + MVVM (CommunityToolkit.Mvvm) + Generic Host/DI: janela da ilha, Configurações
                             (WebView2, páginas em Web/), bandeja, atalhos globais
  DynaIsland.Tests/           xUnit: regras do Core, integração com SQLite/Win32/DLL nativa e módulos com dublês
  NativeEngine/               C++20, DLL x64 (CMake): ganchos globais de mouse, teclado e primeiro plano
installer/                   Script do instalador (Inno Setup)
tools/                       Build-Release.ps1, Build-NativeEngine.ps1 e geração do ícone
docs/                        Guia de uso, arquitetura nativa, especificações e relatórios
```

```
Módulo ──PostActivity──▶ IEventBus ──▶ IslandController (UI) ──▶ IslandCoordinator ──▶ IslandSnapshot
Usuário (clique, hover, roda, atalhos) ──────────────────────────────▲                       │
                                                       IslandWindow ◀── IslandViewModel ◀────┘
```

- **Atividades contínuas** (como a música) ficam enquanto durarem; a de maior prioridade (ou a do módulo fixado) é o
  indicador do compacto. **Transitórias** (volume, notificação, lembrete) expandem a ilha por um tempo e formam fila
  por prioridade; as do mesmo grupo se juntam ("+N").
- Com a ilha oculta, as de prioridade alta esperam e as demais são descartadas com registro no histórico
  de atividades — nada some em silêncio.
- O coordenador não usa timers: informa o próximo prazo e o controlador agenda um único `DispatcherTimer`.
  Os módulos trabalham orientados a eventos e pausam o que não está visível.
- **Motor nativo**: os ganchos de baixo nível do Windows rodam numa thread própria em C++, fora da interface, e entregam
  eventos ao C# por um contrato versionado. Se a DLL faltar, o aplicativo usa a implementação em C#. Detalhes em
  [docs/ARQUITETURA_NATIVA.md](docs/ARQUITETURA_NATIVA.md).

### Criando um módulo

1. Implemente `IIslandModule` em `DynaIsland.Modules/<Nome>/`. O módulo não conhece a janela: publica
   atividades com `IEventBus.PostActivity(...)` e atualiza seus view models via `IUiDispatcher`.
2. Crie o conteúdo implementando `IActivityContent` e um `ResourceDictionary` com DataTemplates de chave
   `"{NomeCompletoDoTipo}:{Compact|Expanded|Interactive}"`.
3. Declare opções em `IIslandModule.Options` (aparecem sozinhas nas configurações), atalhos da tela inicial
   em `IShortcutRegistry`, atalhos globais em `IGlobalShortcutRegistry` e informações rápidas em `IGlanceRegistry`.
4. Registre o módulo e o dicionário em `DynaIsland.Modules/DependencyInjection.cs` (é também onde um módulo
   existente, mas desligado, é religado).

O `ModuleManager` persiste ativo/inativo, isola falhas (um módulo que falha aparece como erro na ilha e
nas configurações) e remove as atividades de um módulo parado.

## Limitações conhecidas

- **Notificações**: sem empacotamento MSIX o Windows não entrega o evento de nova notificação; a lista é
  comparada a cada 1,5 s (só processa diferenças). O toast do próprio Windows continua aparecendo — não há
  API pública para suprimi-lo; desligue as faixas do app no Windows se quiser só a ilha.
- **Brilho**: monitores externos precisam ter DDC/CI ligado no menu do monitor; não há aviso de mudança
  feita pelos botões do monitor (os valores são lidos ao abrir o painel).
- **Troca de saída de áudio**: usa `IPolicyConfig`, interface estável mas não documentada pela Microsoft.
- **Spotify**: a fila e o volume do player exigem conectar a sua conta (Client ID próprio); o volume exige Premium.
- **Motor nativo**: a DLL não é assinada digitalmente; antivírus muito restritivos podem questionar os ganchos globais
  de mouse e teclado (eles só ficam ligados com a ilha aberta).
- O indicador de volume do Windows também aparece ao usar as teclas de volume.
