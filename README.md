# Dynasland

Ilha dinâmica (estilo Dynamic Island) para Windows 10 e Windows 11, em C# / .NET 10 / WPF.

> Estado atual: **Fase 2 — Interface dinâmica** concluída. Veja `docs/master.md` para o escopo completo.

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
- Eventos dos módulos (ex.: volume, notificações — Fase 3) expandem a ilha automaticamente pelo tempo configurado.
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
    Windows/                 Interop Win32: monitores/DPI, estilos de janela, mensagens
    Services/                Instância única (Mutex + EventWaitHandle)
  Dynasland.Modules/         Módulos (serviço + view model + DataTemplates de cada um)
    Clock/                   Relógio
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
