#include "WindowsAPI/HookThread.h"

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>

#include <future>
#include <memory>

namespace dynaisland {

std::atomic<HookThread*> HookThread::instance_{nullptr};

namespace {

constexpr UINT kCommandMessage = WM_APP + 1;
constexpr auto kCommandTimeout = std::chrono::seconds(5);

/// Módulo desta DLL (e não o do executável que a carregou), exigido pelos ganchos globais.
HMODULE ThisModule() {
    HMODULE module = nullptr;
    GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                       reinterpret_cast<LPCWSTR>(&ThisModule), &module);
    return module;
}

di_event MakeEvent(di_event_type type) {
    di_event event{};
    event.size = sizeof(di_event);
    event.type = type;
    return event;
}

}  // namespace

struct HookThread::Command {
    bool enable = false;
    std::promise<di_result> result;
};

HookThread::HookThread(EventSink sink, Logger log) : sink_(std::move(sink)), log_(std::move(log)) {}

HookThread::~HookThread() { Stop(); }

di_result HookThread::Start() {
    HookThread* expected = nullptr;
    if (!instance_.compare_exchange_strong(expected, this)) {
        return DI_ERR_ALREADY_STARTED;
    }

    std::promise<di_result> ready;
    auto ready_future = ready.get_future();
    try {
        thread_ = std::thread([this, &ready] {
            MSG message;
            PeekMessageW(&message, nullptr, WM_USER, WM_USER, PM_NOREMOVE);  // cria a fila de mensagens
            thread_id_ = GetCurrentThreadId();

            foreground_hook_ = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, nullptr,
                                               reinterpret_cast<WINEVENTPROC>(&HookThread::ForegroundProc), 0, 0,
                                               WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
            if (foreground_hook_ == nullptr) {
                log_(DI_LOG_ERROR, "SetWinEventHook(primeiro plano) falhou: erro " + std::to_string(GetLastError()));
                ready.set_value(DI_ERR_HOOK_FAILED);
                return;
            }

            ready.set_value(DI_OK);
            Run();
        });
    } catch (const std::system_error&) {
        instance_ = nullptr;
        return DI_ERR_INTERNAL;
    }

    const di_result result = ready_future.get();
    if (result != DI_OK) {
        thread_.join();
        thread_id_ = 0;
        instance_ = nullptr;
    }
    return result;
}

void HookThread::Run() {
    MSG message;
    while (GetMessageW(&message, nullptr, 0, 0) > 0) {
        if (message.message == kCommandMessage) {
            std::unique_ptr<std::shared_ptr<Command>> owned(reinterpret_cast<std::shared_ptr<Command>*>(message.lParam));
            OnCommand(**owned);
            continue;
        }
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }

    RemoveInputHooks();
    if (foreground_hook_ != nullptr) {
        UnhookWinEvent(static_cast<HWINEVENTHOOK>(foreground_hook_));
        foreground_hook_ = nullptr;
    }
}

void HookThread::OnCommand(Command& command) {
    if (command.enable) {
        command.result.set_value(InstallInputHooks());
    } else {
        RemoveInputHooks();
        command.result.set_value(DI_OK);
    }
}

di_result HookThread::InstallInputHooks() {
    if (mouse_hook_ != nullptr) {
        return DI_OK;  // já ligados
    }

    const HMODULE module = ThisModule();
    mouse_hook_ = SetWindowsHookExW(WH_MOUSE_LL, reinterpret_cast<HOOKPROC>(&HookThread::MouseProc), module, 0);
    if (mouse_hook_ == nullptr) {
        log_(DI_LOG_ERROR, "SetWindowsHookEx(mouse) falhou: erro " + std::to_string(GetLastError()));
        return DI_ERR_HOOK_FAILED;
    }

    keyboard_hook_ = SetWindowsHookExW(WH_KEYBOARD_LL, reinterpret_cast<HOOKPROC>(&HookThread::KeyboardProc), module, 0);
    if (keyboard_hook_ == nullptr) {
        log_(DI_LOG_ERROR, "SetWindowsHookEx(teclado) falhou: erro " + std::to_string(GetLastError()));
        RemoveInputHooks();
        return DI_ERR_HOOK_FAILED;
    }

    return DI_OK;
}

