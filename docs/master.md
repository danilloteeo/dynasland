# PROMPT MASTER — DYNASLAND
## Aplicativo Dynamic Island para Windows 10 e Windows 11

Atue como um engenheiro de software sênior, arquiteto de aplicações desktop e especialista em interfaces modernas para Windows.

Seu objetivo é desenvolver um aplicativo chamado **Dynasland**, inspirado no conceito Dynamic Island dos iPhones, mas projetado especificamente para computadores com Windows 10 e Windows 11.

O resultado deve ser um aplicativo desktop profissional, moderno, extremamente fluido, leve, personalizável e com arquitetura preparada para receber novos módulos e funcionalidades.

Não desenvolva apenas uma demonstração visual. Desenvolva um software funcional, com integrações reais com o sistema operacional.

---

# 1. IDENTIDADE DO PROJETO

**Nome:** Dynasland

**Categoria:** Desktop Utility / Dynamic Island

**Plataformas:** Windows 10 e Windows 11 (x64).

**Idioma inicial:** Português do Brasil.

**Objetivo principal:**

Criar uma ilha dinâmica flutuante na parte superior da tela do Windows, capaz de apresentar informações, notificações, controles e atividades do computador em tempo real.

A interface deve ser discreta quando estiver inativa e se expandir automaticamente quando algum evento relevante acontecer.

O aplicativo deverá funcionar em segundo plano, sem precisar manter uma janela convencional aberta.

# 2. TECNOLOGIAS OBRIGATÓRIAS

Utilize as seguintes tecnologias:

- C# como linguagem principal.
- .NET 10 como plataforma de desenvolvimento.
- WPF para construção das interfaces.
- XAML para estilização e animações.
- MVVM para organização da aplicação.
- Win32 API para integração com o Windows.
- SQLite para armazenamento local.
- Dependency Injection para gerenciamento dos serviços.
- CommunityToolkit.Mvvm para implementação do MVVM.
- Microsoft.Extensions.Hosting para gerenciamento do ciclo de vida e serviços.

Utilize bibliotecas adicionais somente quando realmente necessárias.

Evite Electron, aplicações baseadas em navegador e dependências pesadas desnecessárias.

# 3. INTERFACE PRINCIPAL — DYNAMIC ISLAND

A ilha dinâmica deverá ficar centralizada horizontalmente na região superior da tela.

Características:

- Fundo predominantemente preto.
- Bordas totalmente arredondadas.
- Aparência minimalista.
- Cantos suaves.
- Animações fluidas.
- Sombras discretas.
- Transparência quando apropriado.
- Interface responsiva.
- Dimensões adaptáveis ao conteúdo.
- Suporte a diferentes resoluções e escalas de DPI.

A janela deverá ser configurada para:

- Não apresentar bordas tradicionais.
- Não aparecer na barra de tarefas.
- Permanecer sobre outras janelas quando necessário.
- Não roubar o foco do usuário.
- Permitir interações com os controles.
- Possuir posicionamento inteligente.
- Não bloquear desnecessariamente os cliques em outras regiões da tela.

A ilha deve possuir três estados principais:

### Estado 1 — Compacto

Uma pequena cápsula preta centralizada no topo.

Deverá apresentar indicadores discretos, como:

- Reprodução de mídia.
- Notificações pendentes.
- Downloads ativos.
- Atividades em andamento.

### Estado 2 — Expandido

Ao receber um evento, a ilha deverá expandir suavemente.

Exemplos:

- Música iniciada.
- Volume alterado.
- Notificação recebida.
- Download iniciado.
- Cronômetro ativado.

A expansão deverá apresentar as informações relevantes do evento.

### Estado 3 — Expandido interativo

Ao clicar ou interagir com a ilha, ela deverá apresentar controles adicionais.

Exemplos:

- Controles de música.
- Informações detalhadas.
- Botões de ação.
- Indicadores de progresso.
- Controles de volume.

A ilha deverá retornar ao estado compacto automaticamente após determinado período de inatividade, quando apropriado.

# 4. SISTEMA DE ANIMAÇÕES

As animações são uma das partes mais importantes do Dynasland.

Desenvolva animações profissionais utilizando WPF e Composition APIs quando apropriado.

Implementar:

- Expansão horizontal.
- Recolhimento horizontal.
- Expansão vertical.
- Transições entre módulos.
- Fade in e fade out.
- Animações de ícones.
- Indicadores de progresso.
- Animações de carregamento.
- Transições entre notificações.
- Animações de entrada e saída.

