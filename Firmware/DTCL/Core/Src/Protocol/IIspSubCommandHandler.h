#pragma once
#include <cstdint>
#include "IspProtocolDefs.h"
#include "IspFramingUtils.h"
#include <cstring>
#include "safeBuffer.h"

class IIspSubCommandHandler {
public:
	virtual uint32_t prepareForRx(const uint8_t* data, const uint8_t subcmd,uint32_t len) {return 1024;};
    virtual uint8_t processRxData(const uint8_t* data, const uint8_t subcmd, uint32_t len) {return 0;};
    virtual uint8_t prepareDataToTx(const uint8_t* data,const uint8_t subcmd,uint32_t& outLen) {return 0;};

    virtual uint16_t DecodeCmdReq(uint8_t* data)
    {
    	uint16_t len = (data[2] << 8) | data[3];
    	if(len<=56)
    	{
    	   SafeWriteToRxBuffer(&data[4], 0, len);
    	   return len;
    	}
    	else return 0;
    };

    virtual uint16_t EnocdeCmdRes(uint8_t subCmd, uint8_t *data, uint16_t len)
    {
        txBuffer[0] = (uint8_t)IspResponse::CMD_RESP;
        txBuffer[1] = subCmd;
        txBuffer[2] = len >> 8;
        txBuffer[3] = len & 0xFF;
        if(len>0)
        SafeWriteToTxBuffer(data, 4, len);
        return len+4;
        //frameLen = IspFramingUtils::encodeFrame(payload, len+4, framedPayload, sizeof(framedPayload));
    	//return frameLen;
    };

    virtual IspSubCommand getSubCmd(){return IspSubCommand::BOARD_ID;};

    virtual uint16_t processCmdReq(uint8_t* data) {return 0;};


    virtual ~IIspSubCommandHandler() = default;

};
