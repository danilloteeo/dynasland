# Notas da versão

## 1.6.0 — 2026-11-12

### Novidades
- **Interface renovada**, inspirada no Volum: fundo preto puro, destaque branco, fonte Sora, bordas finas e
  componentes novos (interruptor, slider, segmentado com pílula deslizante, barra lateral em pílula).
  Vale para Configurações, Central e ilha. O azul fica só para a água.
- **Ícones de contorno** (traço fino, pontas arredondadas) em toda a interface, no lugar dos glifos do Windows.
- **Abas da ilha com o ícone de cada módulo**; volta aos pontos se algum módulo não tiver ícone ou se não couberem.
- **Som animado** no volume (7 barras que oscilam; mudo vira uma linha), entrada e saída suaves da ilha e
  cascata nas linhas do painel interativo.
- Slider com bolinha branca de 10 px (halo ao arrastar), cartões com raio 14, rótulos de seção em caixa alta e
  selo de contagem em "Aplicativos".
- **Mascote em pixel art** na marca dos menus e nos estados vazios da Central.
- **Novidades** em uma janela com páginas, mostrada uma vez depois de atualizar (`--novidades` mostra de novo).
- Cor de destaque **Branco**, o novo padrão. Quem já escolheu outra cor mantém a dela. No tema claro o
  destaque branco vira quase preto para não sumir.
- O texto sobre a cor de destaque agora usa sempre o de maior contraste (antes, destaques cinza ficavam
  com letra branca difícil de ler).

### Correções
- Textos que ficavam ilegíveis ou cortados com a fonte nova: listas da Central, filtro de aplicativos das
  notificações (mostrava o texto interno do objeto), cabeçalho da Agenda, cartões do Foco e atalhos longos.

## 1.5.0 — 2026-11-11

### Novidades
- **Lembrete de água** (novo módulo): a ilha avisa de hora em hora, das 8h às 22h, com os botões
  **Bebi** e **Adiar 10 min**. Conta os copos do dia contra uma meta (8 por padrão) e, batida a meta,
  para os avisos até o dia seguinte. Intervalo, horário, meta e som ficam em Configurações › Módulos ›
  Água. O progresso aparece na tela inicial da ilha ("3/8 copos") e na nova página **Água** da Central.
  `Ctrl+Alt+G` registra um copo sem abrir nada. Um ícone próprio de gota, no estilo dos ícones do
  Windows, marca o atalho na tela inicial da ilha, o aviso e o item **Água** da Central.
- **Página Água animada**: uma garrafa que enche conforme você bebe, com ondas, respingos a cada copo
  e brilho ao bater a meta. Funciona também com o **Bebi** da ilha e o `Ctrl+Alt+G`. Ao marcar que bebeu pela ilha
  ou com `Ctrl+Alt+G`, uma gota enche de água na própria ilha.
- **Corpo que se enche de água**: o botão **Água** da tela inicial da ilha abre um painel com um corpo
  humano que se enche de água, dos pés à cabeça, conforme você bebe. Mostra os copos do dia, a
  porcentagem da meta e os botões **Bebi um copo** e **Desfazer**; a cada copo o nível sobe com ondas e
  gotinhas, e na meta o corpo fica cheio e brilha.
- **Ilha mais fluida**: ao trocar de conteúdo, o anterior esmaece enquanto o novo entra (sem quadro
  vazio); os botões da ilha encolhem de leve ao toque; avisos importantes (lembretes, fim de timer,
  bateria baixa) fazem um anel de destaque pulsar uma vez ao redor da cápsula; e as animações da ilha
  também respeitam a opção do Windows "Animar controles e elementos dentro das janelas".

## 1.4.0 — 2026-11-11

### Novidades
- **Volume por aplicativo**: o painel de volume ganhou o bloco "Aplicativos", com controle e mudo para
  cada programa que está fazendo som. Há um atalho novo, "Volume e aplicativos", na tela inicial da ilha.
- **Letra sincronizada**: o painel de mídia mostra o verso anterior, o atual e o próximo acompanhando a
  música. Vem desligada: ligue em Configurações › Módulos › Música e mídia. Consulta o lrclib.net,
  enviando título, artista, álbum e duração da faixa.
- **Velocidade de rede**: download e upload ao vivo no painel do sistema e como opção "Rede e hora" em
  Aparência › Ilha em repouso (precisa do módulo Monitor do sistema ligado).
- **Bluetooth** (novo módulo): aviso quando um dispositivo pareado conecta ou desconecta, com a bateria
  quando o Windows informa, e alerta de bateria baixa. Precisa de um adaptador Bluetooth ligado ao
  iniciar o Dynasland.

## 1.3.0 — 2026-11-11

### Novidades
- **Tema que segue a música**: com "Seguir a cor da música" ligado em Configurações › Aparência, o fundo
  da ilha usa a cor da capa enquanto uma música ou vídeo toca e volta ao tema escolhido ao parar.
- **Ilha em repouso**: escolha o que a ilha mostra sem nada acontecendo: data e hora, só a hora, clima e
  hora, próximo compromisso e hora, ou nada.

### Mudanças
- A troca de tema da ilha agora é suave, em vez de instantânea.

## 1.2.0 — 2026-11-11

### Novidades
- **Temas da ilha**: 13 fundos para a ilha em Configurações › Aparência › Tema da ilha (Preto, Grafite,
  Meia-noite, Aurora, Pôr do sol, Floresta, Aço, Vinho, Cobre, Brasa, Magenta, Crepúsculo e um tom
  escuro da cor de destaque). A troca vale na hora e respeita a opacidade escolhida.
- **Hora na ilha em repouso**: sem nada para mostrar, a ilha compacta exibe a data abreviada e a hora
  em vez de ficar vazia.

## 1.1.0 — 2026-11-10

### Novidades
- **Atualização dentro do aplicativo**: o aviso de nova versão na ilha ganhou o botão "Atualizar agora".
  O Dynasland baixa o instalador mostrando o progresso na ilha, confere o arquivo (tamanho e SHA-256),
  instala sem janelas e reabre sozinho, confirmando a versão instalada. O mesmo botão fica em
  Configurações › Sobre. A versão portátil continua abrindo a página de download.
- **Instalador com a cara do Dynasland**: tema escuro, imagens próprias e textos em português.
- **Modo de atualização no instalador**: ao rodar o instalador com uma versão já instalada, ele mostra
  a versão atual e a nova e pede só um clique em "Atualizar", sem repetir as perguntas da instalação.

### Mudanças
- Atualizar não mexe mais em "Iniciar com o Windows": vale o que estiver ligado ou desligado no aplicativo.

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
