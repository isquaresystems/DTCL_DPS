#ifdef __cplusplus
extern "C" {
#endif

// C-compatible section (leave empty if needed)

#ifdef __cplusplus
}
#endif

#ifdef __cplusplus

#pragma once
#include "VennaCommandHandler.h"
#include "VennaProtocolDefs.h"
#include <vector>

class VennaCommandManager {
public:
    void addHandler(VennaCommandHandler* handler);
    void handleData(uint8_t* data, uint32_t len);
    void tick();
    VennaBoardId getBoardID();
    void setBoardID(VennaBoardId id);

private:
    std::vector<VennaCommandHandler*> handlers;
    VennaBoardId boardId;
};

#endif // __cplusplus
