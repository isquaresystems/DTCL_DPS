#pragma once
#include "Protocol/IIspSubCommandHandler.h"
#include "Darin2Cart_Driver.h"
#include "stm32f4xx_hal.h"
#include "main.h"
#include <stdint.h>

class Darin2 : public IIspSubCommandHandler {
public:
	Darin2();
	uint32_t prepareForRx(const uint8_t* data, const uint8_t subcmd,uint32_t len) override;
	uint8_t processRxData(const uint8_t* data, const uint8_t subcmd, uint32_t len) override;
    uint8_t prepareDataToTx(const uint8_t* data, const uint8_t subcmd, uint32_t& outLen) override;
    void TestDarinIIFlash(int startPage, int endPage);

private:
    uint32_t storedLength = 0;
    #define Output 1
    #define Input  0

    int m_Address_Flash_Page;
    int m_NumBlocks;
    int m_Last_Block_size;
    CartridgeID m_cartID;

};
