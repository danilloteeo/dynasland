# MASTER PROMPT — DynaIsland
## Migração evolutiva para arquitetura híbrida C# + C++

Você é um engenheiro de software sênior especializado em C#, C++, Windows API, WPF, arquitetura de sistemas desktop e desenvolvimento de aplicações nativas para Windows.

Sua missão é evoluir minha aplicação EXISTENTE chamada **DynaIsland**, transformando-a em uma aplicação profissional de Dynamic Island para Windows, utilizando uma arquitetura híbrida C# + C++.

**ATENÇÃO: NÃO CRIE OUTRO PROJETO DO ZERO.**

O projeto já existe e possui uma implementação anterior. Seu trabalho é analisar, preservar, corrigir, melhorar e expandir o código existente.

---

# 1. OBJETIVO PRINCIPAL

Transformar o DynaIsland em uma aplicação de Dynamic Island inspirada na experiência do iPhone, mas desenvolvida especificamente para Windows 10 e Windows 11.

A aplicação deverá possuir:
- Interface moderna e profissional.
- Animações fluidas.
- Expansão e retração inteligente.
- Múltiplos módulos independentes.
- Integração profunda com Windows.
- Monitoramento de hardware.
- Integração com aplicativos externos.
- Sistema de personalização.
- Arquitetura extensível.
- Baixo consumo de recursos.
- Execução em segundo plano.

A evolução deverá aproveitar a implementação existente.

# 2. ARQUITETURA HÍBRIDA

A aplicação deverá utilizar duas linguagens, cada uma com responsabilidades bem definidas.

## C# — Aplicação principal

Tecnologias:
- .NET 10.
- WPF.
- C#.
- MVVM.
- SQLite.
- Windows API.

Responsabilidades:
- Interface gráfica.
- Renderização dos componentes.
- Animações.
- Gerenciamento das janelas.
- Controle de estados da Dynamic Island.
- Gerenciamento de módulos.
- Configurações.
- Temas.
- Comunicação com o motor nativo.
- Integração com notificações.
- Gerenciamento de eventos.

O C# será responsável pela experiência completa do usuário.

## C++ — Native Engine

Tecnologias:
- C++20.
- Visual Studio C++.
- Windows SDK.
- Win32 API.
- Biblioteca DLL nativa.

Responsabilidades:
- Operações de baixo nível.
- Integrações nativas com Windows.
- Monitoramento de recursos do sistema quando necessário.
- Operações que demandem maior controle de hardware.
- Processamento intensivo.
- Funcionalidades que se beneficiem de código nativo.

Não transfira funcionalidades para C++ apenas por questão de desempenho. Utilize C++ quando houver benefício técnico real.

## Comunicação entre C# e C++

Utilizar P/Invoke como mecanismo principal de comunicação.

Criar uma interface nativa bem definida, com:
- Funções exportadas.
- Tipos de dados compatíveis.
- Tratamento de erros.
- Gerenciamento de memória.
- Controle de ciclo de vida.
- Logs.
- Versionamento da API nativa.

Utilizar extern "C" nas funções exportadas para evitar problemas de name mangling.

Evitar compartilhar objetos complexos diretamente entre C# e C++.

Definir contratos claros para a comunicação.

# 3. ESTRUTURA DO PROJETO

Primeiramente, analise a estrutura atual.

Não renomeie projetos, pastas, namespaces ou componentes sem necessidade.

A estrutura desejada, adaptada ao projeto existente, será semelhante a:

DynaIsland/
├── src/
│   ├── DynaIsland.App/
│   ├── DynaIsland.Core/
│   ├── DynaIsland.Infrastructure/
│   ├── DynaIsland.Modules/
│   ├── DynaIsland.Settings/
│   └── NativeEngine/
│       ├── Hardware/
│       ├── WindowsAPI/
│       ├── Monitoring/
│       ├── Interop/
│       ├── CoreEngine.cpp
│       └── CoreEngine.h
│
├── resources/
├── plugins/
├── tests/
├── installer/
└── documentation/

Essa estrutura é uma referência arquitetural. Preserve a organização atual quando ela já atender aos requisitos.

