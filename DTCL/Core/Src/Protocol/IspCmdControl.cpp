#include "IspCmdControl.h"
#include "IspProtocolDefs.h"
#include "IspFramingUtils.h"
#include "../Darin3Cart_Driver.h"
#include <cstring>

IspCmdControl::IspCmdControl() : processor(nullptr), handlerCount(0)
{
    // Initialize handler array
    for (uint8_t i = 0; i < MAX_CONTROL_HANDLERS; i++) {
        subCmdHandlerList[i].valid = false;
    }
}

IspCmdControl::~IspCmdControl()
{
    // Clean up - handlers are static objects, just clear our references
    for (uint8_t i = 0; i < handlerCount; i++) {
        subCmdHandlerList[i].valid = false;
        subCmdHandlerList[i].handler = nullptr;
    }
}

void IspCmdControl::setSubProcessor(IspSubCommandProcessor* proc) {
    processor = proc;
    //LED_CTRL_STATE =0x00; //firmware ctrl led
}

bool IspCmdControl::match(uint8_t cmd) {
    return cmd == static_cast<uint8_t>(IspCommand::CMD_REQ);
}

IIspSubCommandHandler* IspCmdControl::findHandler(IspSubCommand subCmd) {
    for (uint8_t i = 0; i < handlerCount; i++) {
        if (subCmdHandlerList[i].valid && subCmdHandlerList[i].subCmd == subCmd) {
            return subCmdHandlerList[i].handler;
        }
    }
    return nullptr;
}

void IspCmdControl::execute(uint8_t* data, uint32_t len)
{
    uint8_t type = data[0];
    IspSubCommand subCmd = static_cast<IspSubCommand>(data[1]);

    IIspSubCommandHandler* handler = findHandler(subCmd);
    if (!handler) {
        return;
    }

    if (type == static_cast<uint8_t>(IspCommand::CMD_REQ))
    {
        if (!transport) return;

        uint16_t length = handler->processCmdReq(data);

        uint8_t framed[100];
        std::size_t frameLen = IspFramingUtils::encodeFrame(txBuffer, length, framed, sizeof(framed));
        transport->transmit(framed, frameLen);
    }
}

void IspCmdControl::registerSubCmdHandlers(IIspSubCommandHandler* handler)
{
    if (handlerCount < MAX_CONTROL_HANDLERS && handler) {
        IspSubCommand cmd = handler->getSubCmd();
        subCmdHandlerList[handlerCount].subCmd = cmd;
        subCmdHandlerList[handlerCount].handler = handler;
        subCmdHandlerList[handlerCount].valid = true;
        handlerCount++;
    }
}




