// Testes do motor nativo, sem biblioteca externa: cada Check falha o processo com o número da linha.
// Não geram nenhum clique nem tecla; só instalam e removem os ganchos.
#include <atomic>
#include <chrono>
#include <cstdio>
#include <cstdlib>
#include <string>
#include <thread>

#include "EventQueue.h"
#include "Interop/dynaisland_engine.h"

#define Check(condition)                                                                       \
    do {                                                                                       \
        if (!(condition)) {                                                                    \
            std::fprintf(stderr, "FALHOU %s:%d: %s\n", __FILE__, __LINE__, #condition);       \
            std::exit(1);                                                                      \
        }                                                                                      \
    } while (0)

namespace {

std::atomic<int> g_events{0};
std::atomic<int> g_info_logs{0};

void __stdcall OnEvent(const di_event* event, void*) {
    if (event != nullptr && event->size == sizeof(di_event)) {
        ++g_events;
    }
}

void __stdcall OnLog(int32_t level, const char* message, void*) {
    if (level == DI_LOG_INFO && message != nullptr && std::string(message).find("iniciado") != std::string::npos) {
        ++g_info_logs;
    }
}

di_event MakeEvent(int32_t x) {
    di_event event{};
    event.size = sizeof(di_event);
    event.type = DI_EVENT_MOUSE_DOWN;
    event.x = x;
    return event;
}

void ApiVersionMatchesHeader() { Check(di_api_version() == DI_API_VERSION); }

void StartRejectsBadArguments() {
    Check(di_start(DI_API_VERSION, nullptr, nullptr, nullptr, nullptr) == DI_ERR_INVALID_ARGUMENT);
    Check(di_start(DI_API_VERSION + 1, OnEvent, nullptr, nullptr, nullptr) == DI_ERR_VERSION_MISMATCH);
}

void CallsBeforeStartAreSafe() {
    Check(di_set_input_hooks(1) == DI_ERR_NOT_STARTED);
    Check(di_stop() == DI_OK);  // idempotente
}

void LifecycleStartsAndStopsCleanly() {
    Check(di_start(DI_API_VERSION, OnEvent, nullptr, OnLog, nullptr) == DI_OK);
    Check(di_start(DI_API_VERSION, OnEvent, nullptr, nullptr, nullptr) == DI_ERR_ALREADY_STARTED);

    Check(di_set_input_hooks(1) == DI_OK);
    Check(di_set_input_hooks(1) == DI_OK);  // já ligados
    Check(di_set_input_hooks(0) == DI_OK);
    Check(di_set_input_hooks(0) == DI_OK);  // já desligados

    Check(di_stop() == DI_OK);
    Check(di_stop() == DI_OK);
    Check(g_info_logs.load() >= 1);
}

void EngineCanRestartAfterStop() {
    for (int i = 0; i < 3; ++i) {
        Check(di_start(DI_API_VERSION, OnEvent, nullptr, nullptr, nullptr) == DI_OK);
        Check(di_set_input_hooks(1) == DI_OK);
        Check(di_stop() == DI_OK);
    }
}

void ResultMessagesAreNeverNull() {
    for (int code = DI_OK; code <= DI_ERR_INTERNAL; ++code) {
        Check(di_result_message(static_cast<di_result>(code)) != nullptr);
    }
    Check(di_result_message(static_cast<di_result>(999)) != nullptr);
}

void QueueKeepsOrderAndDropsWhenFull() {
    dynaisland::EventQueue queue(2);
    Check(queue.Push(MakeEvent(1)));
    Check(queue.Push(MakeEvent(2)));
    Check(!queue.Push(MakeEvent(3)));  // cheia: descarta o novo
    Check(queue.Dropped() == 1);

    Check(queue.Pop()->x == 1);
    Check(queue.Pop()->x == 2);
}

void QueueCloseDrainsThenEnds() {
    dynaisland::EventQueue queue(4);
    queue.Push(MakeEvent(7));
    queue.Close();
    Check(!queue.Push(MakeEvent(8)));  // encerrada
    Check(queue.Pop()->x == 7);        // o que já estava lá ainda sai
    Check(!queue.Pop().has_value());   // depois, fim
}

void QueueCloseWakesBlockedConsumer() {
    dynaisland::EventQueue queue(4);
    std::atomic<bool> finished{false};
    std::thread consumer([&] {
        Check(!queue.Pop().has_value());
        finished = true;
    });
    std::this_thread::sleep_for(std::chrono::milliseconds(50));
    Check(!finished.load());
    queue.Close();
    consumer.join();
    Check(finished.load());
}

}  // namespace

int main() {
    ApiVersionMatchesHeader();
    StartRejectsBadArguments();
    CallsBeforeStartAreSafe();
    LifecycleStartsAndStopsCleanly();
    EngineCanRestartAfterStop();
    ResultMessagesAreNeverNull();
    QueueKeepsOrderAndDropsWhenFull();
    QueueCloseDrainsThenEnds();
    QueueCloseWakesBlockedConsumer();
    std::puts("engine_tests: ok");
    return 0;
}
