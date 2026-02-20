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

    // Enhanced CF initialization sequence with longer delays and multiple reset attempts
    DataBus_Configure(DIR_INPUT);
    write_address_port(0);
    GPIO_WritePin(GPIOD, CF_WE, 1);        // Disable write signal
    GPIO_WritePin(GPIOB, CF_OE, 1);        // Disable output enable
    
    // Disable all other cartridge chip selects first
    for(int cart = 0; cart < 4; cart++) {
        if(cart != id) {
            GPIO_WritePin(GPIOD, get_CE_pin((CartridgeID)cart), 1);
        }
    }
    
    GPIO_WritePin(GPIOD, get_CE_pin(id), 0); // Enable chip select for this cartridge
    short_delay_us(100);  // Let CE settle
    
    // Extended reset sequence for problematic CF cards
    GPIO_WritePin(GPIOD, CF_RST, 1);       // Enable reset
    short_delay_us(1000);  // Longer reset assertion
    GPIO_WritePin(GPIOD, CF_RST, 0);       // Disable reset
    short_delay_us(10000); // Much longer delay for CF card initialization (10ms)
    
    // Additional reset cycle if needed
    GPIO_WritePin(GPIOD, CF_RST, 1);       // Second reset
    short_delay_us(500);
    GPIO_WritePin(GPIOD, CF_RST, 0);       // Release reset
    short_delay_us(15000); // Even longer delay (15ms total)

    // Check initial status
    DataBus_Configure(DIR_INPUT);
    write_address_port(status_reg);
    short_delay_us(50);
    GPIO_WritePin(GPIOB, CF_OE, 0);
    short_delay_us(10);
    uint8_t initial_status = DataBus_ReadByte();
    GPIO_WritePin(GPIOB, CF_OE, 1);

    // Multiple status read attempts for flaky CF cards
    uint8_t status_attempts[3];
    for(int attempt = 0; attempt < 3; attempt++) {
        short_delay_us(100);
        write_address_port(status_reg);
        short_delay_us(10);
        GPIO_WritePin(GPIOB, CF_OE, 0);
        status_attempts[attempt] = DataBus_ReadByte();
        GPIO_WritePin(GPIOB, CF_OE, 1);
    }

    // CF ready if status bit 6 is set and bit 7 is clear
    // Also check if any of the retry attempts show a valid status
    int cf_ready = 0;
    cf_ready |= ((initial_status & 0x40) && !(initial_status & 0x80));
    cf_ready |= ((status_attempts[0] & 0x40) && !(status_attempts[0] & 0x80));
    cf_ready |= ((status_attempts[1] & 0x40) && !(status_attempts[1] & 0x80));
    cf_ready |= ((status_attempts[2] & 0x40) && !(status_attempts[2] & 0x80));

    // Alternative detection: if status reads are all zero, try a different approach
    if(!cf_ready && initial_status == 0 && status_attempts[0] == 0) {
        // Try sending an IDENTIFY command to wake up the CF card
        DataBus_Configure(DIR_OUTPUT);
        write_address_port(command);
        short_delay_us(10);
        DataBus_WriteByte(0xEC);  // IDENTIFY command
        short_delay_us(5);
        GPIO_WritePin(GPIOD, CF_WE, 0);
        short_delay_us(5);
        GPIO_WritePin(GPIOD, CF_WE, 1);
        short_delay_us(1000);  // Wait for command processing
        
        // Check status after IDENTIFY
        DataBus_Configure(DIR_INPUT);
        write_address_port(status_reg);
        short_delay_us(50);
        GPIO_WritePin(GPIOB, CF_OE, 0);
        short_delay_us(10);
        uint8_t post_identify_status = DataBus_ReadByte();
        GPIO_WritePin(GPIOB, CF_OE, 1);
        
        // Accept if we get any non-zero status after IDENTIFY
        if(post_identify_status != 0) {
            cf_ready = 1;
        }
    }

    if(cf_ready)
    {
        disk_initialized = 1;
        last_initialized_cart = id;
        return RES_OK;
    }
    else
    {
        disk_initialized = 0;
        return RES_ERROR;
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
    int timeout = 50000;
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
    int timeout = 50000;
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
    timeout = 100000;
    do {
        GPIO_WritePin(GPIOB, CF_OE, 0);
        short_delay_us(2);
        write_complete_status = DataBus_ReadByte();
        GPIO_WritePin(GPIOB, CF_OE, 1);
        short_delay_us(100);
        timeout--;
    } while ((write_complete_status & 0x80) != 0 && timeout > 0);

    if(timeout == 0) return RES_ERROR;  // Timeout

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
            *buff = 2048000;  // 1GB CF card (1GB = 2,048,000 sectors of 512 bytes)
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