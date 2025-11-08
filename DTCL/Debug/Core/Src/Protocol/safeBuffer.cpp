// safe_buffer_access.h
#pragma once

#include <cstdint>
#include <cstddef>
#include <cstring>
#include "safeBuffer.h"

// Functions to safely access buffers
bool SafeWriteToTxBuffer(const uint8_t* data, uint32_t offset, uint32_t size);
bool SafeWriteToRxBuffer(const uint8_t* data, uint32_t offset, uint32_t size);

bool SafeReadFromTxBuffer(uint8_t* outData, uint32_t offset, uint32_t size);
bool SafeReadFromRxBuffer(uint8_t* outData, uint32_t offset, uint32_t size);

// Helper functions to get buffer sizes
constexpr uint32_t GetTxBufferSize() { return TX_BUFFER_SIZE; }
constexpr uint32_t GetRxBufferSize() { return RX_BUFFER_SIZE; }

// safe_buffer_access.cpp
#include "safeBuffer.h"

uint8_t txBuffer[TX_BUFFER_SIZE] = {0};
uint8_t rxBuffer[RX_BUFFER_SIZE] = {0};

bool SafeWriteToTxBuffer(const uint8_t* data, uint32_t offset, uint32_t size) {
    if (offset + size > TX_BUFFER_SIZE) return false;
    std::memcpy(&txBuffer[offset], data, size);
    return true;
}

bool SafeWriteToRxBuffer(const uint8_t* data, uint32_t offset, uint32_t size) {
    if (offset + size > RX_BUFFER_SIZE) return false;
    std::memcpy(&rxBuffer[offset], data, size);
    return true;
}

bool SafeReadFromTxBuffer(uint8_t* outData, uint32_t offset, uint32_t size) {
    if (offset + size > TX_BUFFER_SIZE) return false;
    std::memcpy(outData, &txBuffer[offset], size);
    return true;
}

bool SafeReadFromRxBuffer(uint8_t* outData, uint32_t offset, uint32_t size) {
    if (offset + size > RX_BUFFER_SIZE) return false;
    std::memcpy(outData, &rxBuffer[offset], size);
    return true;
}

// Example Usage (in your code):
//
// #include "safe_buffer_access.h"
//
// uint8_t temp[100];
// if (SafeWriteToTxBuffer(temp, 0, sizeof(temp)))
// {
//     // Success
// }
// else
// {
//     // Handle error
// }
//
// uint8_t readBack[100];
// if (SafeReadFromRxBuffer(readBack, 0, sizeof(readBack)))
// {
//     // Data read successfully
// }
