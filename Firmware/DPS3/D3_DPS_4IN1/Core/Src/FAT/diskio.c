#if 1
#include <stdio.h>
#include "diskio.h"
#include "main.h"
#include "stm32f4xx_hal.h"
#include "stm32f4xx_ll_gpio.h"
#include "stm32f4xx_ll_bus.h"
#include "ff.h"
#include <stdlib.h>
#include <string.h>
#include "../Darin3Cart_Driver.h"

// ===== DIRECT CF IMPLEMENTATION - USE EXISTING SYMBOLS FROM WORKING DRIVER =====

// External references to symbols from working driver
extern uint16_t CF_WE;
extern uint16_t CF_OE;
extern uint16_t CF_RST;
extern uint16_t A0, A1, A2, A3;
extern uint16_t M_D0, M_D1, M_D2, M_D3, M_D4, M_D5, M_D6, M_D7;
extern uint16_t CF_CE[4];

// CF register addresses (copied from working driver)
#define data_reg       0x00
#define feature        0x01
#define sector_count   0x02
#define sector_num     0x03
#define cyc_low        0x04
#define cyc_high       0x05
#define drive          0x06
#define command        0x07
#define status_reg     0x07

// FatFs disk control constants
#define CTRL_SYNC       0
#define GET_SECTOR_COUNT    1
#define GET_SECTOR_SIZE     2
#define GET_BLOCK_SIZE      3

CartridgeID m_CartId = CARTRIDGE_1;  // Current cartridge
static int disk_initialized = 0;  // Track if disk is initialized
static CartridgeID last_initialized_cart = (CartridgeID)-1;  // Track last initialized cart

// ===== EXTERNAL FUNCTION DECLARATIONS - USE WORKING DRIVER FUNCTIONS =====

// External function declarations from working driver
extern uint16_t get_CE_pin(CartridgeID id);
extern void short_delay_us(uint32_t us);
extern void write_address_port(uint8_t data);
extern void DataBus_SetOutput(void);
extern void DataBus_SetInput(void);
extern void DataBus_Configure(DataBusDirection direction);
extern void DataBus_WriteByte(uint8_t data);
extern uint8_t DataBus_ReadByte(void);

// Helper function to replace HAL_GPIO_WritePin (keep local)
static inline void GPIO_WritePin(GPIO_TypeDef *GPIOx, uint16_t GPIO_Pin, uint8_t PinState)
{
    if (PinState)
    {
        LL_GPIO_SetOutputPin(GPIOx, GPIO_Pin);
    }
    else
    {
        LL_GPIO_ResetOutputPin(GPIOx, GPIO_Pin);
    }
}

// ===== FATFS INTERFACE FUNCTIONS =====

// FatFs time function
DWORD get_fattime(void)
{
  return(0x00000000);
}

// Disk initialization using direct CF logic
DSTATUS disk_initialize(BYTE drv)
{
    if(drv != 0) return RES_PARERR;  // Only support drive 0

    CartridgeID id = m_CartId;

    // CF initialization: CE-only selection, no RST assertion (see comment below)
    DataBus_Configure(DIR_INPUT);
    write_address_port(0);
    GPIO_WritePin(GPIOD, CF_WE, 1);        // Disable write signal
    GPIO_WritePin(GPIOB, CF_OE, 1);        // Disable output enable
    
    // CRITICAL: Do NOT assert RESET# (CF_RST, PD13).
    // CF_RST is shared across all 4 card slots. Every previous attempt to assert RST
    // inside disk_initialize caused the odd-slot failure pattern (slots 1 & 3 always
    // fail when all 4 cards are inserted). Bus isolation during the POR window did not
    // help — the RST signal itself is the source of the interference, whether due to
    // polarity inversion in hardware leaving the target card in reset during the BSY
    // poll, or because non-target cards drive the shared data bus in response to RST
    // regardless of CE# state.
    //
    // Cards complete hardware POR automatically at power-on — no software RST needed
    // for normal slot switching. For intentional hardware reset use D3_Power_Cycle_SubCmdProcess
    // (controls the power rail, isolated per-slot, not shared RST).

    // Deassert ALL chip selects, wait for all cards to tri-state, then assert target only.
    for(int cart = 0; cart < 4; cart++) {
        GPIO_WritePin(GPIOD, get_CE_pin((CartridgeID)cart), 1);
    }
    blocking_delay_ms(5);    // Bus settle: all non-target cards fully tri-state outputs
    GPIO_WritePin(GPIOD, get_CE_pin(id), 0);  // Assert target CE only
    short_delay_us(100);     // CE setup time before register access

    // Poll BSY (bit 7) and RDY (bit 6): up to 500ms.
    // After normal operations the card is immediately ready (exits on first poll).
    // After D3_Power_Cycle_SubCmdProcess, the handler already waits 500ms before
    // calling mount, so BSY is clear before disk_initialize is reached.
    // blocking_delay_ms is DWT-based — safe in USB CDC ISR (no SysTick dependency).
    DataBus_Configure(DIR_INPUT);
    int cf_ready = 0;
    for (int ms = 0; ms < 500; ms++) {
        write_address_port(status_reg);
        short_delay_us(10);
        GPIO_WritePin(GPIOB, CF_OE, 0);
        short_delay_us(2);
        uint8_t st = DataBus_ReadByte();
        GPIO_WritePin(GPIOB, CF_OE, 1);

        if (!(st & 0x80) && (st & 0x40)) {   // BSY=0, RDY=1 → card ready
            cf_ready = 1;
            break;
        }
        blocking_delay_ms(1);
    }

    if (cf_ready)
    {
        disk_initialized = 1;
        last_initialized_cart = id;
        return RES_OK;
    }
    else
    {
        disk_initialized = 0;
        return STA_NOINIT;
    }
}

