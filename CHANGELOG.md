# Notas da versão

## 0.1.0 — 2026-11-10

Primeira versão pública.

### Ilha dinâmica
- Cápsula no topo da tela com estados compacto, expandido e interativo, animações com efeito de mola
  e troca suave de conteúdo.
- Fila de atividades por prioridade; várias atividades simultâneas viram abas no painel interativo.
- Não rouba o foco, deixa cliques passarem fora da cápsula, suporta vários monitores e DPI por monitor.
- Oculta em tela cheia (opcional), com a sessão bloqueada e durante a suspensão; reaparece e se
  reposiciona ao voltar.

### Módulos
- Música e mídia (várias sessões ao mesmo tempo), volume, brilho, notificações, bateria, relógio,
  downloads dos navegadores, área de transferência, monitor do sistema, cronômetro e temporizador.

### Configurações
- Janela completa: geral, aparência (tema claro/escuro/automático, cor de destaque, tamanhos),
  animações, módulos com opções, filtros de notificações e sistema.
- Iniciar com o Windows, iniciar minimizado, verificação de atualizações pelo GitHub.

### Confiabilidade
- Banco de dados local (SQLite) com migrações; arquivo corrompido é guardado e recriado com os padrões.
- Falha em um módulo não derruba a ilha; logs em `%LocalAppData%\Dynasland\logs`.

### Distribuição
- Instalador por usuário, sem administrador (`DynaslandSetup-0.1.0.exe`), e versão portátil (`.zip`).

### Limitações conhecidas
- Notificações: sem pacote MSIX o Windows não entrega eventos; a lista é comparada a cada 1,5 s, e o
  toast do próprio Windows continua aparecendo.
- Brilho só em telas integradas (notebooks/tablets).
- O indicador de volume do Windows também aparece ao usar as teclas de volume.
- Arquivos sem assinatura digital: o SmartScreen pode avisar na primeira execução.
