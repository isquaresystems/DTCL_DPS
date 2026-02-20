#include "IspCmdTransmitData.h"
#include "IspProtocolDefs.h"
#include "IspFramingUtils.h"
#include "safeBuffer.h"
#include <cstring>

IspCmdTransmitData::IspCmdTransmitData() : processor(nullptr) {
    txSize = sentSize = currentSeq = subCommand = 0;
    currentState = State::IDLE;
    // Logger removed
}

void IspCmdTransmitData::setSubProcessor(IspSubCommandProcessor* proc) {
    processor = proc;
}

void IspCmdTransmitData::setDataToSend(uint32_t size) {
    txSize = size;
    sentSize = 0;
    currentSeq = 0;
    currentState = State::IDLE;
    // Logger removed
}

void IspCmdTransmitData::startTransmission() {
    if (txBuffer && txSize > 0 && transport) {
        currentState = State::WAIT_ACK;
        // Logger removed
        sendNextPacket(currentSeq);
    }
}

bool IspCmdTransmitData::match(uint8_t cmd) {
    return cmd == static_cast<uint8_t>(IspCommand::TX_DATA) ||
    		cmd == static_cast<uint8_t>(IspCommand::TX_DATA_RESET) ||
           cmd == static_cast<uint8_t>(IspResponse::ACK) ||
           cmd == static_cast<uint8_t>(IspResponse::ACK_DONE) ||
           cmd == static_cast<uint8_t>(IspResponse::RX_MODE_ACK) ||
           cmd == static_cast<uint8_t>(IspResponse::TX_MODE_ACK) ||
           cmd == static_cast<uint8_t>(IspResponse::NACK);
}

void IspCmdTransmitData::execute(uint8_t* data, uint32_t len) {
    if (len == 0 || !data) return;

    uint8_t type = data[0];

    switch (type) {
        case static_cast<uint8_t>(IspCommand::TX_DATA_RESET):
            handleTxReset();
            break;

        case static_cast<uint8_t>(IspCommand::TX_DATA):
            handleTxDataCommand(data, len);
            break;

        case static_cast<uint8_t>(IspResponse::ACK):
            handleAck(data, len);
            break;

        case static_cast<uint8_t>(IspResponse::NACK):
            handleNack(data, len);
            break;

        case static_cast<uint8_t>(IspResponse::ACK_DONE):
            handleAckDone();
            break;

        default:
            //// Logger removed
            break;
    }
}

void IspCmdTransmitData::handleTxReset() {
    reset();
    // Logger removed
}

void IspCmdTransmitData::handleTxDataCommand(uint8_t* data, uint32_t len) {
    subCommand = data[1];
    // Logger removed

    if (!processor) {
        sendTXNack(subCommand, 1);
        reset();
        return;
    }

    uint32_t totalLen = (data[2] << 24) | (data[3] << 16) | (data[4] << 8) | data[5];

    // Ask Darin3 to prepare for transmission and load first chunk
    uint32_t outLen = 0;
    uint8_t status = processor->prepareTxData(subCommand, &data[6], outLen);

    if (status != 0 || outLen == 0) {
        sendTXNack(subCommand, status);
        reset();
        return;
    }

    // Use the host's requested totalLen, not just the first chunk size
    // Device will stream the entire file using MAX_BUF_SIZE buffer chunks
    setDataToSend(totalLen);
    startTransmission();
}

void IspCmdTransmitData::handleAck(uint8_t* data, uint32_t len) {
   // if (currentState != State::WAIT_ACK || len < 3) return;

    uint16_t ackedSeq = (data[1] << 8) | data[2];

    // Check if this is the ACK we're waiting for
    if (ackedSeq != currentSeq) {
        // Check if this is an old ACK (ackedSeq < currentSeq)
        if (ackedSeq < currentSeq) {
            // GUI is behind - resend the next packet it's expecting
            uint16_t missingSeq = ackedSeq + 1;
            resendPacketForSequence(missingSeq);
            return;
        }
        // ackedSeq > currentSeq - shouldn't happen, ignore
        return;
    }

    // This is the expected ACK - update sentSize using actual sent packet size
    sentSize += lastSentPacketSize;
    currentSeq++;

    // Check if we've finished sending everything
    if (sentSize >= txSize) {
        currentState = State::IDLE;
        return;  // Transfer complete
    }

    // Send next packet
    sendNextPacket(currentSeq);
}

void IspCmdTransmitData::handleNack(uint8_t* data, uint32_t len) {
    //if (currentState != State::WAIT_ACK || len < 4) return;

    uint16_t seq = (data[1] << 8) | data[2];
    IspReturnCodes code = static_cast<IspReturnCodes>(data[3]);

    if (code == IspReturnCodes::SUBCMD_SEQMISMATCH) {
        // Sequence mismatch - resend the requested packet
        sendNextPacket(seq);
    } else if (code == IspReturnCodes::BUFFER_OVERFLOW) {
        // Buffer overflow - wait and retry current packet
        sendNextPacket(currentSeq);
    } else {
        // Other errors - abort transmission
        reset();
        currentState = State::IDLE;
    }
}

