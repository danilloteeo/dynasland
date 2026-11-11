# Lembrete de água — design

Data: 2026-11-11 · Versão alvo: 1.5.0

## Objetivo

Lembrar o usuário de beber água na ilha, com contador de copos do dia e meta diária. Segue o padrão do
módulo Agenda (`CalendarModule`): alerta transitório na ilha, opções do módulo, página na Central e
armazenamento em SQLite.

## Comportamento

- **Lembrete:** alerta transitório "Hora de beber água" com os botões **Bebi** e **Adiar 10 min**. Som de
  alerta opcional (opção do módulo, como `CalendarModule.ReminderSound`).
- **Intervalo e horário ativo:** padrão de 60 min entre avisos, das 08:00 às 22:00; configuráveis.
- **Meta diária:** padrão de 8 copos; configurável.
- **Contador:** **Bebi** soma 1 copo no dia atual. O dia vira à meia-noite (data local).
- **Meta atingida:** os avisos do dia param e a ilha mostra uma mensagem curta de parabéns.
- **Adiar:** reagenda o próximo aviso para daqui a 10 min, sem alterar o contador.
- **Fora do horário ativo:** nenhum aviso é disparado. Avisos que venceram com o Dynasland fechado são
  descartados; o ciclo recomeça a partir do início ao abrir.

## Interface

- **Central:** nova página "Água" (`HubPage.Water`) com copos de hoje contra a meta, botões **+1** e **−1**.
- **Tela inicial da ilha:** glance "3/8 copos" (`IGlanceRegistry`).
- **Atalho global:** registrar um copo sem abrir nada, `Ctrl+Alt+G` (`Ctrl+Alt+W` já é a captura de
  janela do módulo Capture).

## Arquitetura

- `WaterModule : IIslandModule` em `src/Dynasland.Modules/Water/`. Agenda o próximo aviso com um único
  `ITimer` (via `TimeProvider`), reagendado quando o contador ou as opções mudam e ao retomar da
  suspensão (`SessionChangedEvent`/`SessionChange.Resumed`).
- O aviso usa `WaterReminderContent` (botões Bebi e Adiar) com templates próprios em
  `WaterTemplates.xaml`, porque `StatusContent` só comporta uma ação.
- `IWaterStore` em `Dynasland.Core/Interfaces` (mesmo molde de `ICalendarStore`): contagem do dia,
  incrementar/decrementar, evento `Changed`. Implementação SQLite em `SqliteHistoryStores.cs` ou arquivo
  próprio na mesma pasta.
- Migration `M003_WaterLog` com a tabela `water_log(day TEXT PRIMARY KEY, glasses INTEGER NOT NULL)`.
- Opções do módulo: som, intervalo, início/fim do horário ativo, meta (via `ModuleOption`).
- Página da Central: `WaterPageViewModel` + `WaterPage.xaml`, registrada no `HubViewModel`/`HubWindow`.
- Registro no `DependencyInjection` de Modules e Infrastructure.

## Erros e resiliência

- Falhas de leitura/escrita no store são registradas no log e não derrubam o módulo (como em
  `CalendarModule.RescheduleAsync`).
- Intervalo, horários e meta inválidos nas opções caem para os padrões.
- Se o fim do horário ativo for anterior ao início, usar os padrões.

## Testes

Em `Dynasland.Tests/Modules`, com `TimeProvider` falso:
- Agendamento: respeita o horário ativo, soma o intervalo, adiar em 10 min, para ao atingir a meta, vira o dia.
- Store: incrementar, decrementar sem ficar abaixo de zero, contagem por dia.

## Aba Água animada (revisão pedida após a primeira versão)

A página Água da Central (`WaterPage`) vira a tela principal do recurso, com uma garrafa que enche.

- **Garrafa:** grande, ao centro; o nível da água sobe até a meta, com duas camadas de onda contínuas
  (velocidades diferentes). Abaixo: "N de M copos", a mensagem de progresso e os botões
  **Bebi um copo** (principal) e **Desfazer**.
- **+1:** a água sobe com easing, a onda agita por um instante, sobem gotas/respingos do topo e o número
  faz um pulso. **−1:** a água desce com a mesma suavidade, sem respingos.
- **Meta batida:** a garrafa brilha e sobem gotas comemorativas; a mensagem vira "Meta de hoje batida!".
- **Qualquer origem dispara:** botão da página, **Bebi** da ilha e `Ctrl+Alt+G`, com a aba aberta (a página
  reage à mudança de `Glasses`, não ao clique).
- **Desempenho e acessibilidade:** animações contínuas (ondas) só rodam enquanto a página está carregada e
  visível; com `SystemParameters.ClientAreaAnimation` desligado, o nível muda direto, sem movimento.
- **Tema:** água azul fixa; garrafa e fundo seguem o tema claro/escuro (`Settings*Brush`).
- **Código:** só `WaterPage.xaml`, `WaterPage.xaml.cs` e `WaterPageViewModel` (expõe `Glasses` e `Goal`
  como números). Módulo, store e ilha não mudam.

## Confirmação animada na ilha (revisão pedida: "cadê a animação quando marco que bebi?")

Hoje, **Bebi** (aviso ou atalho da ilha) e `Ctrl+Alt+G` mostram só o texto "Boa! N de M copos hoje". Passam a
mostrar uma confirmação animada na própria ilha, sem precisar abrir a Central.

- **Conteúdo novo** `WaterDrankContent` (substitui o `StatusContent` de confirmação do `DrinkAsync`; "Adiado"
  continua `StatusContent`). Textos iguais aos de hoje: título "Boa!" ou "Meta de água batida!" e mensagem
  "N de M copos hoje". Carrega o progresso anterior ((N−1)/M) e o novo (N/M, no máximo 1) e se a meta foi batida.
- **Animação:** a gota do ícone (mesmo contorno de `VectorIcons.Water`) **enche de água** do nível anterior ao novo
  (~900 ms, easing de desaceleração), com uma onda deslizando na superfície, um pulso final (1 → 1,12 → 1) e
  algumas gotinhas subindo. Meta batida: brilho azul e mais gotas.
- **Tamanhos:** templates Compact/Expanded/Interactive (como o aviso); a gota maior no Expanded/Interactive.
- **Controle reutilizável** `WaterFillMeter` (em `Dynasland.Modules/Water`): propriedades `From`, `To`,
  `Celebrate`; anima ao carregar; para tudo ao descarregar; respeita `SystemParameters.ClientAreaAnimation`
  (desligado: mostra direto o nível final).
- **Duração** da confirmação: 5 s (a animação toca nos primeiros ~1,2 s).
- **Testes:** os testes do módulo passam a verificar `WaterDrankContent` (título, mensagem, progresso
  anterior/novo, meta batida) em vez de `StatusContent`.

## Fora de escopo

Histórico por dia e gráfico semanal; volume em ml; sincronização externa.

## Entrega

Versão 1.5.0 e entrada no CHANGELOG, como nas versões anteriores.