# 4. INTERFACE VISUAL

Manter a identidade visual já definida para o DynaIsland.

Características:
- Design inspirado na Dynamic Island do iPhone.
- Interface escura.
- Elementos translúcidos.
- Bordas arredondadas.
- Animações suaves.
- Ícones modernos.
- Tipografia profissional.
- Aparência premium.
- Integração visual com Windows 11.

Utilizar:
- WPF.
- Storyboards.
- Acrylic/Mica quando apropriado.
- SVG ou ícones Lucide.
- Componentes reutilizáveis.

A Dynamic Island deverá possuir três estados principais:

1. Compact:
   - Barra pequena.
   - Informações essenciais.
   - Indicadores de atividades.

2. Expanded:
   - Expansão ao interagir.
   - Informações detalhadas.
   - Controles de mídia.
   - Botões de ação.

3. Activity:
   - Exibição de atividades em andamento.
   - Downloads.
   - Notificações.
   - Reprodução de mídia.
   - Monitoramento.

# 5. SISTEMA DE MÓDULOS

Desenvolver uma arquitetura modular.

Cada funcionalidade deverá ser independente e possuir seus próprios serviços, configurações e componentes visuais.

Módulos planejados:

## Música
- Integração com Windows Media Session API.
- Spotify.
- YouTube e outros players compatíveis com sessões de mídia.
- Nome da música.
- Artista.
- Capa do álbum.
- Play/Pause.
- Próxima faixa.
- Faixa anterior.
- Barra de progresso, quando disponível.

## Notificações
- Integração com notificações do Windows.
- Exibição de notificações na Dynamic Island.
- Identificação do aplicativo de origem.
- Animações de entrada e saída.

## Hardware
- CPU.
- GPU.
- RAM.
- Temperatura, quando disponível.
- Uso de recursos em tempo real.

Utilizar C# e bibliotecas existentes quando forem suficientes. Recorrer ao C++ quando necessário.

## Downloads
- Monitoramento de downloads.
- Progresso.
- Velocidade.
- Status.
- Notificação de conclusão.

## Bateria
- Percentual.
- Estado de carregamento.
- Tempo restante, quando disponível.

## Controles rápidos
- Volume.
- Dispositivos de áudio.
- Atalhos.
- Controles do Windows.

# 6. GERENCIAMENTO DE JANELAS

A Dynamic Island deverá:
- Permanecer posicionada no topo da tela.
- Possuir comportamento inteligente de expansão.
- Ser compatível com múltiplos monitores.
- Respeitar a resolução e escala de cada monitor.
- Ocultar-se quando necessário em aplicativos de tela cheia, conforme configuração.
- Não atrapalhar o uso normal do Windows.
- Permitir execução em segundo plano.
- Possuir inicialização automática opcional.

# 7. CONFIGURAÇÕES

Criar ou evoluir o painel de configurações existente.

Permitir:
- Ativar e desativar módulos.
- Personalizar aparência.
- Configurar transparência.
- Ajustar animações.
- Definir posição.
- Configurar inicialização automática.
- Personalizar comportamento de expansão.
- Configurar monitores.
- Restaurar configurações padrão.

Utilizar SQLite para persistência local.

# 8. DESEMPENHO

O sistema deverá ser otimizado para:
- Baixo consumo de RAM.
- Baixo consumo de CPU em repouso.
- Atualizações eficientes.
- Eventos em vez de polling desnecessário.
- Comunicação assíncrona quando apropriado.
- Liberação correta de recursos.
- Ausência de vazamentos de memória.

Evitar chamadas excessivas entre C# e C++.

Implementar comunicação eficiente, preferencialmente orientada a eventos para atualizações contínuas.

# 9. SEGURANÇA E ESTABILIDADE

Implementar:
- Tratamento global de exceções.
- Logs estruturados.
- Recuperação de falhas dos módulos.
- Validação de dados recebidos da DLL.
- Proteção contra falhas de comunicação.
- Gerenciamento correto de recursos nativos.
- Testes unitários.
- Testes de integração C# / C++.

