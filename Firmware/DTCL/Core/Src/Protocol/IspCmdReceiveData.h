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
#include "IspSubCommandProcessor.h"
#include "IspProtocolDefs.h"
#include <stdint.h>

class IspCmdReceiveData : public IspCommandHandler {
public:
    IspCmdReceiveData();
    void setSubProcessor(IspSubCommandProcessor* proc);
    bool match(uint8_t cmd) override;
    void execute(uint8_t* data, uint32_t len) override;

    bool isReceiving() const { return currentState == State::RECEIVING; }
    void reset();

private:
    enum class State { IDLE, RECEIVING };
    State currentState;

    uint32_t totalSize;
    uint32_t receivedSize;
    uint16_t expectedSeq;
    uint8_t subCommand;
    IspReturnCodes retCode;

    IspSubCommandProcessor* processor;


    void sendAck(uint16_t seq, IspReturnCodes retCode);
    void sendNack(uint16_t seq, IspReturnCodes retCode);
    void sendDoneAck(IspReturnCodes retCode);
    void handleStartCommand(const uint8_t* data, uint32_t len);
    void handleDataChunk(const uint8_t* data, uint32_t len);
    void sendRXAck(uint8_t subcmd);
    void sendRXNack(uint8_t subcmd, IspReturnCodes code);
};

#endif // __cplusplus
