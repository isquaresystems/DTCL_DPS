#include "IspCmdReceiveData.h"
#include "IspCommandManager.h"
#include "IspProtocolPacket.h"
#include "IspFramingUtils.h"
#include "safeBuffer.h"
#include <cstring>
#include <cstdlib>
// MAX_BUF_SIZE now defined in safeBuffer.h for unified buffer management

IspCmdReceiveData::IspCmdReceiveData() : processor(nullptr) {
    reset();
}

void IspCmdReceiveData::setSubProcessor(IspSubCommandProcessor* proc) {
    processor = proc;
}

bool IspCmdReceiveData::match(uint8_t cmd) {
    return cmd == static_cast<uint8_t>(IspCommand::RX_DATA) || cmd == static_cast<uint8_t>(IspCommand::RX_DATA_RESET);
}

void IspCmdReceiveData::execute(uint8_t* data, uint32_t len) {
    if (currentState == State::IDLE && len >= sizeof(IspCommandHeader))
    {
        handleStartCommand(data, len);
    } else if (currentState == State::RECEIVING && len >= 2) {
        handleDataChunk(data, len);
    }
    else if (data[0] == static_cast<uint8_t>(IspCommand::RX_DATA_RESET))
    {
        reset();
    }
}

void IspCmdReceiveData::handleStartCommand(const uint8_t* data, uint32_t len)
{
    subCommand = data[1];
    //totalSize = (data[2] << 8) | data[3];
    totalSize = (data[2] << 24) | (data[3] << 16) | (data[4] << 8) | (data[5]);

    uint32_t res=0;

    // Logger removed: [RX] Start command received

    if (processor)
    {
        res = processor->prepareForRx(subCommand, &data[6], totalSize);
    }
    else
    {
        // Logger removed
        sendRXNack(subCommand, IspReturnCodes::SUBCMD_NOTHANDLED);
        reset();
        return;
    }

    if(totalSize == 0)
    {
    	currentState = State::IDLE;
    	// Logger removed
    	if (!res)
    	 sendDoneAck(IspReturnCodes::SUBCMD_SUCESS);
    	else
    		sendDoneAck(IspReturnCodes::SUBCMD_FAILED);
    	reset();
    }
    else
    {
    	currentState = State::RECEIVING;
    	receivedSize = 0;
    	expectedSeq  = 0;

    	// Logger removed
    	sendRXAck(subCommand);
    }
}

void IspCmdReceiveData::handleDataChunk(const uint8_t* data, uint32_t len)
{
    if (len < 2) return;

    uint16_t seq = (data[1] << 8) | data[2];
    uint8_t dataLen = data[3];

    if (len < (uint16_t)(4 + dataLen))
    {
        // Invalid packet length
        return;
    }

    // Handle duplicate/retransmitted packet (already processed sequence)
    if (seq < expectedSeq) {
        // This is a retransmission of an already processed packet
        // Just re-send the ACK - don't process data again
        sendAck(seq, IspReturnCodes::SUBCMD_SEQMATCH);
        return;
    }
    
    if (seq == expectedSeq && (receivedSize + dataLen) <= totalSize)
    {
        // Check if adding this data would exceed buffer BEFORE writing
        if (receivedSize + dataLen > MAX_BUF_SIZE && totalSize > MAX_BUF_SIZE) {
            // Process accumulated data first to make room
            processor->processRxSubCommand(subCommand, rxBuffer, receivedSize);
            
            // Update totalSize BEFORE clearing receivedSize
            totalSize -= receivedSize;
            // Clear buffer for new data
            receivedSize = 0;
        }
        
        if (!SafeWriteToRxBuffer(&data[4], receivedSize, dataLen))
        {
            // Logger removed
            sendNack(expectedSeq, IspReturnCodes::BUFFER_OVERFLOW);
            return;
        }

        receivedSize += dataLen;

        if (receivedSize >= MAX_BUF_SIZE && totalSize > MAX_BUF_SIZE)
        {
        	// Process 4KB chunk and send to Darin3 for immediate handling
        	processor->processRxSubCommand(subCommand, rxBuffer, MAX_BUF_SIZE);
        	sendAck(seq, IspReturnCodes::SUBCMD_SEQMATCH);
        	expectedSeq++;

        	// Slide remaining data to beginning of buffer
        	size_t leftover = receivedSize - MAX_BUF_SIZE;
        	memmove(rxBuffer, rxBuffer + MAX_BUF_SIZE, leftover);
        	receivedSize = leftover;
        	totalSize  -= MAX_BUF_SIZE;
        	// State remains RECEIVING for next chunk
        }
        else if (receivedSize >= totalSize)
        {
            if (processor)
            {
                uint8_t res = processor->processRxSubCommand(subCommand, &rxBuffer[0], receivedSize);
                // Signal end of transfer with zero-length call
                processor->processRxSubCommand(subCommand, &rxBuffer[0], 0);
                if(!res)
                  sendDoneAck(IspReturnCodes::SUBCMD_SUCESS);
                else
                  sendDoneAck(IspReturnCodes::SUBCMD_FAILED);
            }
            else
            	sendDoneAck(IspReturnCodes::SUBCMD_NOTHANDLED);

            reset();
        }
        else
        {
            sendAck(seq, IspReturnCodes::SUBCMD_SEQMATCH);
            expectedSeq++;
            // Logger removed
        }
    }
    else
    {
        // Logger removed
        sendNack(seq, IspReturnCodes::SUBCMD_SEQMISMATCH);
    }

}

