# Dynasland

Ilha dinâmica (estilo Dynamic Island) para Windows 10 e Windows 11, em C# / .NET 10 / WPF.

> Estado atual: **Fase 3 — Integrações** concluída. Veja `docs/master.md` para o escopo completo.

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

## Uso

- A ilha aparece como uma cápsula preta no topo do monitor principal.
- **Passar o mouse**: leve ampliação. **Clicar**: abre o estado interativo com a atividade principal
  (sem outras atividades, o módulo Relógio mostra hora e data); clicar de novo recolhe.
  Sem interação, recolhe sozinha após 6 s. **Roda do mouse** no estado interativo alterna entre atividades.
- Eventos dos módulos (volume, nova música, notificação, bateria) expandem a ilha automaticamente.
- A ilha nunca rouba o foco do aplicativo em uso e deixa passar os cliques fora da cápsula.
- **Ícone na bandeja**: clique esquerdo mostra/oculta a ilha; o menu permite escolher o monitor, ativar/desativar módulos e sair.
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
  Dynasland.App/             WPF + MVVM (CommunityToolkit.Mvvm) + Generic Host/DI
    Views/                   IslandWindow (janela transparente, camadas por estado, animações)
    ViewModels/              IslandViewModel (projeção do estado do coordenador)
    Services/                IslandController, posicionamento, bandeja, hospedagem dos módulos
  Dynasland.Tests/           xUnit: regras do Core e integração com SQLite/Win32
tools/New-AppIcon.ps1        Gera Assets/dynasland.ico
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

## Módulos

| Módulo | Integração | Na ilha |
|---|---|---|
| Música e mídia | GSMTC (`GlobalSystemMediaTransportControlsSessionManager`) | Compacto: capa + equalizador. Nova faixa expande. Interativo: capa, título, artista, app, progresso, anterior/play-pausa/próxima |
| Volume | Core Audio (`IAudioEndpointVolume` + callbacks, troca de dispositivo padrão) | Mudança externa expande com nível. Interativo: dispositivo, slider e mudo |
| Brilho | WMI (`WmiMonitorBrightness*`) | Mudança externa expande. Interativo: slider |
| Notificações | `UserNotificationListener` | Nova notificação expande. Interativo: texto completo, Abrir, Dispensar, Silenciar app |
| Bateria | `Windows.System.Power.PowerManager` | Conectar/desconectar, bateria fraca (20%) e muito fraca (10%). Interativo: carga, estado e autonomia |
| Relógio | Hora local | Conteúdo padrão ao abrir a ilha sem outras atividades |

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
