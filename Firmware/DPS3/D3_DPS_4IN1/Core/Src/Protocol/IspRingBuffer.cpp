#include "IspRingBuffer.h"

IspRingBuffer::IspRingBuffer() : head(0), tail(0) {}

bool IspRingBuffer::put(uint8_t byte) {
    uint16_t next = (head + 1) % Size;
    if (next == tail) return false; // full
    buffer[head] = byte;
    head = next;
    return true;
}

bool IspRingBuffer::get(uint8_t &byte) {
    if (head == tail) return false; // empty
    byte = buffer[tail];
    tail = (tail + 1) % Size;
    return true;
}

bool IspRingBuffer::available() const {
    return head != tail;
}

void IspRingBuffer::reset() {
    head = tail = 0;
}