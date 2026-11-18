# Guia de uso do DynaIsland

> O DynaIsland se chamava Dynasland até a versão 1.7.1. Instalação, atualização e dados: veja o [README](../README.md).

## A ilha

A ilha é uma **pílula preta no topo da tela** (ou embaixo, se você escolher em Configurações › Geral). Em repouso
mostra o clima à esquerda, a hora no meio e, quando há música, uma onda de som à direita.

- **Passar o mouse** amplia levemente a ilha e mostra as **setas** e as **bolinhas** para trocar de página; enquanto
  o ponteiro estiver sobre ela, nada se fecha sozinho.
- **Clicar** abre a ilha (mostra data e hora e as páginas dos módulos); clicar fora, ou apertar **Esc**, recolhe.
- Quando algo acontece (nova música, volume, notificação, lembrete de água), a ilha se expande sozinha por alguns
  segundos e volta ao repouso.
- Com várias mídias tocando (ex.: Spotify e um vídeo no navegador), há **abas**: use as setas ou as bolinhas.
- A ilha nunca rouba o foco do programa que você está usando, e os cliques fora da cápsula passam para a janela
  de trás. Ela se mantém por cima das outras janelas.
- Ela se esconde sozinha com o computador **bloqueado** (Win+L), em **suspensão** e — se ligado nas
  configurações — quando um programa ocupa a tela inteira (jogos, vídeos, apresentações).

## O que aparece

- **Música e vídeo** (Spotify, navegadores, players compatíveis com os controles de mídia do Windows): capa,
  título, artista, progresso e anterior/reproduzir/próxima. Com o Spotify conectado, também a fila ("A seguir").
- **Volume**: ao mudar, a ilha mostra o nível; o painel tem slider, mudo, troca do dispositivo de saída e do
  microfone e o volume de cada aplicativo.
- **Brilho** (telas de notebook ou monitores externos com DDC/CI ligado).
- **Notificações** dos aplicativos: texto, **Abrir**, **Dispensar** e **Silenciar app**.
- **Área de transferência**: o que você copiou; o painel guarda o histórico (texto e imagens) para copiar de novo.
- **Clima**, **cotações** (dólar, euro, bitcoin) e **placar da NBA**, nas páginas da ilha.
- **Água**: um lembrete por hora (8h às 22h); **Bebi** registra um copo, **Adiar** lembra de novo depois. A meta
  diária e o corpo que se enche ficam no painel.
- **Caps Lock e Num Lock**: aviso rápido ao ligar ou desligar.

## Atalhos

| Atalho | Ação |
|---|---|
| Ctrl+Alt+D | Abrir/fechar a ilha |
| Ctrl+Alt+H | Mostrar/ocultar a ilha |
| Ctrl+Alt+V | Histórico da área de transferência |
| Ctrl+Alt+M | Microfone: desligar/ligar |
| Ctrl+Alt+G | Registrar um copo de água |

Em Configurações › Atalhos você troca ou desliga cada um. Se aparecer "Em uso por outro programa", outro aplicativo já
usa essa combinação: escolha outra.

## Bandeja do sistema

O ícone do DynaIsland perto do relógio do Windows:

- **Clique**: abre as configurações.
- **Menu (botão direito)**: Configurações…, Ocultar/Mostrar ilha, Exibir no monitor (principal ou um específico) e
  **Sair do DynaIsland**. Esse é o único jeito de encerrar o programa.

Abrir o DynaIsland de novo (pelo Menu Iniciar) com ele já aberto apenas mostra a ilha.

## Configurações

Abrem numa janela própria, em 11 seções: **Geral** (iniciar com o Windows, mostrar a ilha, borda e distância da
borda, ocultar em tela cheia), **Aparência**, **Animações**, **Comportamento**, **Monitores**, **Módulos** (ligar e
desligar cada um e suas opções), **Notificações** (regras por aplicativo), **Privacidade**, **Atalhos**,
**Armazenamento** (limites, exportar/importar configurações, restaurar padrões) e **Sobre** (versão, atualização e logs).

Pela linha de comando: `DynaIsland.exe --configuracoes=atalhos` abre direto numa seção.

## Atualizações

Quando há versão nova, a ilha avisa. **Atualizar agora** baixa, confere e instala sozinha, e o DynaIsland reabre. Nas
versões que trazem novidades visuais, uma janelinha as mostra na primeira abertura depois de atualizar.

## Problemas comuns

- **A ilha ficou atrás de outra janela** (barra de tarefas, menu Iniciar): a ilha se recoloca por cima quando outra
  janela assume o primeiro plano. Se ainda acontecer com algum programa específico, avise qual.
- **Notificações não aparecem**: o Windows precisa permitir o acesso às notificações. Em Configurações › Módulos o
  módulo mostra "indisponível" com o motivo; libere em Configurações do Windows › Privacidade › Notificações.
- **Brilho indisponível**: monitores externos precisam ter DDC/CI ligado no menu do próprio monitor.
- **"O banco de dados estava danificado"**: o arquivo de configurações corrompeu (ex.: queda de energia).
  O DynaIsland recriou com os padrões e guardou o antigo ao lado, como `dynaisland.db.corrompido-<data>`.
- **Depois de atualizar do Dynasland**: os dados foram copiados para `%LocalAppData%\DynaIsland`; a pasta antiga
  `%LocalAppData%\Dynasland` continua lá, intacta, e pode ser apagada quando você conferir que está tudo certo.
- **Logs** para relatar problemas: `%LocalAppData%\DynaIsland\logs` (também em Configurações › Sobre). Linhas
  "Motor nativo: ..." mostram o estado do motor em C++; sem a DLL o aplicativo funciona do mesmo jeito.