// Disk status - perform actual CF status checking
DSTATUS disk_status(BYTE drv)
{
    if(drv != 0) return RES_PARERR;
    
    // If disk not initialized or cartridge changed, return not ready
    if (!disk_initialized || last_initialized_cart != m_CartId) {
        return STA_NOINIT;  // Force initialization
    }

    // Assert CE for the active cartridge before touching the bus.
    // CE should already be asserted after disk_initialize / disk_read / disk_write,
    // but guard here in case any raw driver call deasserted it.
    GPIO_WritePin(GPIOD, get_CE_pin(m_CartId), 0);
    short_delay_us(5);

    // Check CF status register
    DataBus_Configure(DIR_INPUT);
    write_address_port(status_reg);
    short_delay_us(10);
    GPIO_WritePin(GPIOB, CF_OE, 0);
    uint8_t current_status = DataBus_ReadByte();
    GPIO_WritePin(GPIOB, CF_OE, 1);

    // CF ready if status bit 6 is set and bit 7 is clear
    // Bit 7 = BSY (busy), Bit 6 = RDY (ready), Bit 3 = DRQ (data request)
    if((current_status & 0x40) && !(current_status & 0x80))
    {
        return RES_OK;  // CF is ready
    }
    else
    {
        return RES_NOTRDY;  // CF not ready
    }
}