As animações devem apresentar continuidade visual.

Evite mudanças bruscas de tamanho ou posição.

Utilize curvas de aceleração e desaceleração naturais.

Permita configurar a intensidade e a velocidade das animações.

Evite animações desnecessárias que consumam recursos continuamente.

# 5. MÓDULOS E FUNCIONALIDADES

O Dynasland deverá possuir uma arquitetura modular.

Cada funcionalidade deverá ser implementada como um módulo independente, podendo ser habilitada ou desabilitada nas configurações.

## 5.1. Música e mídia

Integrar com a sessão de mídia do Windows utilizando a API Global System Media Transport Controls (GSMTC).

Funcionalidades:

- Identificar a mídia em reprodução.
- Exibir nome da música.
- Exibir artista.
- Exibir capa do álbum.
- Reproduzir.
- Pausar.
- Próxima música.
- Música anterior.
- Exibir progresso quando disponibilizado pela sessão.
- Exibir duração quando disponibilizada.
- Identificar aplicativo responsável pela reprodução.

Compatibilidade desejada com Spotify, navegadores e outros aplicativos que disponibilizem sessões de mídia pelo Windows.

## 5.2. Controle de volume

Detectar alterações de volume do sistema.

Exibir:

- Nível atual.
- Barra de progresso.
- Ícone correspondente.
- Estado de mudo.

Permitir alterar o volume diretamente pela ilha.

Utilizar Core Audio APIs do Windows.

## 5.3. Brilho da tela

Detectar alterações de brilho quando suportado pelo hardware.

Exibir:

- Porcentagem atual.
- Indicador visual.
- Ícone de brilho.

Permitir controlar o brilho em dispositivos compatíveis.

Não apresentar controles falsos em computadores sem suporte.

## 5.4. Notificações

Criar um módulo de notificações integrado ao sistema de notificações do Windows.

Exibir:

- Aplicativo de origem.
- Título.
- Mensagem.
- Ícone.
- Horário.

Permitir:

- Dispensar notificações.
- Expandir detalhes.
- Abrir o aplicativo de origem quando possível.
- Configurar quais aplicativos podem gerar notificações.

Evitar duplicação de notificações entre o Windows e o Dynasland.

## 5.5. Downloads

Criar uma interface para acompanhar downloads registrados pelos módulos compatíveis.

Exibir:

- Nome do arquivo.
- Tamanho.
- Progresso.
- Velocidade.
- Tempo estimado.
- Estado do download.

Não presumir que todos os navegadores disponibilizam seus downloads automaticamente. Criar uma arquitetura de provedores para futuras integrações.

## 5.6. Monitoramento do sistema

Criar um módulo de monitoramento em tempo real.

Exibir:

- Uso da CPU.
- Uso da memória RAM.
- Temperatura da CPU quando disponível.
- Temperatura da GPU quando disponível.
- Uso da GPU quando disponível.
- Espaço disponível em disco.

Utilizar APIs e mecanismos confiáveis.

Não inventar valores quando algum sensor não estiver disponível.

## 5.7. Bateria

Para notebooks:

- Porcentagem da bateria.
- Estado de carregamento.
- Estimativa de autonomia quando disponível.
- Avisos de bateria fraca.

## 5.8. Área de transferência

Criar integração com o histórico de área de transferência do Windows.

Exibir:

- Texto copiado.
- Imagens copiadas.
- Conteúdo recente.

Permitir copiar novamente o conteúdo.

Respeitar as permissões e configurações de privacidade do Windows.

Não armazenar senhas ou informações sensíveis deliberadamente.

## 5.9. Cronômetro e temporizador

Implementar:

- Cronômetro.
- Temporizador regressivo.
- Pausar.
- Continuar.
- Reiniciar.
- Notificação ao finalizar.

Permitir utilizar o cronômetro diretamente na ilha.

# 6. PAINEL DE CONFIGURAÇÕES

Criar uma janela independente de configurações com aparência moderna.

Organizar em categorias:

### Geral

- Iniciar com o Windows.
- Inicialização minimizada.
- Idioma.
- Exibir na tela principal.
- Comportamento da ilha.

### Aparência

- Tema escuro.
- Tema claro.
- Tema automático.
- Cor de destaque.
- Transparência.
- Intensidade das sombras.
- Arredondamento.
- Largura mínima e máxima.
- Altura da ilha.

### Animações

- Ativar ou desativar animações.
- Velocidade.
- Intensidade.
- Tempo de permanência expandida.

### Módulos

