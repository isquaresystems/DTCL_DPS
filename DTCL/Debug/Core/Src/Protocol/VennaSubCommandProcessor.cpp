#include "VennaSubCommandProcessor.h"
#include <cstdio>

void VennaSubCommandProcessor::registerHandler(uint8_t subCmd, IVennaSubCommandHandler* handler) {
    handlers[subCmd] = handler;
}

uint32_t VennaSubCommandProcessor::prepareForRx(uint8_t subCmd, const uint8_t* data, uint32_t len) {
    auto it = handlers.find(subCmd);
    if (it != handlers.end()) {
    	return it->second->prepareForRx(data, subCmd, len);
    } else {
        printf("[RX] No handler for sub command 0x%02X\n", subCmd);
    }
    return 1024; //default
}

uint8_t VennaSubCommandProcessor::processRxSubCommand(uint8_t subCmd, const uint8_t* data, uint32_t len) {
    auto it = handlers.find(subCmd);
    if (it != handlers.end()) {
        it->second->processRxData(data,subCmd, len);
    } else {
        printf("[RX] No handler for sub command 0x%02X\n", subCmd);
    }
}

uint8_t VennaSubCommandProcessor::prepareTxData(uint8_t subCmd, const uint8_t* data, uint32_t& outLen) {
    auto it = handlers.find(subCmd);
    if (it != handlers.end()) {
        return it->second->prepareDataToTx(data,subCmd, outLen);
    }
    printf("[TX] No handler for sub command 0x%02X\n", subCmd);
    outLen = 0;
    return 1;
}
