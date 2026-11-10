# Guia de uso do Dynasland

## A ilha

A ilha é a cápsula preta no topo da tela. Ela tem três estados:

| Estado | Como aparece | Como chegar |
|---|---|---|
| **Compacto** | Cápsula pequena; mostra um indicador da atividade principal (capa e equalizador da música, cronômetro, download...) | Estado normal |
| **Expandido** | Cápsula maior por alguns segundos, com o evento (nova música, volume, notificação, bateria) | Automático, quando algo acontece |
| **Interativo** | Painel com controles | Clique na ilha |

- **Passar o mouse** amplia levemente a ilha; enquanto o ponteiro estiver sobre ela, nada se fecha sozinho.
- **Clique** abre o painel interativo; clique de novo (fora dos botões) para recolher. Sem uso, recolhe em 6 s.
- Com várias atividades (ex.: Spotify e um vídeo no navegador), o painel tem **abas**: use as setas
  ou os pontos do rodapé para trocar.
- A **engrenagem** no rodapé abre as configurações.
- A ilha nunca rouba o foco do programa que você está usando, e os cliques fora da cápsula passam para
  a janela de trás.
- A ilha se esconde sozinha com o computador **bloqueado** (Win+L), em **suspensão** e — se ligado nas
  configurações — quando um programa ocupa a tela inteira (jogos, vídeos, apresentações).

## Tela inicial (sem atividades)

Ao clicar na ilha sem nada acontecendo, ela mostra hora e data e atalhos:

- **Cronômetro** — inicia na hora; pausa/continua e zera no painel.
- **Temporizador** — escolha 1 min a 1 h; no painel: pausar, +1 minuto e cancelar. Ao terminar, a ilha
  avisa (com alarme, se ligado) e oferece **Repetir**.
- **Sistema** — CPU, memória, GPU, temperaturas (quando o computador informa) e discos.
- **Configurações**.

## O que aparece automaticamente

- **Música e vídeo** (Spotify, navegadores, players compatíveis com os controles de mídia do Windows):
  nova faixa expande a ilha; o painel tem capa, progresso e anterior/reproduzir/próxima.
- **Volume e brilho**: ao mudar, a ilha mostra o nível; no painel há slider (e mudo, no volume).
- **Notificações** dos aplicativos: texto, **Abrir**, **Dispensar** e **Silenciar app**.
- **Bateria** (notebooks): carregador conectado/desconectado, bateria fraca (20%) e muito fraca (10%).
- **Downloads** dos navegadores: progresso; ao terminar, **Abrir** ou **Mostrar na pasta**.
- **Área de transferência**: o que você copiou; no painel, o histórico recente para copiar de novo.
- **Avisos do sistema**: memória quase cheia ou disco com pouco espaço.

## Bandeja do sistema

O ícone do Dynasland perto do relógio do Windows:

- **Clique**: abre as configurações.
- **Menu (botão direito)**: configurações, mostrar/ocultar a ilha, escolher o monitor, sair. Os módulos são ligados e desligados em Configurações › Módulos.

Abrir o Dynasland de novo (pelo Menu Iniciar) com ele já aberto apenas mostra a ilha.

## Configurações

- **Geral**: iniciar com o Windows, iniciar minimizado (ilha oculta até você mostrar pela bandeja),
  mostrar a ilha, ocultar em tela cheia, distância do topo, monitor.
- **Aparência**: tema das janelas, cor de destaque, opacidade, sombra, arredondamento e tamanhos da ilha.
- **Animações**: ligar/desligar, velocidade, efeito de mola e quanto tempo a ilha fica expandida.
- **Módulos**: ligar/desligar cada um e suas opções (ex.: anunciar vídeos, equalizador animado, alarme).
- **Notificações**: todos os aplicativos exceto os bloqueados, ou somente os permitidos; duração e prioridade.
- **Sistema**: verificar atualizações, restaurar padrões, versão e pasta de logs.

## Problemas comuns

- **Notificações não aparecem**: o Windows precisa permitir o acesso às notificações. Em Configurações ›
  Módulos o módulo mostra "indisponível" com o motivo; libere em Configurações do Windows › Privacidade ›
  Notificações.
- **Brilho indisponível**: monitores externos não informam o brilho ao Windows; só telas de notebook/tablet.
- **Temperaturas ausentes**: dependem do hardware informar ao Windows (ou de placa NVIDIA para a GPU).
- **"O banco de dados estava danificado"**: o arquivo de configurações corrompeu (ex.: queda de energia).
  O Dynasland recriou com os padrões e guardou o antigo ao lado, como `dynasland.db.corrompido-<data>`.
- **Logs** para relatar problemas: `%LocalAppData%\Dynasland\logs` (também em Configurações › Sistema).
