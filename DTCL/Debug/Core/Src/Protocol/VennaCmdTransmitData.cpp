#include "VennaCmdTransmitData.h"
#include "VennaProtocolDefs.h"
#include "VennaFramingUtils.h"
#include "safeBuffer.h"
#include "Logger.h"
#include <cstring>

VennaCmdTransmitData::VennaCmdTransmitData() : processor(nullptr) {
    txSize = sentSize = currentSeq = subCommand = 0;
    currentState = State::IDLE;

    SimpleLogger::getInstance().log(SimpleLogger::LOG_INFO, "[TX] Initialized transmit handler");
}

void VennaCmdTransmitData::setSubProcessor(VennaSubCommandProcessor* proc) {
    processor = proc;
}

void VennaCmdTransmitData::setDataToSend(uint32_t size) {
    txSize = size;
    sentSize = 0;
    currentSeq = 0;
    currentState = State::IDLE;

    SimpleLogger::getInstance().log(SimpleLogger::LOG_INFO, "[TX] Data set for transmission");
}

void VennaCmdTransmitData::startTransmission() {
    if (txBuffer && txSize > 0 && transport) {
        currentState = State::WAIT_ACK;
        SimpleLogger::getInstance().log(SimpleLogger::LOG_INFO, "[TX] Starting transmission");
        sendNextPacket(currentSeq);
    }
}

bool VennaCmdTransmitData::match(uint8_t cmd) {
    return cmd == static_cast<uint8_t>(VennaCommand::TX_DATA) ||
           cmd == static_cast<uint8_t>(VennaResponse::ACK) ||
           cmd == static_cast<uint8_t>(VennaResponse::ACK_DONE) ||
           cmd == static_cast<uint8_t>(VennaResponse::RX_MODE_ACK) ||
           cmd == static_cast<uint8_t>(VennaResponse::TX_MODE_ACK) ||
           cmd == static_cast<uint8_t>(VennaResponse::NACK);
}

void VennaCmdTransmitData::execute(uint8_t* data, uint32_t len) {
    uint8_t type = data[0];

    if (type == static_cast<uint8_t>(VennaCommand::TX_DATA_RESET))
    {
      reset();
    }

    if (type == static_cast<uint8_t>(VennaCommand::TX_DATA))
    {
        subCommand = data[1];
        SimpleLogger::getInstance().log(SimpleLogger::LOG_INFO, "[TX] TX_DATA received");

        if (processor)
        {
        	uint32_t outLen = 0;
        	uint32_t len = (data[2] << 24) | (data[3] << 16) | (data[4] << 8) | (data[5]);
        	uint8_t status = processor->prepareTxData(subCommand , &data[4],outLen);
        	if (status !=0)
        		return;

        	setDataToSend(outLen);
        	startTransmission();
        	len = len - outLen;

        	while (len > 0) {
        		status = processor->prepareTxData(subCommand , &data[4],outLen);
        		if (status !=0)
        			break;

        		txSize = outLen;
        		startTransmission();
        		len = len - outLen;
        	}
        }
    }
    else if (type == static_cast<uint8_t>(VennaResponse::ACK) && currentState == State::WAIT_ACK)
    {
    	uint16_t seq = (data[1] << 8) | data[2];
        if (seq == currentSeq) {
            sentSize += 56;
            currentSeq++;

            SimpleLogger::getInstance().log(SimpleLogger::LOG_DEBUG, "[TX] ACK received, sending next");

            if (sentSize < txSize) {
                sendNextPacket(currentSeq);
            } else {
                currentState = State::IDLE;
                SimpleLogger::getInstance().log(SimpleLogger::LOG_INFO, "[TX] Transmission complete");
            }
        }
    }
    else if (type == static_cast<uint8_t>(VennaResponse::NACK) && currentState == State::WAIT_ACK)
    {
    	uint16_t seq = (data[1] << 8) | data[2];
    	VennaReturnCodes code = static_cast<VennaReturnCodes>(data[3]);
        //SimpleLogger::getInstance().log(SimpleLogger::LOG_CRITICAL, "[TX] NACK received, resending");
    	if(code == VennaReturnCodes::SUBCMD_SEQMISMATCH)
           sendNextPacket(seq); // resend
    	else
    	{
    		reset();
    		currentState = State::IDLE;
    	}
    }
    else if (type == static_cast<uint8_t>(VennaResponse::ACK_DONE)) {
        SimpleLogger::getInstance().log(SimpleLogger::LOG_INFO, "[TX] ACK_DONE received, resetting");
        reset();
        currentState = State::IDLE;
    }
}

void VennaCmdTransmitData::sendNextPacket(uint16_t seq) {
    if (!transport) return;

    uint8_t packet[60];
    uint8_t chunkSize = (txSize - sentSize >= 56) ? 56 : (txSize - sentSize);

    packet[0] = static_cast<uint8_t>(VennaCommand::RX_DATA);
    packet[1] = seq >> 8;
    packet[2] = seq & 0xFF;
    packet[3] = chunkSize;

    memcpy(&packet[4], &txBuffer[sentSize], chunkSize);

    uint8_t framed[100];
    std::size_t frameLen = VennaFramingUtils::encodeFrame(packet, chunkSize + 4, framed, sizeof(framed));
    transport->transmit(framed, frameLen);

    SimpleLogger::getInstance().log(SimpleLogger::LOG_DEBUG, "[TX] Packet sent");
}

void VennaCmdTransmitData::reset() {
    txSize = 0;
    sentSize = 0;
    currentSeq = 0;
    subCommand = 0;
    currentState = State::IDLE;

    /*if (transport) {
        uint8_t ack = static_cast<uint8_t>(VennaResponse::ACK_DONE);
        uint8_t framed[30];
        std::size_t frameLen = VennaFramingUtils::encodeFrame(&ack, 1, framed, sizeof(framed));
        transport->transmit(framed, frameLen);
    }*/

    SimpleLogger::getInstance().log(SimpleLogger::LOG_INFO, "[TX] Transmission reset and ACK_DONE sent");
}

void VennaCmdTransmitData::handleRawData(uint8_t subCmd, uint8_t *data, uint16_t length)
{
    if (!transport) return;

    switch(subCmd)
    {
       case 0x04:
    	   VennaBoardId boardId = VennaBoardId::DPS_4_IN_1;
    	   uint8_t packet[60];
    	       uint8_t chunkSize = 56;
    	       uint16_t len = 1;

    	       packet[0] = static_cast<uint8_t>(VennaResponse::CMD_RESP);
    	       packet[1] = len >> 8;
    	       packet[2] = len & 0xFF;
    	       packet[3] = static_cast<uint8_t>(boardId);


    	       uint8_t framed[20];
    	       std::size_t frameLen = VennaFramingUtils::encodeFrame(packet, 4, framed, sizeof(framed));
    	       transport->transmit(framed, frameLen);

    	       SimpleLogger::getInstance().log(SimpleLogger::LOG_DEBUG, "[TX] Packet sent");

    	   break;

    }
}


