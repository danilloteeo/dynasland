// API pública do DynaIsland Native Engine (DLL nativa, C++20), consumida pelo C# via P/Invoke.
//
// Contrato:
//  - Só funções extern "C" e tipos simples de tamanho fixo; nenhum objeto C++ cruza a fronteira.
//  - Nenhuma função lança exceção: erros voltam como di_result; di_result_message descreve o código.
//  - Convenção de chamada __stdcall (WINAPI), ponteiros de 64 bits (a DLL é x64).
//  - Os callbacks rodam numa thread própria do motor (nunca na thread dos ganchos do Windows) e NÃO podem
//    chamar di_stop (deadlock): o C# deve apenas enfileirar o trabalho no Dispatcher e retornar.
//  - Qualquer mudança incompatível (campos, valores, assinaturas) sobe DI_API_VERSION.
#pragma once

#include <stddef.h>
#include <stdint.h>

#ifdef DI_BUILDING_DLL
#define DI_API extern "C" __declspec(dllexport)
#else
#define DI_API extern "C" __declspec(dllimport)
#endif

#define DI_API_VERSION 1u

typedef enum di_result {
    DI_OK = 0,
    DI_ERR_INVALID_ARGUMENT = 1,
    DI_ERR_VERSION_MISMATCH = 2,
    DI_ERR_ALREADY_STARTED = 3,
    DI_ERR_NOT_STARTED = 4,
    DI_ERR_HOOK_FAILED = 5,
    DI_ERR_INTERNAL = 6
} di_result;

typedef enum di_event_type {
    /// Botão do mouse apertado (x, y em pixels físicos da tela; button = 1 esquerdo, 2 direito, 3 do meio).
    DI_EVENT_MOUSE_DOWN = 1,
    /// Tecla Esc apertada (a tecla continua valendo para o aplicativo em foco).
    DI_EVENT_ESCAPE_DOWN = 2,
    /// Outra janela assumiu o primeiro plano (window = HWND). Sempre ativo enquanto o motor estiver iniciado.
    DI_EVENT_FOREGROUND_CHANGED = 3
} di_event_type;

typedef enum di_log_level {
    DI_LOG_DEBUG = 0,
    DI_LOG_INFO = 1,
    DI_LOG_WARNING = 2,
    DI_LOG_ERROR = 3
} di_log_level;

typedef struct di_event {
    uint32_t size;    // sizeof(di_event), para evoluir sem quebrar
    uint32_t type;    // di_event_type
    int32_t x;
    int32_t y;
    uint64_t window;  // HWND como inteiro
    uint32_t button;
    uint32_t reserved;
} di_event;

// O espelho em C# (NativeMethods.Event) depende deste layout exato.
static_assert(sizeof(di_event) == 32, "di_event deve ter 32 bytes");
static_assert(offsetof(di_event, window) == 16, "di_event.window deve ficar no offset 16");
static_assert(offsetof(di_event, button) == 24, "di_event.button deve ficar no offset 24");

typedef void(__stdcall* di_event_callback)(const di_event* event, void* user_data);
typedef void(__stdcall* di_log_callback)(int32_t level, const char* utf8_message, void* user_data);

/// Versão da API desta DLL (DI_API_VERSION com que foi compilada).
DI_API uint32_t __stdcall di_api_version(void);

/// Inicia o motor. expected_api_version é a versão com que o chamador foi escrito; se diferir da da DLL,
/// devolve DI_ERR_VERSION_MISMATCH e nada é iniciado. on_event é obrigatório; on_log é opcional.
DI_API di_result __stdcall di_start(uint32_t expected_api_version, di_event_callback on_event, void* event_user_data,
                                    di_log_callback on_log, void* log_user_data);

/// Para o motor, remove todos os ganchos e espera as threads terminarem. Idempotente.
DI_API di_result __stdcall di_stop(void);

/// Liga/desliga os ganchos globais de mouse e teclado (Esc). Só os ganchos de entrada: o evento de primeiro
/// plano não depende disto. Deve ser usado só enquanto precisar (ex.: ilha aberta), pois todo clique passa por eles.
DI_API di_result __stdcall di_set_input_hooks(int32_t enabled);

/// Texto fixo (ASCII) que descreve o código; nunca nulo e não deve ser liberado.
DI_API const char* __stdcall di_result_message(di_result result);
