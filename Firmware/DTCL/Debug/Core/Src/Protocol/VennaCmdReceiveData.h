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
#include "VennaSubCommandProcessor.h"
#include "VennaProtocolDefs.h"
#include <stdint.h>

class VennaCmdReceiveData : public VennaCommandHandler {
public:
    VennaCmdReceiveData();
    void setSubProcessor(VennaSubCommandProcessor* proc);
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
    VennaReturnCodes retCode;

    VennaSubCommandProcessor* processor;


    void sendAck(uint16_t seq, VennaReturnCodes retCode);
    void sendNack(uint16_t seq, VennaReturnCodes retCode);
    void sendDoneAck(VennaReturnCodes retCode);
    void handleStartCommand(const uint8_t* data, uint32_t len);
    void handleDataChunk(const uint8_t* data, uint32_t len);
    void sendRXAck(uint8_t subcmd);
    void sendRXNack(uint8_t subcmd, VennaReturnCodes code);
};

#endif // __cplusplus
