#pragma once
#include "VennaCommandHandler.h"
#include "VennaCmdReceiveData.h"
#include "VennaCmdTransmitData.h"
#include "VennaSubCommandProcessor.h"
#include "../Darin3Cart_Driver.h"
#include <memory>
#include <map>

enum class VennaHostState : uint8_t {
    IDLE         = 0x00,
    RECEIVING    = 0x01,
    TRANSMITTING = 0x02,
};

class VennaCmdControl : public VennaCommandHandler {
public:
    VennaCmdControl();

    void setSubProcessor(VennaSubCommandProcessor* proc);
    bool match(uint8_t cmd) override;
    void execute(uint8_t* data, uint32_t len) override;
    uint8_t get_LedState();
    void registerSubCmdHandlers(std::unique_ptr<IVennaSubCommandHandler> handler);


private:
    VennaSubCommandProcessor* processor;
    std::map<VennaSubCommand, std::unique_ptr<IVennaSubCommandHandler>> subCmdHandlerList;
};

class FirmwareVersion_SubCmdProcess : public IVennaSubCommandHandler {
public:
	FirmwareVersion_SubCmdProcess(){};

	virtual uint16_t processCmdReq(uint8_t* reqData) override
	{
		DecodeCmdReq(reqData);
		uint8_t data[3] = {0x31, 0x2E, 0x30};
		uint16_t len = EnocdeCmdRes((uint8_t)VennaSubCommand::FIRMWARE_VERSION, &data[0] ,3);
		return len;
	}
	virtual VennaSubCommand getSubCmd()override
	{
		return VennaSubCommand::FIRMWARE_VERSION;
	}

private:
};

class BoardID_SubCmdProcess : public IVennaSubCommandHandler {
public:
	BoardID_SubCmdProcess(){};

	virtual uint16_t processCmdReq(uint8_t* reqData) override
	{
		DecodeCmdReq(reqData);
		uint8_t packet[10];
		packet[0] = (uint8_t)VennaBoardId::DPS_4_IN_1;
		uint16_t len = EnocdeCmdRes((uint8_t)VennaSubCommand::CART_STATUS, packet ,1);

		return len;
	};
	virtual VennaSubCommand getSubCmd() override
	{
		return VennaSubCommand::BOARD_ID;
	};

private:
};

class CartStatus_SubCmdProcess : public IVennaSubCommandHandler {
public:
	CartStatus_SubCmdProcess(){};

	virtual uint16_t processCmdReq(uint8_t* reqData) override
	{
		DecodeCmdReq(reqData);
        uint8_t packet[10];
		UpdateD3SlotStatus();
		packet[0] = get_D3_slt_status(CARTRIDGE_1);
		packet[1] = get_D3_slt_status(CARTRIDGE_2);
		packet[2] = get_D3_slt_status(CARTRIDGE_3);
		packet[3] = get_D3_slt_status(CARTRIDGE_4);

		uint16_t len = EnocdeCmdRes((uint8_t)VennaSubCommand::CART_STATUS, packet ,4);

		return len;
	};
	virtual VennaSubCommand getSubCmd() override
	{
		return VennaSubCommand::CART_STATUS;
	};

private:
};

class GuiCtrlLed_SubCmdProcess : public IVennaSubCommandHandler {
public:
	GuiCtrlLed_SubCmdProcess(){};

	static uint8_t LED_CTRL_STATE;

	virtual uint16_t processCmdReq(uint8_t* reqData) override
	{
		DecodeCmdReq(reqData);
		LED_CTRL_STATE = rxBuffer[0];
		uint16_t len = EnocdeCmdRes((uint8_t)VennaSubCommand::GUI_CTRL_LED, nullptr ,0);

		return len;
	};
	virtual VennaSubCommand getSubCmd() override
	{
		return VennaSubCommand::GUI_CTRL_LED;
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

class GreenLed_SubCmdProcess : public IVennaSubCommandHandler {
public:
	GreenLed_SubCmdProcess(){};

	virtual uint16_t processCmdReq(uint8_t* reqData) override
	{
		DecodeCmdReq(reqData);
		uint8_t slot_No = rxBuffer[0];
		uint8_t pin_state = rxBuffer[1];
		setGreenLed(CartridgeID(slot_No-1), pin_state);

		uint16_t len = EnocdeCmdRes((uint8_t)VennaSubCommand::GREEN_LED, nullptr ,0);

		return len;
	};
	virtual VennaSubCommand getSubCmd() override
	{
		return VennaSubCommand::GREEN_LED;
	};

private:
};

class RedLed_SubCmdProcess : public IVennaSubCommandHandler {
public:
	RedLed_SubCmdProcess(){};

	virtual uint16_t processCmdReq(uint8_t* reqData) override
	{
		DecodeCmdReq(reqData);
		uint8_t slot_No = rxBuffer[0];
		uint8_t pin_state = rxBuffer[1];
		setRedLed(CartridgeID(slot_No-1), pin_state);

		uint16_t len = EnocdeCmdRes((uint8_t)VennaSubCommand::RED_LED, nullptr ,0);

		return len;
	};
	virtual VennaSubCommand getSubCmd() override
	{
		return VennaSubCommand::RED_LED;
	};

private:
};