// Disk read using exact logic from ComprehensiveTest512
DRESULT disk_read(BYTE drv, BYTE* buff, DWORD sector, BYTE count)
{
    if(drv != 0) return RES_PARERR;  // Only support drive 0
    if(count == 0) return RES_PARERR;

    // For now, only support single sector reads
    if(count != 1) return RES_ERROR;

    // Assert CE for the active cartridge.
    // disk_read must not rely on CE being left over from disk_initialize — any
    // raw CF function (post_read_compact_flash / post_write_compact_flash) deasserts
    // CE, silently breaking subsequent FatFS reads without this guard.
    GPIO_WritePin(GPIOD, get_CE_pin(m_CartId), 0);
    short_delay_us(5);  // CE setup time before first register access

    // === READ OPERATION - EXACT COPY FROM ComprehensiveTest512 ===
    DataBus_Configure(DIR_OUTPUT);

    // Set up read command for specified sector
    write_address_port(sector_count);
    short_delay_us(5);
    DataBus_WriteByte(0x01);  // 1 sector
    short_delay_us(2);
    GPIO_WritePin(GPIOD, CF_WE, 0);
    short_delay_us(5);
    GPIO_WritePin(GPIOD, CF_WE, 1);
    short_delay_us(10);

    write_address_port(sector_num);
    short_delay_us(5);
    DataBus_WriteByte(sector & 0xFF);  // Sector number low byte
    short_delay_us(2);
    GPIO_WritePin(GPIOD, CF_WE, 0);
    short_delay_us(5);
    GPIO_WritePin(GPIOD, CF_WE, 1);
    short_delay_us(10);

    write_address_port(cyc_low);
    short_delay_us(5);
    DataBus_WriteByte((sector >> 8) & 0xFF);  // Cylinder low
    short_delay_us(2);
    GPIO_WritePin(GPIOD, CF_WE, 0);
    short_delay_us(5);
    GPIO_WritePin(GPIOD, CF_WE, 1);
    short_delay_us(10);

    write_address_port(cyc_high);
    short_delay_us(5);
    DataBus_WriteByte((sector >> 16) & 0xFF);  // Cylinder high
    short_delay_us(2);
    GPIO_WritePin(GPIOD, CF_WE, 0);
    short_delay_us(5);
    GPIO_WritePin(GPIOD, CF_WE, 1);
    short_delay_us(10);

    write_address_port(drive);
    short_delay_us(5);
    DataBus_WriteByte(0xE0 | ((sector >> 24) & 0x0F));  // Drive/head with LBA bits
    short_delay_us(2);
    GPIO_WritePin(GPIOD, CF_WE, 0);
    short_delay_us(5);
    GPIO_WritePin(GPIOD, CF_WE, 1);
    short_delay_us(10);

    write_address_port(command);
    short_delay_us(5);
    DataBus_WriteByte(0x20);  // Read sectors command
    short_delay_us(2);
    GPIO_WritePin(GPIOD, CF_WE, 0);
    short_delay_us(5);
    GPIO_WritePin(GPIOD, CF_WE, 1);
    short_delay_us(100);

    // Wait for read command to complete and data ready
    DataBus_Configure(DIR_INPUT);
    write_address_port(status_reg);
    short_delay_us(500);

    uint8_t read_status;
    // H4 fix: ~200ms timeout per sector (was 50000 × ~16.6µs ≈ 832ms).
    // Must fail fast enough that multiple stuck sectors stay well under the 20s ISP timeout.
    int timeout = 12000;
    do {
        GPIO_WritePin(GPIOB, CF_OE, 0);
        short_delay_us(2);
        read_status = DataBus_ReadByte();
        GPIO_WritePin(GPIOB, CF_OE, 1);
        short_delay_us(50);
        timeout--;
    } while (((read_status & 0x80) != 0 || (read_status & 0x08) == 0) && timeout > 0);

    if(timeout == 0) return RES_ERROR;  // Timeout

    // Read all 512 bytes
    write_address_port(data_reg);
    short_delay_us(10);

    for (int i = 0; i < 512; i++) {
        GPIO_WritePin(GPIOB, CF_OE, 0);
        short_delay_us(2);
        buff[i] = DataBus_ReadByte();
        GPIO_WritePin(GPIOB, CF_OE, 1);
        short_delay_us(2);
    }

    return RES_OK;
}

