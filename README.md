# Dynasland

Ilha dinâmica (estilo Dynamic Island) para Windows 10 e Windows 11, em C# / .NET 10 / WPF.

> Estado atual: **Fase 1 — Fundação** concluída. Veja `docs/master.md` para o escopo completo.

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
- **Passar o mouse**: leve ampliação. **Clicar**: expande (relógio e data); clicar de novo recolhe.
  Sem interação, recolhe sozinha após 6 s.
- A ilha nunca rouba o foco do aplicativo em uso e deixa passar os cliques fora da cápsula.
- **Ícone na bandeja**: clique esquerdo mostra/oculta a ilha; o menu permite escolher o monitor e sair.
- Executar o app novamente com ele aberto apenas reexibe a ilha (instância única).

Configurações e dados: `%LocalAppData%\Dynasland\dynasland.db` (SQLite, criado automaticamente).

## Estrutura

```
src/
  Dynasland.Core/            Modelos, interfaces e regras puras (sem dependência de Windows/WPF)
    Layout/                  Dimensões por estado, cálculo de posição, escolha de monitor
    Services/                Serviço de configurações da ilha
  Dynasland.Infrastructure/  Implementações dependentes do sistema
    Database/                Conexão SQLite e migrações versionadas (schema_migrations)
    Repositories/            Armazenamento de configurações
    Windows/                 Interop Win32: monitores/DPI, estilos de janela, mensagens
    Services/                Instância única (Mutex + EventWaitHandle)
  Dynasland.App/             WPF + MVVM (CommunityToolkit.Mvvm) + Generic Host/DI
    Views/                   IslandWindow (janela transparente + animações)
    ViewModels/              IslandViewModel (estado, relógio, auto-recolhimento)
    Services/                Posicionamento da ilha, ícone da bandeja
  Dynasland.Tests/           xUnit: regras do Core e integração com SQLite/Win32
tools/New-AppIcon.ps1        Gera Assets/dynasland.ico
```

`Dynasland.Modules` será criado na Fase 3, quando o primeiro módulo real (mídia) for implementado.

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
