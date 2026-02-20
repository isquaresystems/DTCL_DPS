// safe_buffer_access.h
#pragma once

#include <cstdint>
#include <cstddef>

constexpr uint32_t MAX_BUF_SIZE = 1023;

// Buffer sizes  
constexpr uint32_t TX_BUFFER_SIZE = MAX_BUF_SIZE;
constexpr uint32_t RX_BUFFER_SIZE = MAX_BUF_SIZE;

// Global buffers (defined elsewhere)
extern uint8_t txBuffer[TX_BUFFER_SIZE];
extern uint8_t rxBuffer[RX_BUFFER_SIZE];

// Functions to safely access buffers
bool SafeWriteToTxBuffer(const uint8_t* data, uint32_t offset, uint32_t size);
bool SafeWriteToRxBuffer(const uint8_t* data, uint32_t offset, uint32_t size);

bool SafeReadFromTxBuffer(uint8_t* outData, uint32_t offset, uint32_t size);
bool SafeReadFromRxBuffer(uint8_t* outData, uint32_t offset, uint32_t size);

// Helper functions to get buffer sizes
constexpr uint32_t GetTxBufferSize();
constexpr uint32_t GetRxBufferSize();

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
