#include "VennaRingBuffer.h"

VennaRingBuffer::VennaRingBuffer() : head(0), tail(0) {}

bool VennaRingBuffer::put(uint8_t byte) {
    uint16_t next = (head + 1) % Size;
    if (next == tail) return false; // full
    buffer[head] = byte;
    head = next;
    return true;
}

bool VennaRingBuffer::get(uint8_t &byte) {
    if (head == tail) return false; // empty
    byte = buffer[tail];
    tail = (tail + 1) % Size;
    return true;
}

bool VennaRingBuffer::available() const {
    return head != tail;
}

void VennaRingBuffer::reset() {
    head = tail = 0;
}