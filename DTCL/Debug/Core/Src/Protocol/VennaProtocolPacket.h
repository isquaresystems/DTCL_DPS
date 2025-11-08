#pragma once
#include <cstdint>

#pragma pack(push, 1)
struct VennaCommandHeader {
    uint8_t command;       // VennaCommand
    uint8_t subCommand;    // VennaRxSubCommand (if applicable)
    uint16_t length;
    uint8_t* data;
};
#pragma pack(pop)