// Disk write using exact logic from ComprehensiveTest512
DRESULT disk_write(BYTE drv, const BYTE* buff, DWORD sector, BYTE count)
{
    if(drv != 0) return RES_PARERR;  // Only support drive 0
    if(count == 0) return RES_PARERR;

    // For now, only support single sector writes
    if(count != 1) return RES_ERROR;

    // Assert CE for the active cartridge — same reasoning as disk_read.
    GPIO_WritePin(GPIOD, get_CE_pin(m_CartId), 0);
    short_delay_us(5);  // CE setup time before first register access

    // === WRITE OPERATION - EXACT COPY FROM ComprehensiveTest512 ===
    DataBus_Configure(DIR_OUTPUT);

    // Set up write command for specified sector
    write_address_port(sector_count);
    short_delay_us(5);
    DataBus_WriteByte(0x01);  // 1 sector
    short_delay_us(2);
    GPIO_WritePin(GPIOD, CF_WE, 0);
    short_delay_us(5);
    GPIO_WritePin(GPIOD, CF_WE, 1);
    short_delay_us(10);

    write_address_port(sector_num);
    short_delay_us(5);
    DataBus_WriteByte(sector & 0xFF);  // Sector number low byte
    short_delay_us(2);
    GPIO_WritePin(GPIOD, CF_WE, 0);
    short_delay_us(5);
    GPIO_WritePin(GPIOD, CF_WE, 1);
    short_delay_us(10);

    write_address_port(cyc_low);
    short_delay_us(5);
    DataBus_WriteByte((sector >> 8) & 0xFF);  // Cylinder low
    short_delay_us(2);
    GPIO_WritePin(GPIOD, CF_WE, 0);
    short_delay_us(5);
    GPIO_WritePin(GPIOD, CF_WE, 1);
    short_delay_us(10);

    write_address_port(cyc_high);
    short_delay_us(5);
    DataBus_WriteByte((sector >> 16) & 0xFF);  // Cylinder high
    short_delay_us(2);
    GPIO_WritePin(GPIOD, CF_WE, 0);
    short_delay_us(5);
    GPIO_WritePin(GPIOD, CF_WE, 1);
    short_delay_us(10);

    write_address_port(drive);
    short_delay_us(5);
    DataBus_WriteByte(0xE0 | ((sector >> 24) & 0x0F));  // Drive/head with LBA bits
    short_delay_us(2);
    GPIO_WritePin(GPIOD, CF_WE, 0);
    short_delay_us(5);
    GPIO_WritePin(GPIOD, CF_WE, 1);
    short_delay_us(10);

    write_address_port(command);
    short_delay_us(5);
    DataBus_WriteByte(0x30);  // Write sectors command
    short_delay_us(2);
    GPIO_WritePin(GPIOD, CF_WE, 0);
    short_delay_us(5);
    GPIO_WritePin(GPIOD, CF_WE, 1);
    short_delay_us(100);  // Wait for command acceptance

    // Wait for CF to be ready for data (DRQ = 1)
    DataBus_Configure(DIR_INPUT);
    write_address_port(status_reg);
    short_delay_us(100);

    uint8_t write_status;
    // H4 fix: ~200ms DRQ timeout (was 50000 × ~16.6µs ≈ 832ms).
    int timeout = 12000;
    do {
        GPIO_WritePin(GPIOB, CF_OE, 0);
        short_delay_us(2);
        write_status = DataBus_ReadByte();
        GPIO_WritePin(GPIOB, CF_OE, 1);
        short_delay_us(50);
        timeout--;
    } while (((write_status & 0x80) != 0 || (write_status & 0x08) == 0) && timeout > 0);

    if(timeout == 0) return RES_ERROR;  // Timeout

    // Write all 512 bytes
    DataBus_Configure(DIR_OUTPUT);
    write_address_port(data_reg);
    short_delay_us(10);

    for (int i = 0; i < 512; i++) {
        DataBus_WriteByte(buff[i]);
        short_delay_us(1);
        GPIO_WritePin(GPIOD, CF_WE, 0);
        short_delay_us(2);
        GPIO_WritePin(GPIOD, CF_WE, 1);
        short_delay_us(1);
    }

    // Wait for write completion
    DataBus_Configure(DIR_INPUT);
    write_address_port(status_reg);
    short_delay_us(1000);

    uint8_t write_complete_status;
    // H4 fix: ~500ms write-complete timeout (was 100000 × ~32.6µs ≈ 3.26s).
    timeout = 15000;
    do {
        GPIO_WritePin(GPIOB, CF_OE, 0);
        short_delay_us(2);
        write_complete_status = DataBus_ReadByte();
        GPIO_WritePin(GPIOB, CF_OE, 1);
        short_delay_us(100);
        timeout--;
    } while ((write_complete_status & 0x80) != 0 && timeout > 0);

    if(timeout == 0) return RES_ERROR;  // Timeout

    // C3 fix: after BSY clears, check ERR (bit 0) and DF/Device Fault (bit 5).
    // Previously RES_OK was returned unconditionally, silently swallowing write errors.
    if (write_complete_status & 0x21)   // 0x21 = bit5 (DF) | bit0 (ERR)
        return RES_ERROR;

    return RES_OK;
}

// Disk I/O control (minimal implementation)
DRESULT disk_ioctl(BYTE drv, BYTE cmd, DWORD* buff)
{
    if(drv != 0) return RES_PARERR;

    switch(cmd)
    {
        case CTRL_SYNC:
            return RES_OK;  // Always synchronized for direct CF access

        case GET_SECTOR_COUNT:
            // H3 fix: was hardcoded at 2,048,000 (1 GB) regardless of actual card.
            // Overshooting causes FatFS mkfs to place clusters beyond the card's real
            // end, corrupting the filesystem on cards smaller than 1 GB.
            // 262,144 sectors = 128 MB — safe minimum for all CF cards used in this
            // system. Replace with IDENTIFY DEVICE sector count if larger cards arrive.
            *buff = 262144;
            return RES_OK;

        case GET_SECTOR_SIZE:
            *buff = 512;
            return RES_OK;

        case GET_BLOCK_SIZE:
            *buff = 1;  // Single sector erase block
            return RES_OK;

        default:
            return RES_PARERR;
    }
}

void SetCartNo(CartridgeID id)
{
	if (m_CartId != id) {
		disk_initialized = 0;  // Force re-initialization when cart changes
		last_initialized_cart = (CartridgeID)-1;  // Clear last initialized cart
	}
	m_CartId = id;
}

// Force disk reinitialization - call this when switching cartridges
DSTATUS ForceCartridgeReinit(CartridgeID id)
{
    SetCartNo(id);
    disk_initialized = 0;
    last_initialized_cart = (CartridgeID)-1;
    
    // Force immediate reinitialization
    return disk_initialize(0);
}

#endif