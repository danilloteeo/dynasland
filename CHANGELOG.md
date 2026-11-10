# Notas da versão

## 1.0.6 — 2026-11-10

### Correções
- **Ícone das notificações**: aplicativos da Store (como o WhatsApp) apareciam com o ícone minúsculo
  dentro de uma placa cinza. Agora a margem transparente é recortada e o ícone aparece no tamanho
  certo e mais nítido.

## 1.0.5 — 2026-11-10

### Novidades
- **Cartão de notificação redesenhado**: remetente em destaque, mensagem em até duas linhas, ícone
  maior e "aplicativo · hora" à direita.
- **Cor do aplicativo**: o cartão ganha um brilho na cor dominante do ícone do aplicativo
  (ícones sem cor mantêm o visual neutro).
- **Tempo restante**: uma linha fina mostra quanto falta para a notificação sair da ilha e pausa
  com o ponteiro em cima.

## 1.0.4 — 2026-11-10

### Mudanças
- A roda do mouse não troca mais a aba da ilha (evita trocas acidentais ao rolar a página);
  use as setas ou os pontos do rodapé.

## 1.0.3 — 2026-11-10

### Novidades
- **Fila do Spotify no painel de mídia**: "A seguir" com as próximas faixas e capas, pular direto para
  uma faixa e ver a fila completa. Requer conectar sua conta em Configurações › Geral › Spotify
  (com um Client ID próprio do painel de desenvolvedor do Spotify; o token fica criptografado no PC).
- **Volume no painel de mídia**: com o Spotify conectado, ajusta o volume do próprio Spotify
  (Spotify Premium) e avisa quando ele está em 0%; nos outros players, o volume do sistema.
- **Clima redesenhado**: ícones vetoriais coloridos, máxima e mínima do dia, sensação, umidade, vento
  e chance de chuva hoje; previsão de 5 dias com a chance de chuva de cada dia.

### Correções
- Painéis maiores (fila, volume) não são mais cortados pela altura máxima da ilha.
- O aviso de estado do clima não se sobrepõe mais aos detalhes.

## 1.0.0 — 2026-11-10

### Novos módulos
- **Capture Center**: captura de região (sobre a tela congelada), tela inteira e janela ativa, com
  temporizador; gravação de tela em MP4 (H.264 + AAC) com áudio do sistema, microfone, pausa e retomada.
- **Gaming Monitor**: detecção de jogos, modo gamer (silencia notificações comuns, pode ocultar a ilha),
  FPS via ETW do DirectX quando há permissão, CPU, GPU, memória, VRAM, frequência e temperaturas.
- **Life Center**: clima pelo Open-Meteo com cache e aviso de dado desatualizado; agenda local com
  calendário, lembretes na ilha (inclusive os perdidos com o app fechado).

### Módulos ampliados
- **Focus Center**: Pomodoro configurável, voltas no cronômetro e histórico de sessões.
- **Clipboard Manager**: histórico próprio de texto e imagens, busca, favoritos, exclusão e filtro de
  senhas, cartões e chaves.
- **Notification Center**: histórico, regras por aplicativo (mostrar, só histórico, bloquear),
  prioridade e duração próprias; mensagens seguidas do mesmo app agrupadas.
- **Hardware Control**: brilho de monitores externos (DDC/CI), troca do dispositivo de saída de áudio,
  microfone com nível, mudo e atalho.

### Ilha e aplicativo
- Ciclo de vida e histórico das atividades, agrupamento, fila preservada com a ilha oculta, fechar e
  fixar atividades, clique fora para recolher, duplo clique configurável, estados de carregamento/vazio/erro.
- Atalhos de teclado globais configuráveis.
- **Central** com históricos (área de transferência, notificações, foco, capturas) e agenda.
- Configurações em 11 categorias, com privacidade, atalhos e armazenamento (retenção, compactação,
  exportar/importar). Argumentos `--central` e `--configuracoes` também com o app já aberto.
- Banco com tabelas de histórico, índices, retenção e cópia automática das configurações.

### Correções
- A leitura da área de transferência falhava fora de thread STA (RPC_E_WRONG_THREAD) e não registrava cópias.

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
