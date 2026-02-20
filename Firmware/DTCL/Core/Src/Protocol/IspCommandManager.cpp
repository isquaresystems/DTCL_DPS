#include "IspCommandManager.h"
#include "IspFramingUtils.h"
#include "IspProtocolDefs.h"

IspCommandManager::IspCommandManager() : handlerCount(0), boardId(IspBoardId::DPS3_4_IN_1) {
    // Initialize handler array
    for (uint8_t i = 0; i < MAX_COMMAND_HANDLERS; i++) {
        handlers[i] = nullptr;
    }
}

void IspCommandManager::addHandler(IspCommandHandler* handler) {
    if (handlerCount < MAX_COMMAND_HANDLERS) {
        handlers[handlerCount] = handler;
        handlerCount++;
    }
}

void IspCommandManager::handleData(uint8_t* payload, uint32_t payloadLen) {
    uint8_t cmd = payload[0];
    for (uint8_t i = 0; i < handlerCount; i++) {
        if (handlers[i] && handlers[i]->match(cmd)) {
            handlers[i]->execute(payload, payloadLen);  // <-- decoded payload
            break;
        }
    }
}

void IspCommandManager::setBoardID(IspBoardId id)
{
    boardId = id;
}

IspBoardId IspCommandManager::getBoardID()
{
    return boardId;
}

void IspCommandManager::tick() {
    for (uint8_t i = 0; i < handlerCount; i++) {
        if (handlers[i]) {
            // handler->tick(); // Uncomment if needed
        }
    }
}
