#ifdef __cplusplus
extern "C" {
#endif

// C-compatible section (leave empty if needed)

#ifdef __cplusplus
}
#endif

#ifdef __cplusplus

#pragma once
#include <stdint.h>
#include "IspTransportInterface.h"

class IspCommandHandler {
public:
    virtual void setTransport(IspTransportInterface* iface) { transport = iface; }
    virtual bool match(uint8_t cmd) = 0;
    virtual void execute(uint8_t* data, uint32_t len) = 0;
    //virtual void tick() {}  // Optional background work
    virtual ~IspCommandHandler() = default;

protected:
    IspTransportInterface* transport = nullptr;
};
#endif // __cplusplus
