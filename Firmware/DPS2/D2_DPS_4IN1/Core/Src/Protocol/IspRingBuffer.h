#pragma once
#include <cstdint>

class IspRingBuffer {
public:
    static constexpr uint16_t Size = 2048; // Adjust as needed

    IspRingBuffer();
    bool put(uint8_t byte);
    bool get(uint8_t &byte);
    bool available() const;
    void reset();

private:
    uint8_t buffer[Size];
    volatile uint16_t head;
    volatile uint16_t tail;
};