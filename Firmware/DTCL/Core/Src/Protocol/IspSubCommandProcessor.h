#pragma once
#include "IIspSubCommandHandler.h"

// Fixed-size handler registry for embedded systems
constexpr uint8_t MAX_HANDLERS = 16;

struct HandlerEntry {
    uint8_t subCmd;
    IIspSubCommandHandler* handler;
    bool valid;
    
    HandlerEntry() : subCmd(0), handler(nullptr), valid(false) {}
};

class IspSubCommandProcessor {
public:
    IspSubCommandProcessor();
    void registerHandler(uint8_t subCmd, IIspSubCommandHandler* handler);
    uint8_t processRxSubCommand(uint8_t subCmd, const uint8_t* data, uint32_t len);
    uint32_t prepareForRx(uint8_t subCmd, const uint8_t* data, uint32_t len);
    uint8_t prepareTxData(uint8_t subCmd, const uint8_t* data, uint32_t& outLen);

private:
    IIspSubCommandHandler* findHandler(uint8_t subCmd);
    HandlerEntry handlers[MAX_HANDLERS];
    uint8_t handlerCount;
};
