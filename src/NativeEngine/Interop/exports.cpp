// Funções exportadas (API C). Nenhuma exceção C++ sai daqui: tudo é convertido em di_result.
#include <exception>

#include "CoreEngine.h"
#include "Interop/dynaisland_engine.h"

namespace {

template <typename Action>
di_result Guard(Action&& action) noexcept {
    try {
        return action();
    } catch (const std::exception&) {
        return DI_ERR_INTERNAL;
    } catch (...) {
        return DI_ERR_INTERNAL;
    }
}

}  // namespace

DI_API uint32_t __stdcall di_api_version(void) { return DI_API_VERSION; }

DI_API di_result __stdcall di_start(uint32_t expected_api_version, di_event_callback on_event, void* event_user_data,
                                    di_log_callback on_log, void* log_user_data) {
    return Guard([&] {
        return dynaisland::CoreEngine::Instance().Start(expected_api_version, on_event, event_user_data, on_log,
                                                         log_user_data);
    });
}

DI_API di_result __stdcall di_stop(void) {
    return Guard([] { return dynaisland::CoreEngine::Instance().Stop(); });
}

DI_API di_result __stdcall di_set_input_hooks(int32_t enabled) {
    return Guard([&] { return dynaisland::CoreEngine::Instance().SetInputHooks(enabled != 0); });
}

DI_API const char* __stdcall di_result_message(di_result result) {
    switch (result) {
        case DI_OK: return "ok";
        case DI_ERR_INVALID_ARGUMENT: return "invalid argument";
        case DI_ERR_VERSION_MISMATCH: return "api version mismatch";
        case DI_ERR_ALREADY_STARTED: return "engine already started";
        case DI_ERR_NOT_STARTED: return "engine not started";
        case DI_ERR_HOOK_FAILED: return "could not install windows hook";
        case DI_ERR_INTERNAL: return "internal error";
    }
    return "unknown result";
}
