# Próximos passos

Estado em 2026-11-18: **v1.8.0 publicada** (renome para DynaIsland, motor nativo em C++, migração dos dados da pasta
antiga); **v1.8.1 preparada** (janela de novidades sobre o novo nome, documentação). Histórico das versões em `CHANGELOG.md`; arquitetura nativa em `docs/ARQUITETURA_NATIVA.md`.

## Validar no uso real (não testado manualmente)

- **Atualização ponta a ponta** da 1.7.1 instalada para a 1.8.0 pelo botão "Atualizar agora": o app reabre como
  DynaIsland, os dados e "Iniciar com o Windows" continuam, o atalho antigo "Dynasland" some do Menu Iniciar.
- **Encerrar pelo "Sair" da bandeja** e conferir no log `%LocalAppData%\DynaIsland\logs` que o motor nativo parou.
- **Vários monitores**: trocar o monitor principal, desconectar o da ilha e mudar a resolução com o app aberto.
- Win+L e suspensão/retomada: a ilha some e volta no lugar certo.
- A ilha continua por cima depois de clicar na barra de tarefas e de abrir o menu Iniciar (correção da 1.7.1).
- Cliques fora da ilha e Esc recolhendo o painel com o motor nativo ligado.

## Ideias para versões futuras

- **MSIX** com certificado: identidade de pacote para receber notificações por evento (sem a verificação
  a cada 1,5 s) e, de quebra, assinatura que evita o aviso do SmartScreen.
- Assinatura digital do instalador e da `DynaIslandEngine.dll`.
- Renomear também o instalador (`DynaIslandSetup-*`), a pasta de instalação e o repositório, quando não houver mais
  instalações 1.7.x que dependam do nome antigo para se atualizar.
- Religar módulos que existem no código mas estão desligados (Bateria, Downloads, Monitor do sistema, Captura,
  Bluetooth, Pomodoro, Modo gamer, Agenda) — um de cada vez, em `DynaIsland.Modules/DependencyInjection.cs`.
- Mais usos do motor nativo, só onde houver ganho real (hoje ele cuida dos ganchos de mouse, teclado e primeiro plano).
