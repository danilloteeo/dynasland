# Dynasland

Ilha dinâmica (estilo Dynamic Island) para Windows 10 e Windows 11, em C# / .NET 10 / WPF.

> Versão **0.1.0** — todas as fases do escopo (`docs/master.md`) concluídas.
> [Guia de uso](docs/GUIA_DE_USO.md) · [Notas da versão](CHANGELOG.md) · [Relatório final](docs/RELATORIO_FINAL.md)

## Instalar

Baixe em [Releases](https://github.com/danilloteeo/dynasland/releases):

- **`DynaslandSetup-<versão>.exe`** (recomendado): instala só para o seu usuário, sem pedir administrador,
  em `%LocalAppData%\Programs\Dynasland`. Cria atalho no Menu Iniciar (e, se marcado, na Área de Trabalho)
  e oferece "Iniciar com o Windows". Instalar uma versão nova por cima mantém as configurações; o
  Dynasland aberto é fechado automaticamente. Desinstale por Configurações › Aplicativos — a
  desinstalação pergunta se deve apagar também as configurações e os logs (`%LocalAppData%\Dynasland`).
- **`Dynasland-<versão>-portable.zip`**: um único `Dynasland.exe`, sem instalação (as configurações
  ficam no mesmo `%LocalAppData%\Dynasland`).

O Windows SmartScreen pode avisar na primeira execução (os arquivos não são assinados digitalmente):
clique em **Mais informações › Executar assim mesmo**.

## Requisitos

- Windows 10 (1809+) ou Windows 11, x64
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (fixado em `global.json`, com roll-forward)

## Compilar, testar e executar

```powershell
dotnet build Dynasland.sln
dotnet test Dynasland.sln
dotnet run --project src/Dynasland.App
```

O executável fica em `src/Dynasland.App/bin/Debug/net10.0-windows10.0.19041.0/Dynasland.exe`.
Feche o Dynasland antes de compilar (os DLLs ficam travados enquanto ele roda).

## Gerar uma versão (instalador e portátil)

Requer o [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`).

```powershell
powershell -ExecutionPolicy Bypass -File tools\Build-Release.ps1
```

O script lê a versão de `<Version>` em `Directory.Build.props`, roda os testes e gera em `publish\`:

- `DynaslandSetup-<versão>.exe` — publicação self-contained (ReadyToRun) empacotada por `installer\Dynasland.iss`;
- `Dynasland-<versão>-portable.zip` — executável único self-contained.

Para publicar: crie um Release `v<versão>` no GitHub com os dois arquivos. A verificação de atualizações
do aplicativo compara a versão instalada com o Release mais recente do repositório configurado em
`DynaslandUpdateRepository` (`Directory.Build.props`).

## Uso

- A ilha aparece como uma cápsula preta no topo do monitor principal.
- **Passar o mouse**: leve ampliação. **Clicar**: abre o estado interativo com a atividade principal
  (sem outras atividades, o módulo Relógio mostra hora e data); clicar de novo recolhe.
  Sem interação, recolhe sozinha após 6 s. **Roda do mouse** no estado interativo alterna entre atividades.
- Eventos dos módulos (volume, nova música, notificação, bateria) expandem a ilha automaticamente.
- A ilha nunca rouba o foco do aplicativo em uso e deixa passar os cliques fora da cápsula.
- **Ícone na bandeja**: clique esquerdo abre as configurações; o menu permite abrir as configurações, mostrar/ocultar a ilha, escolher o monitor e sair.
- Executar o app novamente com ele aberto apenas reexibe a ilha (instância única).

Configurações e dados: `%LocalAppData%\Dynasland\dynasland.db` (SQLite, criado automaticamente).

## Estrutura

```
src/
  Dynasland.Core/            Modelos, interfaces e regras puras (sem dependência de Windows/WPF)
    Activities/              Atividades, eventos e IslandCoordinator (máquina de estados da ilha)
    Events/                  Barramento de eventos (IEventBus)
    Modules/                 IIslandModule e ModuleManager
    Layout/                  Dimensões por estado, cálculo de posição, escolha de monitor
    Services/                Serviço de configurações da ilha
  Dynasland.Infrastructure/  Implementações dependentes do sistema
    Database/                Conexão SQLite e migrações versionadas (schema_migrations)
    Repositories/            Armazenamento de configurações
    Windows/                 Interop Win32 (monitores/DPI, janela) e integrações:
                             Media (GSMTC), Audio (Core Audio/COM), Brightness (WMI),
                             Power (PowerManager), Notifications (UserNotificationListener)
    Services/                Instância única (Mutex + EventWaitHandle)
  Dynasland.Modules/         Módulos (lógica + view model + DataTemplates de cada um)
    Media/ Volume/ Brightness/ Notifications/ Battery/ Clock/
    Downloads/ Clipboard/ SystemMonitor/ Timer/
  Dynasland.App/             WPF + MVVM (CommunityToolkit.Mvvm) + Generic Host/DI
    Views/                   IslandWindow (janela transparente, camadas por estado, animações)
    ViewModels/              IslandViewModel (projeção do estado do coordenador)
    Services/                IslandController, posicionamento, bandeja, hospedagem dos módulos
  Dynasland.Tests/           xUnit: regras do Core e integração com SQLite/Win32
tools/New-AppIcon.ps1        Gera Assets/dynasland.ico
tools/Build-Release.ps1      Testes + instalador + portátil (publish/)
installer/Dynasland.iss      Script do instalador (Inno Setup 6)
docs/                        Escopo (master.md), guia de uso, relatório final
```

## Como funciona a ilha

```
Módulo ──PostActivity──▶ IEventBus ──▶ IslandController (thread de UI) ──▶ IslandCoordinator
                                                                              │ IslandSnapshot
Usuário (clique, hover, roda) ──▶ IslandViewModel ◀───────────────────────────┘
                                         │
                                  IslandWindow (anima tamanho e camadas)
```

- **Atividade contínua** (`ActivityKind.Live`): existe enquanto durar (música, download). A de maior
  prioridade aparece como indicador no estado compacto. `ExpandOnStart` expande a ilha ao começar.
- **Atividade transitória** (`ActivityKind.Transient`): expande a ilha por um tempo e some (volume,
  notificação). Chegando várias, formam fila por prioridade; prioridade maior substitui a atual.
  Republicar o mesmo `Id` atualiza a atividade e reinicia o tempo.
- Com a ilha interativa, só prioridade `High`+ interrompe o usuário. Com o ponteiro sobre a ilha, nada expira.
- O coordenador não usa timers: informa o próximo prazo e o controlador agenda um único `DispatcherTimer`.

## Criando um módulo

1. Implemente `IIslandModule` (em `Dynasland.Modules/<Nome>/`). O módulo não conhece a janela:
   publica atividades com `IEventBus.PostActivity(...)` e atualiza seus view models via `IUiDispatcher`.
2. Crie o view model do conteúdo implementando `IActivityContent`.
3. Crie um `ResourceDictionary` com DataTemplates de chave `"{NomeCompletoDoTipo}:{Estado}"`
   (`Compact`, `Expanded`, `Interactive`; o interativo recorre ao expandido se ausente).
4. Registre em `Dynasland.Modules/DependencyInjection.cs` o módulo e `ModuleTemplates.FromComponent(...)`.

O `ModuleManager` persiste ativo/inativo na tabela `modules`, isola falhas de inicialização e, ao parar
um módulo, remove suas atividades da ilha.

## Configurações

Abra pelo clique no ícone da bandeja, pelo menu da bandeja ou pela engrenagem da ilha
(Dynasland.exe --configuracoes abre direto). Categorias:

- **Geral**: iniciar com o Windows (registro Run do usuário), iniciar minimizado, idioma, mostrar a ilha,
  ocultar em tela cheia, distância do topo e monitor.
- **Aparência**: tema automático/escuro/claro (janelas; a ilha é sempre preta), cor de destaque,
  opacidade, sombra, arredondamento, largura mínima/máxima e altura da ilha.
- **Animações**: ligar/desligar, velocidade, intensidade do efeito de mola e tempo expandida.
- **Módulos**: ligar/desligar, status (ativo, indisponível com motivo, falha) e opções de cada módulo.
- **Notificações**: todos exceto bloqueados ou somente permitidos, duração, prioridade alta e lista de aplicativos.
- **Sistema**: verificar atualizações (Releases públicos do GitHub configurados em DynaslandUpdateRepository),
  restaurar padrões, versão, privacidade e pasta de logs.

Novas opções de módulo: declare ModuleOptions em IIslandModule.Options e leia com IModuleOptions;
a janela as exibe automaticamente.

## Módulos

| Módulo | Integração | Na ilha |
|---|---|---|
| Música e mídia | GSMTC (`GlobalSystemMediaTransportControlsSessionManager`) | Compacto: capa + equalizador. Nova faixa expande. Interativo: capa, título, artista, app, progresso, anterior/play-pausa/próxima |
| Volume | Core Audio (`IAudioEndpointVolume` + callbacks, troca de dispositivo padrão) | Mudança externa expande com nível. Interativo: dispositivo, slider e mudo |
| Brilho | WMI (`WmiMonitorBrightness*`) | Mudança externa expande. Interativo: slider |
| Notificações | `UserNotificationListener` | Nova notificação expande. Interativo: texto completo, Abrir, Dispensar, Silenciar app |
| Bateria | `Windows.System.Power.PowerManager` | Conectar/desconectar, bateria fraca (20%) e muito fraca (10%). Interativo: carga, estado e autonomia |
| Relógio | Hora local | Tela inicial ao abrir a ilha sem outras atividades, com atalhos (cronômetro, temporizador, sistema, configurações) |
| Downloads | Arquivos temporários dos navegadores na pasta Downloads (Chrome, Edge, Opera, Brave: `.crdownload`; Firefox: `.part`) | Nome, bytes recebidos e velocidade (o navegador não informa o tamanho total, então o progresso é indeterminado); ao terminar: Abrir e Mostrar na pasta |
| Área de transferência | `AddClipboardFormatListener` + histórico do Windows (Win+V) | Mostra o que foi copiado; interativo: histórico recente para copiar de novo (ignora conteúdo marcado como privado) |
| Monitor do sistema | PDH (CPU, GPU), memória, discos, NVML/WMI para temperaturas | Painel com uso atual; avisos de memória alta e disco quase cheio |
| Cronômetro e temporizador | Relógio do sistema (por instante, sem acumular erro) | Contagem no compacto; alarme ao terminar, com Repetir |

Módulos sem suporte no computador (sem bateria, brilho não controlável, acesso às notificações negado)
aparecem como **indisponível** no menu e não exibem controles.

### Limitações conhecidas

- **Brilho** só em telas integradas (notebooks/tablets). Monitores externos (DDC/CI) não expõem
  notificação de mudança via WMI e não são suportados nesta versão.
- **Notificações**: sem identidade de pacote (MSIX), o Windows não entrega o evento
  `NotificationChanged`; o serviço compara a lista a cada 1,5 s (custo baixo, só processa diferenças).
  O Windows continua exibindo o próprio toast — não há API pública para suprimi-lo.
- **Volume**: o indicador nativo de volume do Windows também aparece ao usar as teclas de volume.
- APIs WinRT/COM obtidas em threads MTA são chamadas fora da thread de UI (comandos de mídia e
  remoção de notificações não têm efeito se chamados da thread STA).

## Integrações com o Windows (notas técnicas)

- **Bloqueio e suspensão**: `SystemEvents.SessionSwitch`/`PowerModeChanged` (`SessionStateService`). Com a
  sessão bloqueada ou o computador suspenso a ilha fica oculta (o conteúdo para de animar); ao voltar, ela
  reaparece e recalcula a posição (monitores podem ter mudado). O temporizador reagenda o término ao
  retomar, porque os timers do .NET não contam o tempo em suspensão.
- **Banco corrompido**: na inicialização, `DatabaseRecovery` roda `PRAGMA quick_check`; se o arquivo estiver
  danificado, ele é guardado como `dynasland.db.corrompido-<data>` e recriado com os padrões (o usuário é avisado).

- **Janela sem foco**: `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` + resposta `MA_NOACTIVATE` a `WM_MOUSEACTIVATE`.
  A janela fica fora da barra de tarefas e do Alt+Tab.
- **Cliques atravessando**: janela WPF com `AllowsTransparency` (layered); pixels com alfa 0 não capturam o mouse.
  A janela tem tamanho fixo e só a cápsula é animada, evitando redimensionar o HWND a cada frame.
- **DPI**: manifest `PerMonitorV2`. A posição é calculada em pixels físicos (`GetDpiForMonitor`,
  `GetMonitorInfo`) e recalculada em `WM_DPICHANGED`, `WM_DISPLAYCHANGE` e mudança da área útil
  (`WM_SETTINGCHANGE`/`SPI_SETWORKAREA`). Se o monitor escolhido for desconectado, usa o principal.
- **Bandeja**: `System.Windows.Forms.NotifyIcon` (parte do runtime Windows Desktop, sem pacote extra).

## Migrações

Cada migração implementa `IMigration` (ou herda `SqlMigration`) com versão crescente e é registrada
em `Infrastructure/DependencyInjection.cs`. São aplicadas na inicialização, cada uma em transação própria.
