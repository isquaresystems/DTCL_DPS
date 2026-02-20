#pragma once
#include <stdint.h>
#include <stddef.h>
#include <cstddef>

enum class IspTransportStatus {
    OK,
    ERROR,
    TIMEOUT,
    DISCONNECTED
};

class IspTransportInterface {
public:
    // Core transmission
    virtual bool transmit(volatile const uint8_t* data, std::size_t len) = 0;

    // Optional reception (for polling or mocks)
    virtual std::size_t receive(uint8_t* buffer, std::size_t maxLen) { return 0; }

    // Optional status query
    virtual IspTransportStatus status() const { return IspTransportStatus::OK; }

    // Optional buffer flush (e.g., UART or USB cleanup)
    virtual void flush() {}

    // Optional transport ID
    virtual const char* name() const { return "GenericTransport"; }

    virtual ~IspTransportInterface() = default;
};
