#include "VennaCmdReceiveData.h"
#include "VennaCommandManager.h"
#include "VennaProtocolPacket.h"
#include "VennaFramingUtils.h"
#include "safeBuffer.h"
#include "Logger.h"
#include <cstring>
#include <cstdlib>

VennaCmdReceiveData::VennaCmdReceiveData() : processor(nullptr) {
    reset();
}

void VennaCmdReceiveData::setSubProcessor(VennaSubCommandProcessor* proc) {
    processor = proc;
}

bool VennaCmdReceiveData::match(uint8_t cmd) {
    return cmd == static_cast<uint8_t>(VennaCommand::RX_DATA);
}

void VennaCmdReceiveData::execute(uint8_t* data, uint32_t len) {
    if (currentState == State::IDLE && len >= sizeof(VennaCommandHeader))
    {
        handleStartCommand(data, len);
    } else if (currentState == State::RECEIVING && len >= 2) {
        handleDataChunk(data, len);
    }
    else if (data[0] == static_cast<uint8_t>(VennaCommand::RX_DATA_RESET))
    {
        reset();
    }
}

void VennaCmdReceiveData::handleStartCommand(const uint8_t* data, uint32_t len)
{
    subCommand = data[1];
    //totalSize = (data[2] << 8) | data[3];
    totalSize = (data[2] << 24) | (data[3] << 16) | (data[4] << 8) | (data[5]);

    uint32_t res=0;

    SimpleLogger::getInstance().log(SimpleLogger::LOG_INFO,
        "[RX] Start command received");

    if (processor)
    {
        res = processor->prepareForRx(subCommand, &data[6], len - 6);
    }
    else
    {
        SimpleLogger::getInstance().log(SimpleLogger::LOG_ERROR,
            "[ERR] No subprocessor. Sending RX_NACK");
        sendRXNack(subCommand, VennaReturnCodes::SUBCMD_NOTHANDLED);
        reset();
        return;
    }

    /*if (totalSize > 24 * 1024)
    {
        SimpleLogger::getInstance().log(SimpleLogger::LOG_ERROR,
            "[ERR] Buffer overflow or zero-length");
        sendRXNack(subCommand, VennaReturnCodes::BUFFER_OVERFLOW);
        reset();
        return;
    }*/
    if(totalSize == 0)
    {
    	currentState = State::IDLE;
    	SimpleLogger::getInstance().log(SimpleLogger::LOG_INFO,
    	            "[RX] Nothing to receive");
    	if (!res)
    	 sendDoneAck(VennaReturnCodes::SUBCMD_SUCESS);
    	else
    		sendDoneAck(VennaReturnCodes::SUBCMD_FAILED);
    	reset();
    }
    else
    {
    	currentState = State::RECEIVING;
    	receivedSize = 0;
    	expectedSeq  = 0;

    	SimpleLogger::getInstance().log(SimpleLogger::LOG_INFO,
    	        "[RX] RX_ACK sent. Receiving started");
    	sendRXAck(subCommand);
    }
}