Permitir ativar e desativar individualmente cada módulo.

### Notificações

- Aplicativos permitidos.
- Aplicativos bloqueados.
- Duração.
- Prioridade.
- Comportamento.

### Sistema

- Executar com o Windows.
- Verificar atualizações.
- Restaurar configurações.
- Informações da versão.

# 7. DESIGN DA APLICAÇÃO

A interface deve seguir uma linguagem visual premium.

Referências de experiência:

- Dynamic Island da Apple.
- Windows 11.
- Fluent Design.
- Interfaces minimalistas modernas.
- Aplicativos de produtividade premium.

Diretrizes:

- Evitar excesso de informações.
- Evitar cores muito saturadas.
- Utilizar tipografia moderna.
- Priorizar legibilidade.
- Usar ícones consistentes.
- Criar hierarquia visual.
- Manter espaçamentos uniformes.
- Utilizar animações suaves.

A ilha deve possuir aparência predominantemente preta, enquanto as janelas de configurações podem utilizar tons de cinza e superfícies escuras.

A interface deve parecer um produto comercial acabado.

# 8. ARQUITETURA DO SOFTWARE

Organizar o projeto em camadas bem definidas.

Estrutura sugerida:

Dynasland/
│
├── Dynasland.sln
│
├── src/
│   ├── Dynasland.App/
│   │   ├── App.xaml
│   │   ├── App.xaml.cs
│   │   ├── Views/
│   │   ├── ViewModels/
│   │   ├── Resources/
│   │   ├── Styles/
│   │   └── Assets/
│   │
│   ├── Dynasland.Core/
│   │   ├── Interfaces/
│   │   ├── Models/
│   │   ├── Events/
│   │   └── Abstractions/
│   │
│   ├── Dynasland.Infrastructure/
│   │   ├── Database/
│   │   ├── Windows/
│   │   ├── Services/
│   │   └── Repositories/
│   │
│   ├── Dynasland.Modules/
│   │   ├── Media/
│   │   ├── Volume/
│   │   ├── Brightness/
│   │   ├── Notifications/
│   │   ├── Downloads/
│   │   ├── SystemMonitor/
│   │   ├── Battery/
│   │   ├── Clipboard/
│   │   └── Timer/
│   │
│   └── Dynasland.Tests/
│
├── docs/
├── installer/
└── README.md

A estrutura poderá ser ajustada conforme as necessidades reais do projeto.

Criar interfaces para os módulos e utilizar injeção de dependência.

Implementar um sistema de eventos para comunicação entre módulos e a interface principal.

Os módulos não devem depender diretamente da janela principal.

# 9. BANCO DE DADOS

Utilizar SQLite.

Armazenar:

- Preferências.
- Configurações visuais.
- Módulos habilitados.
- Histórico de atividades, quando permitido.
- Configurações de notificações.
- Preferências de animação.

Criar sistema de migrações para futuras atualizações.

O aplicativo deverá funcionar mesmo quando o banco estiver vazio, criando automaticamente sua estrutura inicial.

# 10. DESEMPENHO

O desempenho é um requisito fundamental.

O Dynasland deverá:

- Consumir pouca memória RAM quando estiver ocioso.
- Evitar uso excessivo da CPU.
- Não executar consultas desnecessárias continuamente.
- Utilizar eventos do Windows sempre que possível.
- Atualizar indicadores somente quando necessário.
- Evitar vazamentos de memória.
- Liberar recursos corretamente.
- Trabalhar em segundo plano.
- Não criar múltiplas instâncias sem necessidade.

Utilizar timers apenas quando necessários e com intervalos adequados.

Implementar cancelamento de tarefas assíncronas.

Evitar operações pesadas na thread de interface.

# 11. COMPATIBILIDADE COM MONITORES

Implementar suporte a:

- Resolução Full HD.
- Resolução QHD.
- Resolução 4K.
- Escala de DPI.
- Múltiplos monitores.
- Alteração de monitor principal.
- Mudança de resolução durante a execução.

A ilha deverá permanecer centralizada no monitor selecionado.

Ao desconectar um monitor, deverá reposicionar a interface adequadamente.

# 12. INTEGRAÇÃO COM O WINDOWS

Implementar:

- Inicialização automática.
- Ícone na bandeja do sistema.
- Menu de contexto.
- Minimização para a bandeja.
- Encerramento completo.
- Gerenciamento de instância única.
- Detecção de sessão bloqueada.
- Comportamento adequado durante suspensão e retomada.

