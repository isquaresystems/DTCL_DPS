#include "IspSubCommandProcessor.h"
#include <cstdio>

IspSubCommandProcessor::IspSubCommandProcessor() : handlerCount(0) {
    // Initialize array
    for (uint8_t i = 0; i < MAX_HANDLERS; i++) {
        handlers[i].valid = false;
    }
}

void IspSubCommandProcessor::registerHandler(uint8_t subCmd, IIspSubCommandHandler* handler) {
    if (handlerCount < MAX_HANDLERS) {
        handlers[handlerCount].subCmd = subCmd;
        handlers[handlerCount].handler = handler;
        handlers[handlerCount].valid = true;
        handlerCount++;
    }
}

IIspSubCommandHandler* IspSubCommandProcessor::findHandler(uint8_t subCmd) {
    for (uint8_t i = 0; i < handlerCount; i++) {
        if (handlers[i].valid && handlers[i].subCmd == subCmd) {
            return handlers[i].handler;
        }
    }
    return nullptr;
}

uint32_t IspSubCommandProcessor::prepareForRx(uint8_t subCmd, const uint8_t* data, uint32_t len) {
    IIspSubCommandHandler* handler = findHandler(subCmd);
    if (handler) {
        return handler->prepareForRx(data, subCmd, len);
    } else {
        printf("[RX] No handler for sub command 0x%02X\n", subCmd);
    }
    return 1024; //default
}

uint8_t IspSubCommandProcessor::processRxSubCommand(uint8_t subCmd, const uint8_t* data, uint32_t len) {
    IIspSubCommandHandler* handler = findHandler(subCmd);
    if (handler) {
        handler->processRxData(data, subCmd, len);
    } else {
        printf("[RX] No handler for sub command 0x%02X\n", subCmd);
    }
    return 0;
}

uint8_t IspSubCommandProcessor::prepareTxData(uint8_t subCmd, const uint8_t* data, uint32_t& outLen) {
    IIspSubCommandHandler* handler = findHandler(subCmd);
    if (handler) {
        return handler->prepareDataToTx(data, subCmd, outLen);
    }
    printf("[TX] No handler for sub command 0x%02X\n", subCmd);
    outLen = 0;
    return 1;
}