void VennaCmdReceiveData::handleDataChunk(const uint8_t* data, uint32_t len)
{
    if (len < 2) return;

    uint16_t seq = (data[1] << 8) | data[2];
    uint8_t dataLen = data[3];

    if (len < 4 + dataLen)
    {
        SimpleLogger::getInstance().log(SimpleLogger::LOG_ERROR,
            "[ERR] Invalid data chunk length");
        return;
    }

    if (seq == expectedSeq && (receivedSize + dataLen) <= totalSize)
    {
        if (!SafeWriteToRxBuffer(&data[4], receivedSize, dataLen))
        {
            SimpleLogger::getInstance().log(SimpleLogger::LOG_ERROR,
                "[ERR] SafeWrite failed");
            sendNack(expectedSeq, VennaReturnCodes::BUFFER_OVERFLOW);
            return;
        }

        receivedSize += dataLen;

        if (receivedSize >= 22400 && totalSize > 22400)
        {
        	// process exactly 22 400
        	processor->processRxSubCommand(subCommand, rxBuffer, 22400);
        	sendAck(seq, VennaReturnCodes::SUBCMD_SEQMATCH);
        	expectedSeq++;

        	// shift leftover down
        	size_t leftover = receivedSize - 22400;
        	memmove(rxBuffer, rxBuffer + 22400, leftover);
        	receivedSize = leftover;
        	totalSize  -= 22400;
        }
        else if (receivedSize >= totalSize)
        {
            if (processor)
            {
                uint8_t res = processor->processRxSubCommand(subCommand, &rxBuffer[0], totalSize);
                if(!res)
                  sendDoneAck(VennaReturnCodes::SUBCMD_SUCESS);
                else
                  sendDoneAck(VennaReturnCodes::SUBCMD_FAILED);
            }
            else
            	sendDoneAck(VennaReturnCodes::SUBCMD_NOTHANDLED);
            //SimpleLogger::getInstance().log(SimpleLogger::LOG_INFO,
             //   "[RX] Full data received. Sending ACK_DONE.");

            reset();
        }
        else
        {
            sendAck(seq, VennaReturnCodes::SUBCMD_SEQMATCH);
            expectedSeq++;
            SimpleLogger::getInstance().log(SimpleLogger::LOG_DEBUG,
                "[SEQ] Acked chunk");
        }
    }
    else
    {
        SimpleLogger::getInstance().log(SimpleLogger::LOG_CRITICAL,
            "[SEQ] Seq mismatch or overflow. Sending NACK.");
        sendNack(seq, VennaReturnCodes::SUBCMD_SEQMISMATCH);
    }
    processor->processRxSubCommand(subCommand, &rxBuffer[0], 0);
}

void VennaCmdReceiveData::reset() {
    totalSize = receivedSize = expectedSeq = subCommand = 0;
    currentState = State::IDLE;

    SimpleLogger::getInstance().log(SimpleLogger::LOG_INFO,
        "[RX] Receiver reset to IDLE");
}

void VennaCmdReceiveData::sendAck(uint16_t seq, VennaReturnCodes retCode)
{
    uint8_t ack[4] = { static_cast<uint8_t>(VennaResponse::ACK), (seq >> 8), (seq & 0xFF), static_cast<uint8_t>(retCode) };

    if (transport)
    {
    	volatile uint8_t framed[20];
        std::size_t frameLen = VennaFramingUtils::encodeFrame(ack, 4, framed, sizeof(framed));
        transport->transmit(framed, frameLen);
    }
}

void VennaCmdReceiveData::sendNack(uint16_t seq, VennaReturnCodes retCode)
{
    uint8_t nack[4] = { static_cast<uint8_t>(VennaResponse::NACK), (seq >> 8), (seq & 0xFF), static_cast<uint8_t>(retCode) };

    if (transport)
    {
    	volatile uint8_t framed[30];
        std::size_t frameLen = VennaFramingUtils::encodeFrame(nack, 4, framed, sizeof(framed));
        transport->transmit(framed, frameLen);
    }
}

void VennaCmdReceiveData::sendDoneAck(VennaReturnCodes retCode)
{
    uint8_t done[4] = { static_cast<uint8_t>(VennaResponse::ACK_DONE), (expectedSeq >> 8), (expectedSeq & 0xFF), static_cast<uint8_t>(retCode) };

    if (transport)
    {
        volatile uint8_t framed[30];
        std::size_t frameLen = VennaFramingUtils::encodeFrame(done, 4, framed, sizeof(framed));
        transport->transmit(framed, frameLen);
    }
}

void VennaCmdReceiveData::sendRXAck(uint8_t subcmd)
{
    uint8_t ack[2] = { static_cast<uint8_t>(VennaResponse::RX_MODE_ACK), subcmd };

    if (transport)
    {
    	volatile uint8_t framed[20];
        std::size_t frameLen = VennaFramingUtils::encodeFrame(ack, 2, framed, sizeof(framed));
        transport->transmit(framed, frameLen);
    }
}

void VennaCmdReceiveData::sendRXNack(uint8_t subcmd, VennaReturnCodes code)
{
    uint8_t nack[3] = { static_cast<uint8_t>(VennaResponse::RX_MODE_NACK), subcmd, static_cast<uint8_t>(code) };

    if (transport)
    {
    	volatile uint8_t framed[30];
        std::size_t frameLen = VennaFramingUtils::encodeFrame(nack, 3, framed, sizeof(framed));
        transport->transmit(framed, frameLen);
    }
}
