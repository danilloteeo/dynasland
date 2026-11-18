# Arquitetura híbrida C# + C++ (Native Engine)

Introduzida na 1.8.0. O DynaIsland continua sendo um aplicativo WPF/.NET 10; o C++ entra só onde traz benefício técnico real.

## O que está no C++ e por quê

`src/NativeEngine` (C++20, DLL x64, CMake) hospeda os **ganchos globais do Windows** numa thread própria:

| Gancho | Uso | Quando fica ligado |
|---|---|---|
| `WH_MOUSE_LL` | clique fora da ilha fecha o painel | só com a ilha aberta (`di_set_input_hooks`) |
| `WH_KEYBOARD_LL` | Esc fecha o painel | idem |
| `EVENT_SYSTEM_FOREGROUND` | detectar tela cheia e jogos | sempre, enquanto o motor roda |

**Benefício:** o Windows remove um gancho de baixo nível que demora a responder. Antes, os ganchos rodavam na thread da
interface do WPF; se ela engasgasse (animação pesada, coleta de lixo), o gancho podia cair e a ilha deixava de detectar
cliques fora. Agora a rotina do gancho só copia o evento para uma fila limitada e retorna em microssegundos; uma segunda
thread entrega ao C#, e o C# apenas enfileira no `Dispatcher`.

Todo o resto (Win32 de captura, brilho, áudio, PDH, notificações) continua em C# via P/Invoke: funciona e não ganharia nada.

## Estrutura

```
src/NativeEngine/
├── Interop/dynaisland_engine.h   contrato público (API C, versionada)
├── Interop/exports.cpp           funções exportadas; nenhuma exceção C++ sai daqui
├── CoreEngine.h/.cpp             ciclo de vida: iniciar, parar, entrega de eventos
├── EventQueue.h                  fila limitada (descarta o evento novo se encher; nunca bloqueia o gancho)
├── WindowsAPI/HookThread.*       thread com laço de mensagens que hospeda os ganchos
└── tests/engine_tests.cpp        testes sem biblioteca externa (CTest)

src/DynaIsland.Core/Interfaces/NativeEngineServices.cs   INativeEngine, NativeEvent
src/DynaIsland.Infrastructure/Native/                    NativeMethods (P/Invoke) e NativeEngineService
```

## Contrato C# / C++

- Somente funções `extern "C"`, `__stdcall`, tipos de tamanho fixo. `di_event` tem 32 bytes (travado por `static_assert`
  no C++ e por teste no C#).
- **Versionamento:** `DI_API_VERSION` (hoje 1). `di_start` recebe a versão esperada pelo chamador e devolve
  `DI_ERR_VERSION_MISMATCH` se diferir; o C# também confere `di_api_version()` antes. Mudança incompatível sobe o número
  nos dois lados.
- **Erros:** todas as funções devolvem `di_result`; `di_result_message` descreve o código. O C# trata DLL ausente,
  versão diferente e falha ao iniciar como "motor indisponível" (`INativeEngine.IsAvailable == false`).
- **Memória:** nenhum ponteiro de propriedade cruza a fronteira. O `di_event` só vale durante o callback; strings de log
  também. Os delegates do C# ficam em campos para o coletor não os recolher.
- **Ciclo de vida:** um motor por processo. `di_start`/`di_stop` são idempotentes e seguros contra chamada dupla.
  `di_stop` não pode ser chamado de dentro de um callback (devolveria `DI_ERR_INTERNAL`).
- **Logs:** o motor envia suas mensagens por `di_log_callback`; o C# as grava no log do aplicativo
  (`NativeEngineService`).

## Falhas e fallback

Uma falha no motor não derruba o aplicativo: sem a DLL, `OutsideClickWatcher` instala os mesmos ganchos em C# na thread
da interface e `FullscreenDetector` instala o seu próprio gancho de primeiro plano, como na 1.7.x. A versão portátil e
o instalador sempre levam a DLL; ela é embutida na DLL o runtime do VC++ (`/MT`), sem depender do redistribuível.

## Build

- `tools\Build-NativeEngine.ps1`: compila (CMake + MSVC) e roda os testes nativos. Requer o *Visual Studio Build Tools
  2022* com a carga "Desenvolvimento para desktop com C++".
- `tools\Build-Release.ps1` chama o script acima antes dos testes e da publicação.
- A `Infrastructure` copia `src/NativeEngine/build/bin/Release/DynaIslandEngine.dll` para a saída e para o publish se ela existir.
  Sem a DLL (máquina sem toolchain), tudo compila e os testes de integração com a DLL são ignorados.

## Testes

- Nativos (`ctest`): ciclo de vida, argumentos inválidos, reinício, fila (ordem, descarte, encerramento).
- C# (`NativeEngineTests`): layout da struct, versão da API, ligar/desligar ganchos com a DLL real, reuso após `Dispose`.
- Os testes não geram cliques nem teclas; só instalam e removem os ganchos.
