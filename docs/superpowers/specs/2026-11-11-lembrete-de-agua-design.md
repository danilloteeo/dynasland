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
- **Atalho global:** registrar um copo sem abrir nada, `Ctrl+Alt+W` (verificar se está livre no
  `IGlobalShortcutRegistry`; se não estiver, escolher outro e documentar).

## Arquitetura

- `WaterModule : IIslandModule` em `src/Dynasland.Modules/Water/`. Agenda o próximo aviso com um único
  `ITimer` (via `TimeProvider`), reagendado quando o contador ou as opções mudam e ao retomar da
  suspensão (`SessionChangedEvent`/`SessionChange.Resumed`).
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

## Fora de escopo

Histórico por dia e gráfico semanal; volume em ml; sincronização externa.

## Entrega

Versão 1.5.0 e entrada no CHANGELOG, como nas versões anteriores.
