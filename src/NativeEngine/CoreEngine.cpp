#include "CoreEngine.h"

namespace dynaisland {

CoreEngine& CoreEngine::Instance() {
    static CoreEngine engine;
    return engine;
}

di_result CoreEngine::Start(uint32_t expected_api_version, di_event_callback on_event, void* event_user_data,
                            di_log_callback on_log, void* log_user_data) {
    if (on_event == nullptr) {
        return DI_ERR_INVALID_ARGUMENT;
    }
    if (expected_api_version != DI_API_VERSION) {
        return DI_ERR_VERSION_MISMATCH;
    }

    std::lock_guard lock(mutex_);
    if (started_) {
        return DI_ERR_ALREADY_STARTED;
    }

    on_event_ = on_event;
    event_user_data_ = event_user_data;
    on_log_ = on_log;
    log_user_data_ = log_user_data;

    queue_ = std::make_unique<EventQueue>(kQueueCapacity);
    hooks_ = std::make_unique<HookThread>([this](const di_event& event) { queue_->Push(event); },
                                          [this](di_log_level level, const std::string& message) { Log(level, message); });

    try {
        delivery_ = std::thread([this] { Deliver(); });
    } catch (const std::system_error&) {
        hooks_.reset();
        queue_.reset();
        return DI_ERR_INTERNAL;
    }

    const di_result result = hooks_->Start();
    if (result != DI_OK) {
        queue_->Close();
        delivery_.join();
        hooks_.reset();
        queue_.reset();
        return result;
    }

    started_ = true;
    Log(DI_LOG_INFO, "Motor nativo iniciado (API " + std::to_string(DI_API_VERSION) + ").");
    return DI_OK;
}

di_result CoreEngine::Stop() {
    std::lock_guard lock(mutex_);
    if (!started_) {
        return DI_OK;
    }
    if (std::this_thread::get_id() == delivery_.get_id()) {
        // Chamado de dentro de um callback: esperar a própria thread travaria para sempre.
        return DI_ERR_INTERNAL;
    }

    hooks_->Stop();   // depois disto ninguém mais enfileira
    queue_->Close();  // a entrega termina de esvaziar a fila e sai
    delivery_.join();

    Log(DI_LOG_INFO, "Motor nativo parado (eventos descartados: " + std::to_string(queue_->Dropped()) + ").");
    hooks_.reset();
    queue_.reset();
    started_ = false;
    return DI_OK;
}

di_result CoreEngine::SetInputHooks(bool enabled) {
    std::lock_guard lock(mutex_);
    if (!started_) {
        return DI_ERR_NOT_STARTED;
    }
    return hooks_->SetInputHooks(enabled);
}

void CoreEngine::Deliver() {
    while (auto event = queue_->Pop()) {
        try {
            on_event_(&*event, event_user_data_);
        } catch (...) {
            // Uma falha no chamador não pode derrubar a entrega dos próximos eventos.
        }
    }
}

void CoreEngine::Log(di_log_level level, const std::string& message) {
    if (on_log_ == nullptr) {
        return;
    }
    try {
        on_log_(level, message.c_str(), log_user_data_);
    } catch (...) {
    }
}

}  // namespace dynaisland
