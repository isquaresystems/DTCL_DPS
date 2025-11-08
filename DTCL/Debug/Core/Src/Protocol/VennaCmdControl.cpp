#include "VennaCmdControl.h"
#include "VennaProtocolDefs.h"
#include "VennaFramingUtils.h"
#include "../Darin3Cart_Driver.h"
#include <cstring>

uint8_t GuiCtrlLed_SubCmdProcess::LED_CTRL_STATE = 0;

VennaCmdControl::VennaCmdControl() : processor(nullptr)
{

}

void VennaCmdControl::setSubProcessor(VennaSubCommandProcessor* proc) {
    processor = proc;

    //LED_CTRL_STATE =0x00; //firmware ctrl led
}

bool VennaCmdControl::match(uint8_t cmd) {
    return cmd == static_cast<uint8_t>(VennaCommand::CMD_REQ);
}

void VennaCmdControl::execute(uint8_t* data, uint32_t len)
{
	uint8_t type = data[0];

	auto it = subCmdHandlerList.find((VennaSubCommand)data[1]);
	if (it == subCmdHandlerList.end()) {
		// Log: Unhandled command
		return;
	}

	if (type == static_cast<uint8_t>(VennaCommand::CMD_REQ))
	{
		if (!transport) return;

		uint16_t length = it->second->processCmdReq(data);

		uint8_t framed[100];
		std::size_t frameLen = VennaFramingUtils::encodeFrame(txBuffer, length, framed, sizeof(framed));
		transport->transmit(framed, frameLen);

		/*VennaSubCommand subCmd = (VennaSubCommand)data[1];
		uint8_t packet[15];
		uint16_t len = 1;
		uint8_t framed[35];
		std::size_t frameLen;
		VennaBoardId boardId;
		uint8_t slot_No;
		uint8_t pin_state;

		    switch(subCmd)
		    {
		       case VennaSubCommand::BOARD_ID:
		    	   boardId = VennaBoardId::DPS_4_IN_1;
		    	   len = 1;
		    	   packet[0] = static_cast<uint8_t>(VennaResponse::CMD_RESP);
		    	   packet[1] = (uint8_t)subCmd;
		    	   packet[2] = len >> 8;
		    	   packet[3] = len & 0xFF;
		    	   packet[4] = static_cast<uint8_t>(boardId);

		    	   frameLen= VennaFramingUtils::encodeFrame(packet, 5, framed, sizeof(framed));
		    	   transport->transmit(framed, frameLen);
		       break;

		       case VennaSubCommand::CART_STATUS:
                   len=4;
                   UpdateD2SlotStatus();
		       	   packet[0] = static_cast<uint8_t>(VennaResponse::CMD_RESP);
		       	   packet[1] = (uint8_t)subCmd;
		       	   packet[2] = len >> 8;
		       	   packet[3] = len & 0xFF;
		       	   packet[4] = get_D2_slt_status(CARTRIDGE_1);
		           packet[5] = get_D2_slt_status(CARTRIDGE_2);
		       	   packet[6] = get_D2_slt_status(CARTRIDGE_3);
		       	   packet[7] = get_D2_slt_status(CARTRIDGE_4);
   		    	   frameLen = VennaFramingUtils::encodeFrame(packet, 8, framed, sizeof(framed));
   		    	   transport->transmit(framed, frameLen);
   		    	   break;

		       case VennaSubCommand::GUI_CTRL_LED:
		    	   len=0;
		    	   //LED_CTRL_STATE = data[4];
		    	   packet[0] = static_cast<uint8_t>(VennaResponse::CMD_RESP);
		    	   packet[1] = (uint8_t)subCmd;
		    	   packet[2] = len >> 8;
		    	   packet[3] = len & 0xFF;
		    	   frameLen = VennaFramingUtils::encodeFrame(packet, 4, framed, sizeof(framed));
		    	   transport->transmit(framed, frameLen);
		    	   break;

		       case VennaSubCommand::GREEN_LED:
		    	   len=0;
		    	   slot_No = data[4];
		    	   pin_state = data[5];
		    	   setGreenLed(CartridgeID(slot_No-1), pin_state);

		    	   packet[0] = static_cast<uint8_t>(VennaResponse::CMD_RESP);
		    	   packet[1] = (uint8_t)subCmd;
		    	   packet[2] = len >> 8;
		    	   packet[3] = len & 0xFF;
		    	   frameLen = VennaFramingUtils::encodeFrame(packet, 4, framed, sizeof(framed));
		    	   transport->transmit(framed, frameLen);
		    	   break;

		       case VennaSubCommand::RED_LED:
		    	   len=0;
		    	   slot_No = data[4];
		    	   pin_state = data[5];
		    	   setRedLed(CartridgeID(slot_No-1), pin_state);

		    	   packet[0] = static_cast<uint8_t>(VennaResponse::CMD_RESP);
		    	   packet[1] = (uint8_t)subCmd;
		    	   packet[2] = len >> 8;
		    	   packet[3] = len & 0xFF;
		    	   frameLen = VennaFramingUtils::encodeFrame(packet, 4, framed, sizeof(framed));
		    	   transport->transmit(framed, frameLen);
		    	   break;

		       case VennaSubCommand::FIRMWARE_VERSION:
		    	   len=0;
		    	   uint8_t data[3] = {0x31, 0x2E, 0x30};
		    	   //frameLen = EnocdeCmdRes((uint8_t)VennaSubCommand::FIRMWARE_VERSION, &data[0]);
		    	   //frameLen = VennaFramingUtils::encodeFrame(packet, 7, framed, sizeof(framed));
		    	   //transport->transmit(framed, frameLen);
		    	   break;
		    }*/
	}
}

void VennaCmdControl::registerSubCmdHandlers(std::unique_ptr<IVennaSubCommandHandler> handler)
{
	VennaSubCommand cmd = handler->getSubCmd();
	subCmdHandlerList[cmd] = std::move(handler);
}




