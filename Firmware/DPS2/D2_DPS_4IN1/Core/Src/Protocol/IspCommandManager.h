#ifdef __cplusplus
extern "C" {
#endif

// C-compatible section (leave empty if needed)

#ifdef __cplusplus
}
#endif

#ifdef __cplusplus

#pragma once
#include "IspCommandHandler.h"
#include "IspProtocolDefs.h"

// Fixed-size handler array for embedded systems
constexpr uint8_t MAX_COMMAND_HANDLERS = 8;

class IspCommandManager {
public:
    IspCommandManager();
    void addHandler(IspCommandHandler* handler);
    void handleData(uint8_t* data, uint32_t len);
    void tick();
    IspBoardId getBoardID();
    void setBoardID(IspBoardId id);

private:
    IspCommandHandler* handlers[MAX_COMMAND_HANDLERS];
    uint8_t handlerCount;
    IspBoardId boardId;
};

#endif // __cplusplus
