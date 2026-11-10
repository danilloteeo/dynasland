# Dynasland

Ilha dinâmica (estilo Dynamic Island) para Windows 10 e Windows 11, em C# / .NET 10 / WPF: uma cápsula
no topo da tela que mostra o que está acontecendo — música, volume, notificações, downloads, timers,
gravações — e vira uma central de controles com um clique.

> Versão **1.0.0**. [Guia de uso](docs/GUIA_DE_USO.md) · [Notas da versão](CHANGELOG.md) ·
> [Especificação](docs/master.md) · [Diagnóstico da evolução](docs/DIAGNOSTICO_EVOLUCAO.md)

## Instalar

Baixe em [Releases](https://github.com/danilloteeo/dynasland/releases):

- **`DynaslandSetup-<versão>.exe`** (recomendado): instala só para o seu usuário, sem pedir administrador,
  em `%LocalAppData%\Programs\Dynasland`. Cria atalho no Menu Iniciar (e, se marcado, na Área de Trabalho)
  e oferece "Iniciar com o Windows". Instalar uma versão nova por cima mantém as configurações; o
  Dynasland aberto é fechado automaticamente. Desinstale por Configurações › Aplicativos — a
  desinstalação pergunta se deve apagar também as configurações e os históricos (`%LocalAppData%\Dynasland`).
- **`Dynasland-<versão>-portable.zip`**: um único `Dynasland.exe`, sem instalação (os dados ficam no
  mesmo `%LocalAppData%\Dynasland`).

O Windows SmartScreen pode avisar na primeira execução (os arquivos não são assinados digitalmente):
clique em **Mais informações › Executar assim mesmo**.

## O que ele faz

| Módulo | O que aparece | Integração com o Windows |
|---|---|---|
| **Media Center** | Capa, título, artista, progresso e controles; várias mídias ao mesmo tempo viram abas | Global System Media Transport Controls (Spotify, navegadores, players) |
| **Hardware Control** | Volume com troca do dispositivo de saída e microfone; brilho de cada tela | Core Audio (`IPolicyConfig` para a saída padrão), WMI na tela do notebook, DDC/CI em monitores externos |
| **Notification Center** | Notificações agrupadas por app, histórico, regras por aplicativo (mostrar, só histórico, bloquear, prioridade, duração) | `UserNotificationListener` |
| **Download Manager** | Arquivo, bytes recebidos e velocidade; abrir ou mostrar na pasta ao terminar | Arquivos temporários dos navegadores na pasta Downloads |
| **Clipboard Manager** | Histórico de texto e imagens com busca, favoritos e exclusão | Listener Win32 + área de transferência do Windows; respeita conteúdo marcado como privado |
| **Gaming Monitor** | Modo gamer automático, FPS, CPU, GPU, memória, VRAM, frequência e temperaturas disponíveis | Detecção de jogos por loja/tela cheia, PDH, NVML, ETW do DirectX (sem injeção) |
| **Focus Center** | Cronômetro com voltas, temporizador e Pomodoro configurável, com histórico | Baseado em instantes reais (continua certo após suspender) |
| **Capture Center** | Captura de região, tela ou janela; gravação de tela em MP4 com áudio do sistema e microfone | GDI/PrintWindow, Windows.Graphics.Capture, Media Foundation, WASAPI loopback |
| **Life Center** | Tempo agora e próximos dias; agenda local com lembretes | [Open-Meteo](https://open-meteo.com) (gratuito, sem chave), SQLite local |
| Bateria · Monitor do sistema · Relógio | Avisos de carga, uso de recursos, tela inicial com atalhos | `PowerManager`, PDH, discos |

Além da ilha, há a **Central** (históricos da área de transferência, notificações, foco e capturas, e a
agenda) e as **Configurações** em 11 categorias. Módulos sem suporte no computador aparecem como
indisponíveis, com o motivo, em vez de mostrar controles que não funcionam.

### Atalhos padrão

| Atalho | Ação |
|---|---|
| Ctrl+Alt+D | Abrir/fechar a ilha |
| Ctrl+Alt+H | Mostrar/ocultar a ilha |
| Ctrl+Alt+V | Histórico da área de transferência |
| Ctrl+Alt+A | Agenda |
| Ctrl+Alt+P | Pomodoro: iniciar/pausar |
| Ctrl+Alt+M | Microfone: desligar/ligar |
| Ctrl+Alt+S | Capturar região |
| Ctrl+Alt+Shift+S | Capturar a tela inteira |
| Ctrl+Alt+W | Capturar a janela ativa |
| Ctrl+Alt+R | Gravar a tela: iniciar/parar |

Todos podem ser trocados ou desligados em Configurações › Atalhos (outras ações também podem receber atalho).

## Uso rápido

- **Passar o mouse** amplia a ilha; **clicar** abre o painel da atividade atual; clicar fora (ou esperar) recolhe.
- Com várias atividades, a **roda do mouse** e os pontos do rodapé trocam de aba. No rodapé também há
  **fixar** (o módulo fica sempre no compacto), **fechar** a atividade e as configurações.
- Sem nenhuma atividade, o clique mostra hora, data, tempo, próximo compromisso e atalhos dos módulos.
- A ilha nunca rouba o foco do programa em uso, deixa passar os cliques fora da cápsula e se esconde em
  tela cheia (opcional), com o PC bloqueado ou suspenso.
- **Ícone na bandeja**: clique abre as configurações; o menu mostra/oculta a ilha, escolhe o monitor e sai.
- Linha de comando (também funciona com o Dynasland já aberto):
  `Dynasland.exe --central[=agenda|notificacoes|foco|capturas]` e `--configuracoes[=atalhos|privacidade|...]`.

Mais detalhes no [guia de uso](docs/GUIA_DE_USO.md).

## Privacidade e dados

Tudo fica neste computador, em `%LocalAppData%\Dynasland` (banco SQLite `dynasland.db`, cópia automática
das configurações e logs). Nada é enviado a servidores, exceto a consulta de versão ao GitHub (se ligada)
e as coordenadas da cidade escolhida para a previsão do tempo. Os históricos têm limites configuráveis e
podem ser apagados em Configurações › Privacidade e › Armazenamento, onde também é possível exportar e
importar as configurações.

## Requisitos

- Windows 10 (1809+) ou Windows 11, x64. A gravação de tela exige Windows 10 1903 ou mais recente.
- Para compilar: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (fixado em `global.json`, com roll-forward).

## Compilar, testar e executar

```powershell
dotnet build Dynasland.sln
dotnet test Dynasland.sln
dotnet run --project src/Dynasland.App
```

Feche o Dynasland antes de compilar (os DLLs ficam travados enquanto ele roda). Os testes de captura e
gravação reais da tela só rodam com `DYNASLAND_CAPTURE_TESTS=1`.

## Gerar uma versão (instalador e portátil)

Requer o [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`).

```powershell
powershell -ExecutionPolicy Bypass -File tools\Build-Release.ps1
```

O script lê a versão de `<Version>` em `Directory.Build.props`, roda os testes e gera em `publish\` o
instalador (`installer\Dynasland.iss`, publicação self-contained com ReadyToRun) e o zip portátil
(executável único). Para publicar, crie um Release `v<versão>` no GitHub com os dois arquivos: a
verificação de atualizações compara a versão instalada com o Release mais recente do repositório
configurado em `DynaslandUpdateRepository`.

## Arquitetura

```
src/
  Dynasland.Core/            Regras puras, sem Windows/WPF: atividades e IslandCoordinator (máquina de
                             estados da ilha), eventos, contratos dos serviços, módulos, atalhos, layout
  Dynasland.Infrastructure/  Windows e dados: SQLite (migrações, repositórios, recuperação), Core Audio,
                             DDC/CI, GSMTC, notificações, área de transferência, captura e gravação,
                             detecção de jogos e FPS, clima (Open-Meteo), métricas, instância única
  Dynasland.Modules/         Um módulo por pasta (lógica + view models + DataTemplates da ilha)
  Dynasland.App/             WPF + MVVM (CommunityToolkit.Mvvm) + Generic Host/DI: janela da ilha,
                             Central, Configurações (uma página por categoria), bandeja, atalhos globais
  Dynasland.Tests/           xUnit: regras do Core, integração com SQLite/Win32 e módulos com dublês
installer/                   Script do instalador (Inno Setup)
tools/                       Build-Release.ps1 e geração do ícone
docs/                        Especificação, guia de uso, diagnóstico e relatórios
```

```
Módulo ──PostActivity──▶ IEventBus ──▶ IslandController (UI) ──▶ IslandCoordinator ──▶ IslandSnapshot
Usuário (clique, hover, roda, atalhos) ──────────────────────────────▲                       │
                                                       IslandWindow ◀── IslandViewModel ◀────┘
```

- **Atividades contínuas** (música, download, gravação, jogo) ficam enquanto durarem; a de maior prioridade
  (ou a do módulo fixado) é o indicador do compacto. **Transitórias** (volume, notificação, lembrete)
  expandem a ilha por um tempo e formam fila por prioridade; as do mesmo grupo se juntam ("+N").
- Com a ilha oculta, as de prioridade alta esperam e as demais são descartadas com registro no histórico
  de atividades — nada some em silêncio.
- O coordenador não usa timers: informa o próximo prazo e o controlador agenda um único `DispatcherTimer`.
  Os módulos trabalham orientados a eventos e pausam o que não está visível.

### Criando um módulo

1. Implemente `IIslandModule` em `Dynasland.Modules/<Nome>/`. O módulo não conhece a janela: publica
   atividades com `IEventBus.PostActivity(...)` e atualiza seus view models via `IUiDispatcher`.
2. Crie o conteúdo implementando `IActivityContent` e um `ResourceDictionary` com DataTemplates de chave
   `"{NomeCompletoDoTipo}:{Compact|Expanded|Interactive}"`.
3. Declare opções em `IIslandModule.Options` (aparecem sozinhas nas configurações), atalhos da tela inicial
   em `IShortcutRegistry`, atalhos globais em `IGlobalShortcutRegistry` e informações rápidas em `IGlanceRegistry`.
4. Registre o módulo e o dicionário em `Dynasland.Modules/DependencyInjection.cs`.

O `ModuleManager` persiste ativo/inativo, isola falhas (um módulo que falha aparece como erro na ilha e
nas configurações) e remove as atividades de um módulo parado.

## Limitações conhecidas

- **Notificações**: sem empacotamento MSIX o Windows não entrega o evento de nova notificação; a lista é
  comparada a cada 1,5 s (só processa diferenças). O toast do próprio Windows continua aparecendo — não há
  API pública para suprimi-lo; desligue as faixas do app no Windows se quiser só a ilha.
- **Downloads**: os navegadores não informam o tamanho total nos arquivos temporários, então o progresso é
  indeterminado (nome, bytes recebidos e velocidade são reais).
- **Brilho**: monitores externos precisam ter DDC/CI ligado no menu do monitor; não há aviso de mudança
  feita pelos botões do monitor (os valores são lidos ao abrir o painel).
- **Troca de saída de áudio**: usa `IPolicyConfig`, interface estável mas não documentada pela Microsoft.
- **Gravação**: conteúdo protegido (DRM) sai preto; no Windows 10 aparece a borda amarela de captura; o
  cursor sempre aparece antes do Windows 10 2004.
- **FPS**: exige executar como administrador ou estar no grupo "Usuários de log de desempenho".
- **Temperaturas**: só as que o hardware expõe ao Windows sem driver (zona térmica ACPI, NVML da NVIDIA);
  consumo de energia não é medido.
- **Detecção de jogos**: heurística por pasta das lojas e tela cheia, não universal.
- O indicador de volume do Windows também aparece ao usar as teclas de volume.
