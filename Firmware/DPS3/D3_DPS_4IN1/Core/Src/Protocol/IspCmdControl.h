#pragma once
#include "IspCommandHandler.h"
#include "IspCmdReceiveData.h"
#include "IspCmdTransmitData.h"
#include "IspSubCommandProcessor.h"
#include "../Darin3Cart_Driver.h"

enum class IspHostState : uint8_t {
    IDLE         = 0x00,
    RECEIVING    = 0x01,
    TRANSMITTING = 0x02,
};

// Fixed-size handler registry for embedded systems
constexpr uint8_t MAX_CONTROL_HANDLERS = 16;

struct ControlHandlerEntry {
    IspSubCommand subCmd;
    IIspSubCommandHandler* handler;
    bool valid;
    
    ControlHandlerEntry() : subCmd(static_cast<IspSubCommand>(0)), handler(nullptr), valid(false) {}
};

class IspCmdControl : public IspCommandHandler {
public:
    IspCmdControl();
    ~IspCmdControl();

    void setSubProcessor(IspSubCommandProcessor* proc);
    bool match(uint8_t cmd) override;
    void execute(uint8_t* data, uint32_t len) override;
    uint8_t get_LedState();
    void registerSubCmdHandlers(IIspSubCommandHandler* handler);

private:
    IIspSubCommandHandler* findHandler(IspSubCommand subCmd);
    IspSubCommandProcessor* processor;
    ControlHandlerEntry subCmdHandlerList[MAX_CONTROL_HANDLERS];
    uint8_t handlerCount;
};