Uma falha em um módulo não deverá derrubar toda a aplicação.

# 10. REGRAS OBRIGATÓRIAS DE DESENVOLVIMENTO

1. Não recriar a aplicação do zero.
2. Não apagar funcionalidades existentes.
3. Não substituir componentes funcionais sem justificativa.
4. Não duplicar serviços ou responsabilidades.
5. Não criar código fictício apenas para simular funcionalidades.
6. Não deixar funcionalidades principais com implementações vazias.
7. Não utilizar comunicação entre linguagens sem contratos definidos.
8. Manter compatibilidade com o banco existente.
9. Manter os padrões de código do projeto sempre que possível.
10. Documentar mudanças arquiteturais.
11. Compilar e testar após cada etapa importante.
12. Corrigir erros antes de avançar para a próxima etapa.

# 11. ETAPAS DE EXECUÇÃO

## ETAPA 1 — AUDITORIA

Antes de modificar qualquer arquivo:
- Examinar toda a solução.
- Identificar projetos existentes.
- Identificar tecnologias e versões.
- Examinar dependências.
- Identificar funcionalidades implementadas.
- Examinar banco de dados.
- Identificar pontos de melhoria.
- Identificar possíveis conflitos com a arquitetura híbrida.

Produzir um relatório técnico.

Não realizar alterações nessa etapa.

## ETAPA 2 — PLANEJAMENTO

Apresentar:
- Arquitetura atual.
- Arquitetura proposta.
- Componentes que serão preservados.
- Componentes que serão modificados.
- Novos componentes.
- Estratégia de comunicação C# / C++.
- Riscos técnicos.
- Ordem de implementação.

Aguardar minha aprovação antes de iniciar alterações estruturais.

## ETAPA 3 — IMPLEMENTAÇÃO DO NATIVE ENGINE

Criar a biblioteca C++20.

Implementar:
- Estrutura da DLL.
- API exportada.
- Inicialização e encerramento.
- Funções nativas necessárias.
- Tratamento de erros.
- Logs.
- Testes.

Integrar a DLL com o projeto C# utilizando P/Invoke.

## ETAPA 4 — INTEGRAÇÃO COM C#

Adaptar a aplicação existente.

Implementar:
- Serviço de comunicação nativa.
- Contratos de interoperabilidade.
- Gerenciamento do ciclo de vida.
- Tratamento de falhas.
- Integração com os serviços existentes.

## ETAPA 5 — MÓDULOS

Evoluir os módulos individualmente.

Priorizar:
1. Música.
2. Notificações.
3. Hardware.
4. Bateria.
5. Downloads.
6. Controles rápidos.

Preservar funcionalidades já existentes e evitar reimplementações desnecessárias.

## ETAPA 6 — INTERFACE E EXPERIÊNCIA

Aprimorar:
- Animações.
- Transições.
- Estados da Dynamic Island.
- Responsividade.
- Configurações.
- Experiência de utilização.

## ETAPA 7 — TESTES E OTIMIZAÇÃO

Executar:
- Compilação completa.
- Testes unitários.
- Testes de integração.
- Testes de desempenho.
- Testes de estabilidade.
- Testes em múltiplos monitores.
- Testes de inicialização e encerramento.

## ETAPA 8 — DISTRIBUIÇÃO

Preparar:
- Build de produção.
- DLL nativa.
- Dependências necessárias.
- Instalador Windows.
- Inicialização automática opcional.
- Documentação de instalação e atualização.

# 12. RESULTADO ESPERADO

Entregar uma evolução real da aplicação DynaIsland.

O resultado deverá ser um software desktop profissional, com arquitetura híbrida, interface moderna, módulos independentes, integração nativa com Windows e possibilidade de expansão futura.

**IMPORTANTE:**

Comece exclusivamente pela ETAPA 1 — AUDITORIA.

Analise o projeto existente antes de escrever ou modificar qualquer código.

Não faça uma migração indiscriminada. A arquitetura híbrida deverá ser introduzida gradualmente, mantendo tudo que já funciona.

Ao finalizar a auditoria, apresente o relatório técnico e aguarde minha aprovação.