void IspCmdReceiveData::reset() {
    totalSize = receivedSize = expectedSeq = subCommand = 0;
    currentState = State::IDLE;

    // Logger removed
}

void IspCmdReceiveData::sendAck(uint16_t seq, IspReturnCodes retCode)
{
    uint8_t ack[4] = { static_cast<uint8_t>(IspResponse::ACK), (uint8_t)(seq >> 8), (uint8_t)(seq & 0xFF), static_cast<uint8_t>(retCode) };

    if (transport)
    {
    	volatile uint8_t framed[20];
        std::size_t frameLen = IspFramingUtils::encodeFrame(ack, 4, framed, sizeof(framed));
        transport->transmit(framed, frameLen);
    }
}

void IspCmdReceiveData::sendNack(uint16_t seq, IspReturnCodes retCode)
{
    uint8_t nack[4] = { static_cast<uint8_t>(IspResponse::NACK), (uint8_t)(seq >> 8), (uint8_t)(seq & 0xFF), static_cast<uint8_t>(retCode) };

    if (transport)
    {
    	volatile uint8_t framed[30];
        std::size_t frameLen = IspFramingUtils::encodeFrame(nack, 4, framed, sizeof(framed));
        transport->transmit(framed, frameLen);
    }
}

void IspCmdReceiveData::sendDoneAck(IspReturnCodes retCode)
{
    uint8_t done[4] = { static_cast<uint8_t>(IspResponse::ACK_DONE), (uint8_t)(expectedSeq >> 8), (uint8_t)(expectedSeq & 0xFF), static_cast<uint8_t>(retCode) };

    if (transport)
    {
        volatile uint8_t framed[30];
        std::size_t frameLen = IspFramingUtils::encodeFrame(done, 4, framed, sizeof(framed));
        transport->transmit(framed, frameLen);
    }
}

void IspCmdReceiveData::sendRXAck(uint8_t subcmd)
{
    uint8_t ack[2] = { static_cast<uint8_t>(IspResponse::RX_MODE_ACK), subcmd };

    if (transport)
    {
    	volatile uint8_t framed[20];
        std::size_t frameLen = IspFramingUtils::encodeFrame(ack, 2, framed, sizeof(framed));
        transport->transmit(framed, frameLen);
    }
}

void IspCmdReceiveData::sendRXNack(uint8_t subcmd, IspReturnCodes code)
{
    uint8_t nack[3] = { static_cast<uint8_t>(IspResponse::RX_MODE_NACK), subcmd, static_cast<uint8_t>(code) };

    if (transport)
    {
    	volatile uint8_t framed[30];
        std::size_t frameLen = IspFramingUtils::encodeFrame(nack, 3, framed, sizeof(framed));
        transport->transmit(framed, frameLen);
    }
}
