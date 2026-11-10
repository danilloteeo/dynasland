# Relatório final — Dynasland 0.1.0

Ilha dinâmica para Windows 10/11 em C# / .NET 10 / WPF, conforme o escopo de `docs/master.md`.
Arquitetura em camadas: `Core` (regras puras, sem Windows/WPF), `Infrastructure` (integrações com o
Windows e SQLite), `Modules` (um módulo por funcionalidade, com view models e templates próprios) e `App`
(WPF + MVVM + Generic Host/DI). 188 testes automatizados (xUnit).

## Fases

### Fase 1 — Fundação
Solução .NET 10, janela flutuante transparente que não rouba o foco (`WS_EX_NOACTIVATE`, `MA_NOACTIVATE`),
cliques atravessando fora da cápsula, posicionamento em pixels físicos por monitor/DPI (PerMonitorV2),
SQLite com migrações versionadas, bandeja do sistema e instância única (Mutex + EventWaitHandle).

### Fase 2 — Interface dinâmica
Barramento de eventos; `ModuleManager` com persistência de ativo/inativo e isolamento de falhas;
`IslandCoordinator` (compacto/expandido/interativo, fila por prioridade, prazos sem polling — um único
timer para o próximo prazo); camadas animadas por estado com DataTemplates por módulo; módulo Relógio.

### Fase 3 — Integrações com o Windows
Mídia (GSMTC), volume (Core Audio + callbacks COM), brilho (WMI), notificações (`UserNotificationListener`)
e bateria (`PowerManager`). Módulos sem suporte no computador aparecem como indisponíveis, com o motivo.
Conteúdo invisível pausa timers e animações.

### Fase 4 — Monitoramento e utilidades
Monitor do sistema (PDH, memória, discos, temperaturas quando disponíveis) com avisos; downloads dos
navegadores; área de transferência (listener Win32, respeito ao conteúdo privado, histórico do Windows);
cronômetro e temporizador por instante; tela inicial com atalhos; log em arquivo.

### Fase 5 — Configurações
Seis categorias (Geral, Aparência, Animações, Módulos, Notificações, Sistema), tema claro/escuro/automático
com cor de destaque, aparência da ilha, opções por módulo declaradas pelo próprio módulo, filtros de
notificação por aplicativo, iniciar com o Windows/minimizado, ocultar em tela cheia, verificação de
atualizações via GitHub Releases, restaurar padrões. Depois: várias sessões de mídia simultâneas como abas.

### Fase 6 — Finalização
- **Sessão bloqueada e suspensão** (seção 12): `SessionStateService` (SystemEvents) oculta a ilha com
  Win+L/suspensão e a restaura ao voltar, recalculando a posição; o temporizador reagenda o término ao
  retomar (timers do .NET não contam o tempo suspenso). Estado combinado testado em `SessionStateTests`.
- **Recuperação de erros**: `DatabaseRecovery` verifica o banco (`PRAGMA quick_check`) antes das
  migrações; arquivo corrompido é guardado como `dynasland.db.corrompido-<data>` e recriado com os padrões,
  com aviso ao usuário. Testado com arquivo inválido e com páginas danificadas.
- **Otimização**: com a janela oculta, nenhum conteúdo é considerado visível (equalizador e barras de
  progresso param); rajadas de eventos de mídia dos navegadores são agrupadas (250 ms) numa só releitura.
- **Acessibilidade**: botões de ícone anunciam a dica como nome (antes o leitor de tela lia o glifo);
  sliders de volume e brilho nomeados. Verificado por UI Automation.
- **Instalador** (seção 14): Inno Setup, por usuário, sem administrador, em
  `%LocalAppData%\Programs\Dynasland`; atalhos; "Iniciar com o Windows" (mesma entrada `Run` do
  aplicativo); fecha o Dynasland aberto antes de instalar/desinstalar; atualização preserva as
  configurações; desinstalação pergunta se apaga `%LocalAppData%\Dynasland`. Script `tools\Build-Release.ps1`.
- **Documentação**: README (instalar, compilar, gerar versão), guia de uso, notas da versão e este relatório.

## Medições (desktop Win10 19045, 24 threads lógicas, Release instalado)

| Situação | Memória privada | CPU |
|---|---|---|
| Ilha oculta, parado | ~46 MB | ~0,9 s de CPU a cada 30 s (≈0,1% do total) — leitura periódica de notificações e eventos de mídia |
| Ilha visível, música tocando (equalizador a 20 fps) | 60–140 MB, oscilando | ~4% de um núcleo (≈0,2% do total) |

Em ~6 minutos de observação a memória privada oscilou (83–140 MB) sem crescer de forma contínua, e o
working set ficou estável (~175 MB); o heap gerenciado ficou em ~4 MB. A variação vem de memória nativa
(WinRT/renderização), não de objetos .NET retidos.

## Testes da Fase 6

| Teste | Resultado |
|---|---|
| Testes automatizados (`dotnet test`) | 188/188 aprovados |
| Instalar silencioso (arquivos, atalho, entrada em Aplicativos, `Run`) | OK |
| Atualizar por cima com o Dynasland aberto (fechado automaticamente, banco preservado) | OK |
| Desinstalar silencioso (entrada em Aplicativos e `Run` removidas, dados preservados) | OK — numa execução, o atalho e a pasta vazia ficaram para trás por um bloqueio temporário do Windows (`in use (32)`) logo após a instalação; removidos em seguida sem erro |
| Banco corrompido (arquivo inválido; páginas danificadas) | Recriado, cópia guardada |
| Nomes acessíveis (UI Automation) | Botão de configurações anunciado como "Configurações" |

## Pendências e limitações

- **Validar no uso real**: Win+L/suspensão, troca de monitor principal, desconexão do monitor da ilha e
  mudança de resolução em execução (a lógica de reposicionamento existe e tem testes unitários, mas não
  houve teste manual nesta sessão), alarme do temporizador e a barra de título no tema claro do Windows 10.
- Notificações sem MSIX: comparação a cada 1,5 s e o toast do Windows continua aparecendo. MSIX exigiria
  certificado de assinatura.
- Brilho apenas em telas integradas; arquivos sem assinatura digital (aviso do SmartScreen).
