#pragma once
#include "Protocol/IIspSubCommandHandler.h"
#include "Darin2Cart_Driver.h"
#include "version.h"
#include "stm32f4xx_hal.h"
#include "main.h"
#include <stdint.h>

class Darin2 : public IIspSubCommandHandler {
public:
	Darin2();
	uint32_t prepareForRx(const uint8_t* data, const uint8_t subcmd,uint32_t len) override;
	uint8_t processRxData(const uint8_t* data, const uint8_t subcmd, uint32_t len) override;
    uint8_t prepareDataToTx(const uint8_t* data, const uint8_t subcmd, uint32_t& outLen) override;
	bool TestDarinIIFlash(int startPage, int endPage, CartridgeID cartId);

private:
    static constexpr uint32_t SIZE = 10240;
    uint8_t flashData[SIZE];
    uint32_t storedLength = 0;
    #define Output 1
    #define Input  0

    int m_Address_Flash_Page;
    int m_NumBlocks;
    int m_Last_Block_size;
    CartridgeID m_cartID;
};

class FirmwareVersion_SubCmdProcess : public IIspSubCommandHandler {
public:
	FirmwareVersion_SubCmdProcess(){};

	virtual uint16_t processCmdReq(uint8_t* reqData) override
	{
		DecodeCmdReq(reqData);
		uint8_t data[3] = FIRMWARE_VERSION_ARRAY;
		uint16_t len = EnocdeCmdRes((uint8_t)IspSubCommand::FIRMWARE_VERSION, &data[0] ,3);
		return len;
	}
	virtual IspSubCommand getSubCmd()override
	{
		return IspSubCommand::FIRMWARE_VERSION;
	}

private:
};

class BoardID_SubCmdProcess : public IIspSubCommandHandler {
public:
	BoardID_SubCmdProcess(){};

	virtual uint16_t processCmdReq(uint8_t* reqData) override
	{
		DecodeCmdReq(reqData);
		uint8_t packet[10];
		packet[0] = (uint8_t)IspBoardId::DPS2_4_IN_1;
		uint16_t len = EnocdeCmdRes((uint8_t)IspSubCommand::BOARD_ID, packet ,1);

		return len;
	};
	virtual IspSubCommand getSubCmd() override
	{
		return IspSubCommand::BOARD_ID;
	};

private:
};

class CartStatus_SubCmdProcess : public IIspSubCommandHandler {
public:
	CartStatus_SubCmdProcess(){};

	virtual uint16_t processCmdReq(uint8_t* reqData) override
	{
		DecodeCmdReq(reqData);
        uint8_t packet[10];
		UpdateD2SlotStatus();
		packet[0] = get_D2_slt_status(CARTRIDGE_1);
		packet[1] = get_D2_slt_status(CARTRIDGE_2);
		packet[2] = get_D2_slt_status(CARTRIDGE_3);
		packet[3] = get_D2_slt_status(CARTRIDGE_4);

		uint16_t len = EnocdeCmdRes((uint8_t)IspSubCommand::CART_STATUS, packet ,4);

		return len;
	};
	virtual IspSubCommand getSubCmd() override
	{
		return IspSubCommand::CART_STATUS;
	};

private:
};

class GuiCtrlLed_SubCmdProcess : public IIspSubCommandHandler {
public:
	GuiCtrlLed_SubCmdProcess(){};

	static uint8_t LED_CTRL_STATE;

	virtual uint16_t processCmdReq(uint8_t* reqData) override
	{
		DecodeCmdReq(reqData);
		LED_CTRL_STATE = rxBuffer[0];
		uint16_t len = EnocdeCmdRes((uint8_t)IspSubCommand::GUI_CTRL_LED, nullptr ,0);

		return len;
	};
	virtual IspSubCommand getSubCmd() override
	{
		return IspSubCommand::GUI_CTRL_LED;
	};

	void set_LedState(uint8_t state)
	{
		 LED_CTRL_STATE = state;
	};

	uint8_t get_LedState()
	{
		return LED_CTRL_STATE;
	};

private:

};

class GreenLed_SubCmdProcess : public IIspSubCommandHandler {
public:
	GreenLed_SubCmdProcess(){};

	virtual uint16_t processCmdReq(uint8_t* reqData) override
	{
		DecodeCmdReq(reqData);
		uint8_t slot_No = rxBuffer[0];
		uint8_t pin_state = rxBuffer[1];
		setGreenLed(CartridgeID(slot_No-1), pin_state);

		uint16_t len = EnocdeCmdRes((uint8_t)IspSubCommand::GREEN_LED, nullptr ,0);

		return len;
	};
	virtual IspSubCommand getSubCmd() override
	{
		return IspSubCommand::GREEN_LED;
	};

private:
};

class RedLed_SubCmdProcess : public IIspSubCommandHandler {
public:
	RedLed_SubCmdProcess(){};

	virtual uint16_t processCmdReq(uint8_t* reqData) override
	{
		DecodeCmdReq(reqData);
		uint8_t slot_No = rxBuffer[0];
		uint8_t pin_state = rxBuffer[1];
		setRedLed(CartridgeID(slot_No-1), pin_state);

		uint16_t len = EnocdeCmdRes((uint8_t)IspSubCommand::RED_LED, nullptr ,0);

		return len;
	};
	virtual IspSubCommand getSubCmd() override
	{
		return IspSubCommand::RED_LED;
	};

private:
};

class SlotLedBlink_SubCmdProcess : public IIspSubCommandHandler {
public:
	SlotLedBlink_SubCmdProcess(){};

	virtual uint16_t processCmdReq(uint8_t* reqData) override
	{
		DecodeCmdReq(reqData);
		uint8_t slot_No = rxBuffer[0];
		uint8_t blink_itr = rxBuffer[1];
		slotLedBlink(CartridgeID(slot_No-1), blink_itr);

		uint16_t len = EnocdeCmdRes((uint8_t)IspSubCommand::SLOT_LED_BLINK, nullptr ,0);

		return len;
	};
	virtual IspSubCommand getSubCmd() override
	{
		return IspSubCommand::SLOT_LED_BLINK;
	};

private:
};

class BlinkAllLed_SubCmdProcess : public IIspSubCommandHandler {
public:
	BlinkAllLed_SubCmdProcess(){};

	virtual uint16_t processCmdReq(uint8_t* reqData) override
	{
		DecodeCmdReq(reqData);
		uint8_t blink_itr = rxBuffer[0];
		BlinkAllLed( blink_itr);

		uint16_t len = EnocdeCmdRes((uint8_t)IspSubCommand::BLINK_ALL_LED, nullptr ,0);

		return len;
	};
	virtual IspSubCommand getSubCmd() override
	{
		return IspSubCommand::BLINK_ALL_LED;
	};

private:
};

class LedLoopBack_SubCmdProcess : public IIspSubCommandHandler {
public:
	LedLoopBack_SubCmdProcess(){};

	virtual uint16_t processCmdReq(uint8_t* reqData) override
	{
		DecodeCmdReq(reqData);
		uint8_t packet[10];
		uint8_t blink_itr = rxBuffer[0];
		packet[0] = LedLoopBack(blink_itr);

		uint16_t len = EnocdeCmdRes((uint8_t)IspSubCommand::LOOPBACK_TEST, packet ,1);

		return len;
	};
	virtual IspSubCommand getSubCmd() override
	{
		return IspSubCommand::LOOPBACK_TEST;
	};

private:
};


