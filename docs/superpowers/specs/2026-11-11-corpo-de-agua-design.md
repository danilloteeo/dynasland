# Corpo que se completa com a água — design

Data: 2026-11-11 (sobre a branch da ilha mais fluida)

## Pedido

O botão de água da tela inicial da ilha hoje registra um copo direto. Deve abrir uma tela com um **corpo humano
completo que vai se enchendo de água conforme o usuário bebe**, e não ser só um botão que conta copos.

## Decisões (confirmadas com o usuário)

- **Onde:** painel na própria ilha, como Volume/Brilho/Área de transferência (o atalho chama `OpenPanel`, que posta
  uma atividade `Transient` com `OpenInteractive = true`). A aba Água da Central continua como está (garrafa).
- **Preenchimento:** a silhueta do corpo inteiro enche de água de baixo para cima (pés → cabeça), com ondas, e fica
  cheia na meta.

## Comportamento

- **Atalho da tela inicial** ("Água", gota): abre o painel. Não registra copo. O id do atalho passa a refletir
  isso (`water.panel`); conferir se algo persiste/ordena atalhos por id (Configurações › Atalhos).
- **Painel (estado Interactive):** corpo grande à esquerda (altura ~200 px), à direita "N de M copos hoje", a
  porcentagem (N/M, limitada a 100%), a mensagem de progresso, e os botões **Bebi um copo** (principal) e
  **Desfazer**. Nível da água = N/M do pés (0) à cabeça (1), limitado a 1.
- **Ao beber (botão do painel, atalho global `Ctrl+Alt+G` ou **Bebi** do aviso, com o painel aberto):** o nível
  sobe com easing (~900 ms), a onda agita um instante, gotinhas sobem e o texto pulsa. Desfazer: o nível desce.
  Meta batida: a silhueta fica totalmente cheia, com brilho azul e mais gotinhas; mensagem "Meta de hoje batida!".
- **Abrir com o painel já com copos:** o nível vai direto ao valor atual, sem animar a partir de zero.
- O painel se atualiza ao vivo (o conteúdo é um objeto vivo que o módulo atualiza a cada mudança do store/opções,
  como o painel de brilho). `Ctrl+Alt+G` e o **Bebi** do aviso continuam mostrando a confirmação animada
  (`WaterDrankContent`) quando o painel NÃO está aberto; com o painel aberto, não substituem o painel.
- Estados Compact/Expanded do conteúdo do painel (a ilha pode recolher): gota pequena + "N/M" e corpo pequeno +
  texto, mínimos, nos moldes dos outros painéis.
- Respeita `AnimationsEnabled`/`ClientAreaAnimation`: sem animação, o nível aparece direto.

## Arquitetura

- `WaterBodyPanelContent` (conteúdo vivo, `ObservableObject`, `IActivityContent`) com `Glasses`, `Goal`, `Progress`,
  textos e comandos Bebi/Desfazer (chamam o módulo/store). Dimensões preferidas do estado Interactive definidas via
  `GetPreferredDimensions` (precisa ser alto o bastante para o corpo; ver `BrightnessContent`/`ClipboardContent`).
- `WaterBodyMeter` (controle WPF em `Dynasland.Modules/Water`, no molde do `WaterFillMeter`): silhueta humana
  vetorial (cabeça, pescoço, ombros, braços levemente afastados, tronco, pernas separadas; um único contorno
  fechado `Geometry`) como `Clip` da água azul (gradiente vertical) com duas camadas de onda; traço do contorno na
  cor do tema; propriedades `Progress` (0..1), `Celebrate`. Animações só enquanto carregado; limpeza no Unloaded.
- `WaterModule`: `OpenPanel()` posta o painel (Id `water.panel`); o módulo atualiza o conteúdo ao vivo; o atalho da
  ilha chama `OpenPanel`. `DrinkAsync` (hotkey/aviso) continua registrando o copo; a confirmação
  (`WaterDrankContent`) só é postada se o painel não estiver aberto.
- Templates em `WaterTemplates.xaml` para `WaterBodyPanelContent:Compact|Expanded|Interactive`.

## Testes

- Unidade (módulo): o atalho abre o painel (atividade `Transient`, `OpenInteractive`, conteúdo `WaterBodyPanelContent`)
  sem registrar copo; o painel reflete Glasses/Goal/Progress após `DrinkAsync`, desfazer e mudança de meta; com o
  painel aberto, `DrinkAsync` não posta a confirmação por cima.
- Visual offscreen: PNGs da silhueta em 0%, 40%, 75% e 100% (cheia + brilho), em 200 px de altura.

## Fora de escopo

Corpo na aba Água da Central; partes do corpo separadas; sexo/biotipo do corpo; metas por peso.