A ilha deverá respeitar o modo de tela cheia e aplicativos que exigem prioridade visual, com opções configuráveis para ocultá-la.

# 13. SEGURANÇA E PRIVACIDADE

- Não solicitar permissões administrativas sem necessidade.
- Não coletar dados pessoais.
- Não enviar informações para servidores externos.
- Armazenar configurações localmente.
- Não armazenar conteúdo sensível da área de transferência deliberadamente.
- Utilizar APIs oficiais sempre que possível.
- Tratar erros sem encerrar inesperadamente o aplicativo.

# 14. INSTALADOR E DISTRIBUIÇÃO

Preparar o projeto para gerar um instalador profissional.

O instalador deverá:

- Instalar o aplicativo.
- Criar atalhos.
- Permitir inicialização automática.
- Criar entrada de desinstalação.
- Preservar configurações durante atualizações, quando apropriado.
- Oferecer desinstalação completa.

Gerar publicação Release x64.

Avaliar publicação self-contained para evitar dependência de instalação prévia do .NET.

# 15. TESTES

Criar testes para:

- Inicialização.
- Persistência de configurações.
- Comunicação entre módulos.
- Gerenciamento de eventos.
- Animações e transições.
- Integrações com Windows.
- Comportamento em múltiplos monitores.
- Recuperação de erros.

Criar testes unitários para as regras de negócio e testes de integração para os serviços que dependem do Windows.

# 16. ETAPAS DE DESENVOLVIMENTO

Não tente implementar todo o aplicativo em uma única etapa.

Trabalhe progressivamente.

### Fase 1 — Fundação

- Criar solução.
- Configurar dependências.
- Implementar arquitetura MVVM.
- Criar janela flutuante.
- Implementar posicionamento.
- Criar a ilha compacta.
- Implementar animações básicas.

### Fase 2 — Interface dinâmica

- Estado compacto.
- Estado expandido.
- Estado interativo.
- Sistema de eventos.
- Gerenciador de módulos.

### Fase 3 — Integrações

- Música.
- Volume.
- Brilho.
- Notificações.
- Bateria.

### Fase 4 — Recursos adicionais

- Monitoramento.
- Downloads.
- Área de transferência.
- Cronômetro.

### Fase 5 — Configurações

- Painel completo.
- Temas.
- Personalização.
- Preferências dos módulos.

### Fase 6 — Finalização

- Testes.
- Otimização.
- Correções.
- Instalador.
- Documentação.

# 17. REGRAS IMPORTANTES PARA O CODEX

1. Desenvolva código real e funcional.
2. Não utilize dados simulados para apresentar funcionalidades como concluídas.
3. Não deixe botões sem funcionamento.
4. Não implemente interfaces que não possuam integração com seus respectivos serviços.
5. Não concentre toda a lógica em uma única classe.
6. Não misture regras de negócio com componentes visuais.
7. Não ignore erros de compilação.
8. Não remova funcionalidades existentes ao implementar novas.
9. Utilize nomes claros para classes, métodos e propriedades.
10. Documente integrações complexas.
11. Mantenha o projeto organizado.
12. Verifique a compatibilidade das bibliotecas com o .NET 10 e Windows.
13. Teste cada etapa antes de avançar.
14. Atualize o README com instruções de compilação e execução.
15. Não declare uma funcionalidade como concluída sem testar sua integração real.

Se uma funcionalidade depender de APIs específicas, hardware ou permissões que não estejam disponíveis, implemente o tratamento adequado e informe a limitação.

# 18. RESULTADO FINAL ESPERADO

Entregar um aplicativo chamado Dynasland, pronto para ser utilizado no Windows 10 e Windows 11.

O software deverá possuir:

- Ilha dinâmica funcional.
- Interface premium.
- Animações fluidas.
- Módulos independentes.
- Integração real com o Windows.
- Configurações completas.
- Banco de dados local.
- Baixo consumo de recursos.
- Suporte a múltiplos monitores.
- Inicialização automática.
- Instalador profissional.

O objetivo é criar um produto que possa ser distribuído comercialmente no futuro.

**Prioridade absoluta: qualidade visual, desempenho, estabilidade e funcionalidades realmente operacionais.**

Comece pela Fase 1. Primeiro, analise o ambiente de desenvolvimento, crie a solução, configure a arquitetura e implemente a primeira versão funcional da ilha. Não avance para as próximas fases sem verificar a compilação e o funcionamento da etapa atual.

Ao finalizar cada fase, apresente um relatório com arquivos criados, funcionalidades concluídas, testes executados e eventuais limitações.
