// Ciclo de vida do motor: junta a thread dos ganchos, a fila e a thread que entrega os eventos ao chamador.
#pragma once

#include <memory>
#include <mutex>
#include <string>
#include <thread>

#include "EventQueue.h"
#include "Interop/dynaisland_engine.h"
#include "WindowsAPI/HookThread.h"

namespace dynaisland {

class CoreEngine {
public:
    static CoreEngine& Instance();

    di_result Start(uint32_t expected_api_version, di_event_callback on_event, void* event_user_data,
                    di_log_callback on_log, void* log_user_data);
    di_result Stop();
    di_result SetInputHooks(bool enabled);

private:
    CoreEngine() = default;

    void Deliver();
    void Log(di_log_level level, const std::string& message);

    static constexpr std::size_t kQueueCapacity = 512;

    std::mutex mutex_;  // serializa Start/Stop/SetInputHooks
    bool started_ = false;

    di_event_callback on_event_ = nullptr;
    void* event_user_data_ = nullptr;
    di_log_callback on_log_ = nullptr;
    void* log_user_data_ = nullptr;

    std::unique_ptr<EventQueue> queue_;
    std::unique_ptr<HookThread> hooks_;
    std::thread delivery_;
};

}  // namespace dynaisland
