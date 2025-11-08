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
#include <cstdint>

class IspCmdTransmitData : public IspCommandHandler {
public:
    IspCmdTransmitData();
    void setSubProcessor(IspSubCommandProcessor* proc);
    bool match(uint8_t cmd) override;
    void execute(uint8_t* data, uint32_t len) override;
   // void tick() override;

    void setDataToSend(uint32_t size);
    void startTransmission();  // <-- NEW

    bool isTransmitting() const { return currentState == State::WAIT_ACK; }
    void reset();  // new: sets state to IDLE, clears pointers
    void sendTXNack(uint8_t subcmd, uint8_t code);

    void handleTxReset();
    void handleTxDataCommand(uint8_t* data, uint32_t len);
    void handleAck(uint8_t* data, uint32_t len);
    void handleNack(uint8_t* data, uint32_t len);
    void handleAckDone();


private:
    enum class State { IDLE, WAIT_ACK };
    State currentState;

    uint32_t txSize;
    uint32_t sentSize;
    uint16_t currentSeq;
    uint32_t lastSendTime;
    uint8_t subCommand;
    IspSubCommandProcessor* processor;
    
    // Retransmission support
    uint16_t lastSentSeq;
    uint8_t lastSentPacketSize;
    uint8_t lastSentPacket[60];  // Store last packet for retransmission

    void sendNextPacket(uint16_t seq);
    void resendPacketForSequence(uint16_t seq);
    void handleAckOrNack(const uint8_t* data, uint32_t len);
    static constexpr uint32_t TIMEOUT_MS = 2000;
};

#endif
