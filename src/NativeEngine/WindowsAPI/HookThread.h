// Thread dedicada, com laço de mensagens, que hospeda os ganchos do Windows: mouse e teclado de baixo nível
// (ligados sob demanda) e o evento de primeiro plano (sempre ligado). As rotinas de gancho só copiam o
// evento para a fila e retornam: o Windows remove ganchos que demoram, e aqui nada depende da interface do C#.
#pragma once

#include <atomic>
#include <functional>
#include <string>
#include <thread>

#include "Interop/dynaisland_engine.h"

namespace dynaisland {

class HookThread {
public:
    using EventSink = std::function<void(const di_event&)>;
    using Logger = std::function<void(di_log_level, const std::string&)>;

    HookThread(EventSink sink, Logger log);
    ~HookThread();

    HookThread(const HookThread&) = delete;
    HookThread& operator=(const HookThread&) = delete;

    /// Sobe a thread e o gancho de primeiro plano. Só uma instância pode existir por vez (as rotinas do
    /// Windows são funções estáticas).
    di_result Start();

    /// Remove todos os ganchos e encerra a thread. Idempotente.
    void Stop();

    /// Liga/desliga mouse e teclado; espera a thread confirmar.
    di_result SetInputHooks(bool enabled);

private:
    struct Command;

    void Run();
    void OnCommand(Command& command);
    di_result InstallInputHooks();
    void RemoveInputHooks();

    static long long __stdcall MouseProc(int code, unsigned long long wParam, long long lParam);
    static long long __stdcall KeyboardProc(int code, unsigned long long wParam, long long lParam);
    static void __stdcall ForegroundProc(void* hook, unsigned long event, void* window, long objectId, long childId,
                                         unsigned long threadId, unsigned long time);

    EventSink sink_;
    Logger log_;
    std::thread thread_;
    std::atomic<unsigned long> thread_id_{0};

    // Só tocados pela própria thread.
    void* mouse_hook_ = nullptr;
    void* keyboard_hook_ = nullptr;
    void* foreground_hook_ = nullptr;

    static std::atomic<HookThread*> instance_;
};

}  // namespace dynaisland
