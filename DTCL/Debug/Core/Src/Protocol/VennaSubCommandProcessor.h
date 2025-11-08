#pragma once
#include "IVennaSubCommandHandler.h"
#include <map>

class VennaSubCommandProcessor {
public:
    void registerHandler(uint8_t subCmd, IVennaSubCommandHandler* handler);
    uint8_t processRxSubCommand(uint8_t subCmd, const uint8_t* data, uint32_t len);
    uint32_t prepareForRx(uint8_t subCmd, const uint8_t* data, uint32_t len);
    uint8_t prepareTxData(uint8_t subCmd, const uint8_t* data, uint32_t& outLen);

private:
    std::map<uint8_t, IVennaSubCommandHandler*> handlers;
};
