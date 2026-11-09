# Próximos passos — Fase 6 (Finalização)

Estado em 2026-11-09: Fases 1 a 5 concluídas, mais suporte a várias mídias simultâneas
(commit `eeca79a`). 181 testes passando. Existe apenas a versão portátil (`publish/Dynasland-portable.zip`).

## Decisões pendentes (confirmar antes de começar)

1. **Formato do instalador** — recomendado: **Inno Setup** agora; MSIX depois.
   - MSIX exige certificado de assinatura, mas daria identidade de pacote (notificações por evento,
     sem a verificação a cada 1,5 s).
2. **GitHub** — criar o repositório **público** `danilloteeo/dynasland` (ou um repositório público só
   para instaladores) e publicar a Release `0.1.0`. É o que faz a verificação de atualizações funcionar
   (`DynaslandUpdateRepository` em `Directory.Build.props`).

## 1. Instalador (seção 14 do prompt)

- Instalação por usuário, sem administrador, em `%LocalAppData%\Programs\Dynasland`.
- Atalhos no Menu Iniciar (e opcional na Área de Trabalho).
- Opção "Iniciar com o Windows" (mesma entrada `Run` usada por `StartupRegistration`, argumento `--inicializacao`).
- Entrada em Configurações › Aplicativos; desinstalação completa com opção de apagar
  `%LocalAppData%\Dynasland` (configurações e logs).
- Atualização preserva configurações; fechar o Dynasland em execução antes de instalar.
- `DynaslandSetup.exe` Release x64 self-contained; script do instalador em `installer/`.

## 2. Otimização (seção 10)

- Memória (hoje 85–127 MB privados): criar a janela de configurações sob demanda e liberá-la ao fechar,
  liberar imagens/conteúdos ocultos, avaliar `TieredPGO`/trimming/ReadyToRun no publish.
- CPU: confirmar 0% parado; medir equalizador (20 fps) e verificação de notificações.
- Vazamentos: abrir/fechar ilha e configurações repetidamente e comparar memória.

## 3. Funcionalidades ainda faltando do prompt

- **Seção 12**: sessão bloqueada (Win+L) e suspensão/retomada — ocultar/pausar e restaurar corretamente
  (`SystemEvents.SessionSwitch`, `PowerModeChanged`).

## 4. Testes (seção 15)

- Troca de monitor principal, desconexão do monitor da ilha, mudança de resolução em execução.
- Recuperação de erros: banco corrompido, falha de módulo durante o uso.
- Instalar / atualizar / desinstalar o instalador.

## 5. Correções conhecidas

- Barra de título não ficou clara no tema claro no Windows 10 (confirmar).
- Validar no uso real: abas de mídia, temporizador (alarme), área de transferência, painel do sistema.
- Revisar nomes acessíveis (leitores de tela) na ilha.

## 6. Documentação

- README final (instalar, compilar, publicar, gerar instalador), guia de uso curto,
  notas da versão 0.1.0 e relatório final de todas as fases.