void IspCmdTransmitData::handleAckDone() {
    // Logger removed
    // ACK_DONE means current chunk complete, wait for next TX_DATA command
    // Don't reset yet - the transfer may continue
    currentState = State::IDLE;
}

void IspCmdTransmitData::sendTXNack(uint8_t subcmd, uint8_t code) {
    uint8_t nack[3] = {
        static_cast<uint8_t>(IspResponse::TX_MODE_NACK),
        subcmd,
        static_cast<uint8_t>(code)
    };

    if (transport) {
        uint8_t framed[30];
        std::size_t frameLen = IspFramingUtils::encodeFrame(nack, 3, framed, sizeof(framed));
        transport->transmit(framed, frameLen);
    }
}

void IspCmdTransmitData::sendNextPacket(uint16_t seq) {
    if (!transport || !processor) return;

    // Calculate buffer position within current chunk
    uint32_t bufferPos = sentSize % MAX_BUF_SIZE;

    // Check if we need to refill buffer from Darin3 (crossed chunk boundary)
    if (bufferPos == 0 && sentSize > 0 && sentSize < txSize) {
        // Buffer is empty and we've already sent data, ask Darin3 for next chunk
        uint32_t outLen = 0;
        uint8_t status = processor->prepareTxData(subCommand, nullptr, outLen);
        if (status != 0 || outLen == 0) {
            // No more data or error - send zero-length packet to signal end
            uint8_t packet[4];
            packet[0] = static_cast<uint8_t>(IspCommand::RX_DATA);
            packet[1] = seq >> 8;
            packet[2] = seq & 0xFF;
            packet[3] = 0; // Zero length

            uint8_t framed[20];
            std::size_t frameLen = IspFramingUtils::encodeFrame(packet, 4, framed, sizeof(framed));
            transport->transmit(framed, frameLen);

            currentState = State::IDLE;
            return;
        }
    }

    // Calculate chunk size for this packet
    uint32_t remainingBytes = txSize - sentSize;

    // If no remaining bytes, we're done
    if (remainingBytes == 0) {
        return;
    }

    uint8_t chunkSize = (remainingBytes >= 56) ? 56 : remainingBytes;

    // Ensure we don't read past buffer boundary
    if (bufferPos + chunkSize > MAX_BUF_SIZE) {
        chunkSize = MAX_BUF_SIZE - bufferPos;
    }

    // Store packet info for potential retransmission
    lastSentSeq = seq;
    lastSentPacketSize = chunkSize;
    memcpy(lastSentPacket, &txBuffer[bufferPos], chunkSize);

    uint8_t packet[60];
    packet[0] = static_cast<uint8_t>(IspCommand::RX_DATA);
    packet[1] = seq >> 8;
    packet[2] = seq & 0xFF;
    packet[3] = chunkSize;

    memcpy(&packet[4], &txBuffer[bufferPos], chunkSize);

    uint8_t framed[100];
    std::size_t frameLen = IspFramingUtils::encodeFrame(packet, chunkSize + 4, framed, sizeof(framed));
    transport->transmit(framed, frameLen);

    // Don't update sentSize here - wait for ACK confirmation
    // The completion check will happen in handleAck() after successful ACK
}

void IspCmdTransmitData::resendPacketForSequence(uint16_t seq) {
    if (!transport) return;

    // Calculate the position in the buffer for this sequence
    uint32_t position = seq * 56;
    if (position >= txSize) return;

    uint32_t bufferPos = position % MAX_BUF_SIZE;
    uint8_t chunkSize = (txSize - position >= 56) ? 56 : (txSize - position);

    // Ensure we don't read past buffer boundary
    if (bufferPos + chunkSize > MAX_BUF_SIZE) {
        chunkSize = MAX_BUF_SIZE - bufferPos;
    }

    uint8_t packet[60];
    packet[0] = static_cast<uint8_t>(IspCommand::RX_DATA);
    packet[1] = seq >> 8;
    packet[2] = seq & 0xFF;
    packet[3] = chunkSize;

    memcpy(&packet[4], &txBuffer[bufferPos], chunkSize);

    uint8_t framed[100];
    std::size_t frameLen = IspFramingUtils::encodeFrame(packet, chunkSize + 4, framed, sizeof(framed));
    transport->transmit(framed, frameLen);
}

void IspCmdTransmitData::reset() {
    txSize = 0;
    sentSize = 0;
    currentSeq = 0;
    subCommand = 0;
    currentState = State::IDLE;
    lastSentSeq = 0;
    lastSentPacketSize = 0;

    // Logger removed
}

