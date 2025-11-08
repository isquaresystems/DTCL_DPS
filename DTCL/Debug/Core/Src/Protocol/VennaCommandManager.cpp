#include "VennaCommandManager.h"
#include "VennaFramingUtils.h"
#include "VennaProtocolDefs.h"

void VennaCommandManager::addHandler(VennaCommandHandler* handler) {
    handlers.push_back(handler);
}

void VennaCommandManager::handleData(uint8_t* payload, uint32_t payloadLen) {

    uint8_t cmd = payload[0];
    for (auto* handler : handlers) {
        if (handler->match(cmd)) {
            handler->execute(payload, payloadLen);  // <-- decoded payload
            break;
        }
    }
}

void VennaCommandManager::setBoardID(VennaBoardId id)
{
	boardId = id;
}

VennaBoardId VennaCommandManager::getBoardID()
{
	return boardId;
}

void VennaCommandManager::tick() {
    //for (auto* handler : handlers) {
        //handler->tick();
    //}
}
