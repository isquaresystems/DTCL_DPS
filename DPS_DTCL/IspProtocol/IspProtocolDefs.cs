namespace IspProtocol
{
    public enum IspCommand : byte
    {
        COMMAND_REQUEST = 0X52,
        RX_DATA_RESET = 0x53,
        TX_DATA_RESET = 0x54,
        RX_DATA = 0x55,
        TX_DATA = 0x56,
    }

    // Subcommands
    public enum IspSubCommand : byte
    {
        D2_WRITE = 0x00,
        D2_READ = 0x01,
        D2_ERASE = 0x02,
        BOARD_ID = 0x03,
        CART_STATUS = 0x04,
        GREEN_LED = 0x05,
        RED_LED = 0x06,
        GUI_CTRL_LED = 0x07,
        FIRM_CTRL_LED = 0x08,
        D2_ERASE_BLOCK = 0x09,
        FIRMWARE_VERSION = 0x0A,
        D3_ERASE = 0x0B,
        D3_WRITE = 0x0C,
        D3_READ = 0x0D,
        D3_FORMAT = 0x0E,
        D3_READ_FILES = 0x0F,
        SLOT_LED_BLINK = 0x10,
        BLINK_ALL_LED = 0x11,
        LOOPBACK_TEST = 0x12,
        D3_POWER_CYCLE = 0x13
    }

    public enum IspSubCmdRespLen : byte
    {
        D2_WRITE = 8,
        D2_READ = 8,
        D2_ERASE = 8,
        BOARD_ID = 8 + 1,
        CART_STATUS = 8 + 4,
        GREEN_LED = 8,
        RED_LED = 8,
        SLOT_LED_BLINK = 8,
        BLINK_ALL_LED = 8,
        LOOPBACK_TEST = 8 + 1,
        GUI_CTRL_LED = 8,
        FIRM_CTRL_LED = 8,
        D2_ERASE_BLOCK = 8,
        FIRMWARE_VERSION = 8 + 3,
        D3_ERASE = 8 + 1,
        D3_WRITE = 8 + 1,
        D3_READ = 8 + 90,
        D3_FORMAT = 8 + 1,
        D3_POWER_CYCLE = 8 + 1
    }

    public enum IspResponse : byte
    {
        COMMAND_RESPONSE = 0XA0,
        ACK = 0xA1,
        NACK = 0xA2,
        ACK_DONE = 0xA3,
        RX_MODE_ACK = 0xA4,
        RX_MODE_NACK = 0xA5,
        TX_MODE_ACK = 0xA6,
        TX_MODE_NACK = 0xA7,
    }

    // Acknowledgement response types
    public enum IspReturnCodes : byte
    {
        SUBCMD_SUCESS = 0xB0,
        SUBCMD_FAILED = 0xB1,
        SUBCMD_SEQMISMATCH = 0xB2,
        SUBCMD_SEQMATCH = 0xB3,
        SUBCMD_NOTHANDLED = 0xB4,
        BUFFER_OVERFLOW = 0xB5
    }

    public enum IspSubCmdResponse : byte
    {
        IN_PROGRESS = 0xC1,
        TX_FAILED = 0xC2,
        SPURIOUS_RESPONSE = 0xC3,  // Firmware sent wrong response type - full operation retry needed
        NO_RESPONSE = 0xFF,
        SUCESS = 0x00,
        FAILED = 0x01
    }

    public enum IspBoardId : byte
    {
        DPS2_4_IN_1 = 0xF1,
        DPS3_4_IN_1 = 0xF2,
        DTCL = 0xF3,
        UNKNOWN_BOARD_ID = 0xFF
    }
}