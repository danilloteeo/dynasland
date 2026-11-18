// Fila limitada de eventos entre a thread dos ganchos (que precisa responder em microssegundos) e a thread
// que entrega os eventos ao C#. Quem produz nunca bloqueia nem espera pelo consumidor.
#pragma once

#include <condition_variable>
#include <cstddef>
#include <deque>
#include <mutex>
#include <optional>

#include "Interop/dynaisland_engine.h"

namespace dynaisland {

class EventQueue {
public:
    explicit EventQueue(std::size_t capacity) : capacity_(capacity) {}

    /// Enfileira sem bloquear. Fila cheia ou encerrada: descarta o evento novo e devolve false.
    bool Push(const di_event& event) {
        {
            std::lock_guard lock(mutex_);
            if (closed_ || queue_.size() >= capacity_) {
                ++dropped_;
                return false;
            }
            queue_.push_back(event);
        }
        ready_.notify_one();
        return true;
    }

    /// Espera o próximo evento; devolve vazio quando a fila foi encerrada e esvaziada.
    std::optional<di_event> Pop() {
        std::unique_lock lock(mutex_);
        ready_.wait(lock, [this] { return closed_ || !queue_.empty(); });
        if (queue_.empty()) {
            return std::nullopt;
        }
        di_event event = queue_.front();
        queue_.pop_front();
        return event;
    }

    /// Acorda quem espera; eventos já enfileirados ainda são entregues, os novos são descartados.
    void Close() {
        {
            std::lock_guard lock(mutex_);
            closed_ = true;
        }
        ready_.notify_all();
    }

    /// Descartados por fila cheia ou encerrada, desde a criação.
    std::size_t Dropped() const {
        std::lock_guard lock(mutex_);
        return dropped_;
    }

private:
    const std::size_t capacity_;
    mutable std::mutex mutex_;
    std::condition_variable ready_;
    std::deque<di_event> queue_;
    std::size_t dropped_ = 0;
    bool closed_ = false;
};

}  // namespace dynaisland