void HookThread::RemoveInputHooks() {
    if (mouse_hook_ != nullptr) {
        UnhookWindowsHookEx(static_cast<HHOOK>(mouse_hook_));
        mouse_hook_ = nullptr;
    }
    if (keyboard_hook_ != nullptr) {
        UnhookWindowsHookEx(static_cast<HHOOK>(keyboard_hook_));
        keyboard_hook_ = nullptr;
    }
}

di_result HookThread::SetInputHooks(bool enabled) {
    const unsigned long thread_id = thread_id_;
    if (thread_id == 0) {
        return DI_ERR_NOT_STARTED;
    }

    auto command = std::make_shared<Command>();
    command->enable = enabled;
    auto result = command->result.get_future();

    // A thread assume a posse deste ponteiro (e o libera) se a mensagem for entregue.
    auto* handoff = new std::shared_ptr<Command>(command);
    if (!PostThreadMessageW(thread_id, kCommandMessage, 0, reinterpret_cast<LPARAM>(handoff))) {
        delete handoff;
        return DI_ERR_INTERNAL;
    }

    if (result.wait_for(kCommandTimeout) != std::future_status::ready) {
        log_(DI_LOG_ERROR, "A thread dos ganchos não respondeu a tempo.");
        return DI_ERR_INTERNAL;
    }
    return result.get();
}

void HookThread::Stop() {
    if (!thread_.joinable()) {
        return;
    }

    PostThreadMessageW(thread_id_, WM_QUIT, 0, 0);
    thread_.join();
    thread_id_ = 0;
    instance_ = nullptr;
}

long long __stdcall HookThread::MouseProc(int code, unsigned long long wParam, long long lParam) {
    HookThread* self = instance_.load();
    if (self != nullptr && code == HC_ACTION && self->mouse_hook_ != nullptr) {
        unsigned button = 0;
        if (wParam == WM_LBUTTONDOWN) button = 1;
        else if (wParam == WM_RBUTTONDOWN) button = 2;
        else if (wParam == WM_MBUTTONDOWN) button = 3;

        if (button != 0) {
            const auto* info = reinterpret_cast<const MSLLHOOKSTRUCT*>(lParam);
            di_event event = MakeEvent(DI_EVENT_MOUSE_DOWN);
            event.x = info->pt.x;
            event.y = info->pt.y;
            event.button = button;
            self->sink_(event);
        }
    }
    return CallNextHookEx(nullptr, code, static_cast<WPARAM>(wParam), static_cast<LPARAM>(lParam));
}

long long __stdcall HookThread::KeyboardProc(int code, unsigned long long wParam, long long lParam) {
    HookThread* self = instance_.load();
    if (self != nullptr && code == HC_ACTION && self->keyboard_hook_ != nullptr && wParam == WM_KEYDOWN) {
        const auto* info = reinterpret_cast<const KBDLLHOOKSTRUCT*>(lParam);
        if (info->vkCode == VK_ESCAPE) {
            self->sink_(MakeEvent(DI_EVENT_ESCAPE_DOWN));
        }
    }
    return CallNextHookEx(nullptr, code, static_cast<WPARAM>(wParam), static_cast<LPARAM>(lParam));
}

void __stdcall HookThread::ForegroundProc(void*, unsigned long, void* window, long objectId, long, unsigned long,
                                          unsigned long) {
    HookThread* self = instance_.load();
    if (self != nullptr && objectId == OBJID_WINDOW) {
        di_event event = MakeEvent(DI_EVENT_FOREGROUND_CHANGED);
        event.window = reinterpret_cast<std::uintptr_t>(window);
        self->sink_(event);
    }
}

}  // namespace dynaisland
