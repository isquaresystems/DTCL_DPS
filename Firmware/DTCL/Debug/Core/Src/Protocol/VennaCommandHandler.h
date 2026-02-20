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
#include "VennaTransportInterface.h"

class VennaCommandHandler {
public:
    virtual void setTransport(VennaTransportInterface* iface) { transport = iface; }
    virtual bool match(uint8_t cmd) = 0;
    virtual void execute(uint8_t* data, uint32_t len) = 0;
    //virtual void tick() {}  // Optional background work
    virtual ~VennaCommandHandler() = default;

protected:
    VennaTransportInterface* transport = nullptr;
};
#endif // __cplusplus
