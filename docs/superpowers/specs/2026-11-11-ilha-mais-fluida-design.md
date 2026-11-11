# Ilha mais fluida — design

Data: 2026-11-11

## Objetivo

Suavizar a ilha em quatro pontos, sem criar um sistema de movimento novo. Tudo em `IslandWindow` e nos
estilos da ilha; respeita `AnimationsEnabled`, `AnimationSpeed` (via `Scaled`) e agora também a opção do
Windows "Animar controles e elementos dentro das janelas" (`SystemParameters.ClientAreaAnimation`).

Achados no código: a janela (HWND) tem tamanho fixo e só a cápsula (`IslandHost`) anima por dentro;
`PlaySwap` só anima a entrada do conteúdo novo (fade 0→1 + deslize de 6 px, 240 ms), e o conteúdo antigo some
no instante em que o binding troca, deixando um quadro vazio. O view model (CommunityToolkit `ObservableObject`)
dispara `PropertyChanging` antes de trocar o conteúdo.

## 1. Cross-fade na troca de conteúdo

- Antes da troca de `ExpandedContent`/`InteractiveContent` (evento `PropertyChanging` do view model, só quando
  o estado atual é Expanded/Interactive e `ShouldAnimate`), a camada atual é capturada
  (`RenderTargetBitmap`/`VisualBrush`) e posta por cima como "fantasma" (Image, `IsHitTestVisible=false`).
- O fantasma esmaece (1→0) e sobe levemente (0→−4 px) em `SwapMilliseconds`, com `SoftEasing`, enquanto o
  conteúdo novo entra como hoje (fade 0→1 + deslize de 6 px). Ao terminar, o fantasma é removido.
- Troca em sequência rápida: o fantasma anterior é removido e um novo é criado. Sem animação: nada de fantasma.
- Só vale para a troca dentro do mesmo estado; a mudança de estado/tamanho da cápsula não muda.

## 2. Resposta ao toque nos botões

- No estilo compartilhado `IslandIconButtonStyle` (e `FooterButton` se não herdar dele): ao pressionar, escala
  1 → 0,9 em ~90 ms; ao soltar, volta a 1 com `BackEase` leve em ~180 ms (`RenderTransformOrigin` 0,5/0,5).
  Respeita `AnimationsEnabled`/`ClientAreaAnimation` quando viável no XAML (se não for viável em XAML, aplicar
  por comportamento anexado/code-behind); sem animação, o estado pressionado só troca o fundo como hoje.

## 3. Pulso de alerta na cápsula

- Quando chega uma atividade `ActivityKind.Transient` com `ActivityPriority.High` (ex.: lembretes da Agenda e da
  Água, fim de timer/Pomodoro), um anel de acento ao redor da cápsula pulsa uma vez (~600 ms: opacidade 0,6→0 e
  escala 1→1,04) e some. Atividades normais, confirmações e a troca de conteúdo não pulsam.
- A decisão "essa atividade pulsa?" é uma função pura testável (`IslandAlertPulse.ShouldPulse(IslandActivity)`).
- O anel é um `Border` extra no `IslandWindow.xaml` (`IsHitTestVisible=false`, cor `IslandAccentBrush`), sem
  efeitos caros.

## 4. Respeitar o Windows

- `ShouldAnimate` passa a incluir `SystemParameters.ClientAreaAnimation`. Vale para tudo na ilha.

## Fora de escopo

Sistema `Motion` compartilhado, animações da Central e das Configurações, tira de quadros para revisão.

## Testes

- Unidade: `IslandAlertPulse.ShouldPulse` (Transient+High pulsa; Transient+Normal, Live, e High não transitória
  não pulsam).
- Verificação visual offscreen: PNG do fantasma sobre o conteúdo novo em alguns instantes e do anel; a animação
  em movimento é conferida pelo usuário rodando o app.
