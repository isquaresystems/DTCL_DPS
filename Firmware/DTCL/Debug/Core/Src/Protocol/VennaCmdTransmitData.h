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
#include <cstdint>

class VennaCmdTransmitData : public VennaCommandHandler {
public:
    VennaCmdTransmitData();
    void setSubProcessor(VennaSubCommandProcessor* proc);
    bool match(uint8_t cmd) override;
    void execute(uint8_t* data, uint32_t len) override;
   // void tick() override;

    void setDataToSend(uint32_t size);
    void startTransmission();  // <-- NEW
    void handleRawData(uint8_t subCmd, uint8_t *data, uint16_t length);

    bool isTransmitting() const { return currentState == State::WAIT_ACK; }
    void reset();  // new: sets state to IDLE, clears pointers


private:
    enum class State { IDLE, WAIT_ACK };
    State currentState;

    uint32_t txSize;
    uint32_t sentSize;
    uint16_t currentSeq;
    uint32_t lastSendTime;
    uint8_t subCommand;
    VennaSubCommandProcessor* processor;

    void sendNextPacket(uint16_t seq);
    void handleAckOrNack(const uint8_t* data, uint32_t len);
    static constexpr uint32_t TIMEOUT_MS = 2000;
};

#endif
