#pragma once
#include <cstdint>

#pragma pack(push, 1)
struct IspCommandHeader {
    uint8_t command;       // IspCommand
    uint8_t subCommand;    // IspRxSubCommand (if applicable)
    uint16_t length;
    uint8_t* data;
};
#pragma pack(pop)
