# Próximos passos

Estado em 2026-11-10: **Fase 6 concluída** — versão 0.1.0 publicada (instalador + portátil).
Detalhes em `docs/RELATORIO_FINAL.md`.

## Validar no uso real (não testado manualmente)

- Win+L e suspensão/retomada: ilha some e volta no lugar certo; temporizador dispara no horário mesmo
  com o PC suspenso no meio.
- Troca do monitor principal, desconexão do monitor da ilha e mudança de resolução com o app aberto.
- Barra de título das configurações no tema claro do Windows 10.
- Alarme do temporizador, abas de mídia, área de transferência e painel do sistema no dia a dia.

## Ideias para versões futuras

- **MSIX** com certificado: identidade de pacote para receber notificações por evento (sem a verificação
  a cada 1,5 s).
- Assinatura digital do instalador (evita o aviso do SmartScreen).
- Reduzir o custo do equalizador na janela transparente (hoje ~4% de um núcleo enquanto toca).
- `Dynasland.exe --configuracoes` com o app já aberto: repassar o argumento à instância em execução.
- Brilho de monitores externos via DDC/CI.
