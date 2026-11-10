# Diagnóstico — evolução do Dynasland 0.1.0 para 1.0

Referência: `docs/dynasland_evolution.md` (seção 20, "Antes de iniciar"). Feito em 2026-11-10 lendo o
código atual, rodando o aplicativo e testando hardware nesta máquina (Win10 19045, 2 monitores, sem
bateria).

## 1. Visão geral do projeto atual

| Projeto | Linhas (C#+XAML) | Papel |
|---|---|---|
| Dynasland.Core | ~2 000 | Atividades, coordenador da ilha, eventos, contratos, layout — sem Windows/WPF |
| Dynasland.Infrastructure | ~3 600 | SQLite, integrações com o Windows (GSMTC, Core Audio, WMI, PDH, notificações, clipboard, downloads) |
| Dynasland.Modules | ~4 100 | 11 módulos, cada um com lógica, view models e templates |
| Dynasland.App | ~3 700 | WPF/MVVM, janela da ilha, configurações, bandeja, host/DI |
| Dynasland.Tests | ~3 400 | 188 testes xUnit (Core, integrações com SQLite/Win32, módulos com dublês) |

Dependências: CommunityToolkit.Mvvm, Microsoft.Extensions.Hosting, Microsoft.Data.Sqlite, System.Management
e, nos testes, xUnit e TimeProvider.Testing. Todas compatíveis com .NET 10, nenhuma desnecessária.
Não há dados simulados no código de produção. Não há `TODO`/`NotImplementedException`, nem `catch` vazio,
e os `async void` são handlers de evento com `try/catch`.

## 2. O que funciona (manter)

- **Arquitetura**: camadas bem separadas. Módulos só falam com a ilha pelo barramento (`IEventBus`) e
  declaram as próprias opções (`ModuleOptions`), que a tela de configurações exibe automaticamente.
  `ModuleManager` liga e desliga módulos, persiste o estado e isola falhas.
- **Ilha**: três estados (compacto, expandido, interativo) e fila por prioridade com 4 níveis (já existe
  `Critical`). Atividades contínuas e transitórias; abas para várias atividades. Prazos sem polling,
  com um único timer. Não rouba o foco, deixa os cliques passarem, tem DPI por monitor, e ao desconectar
  o monitor escolhido volta para o principal. Se oculta com tela cheia, bloqueio e suspensão.
- **Mídia**: GSMTC com várias sessões, capa, progresso, controles, abas e sessão pausada que expira.
- **Volume** (Core Audio, com eventos) e **notificações** (filtros permitidos/bloqueados, silenciar app,
  abrir origem, duração).
- **Cronômetro e temporizador** baseados em instantes: o temporizador já sobrevive à suspensão.
- **Persistência**: migrações versionadas, recuperação de banco corrompido.
- **Distribuição**: instalador por usuário, versão portátil, verificação de atualizações.

## 3. Parcialmente implementado (melhorar)

| Área (seção do documento) | Existe | Falta |
|---|---|---|
| Gerenciador de eventos (3.2) | Id, módulo, prioridade, duração, fila | **Estado e data de ocorrência** do evento, **agrupamento** de eventos compatíveis, histórico. Com a ilha oculta, a fila é **descartada** (`IslandCoordinator.Reset`), o que contraria "não descartar eventos importantes silenciosamente" |
| Estados da ilha (3.1) | Compacto, expandido, interativo, persistente (Live) | Estados de **carregamento** e de **erro** padronizados; "notificações" como estado próprio (hoje é só conteúdo expandido) |
| Interações (3.3) | Clique, hover, roda, abas | **Clique fora para recolher** (hoje recolhe por tempo), **duplo clique** configurável, **atalhos de teclado globais**, **fechar** e **fixar** atividades |
| Monitores (3.4) | Detecção, escolha, DPI, desconexão | Teste real de troca de principal/resolução; escolha de monitor dentro de "Monitores" nas configurações |
| Volume (4.1) | Nível, mudo, nome do dispositivo, eventos | **Lista e troca do dispositivo de saída**, **microfone** (nível/mudo) |
| Brilho (4.2) | WMI em telas integradas | **DDC/CI para monitores externos**: testado nesta máquina, os 2 monitores respondem (brilho 90 e 56) |
| Mídia (5) | Quase tudo | **Álbum** (lido, não exibido), **escolha da sessão** controlada (hoje por aba), tratamento específico do Spotify (ex.: abrir o app pelo cartão) |
| Notificações (6) | Filtros, silenciar, abrir, duração global | **Histórico**, horário exibido, **prioridade e duração por app**, "ocultar" diferente de "silenciar" |
| Downloads (7) | Provedor de navegadores pela pasta Downloads (`.crdownload`, `.part`), concluído, cancelado | Estados **pausado, retomado e erro**; horário de conclusão; provedores de gerenciadores de download. O provedor **não sabe o tamanho total** (progresso indeterminado e sem tempo restante): a interface é honesta, mas o README (escrito na Fase 6) afirmava "tempo restante" — **corrigido junto com este diagnóstico** |
| Área de transferência (8) | Item atual, 4 itens do histórico do Windows, copiar de novo, respeito ao conteúdo privado | **Histórico próprio** (texto e imagem), **pesquisa**, **favoritos**, **excluir**, **limpar**, **pausar a captura**, atalho |
| Monitor do sistema / Gaming (9) | CPU, GPU (PDH), RAM, discos, temperatura por Thermal Zone/NVML | **VRAM, frequência, consumo**; **detecção de jogos**, **modo gamer**, **FPS** |
| Foco (10) | Cronômetro e temporizador | **Voltas**, **Pomodoro** configurável, **histórico de sessões** |
| Configurações (13) | 6 categorias | Mais 5: **Comportamento, Monitores, Privacidade, Atalhos, Armazenamento** (e "Sistema" vira "Sobre"). O cartão "Idioma" mostra um texto fixo, sem ação: fica somente informativo ou sai |
| Provedores (14) | `IDownloadProvider` e serviços por integração | Contratos uniformes para provedores de mídia, hardware, clima, calendário e métricas; ciclo de vida comum |
| Banco (15) | 2 tabelas chave-valor, migrações, recuperação | Tabelas de **histórico** (notificações, clipboard, foco, eventos do calendário), **índices**, **retenção/limpeza**, **backup e exportação** das configurações |

## 4. Inexistente (criar)

- **Capture Center** (11): captura de tela/janela/região e gravação com áudio e microfone.
- **Life Center** (12): clima e calendário local com lembretes.
- **Modo gamer e FPS** (9.2, 9.3).
- **Atalhos de teclado globais** e categoria Atalhos.

## 5. Problemas arquiteturais e de organização

1. **Fila descartada ao ocultar** (`IslandCoordinator.Reset`): correção pontual, prioritária.
2. **Evento sem ciclo de vida**: `IslandActivity` não tem estado (pendente/exibido/concluído/dispensado)
   nem data de ocorrência, então não é possível histórico, agrupamento nem "fechar".
3. **Contratos agrupados por fase** (`Phase4Services.cs`, `Phase5Services.cs`): reorganizar por área
   (Media, Audio, Clipboard...) antes de crescer.
4. **Configurações monolíticas**: `SettingsWindow.xaml` (577 linhas) e `SettingsViewModel` (707 linhas).
   Com 11 categorias, isso precisa virar uma view e um view model por categoria.
5. **Persistência só em chave-valor**: históricos precisam de tabelas próprias, com índice por data e retenção.

## 6. Desempenho (medido na Fase 6)

- Oculto: ~46 MB e ≈0,1% de CPU. Visível com música: 60–140 MB oscilando (sem vazamento) e ~4% de um
  núcleo por causa do **equalizador numa janela transparente** (a renderização em software do WPF
  redesenha a janela inteira, de 480×250, a cada quadro). Opções: janela menor no compacto, menos quadros
  por segundo, ou equalizador estático como padrão.
- Polling restante: notificações a cada 1,5 s (limitação sem MSIX) e amostragem de 1 s só enquanto o
  painel do sistema ou um download estão ativos. O resto é orientado a eventos.
- Novos módulos (clima, gravação, jogos) devem seguir a mesma regra: sem trabalho quando nada é exibido.

## 7. Consistência visual

A ilha e os módulos compartilham pincéis, fontes e estilos (`IslandTheme.xaml`). Faltam estados
padronizados de **carregamento, vazio e erro**, hoje resolvidos caso a caso em cada template. Painéis
maiores (histórico, calendário, captura) vão precisar de componentes reutilizáveis: lista, cabeçalho de
seção e botões de ação.

## 8. Viabilidade e riscos por item novo

| Item | Como | Risco / limitação |
|---|---|---|
| Brilho DDC/CI | Monitor Configuration API (`dxva2`), documentada | Baixo. Funciona nos 2 monitores desta máquina; é lento (~50 ms por chamada), então nada de chamar em loop |
| Troca de dispositivo de áudio | `IPolicyConfig` (COM **não documentado**, o mesmo que ferramentas conhecidas usam) | Médio: API não oficial. Alternativa: abrir as configurações de som do Windows |
| Microfone | Core Audio no endpoint de captura | Baixo |
| Captura de tela | Windows.Graphics.Capture (Win10 1903+) | Baixo/médio. O mínimo do projeto é 1809: a captura fica indisponível abaixo de 1903, com aviso |
| Gravação | WGC + Media Foundation (H.264/MP4, codificador do Windows) + WASAPI loopback e microfone | **Alto** (o maior item). Placa GeForce 210 sem codificador de hardware: a gravação será em software, mais pesada. Conteúdo protegido (DRM) sai preto, por regra do Windows |
| Temperaturas e sensores | LibreHardwareMonitor | **Exige administrador**, e o driver WinRing0 usado por ele é sinalizado pelo Microsoft Defender como driver vulnerável. **Recomendação: não incluir**; manter Thermal Zone/NVML e marcar como indisponível |
| FPS | PresentMon/ETW (DXGI Present) | Exige administrador ou o grupo "Usuários de log de desempenho". Recomendação: provedor opcional que só ativa se houver permissão; sem injeção (o documento proíbe) |
| Detecção de jogos | Processo em primeiro plano + tela cheia (já detectada) + lista configurável | Baixo; heurística, nunca "universal" |
| Clima | Abstração de provedor; sugestão: **Open-Meteo** (gratuito, sem chave), com geocodificação para escolher a cidade | Precisa de internet. Cache com horário, nunca mostrar dado antigo como atual |
| Calendário | Local (SQLite) com lembretes pela ilha; contratos prontos para Google/Outlook | Baixo. Integrações externas ficam para depois (OAuth) |
| Notificação duplicada | Não há API pública para suprimir o toast do Windows | Documentar: o usuário pode desligar as faixas do app no Windows e deixar só a ilha |
| Spotify | Continuar no GSMTC, sem a API Web (exige OAuth) | Baixo |

## 9. Ordem recomendada de execução

Cada etapa termina com compilação, testes, verificação de regressões e documentação atualizada, como
pede a seção 20.

| Etapa | Conteúdo | Por que nesta ordem |
|---|---|---|
| **1. Núcleo da ilha** | Ciclo de vida do evento (estado, ocorrência, histórico em memória); fila preservada ao ocultar; agrupamento; fechar/fixar; clique fora; duplo clique; estados de carregamento/vazio/erro reutilizáveis | Tudo o que vem depois publica eventos e usa esses estados |
| **2. Persistência** | Migração M002: tabelas de histórico com índices e retenção; backup/exportação das configurações; reorganizar contratos por área | Pré-requisito dos históricos (foco, clipboard, notificações, calendário) |
| **3. Focus Center** | Voltas, Pomodoro configurável, histórico de sessões | Valor alto, risco baixo, base de timer pronta |
| **4. Clipboard Manager** | Histórico próprio (texto/imagem), pesquisa, favoritos, excluir/limpar, pausar, privacidade | Usa a etapa 2 |
| **5. Notification Center** | Histórico, prioridade/duração por app, ocultar × silenciar | Usa as etapas 1 e 2 |
| **6. Hardware Control** | DDC/CI, microfone, lista/troca de saída de áudio | Viável nesta máquina; troca de saída com fallback |
| **7. Configurações v2** | 11 categorias, uma view e um view model por categoria; categoria Atalhos com atalhos globais | Depois que as opções novas existem |
| **8. Life Center** | Calendário local + lembretes; clima com provedor e cache | Independente; precisa da etapa 2 |
| **9. Capture Center** | Primeiro captura (tela, janela, região, timer); depois gravação (WGC + MF + áudio) | O maior risco: vem quando o resto está estável |
| **10. Gaming Monitor** | Detecção de jogos, modo gamer, VRAM/frequência quando disponíveis, provedor de FPS opcional | Depende de detecção de tela cheia (pronta) e das regras de notificação |
| **11. Fechamento 1.0** | Padronização de provedores/plugins (sem carregar DLLs externas), revisão de desempenho e visual, testes por módulo (seção 18), documentação, versão 1.0 | Seção 21 |

## 10. Decisões pendentes

1. **Clima**: usar o Open-Meteo? (recomendado)
2. **Sensores**: confirmar que o LibreHardwareMonitor fica de fora (motivos na seção 8).
3. **Troca do dispositivo de áudio** via API não documentada (`IPolicyConfig`): aceitar ou só abrir as
   configurações de som do Windows?
4. **Versionamento**: publicar uma versão a cada etapa (0.2, 0.3...) ou só a 1.0 no fim?
5. **`docs/dynasland_evolution.md`**: adicionar ao repositório público?
