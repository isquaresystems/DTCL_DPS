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

// Data bus direction enum is now in Darin3Cart_Driver.h

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

// Disk initialization using direct CF logic from ComprehensiveTest512
DSTATUS disk_initialize(BYTE drv)
{
    if(drv != 0) return RES_PARERR;  // Only support drive 0

    CartridgeID id = m_CartId;  // Use cartridge 1 for now

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

    // Basic hardware connectivity test - try to read from different addresses
    volatile uint8_t connectivity_test[8];
    DataBus_Configure(DIR_INPUT);

    // Test reading from multiple CF registers to check basic connectivity
    for(int reg = 0; reg < 8; reg++) {
        write_address_port(reg);
        short_delay_us(50);
        GPIO_WritePin(GPIOB, CF_OE, 0);
        short_delay_us(10);
        connectivity_test[reg] = DataBus_ReadByte();
        GPIO_WritePin(GPIOB, CF_OE, 1);
        short_delay_us(10);
    }

    // Check initial status with extended diagnostics
    DataBus_Configure(DIR_INPUT);
    write_address_port(status_reg);
    short_delay_us(50);  // Longer setup time
    GPIO_WritePin(GPIOB, CF_OE, 0);
    short_delay_us(10);  // Longer read time
    volatile uint8_t initial_status = DataBus_ReadByte();
    GPIO_WritePin(GPIOB, CF_OE, 1);

    // Store status for debugging (can be examined in debugger)
    volatile uint8_t debug_cart_id = (uint8_t)id;
    volatile uint8_t debug_status = initial_status;
    volatile uint8_t debug_bit7 = (initial_status & 0x80) ? 1 : 0;  // BSY
    volatile uint8_t debug_bit6 = (initial_status & 0x40) ? 1 : 0;  // RDY

    // Test data bus functionality by writing/reading to output register
    DataBus_Configure(DIR_OUTPUT);
    write_address_port(feature);  // Use feature register for testing
    short_delay_us(10);
    DataBus_WriteByte(0xAA);  // Test pattern
    short_delay_us(5);
    GPIO_WritePin(GPIOD, CF_WE, 0);
    short_delay_us(5);
    GPIO_WritePin(GPIOD, CF_WE, 1);
    short_delay_us(10);

    // Read it back
    DataBus_Configure(DIR_INPUT);
    write_address_port(feature);
    short_delay_us(10);
    GPIO_WritePin(GPIOB, CF_OE, 0);
    short_delay_us(5);
    volatile uint8_t bus_test_aa = DataBus_ReadByte();
    GPIO_WritePin(GPIOB, CF_OE, 1);

    // Try different pattern
    DataBus_Configure(DIR_OUTPUT);
    DataBus_WriteByte(0x55);
    short_delay_us(5);
    GPIO_WritePin(GPIOD, CF_WE, 0);
    short_delay_us(5);
    GPIO_WritePin(GPIOD, CF_WE, 1);
    short_delay_us(10);

    DataBus_Configure(DIR_INPUT);
    GPIO_WritePin(GPIOB, CF_OE, 0);
    short_delay_us(5);
    volatile uint8_t bus_test_55 = DataBus_ReadByte();
    GPIO_WritePin(GPIOB, CF_OE, 1);

    // Multiple status read attempts for flaky CF cards
    volatile uint8_t status_attempts[3];
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
    // Some CF cards might need a different initialization sequence
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
        volatile uint8_t post_identify_status = DataBus_ReadByte();
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

    // Store state for debugging
    volatile uint8_t debug_disk_init = disk_initialized ? 1 : 0;
    volatile uint8_t debug_last_cart = (uint8_t)last_initialized_cart;
    volatile uint8_t debug_current_cart = (uint8_t)m_CartId;

    // If disk not initialized or cartridge changed, return not ready
    if (!disk_initialized || last_initialized_cart != m_CartId) {
        return STA_NOINIT;  // Force initialization
    }

    CartridgeID id = m_CartId;

    // Check CF status register (exact copy from ComprehensiveTest512)
    DataBus_Configure(DIR_INPUT);
    write_address_port(status_reg);
    short_delay_us(10);
    GPIO_WritePin(GPIOB, CF_OE, 0);
    volatile uint8_t current_status = DataBus_ReadByte();
    GPIO_WritePin(GPIOB, CF_OE, 1);

    // Store for debugging
    volatile uint8_t debug_status_check = current_status;

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

    CartridgeID id = m_CartId;  // Use cartridge 1 for now

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

    volatile uint8_t read_status;
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

    CartridgeID id = m_CartId;  // Use cartridge 1 for now

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

    volatile uint8_t write_status;
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

    volatile uint8_t write_complete_status;
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

// Debug function to examine boot sector
DRESULT DebugReadBootSector(BYTE* buffer)
{
    if (!disk_initialized) {
        return RES_NOTRDY;
    }
    
    // Read sector 0 (boot sector)
    return disk_read(0, buffer, 0, 1);
}

#if 0
// ===== DISKIO TEST FUNCTIONS =====

// Test function to validate diskio implementation before FatFs usage
void Test_DiskIO_Functions(void)
{
    // Test data buffers
    static uint8_t write_buffer[512];
    static uint8_t read_buffer[512];
    static uint8_t verify_buffer[512];

    // Initialize test pattern - same as ComprehensiveTest512
    for(int i = 0; i < 512; i++)
    {
        write_buffer[i] = i & 0xFF;  // 0, 1, 2, ... 255, 0, 1, 2, ...
    }

    // Clear read buffers
    for(int i = 0; i < 512; i++)
    {
        read_buffer[i] = 0;
        verify_buffer[i] = 0;
    }

    // Test variables for debugging
    volatile DSTATUS init_result;
    volatile DSTATUS status_result;
    volatile DRESULT write_result;
    volatile DRESULT read_result;
    volatile DRESULT ioctl_result;
    volatile DWORD test_sector_count = 0;
    volatile DWORD test_sector_size = 0;
    volatile DWORD test_block_size = 0;

    // === TEST 1: Disk Initialization ===
    init_result = disk_initialize(0);

    if(init_result == RES_OK)
    {
        // === TEST 2: Disk Status ===
        status_result = disk_status(0);

        if(status_result == RES_OK)
        {
            // === TEST 3: I/O Control Tests ===
            ioctl_result = disk_ioctl(0, GET_SECTOR_COUNT, &test_sector_count);
            ioctl_result = disk_ioctl(0, GET_SECTOR_SIZE, &test_sector_size);
            ioctl_result = disk_ioctl(0, GET_BLOCK_SIZE, &test_block_size);
            ioctl_result = disk_ioctl(0, CTRL_SYNC, NULL);

            // === TEST 4: Write Test ===
            write_result = disk_write(0, write_buffer, 0, 1);  // Write to sector 0

            if(write_result == RES_OK)
            {
                // === TEST 5: Read Test ===
                read_result = disk_read(0, read_buffer, 0, 1);  // Read from sector 0

                if(read_result == RES_OK)
                {
                    // === TEST 6: Data Verification ===
                    volatile int matches = 0;
                    volatile int mismatches = 0;
                    volatile uint8_t first_mismatch_pos = 0xFF;
                    volatile uint8_t first_mismatch_expected = 0xFF;
                    volatile uint8_t first_mismatch_actual = 0xFF;

                    for(int i = 0; i < 512; i++)
                    {
                        if(read_buffer[i] == write_buffer[i])
                        {
                            matches++;
                        }
                        else
                        {
                            mismatches++;
                            if(first_mismatch_pos == 0xFF)  // First mismatch
                            {
                                first_mismatch_pos = i;
                                first_mismatch_expected = write_buffer[i];
                                first_mismatch_actual = read_buffer[i];
                            }
                        }
                    }

                    // === TEST 7: Key Value Verification ===
                    volatile uint8_t result_0 = read_buffer[0];      // Should be 0
                    volatile uint8_t result_1 = read_buffer[1];      // Should be 1
                    volatile uint8_t result_10 = read_buffer[10];    // Should be 10
                    volatile uint8_t result_100 = read_buffer[100];  // Should be 100
                    volatile uint8_t result_255 = read_buffer[255];  // Should be 255
                    volatile uint8_t result_256 = read_buffer[256];  // Should be 0 (pattern repeats)
                    volatile uint8_t result_511 = read_buffer[511];  // Should be 255

                    // === TEST 8: Multi-Sector Test ===
                    // Test different sector (sector 1)
                    write_result = disk_write(0, write_buffer, 1, 1);
                    if(write_result == RES_OK)
                    {
                        read_result = disk_read(0, verify_buffer, 1, 1);
                    }

                    // === TEST RESULTS SUMMARY ===
                    volatile uint8_t test_summary = 0;
                    if(init_result == RES_OK) test_summary |= 0x01;
                    if(status_result == RES_OK) test_summary |= 0x02;
                    if(write_result == RES_OK) test_summary |= 0x04;
                    if(read_result == RES_OK) test_summary |= 0x08;
                    if(matches == 512) test_summary |= 0x10;
                    if(test_sector_count > 0) test_summary |= 0x20;
                    if(test_sector_size == 512) test_summary |= 0x40;
                    // test_summary == 0x7F means all tests passed

                    volatile int breakpoint_here = 0;  // Set breakpoint here to examine results
                }
            }
        }
    }

    // Additional error case testing
    volatile DRESULT error_test1 = disk_read(1, read_buffer, 0, 1);    // Invalid drive - should return RES_PARERR
    volatile DRESULT error_test2 = disk_write(1, write_buffer, 0, 1);  // Invalid drive - should return RES_PARERR
    volatile DRESULT error_test3 = disk_read(0, read_buffer, 0, 0);    // Zero count - should return RES_PARERR
    volatile DRESULT error_test4 = disk_read(0, read_buffer, 0, 2);    // Multi-sector - should return RES_ERROR
}

// Quick diskio test - minimal version for basic validation
void Quick_DiskIO_Test(void)
{
    volatile DSTATUS init_status = disk_initialize(0);
    volatile DSTATUS disk_status_result = disk_status(0);

    if(init_status == RES_OK && disk_status_result == RES_OK)
    {
        // Simple single byte test pattern
        static uint8_t test_data[512] = {0xAA, 0x55, 0xAA, 0x55}; // Alternating pattern
        static uint8_t read_data[512];

        // Fill rest with pattern
        for(int i = 4; i < 512; i++)
        {
            test_data[i] = (i % 2) ? 0x55 : 0xAA;
        }

        volatile DRESULT write_status = disk_write(0, test_data, 0, 1);
        volatile DRESULT read_status = disk_read(0, read_data, 0, 1);

        volatile uint8_t byte0 = read_data[0];  // Should be 0xAA
        volatile uint8_t byte1 = read_data[1];  // Should be 0x55
        volatile uint8_t byte2 = read_data[2];  // Should be 0xAA
        volatile uint8_t byte3 = read_data[3];  // Should be 0x55

        volatile uint8_t quick_test_status = 0;
        if(write_status == RES_OK) quick_test_status |= 0x01;
        if(read_status == RES_OK) quick_test_status |= 0x02;
        if(byte0 == 0xAA && byte1 == 0x55) quick_test_status |= 0x04;
        // quick_test_status == 0x07 means quick test passed

        volatile int quick_breakpoint = 0;  // Set breakpoint here
    }
}

// ===== FATFS COMPREHENSIVE TEST FUNCTION =====

// Test function to validate complete FatFs functionality
void Test_FatFs_Functions(void)
{
    // FatFs objects
    static FATFS fs;           // File system object
    static FIL file;           // File object
    static DIR dir;            // Directory object
    static FILINFO fno;        // File information object

    // Test data buffers
    static char write_buffer[512];
    static char read_buffer[512];
    static char test_filename[] = "TEST.TXT";
    static char test_content[] = "Hello FatFs! This is a test file created by the CF diskio driver.\r\n"
                                "Data: 0123456789ABCDEF\r\n"
                                "Pattern: DEADBEEF-CAFEBABE\r\n"
                                "Status: Testing CF + FatFs integration\r\n";

    // Result variables for debugging
    volatile FRESULT mount_result;
    volatile FRESULT open_result;
    volatile FRESULT write_result;
    volatile FRESULT close_result;
    volatile FRESULT reopen_result;
    volatile FRESULT read_result;
    volatile FRESULT unlink_result;
    volatile FRESULT opendir_result;
    volatile FRESULT readdir_result;

    volatile UINT bytes_written = 0;
    volatile UINT bytes_read = 0;
    volatile DWORD free_clusters = 0;
    volatile DWORD total_sectors = 0;

    // Clear buffers
    memset(write_buffer, 0, sizeof(write_buffer));
    memset(read_buffer, 0, sizeof(read_buffer));

    // Copy test content to write buffer
    strncpy(write_buffer, test_content, sizeof(write_buffer) - 1);

    // === TEST 1: Mount File System ===
    mount_result = f_mount(&fs, "", 1);  // Mount with immediate mount

    if(mount_result == FR_OK)
    {
        // === TEST 2: Get File System Info ===
        DWORD fre_clust;
        FATFS* fs_ptr = &fs;
        f_getfree("", &fre_clust, &fs_ptr);
        free_clusters = fre_clust;
        total_sectors = (fs.n_fatent - 2) * fs.csize;

        // === TEST 3: Create and Write File ===
        open_result = f_open(&file, test_filename, FA_CREATE_ALWAYS | FA_WRITE);

        if(open_result == FR_OK)
        {
            write_result = f_write(&file, write_buffer, strlen(write_buffer), &bytes_written);
            close_result = f_close(&file);

            if(write_result == FR_OK && close_result == FR_OK)
            {
                // === TEST 4: Read File ===
                reopen_result = f_open(&file, test_filename, FA_READ);

                if(reopen_result == FR_OK)
                {
                    read_result = f_read(&file, read_buffer, sizeof(read_buffer) - 1, &bytes_read);
                    f_close(&file);

                    // === TEST 5: Verify File Content ===
                    volatile uint8_t content_match = 0;
                    if(bytes_written == bytes_read && bytes_written > 0)
                    {
                        if(strncmp(write_buffer, read_buffer, bytes_written) == 0)
                        {
                            content_match = 1;
                        }
                    }

                    // === TEST 6: Directory Operations ===
                    opendir_result = f_opendir(&dir, "/");
                    if(opendir_result == FR_OK)
                    {
                        // Read directory entries
                        readdir_result = f_readdir(&dir, &fno);
                        f_closedir(&dir);
                    }

                    // === TEST 7: File Management ===
                    // Get file size
                    volatile DWORD file_size = 0;
                    if(f_open(&file, test_filename, FA_READ) == FR_OK)
                    {
                        file_size = f_size(&file);
                        f_close(&file);
                    }

                    // === TEST 8: Clean Up (Optional) ===
                    unlink_result = f_unlink(test_filename);  // Delete test file

                    // === TEST RESULTS SUMMARY ===
                    volatile uint8_t fatfs_test_summary = 0;
                    if(mount_result == FR_OK) fatfs_test_summary |= 0x01;          // Mount success
                    if(open_result == FR_OK) fatfs_test_summary |= 0x02;           // Create success
                    if(write_result == FR_OK) fatfs_test_summary |= 0x04;          // Write success
                    if(read_result == FR_OK) fatfs_test_summary |= 0x08;           // Read success
                    if(content_match == 1) fatfs_test_summary |= 0x10;             // Content match
                    if(bytes_written > 0) fatfs_test_summary |= 0x20;              // Data written
                    if(free_clusters > 0) fatfs_test_summary |= 0x40;              // File system info
                    if(unlink_result == FR_OK) fatfs_test_summary |= 0x80;         // Delete success
                    // fatfs_test_summary == 0xFF means all FatFs tests passed

                    // Detailed verification values
                    volatile uint8_t first_char = read_buffer[0];       // Should be 'H'
                    volatile uint8_t char_e = read_buffer[1];           // Should be 'e'
                    volatile uint8_t char_l1 = read_buffer[2];          // Should be 'l'
                    volatile uint8_t char_l2 = read_buffer[3];          // Should be 'l'
                    volatile uint8_t char_o = read_buffer[4];           // Should be 'o'

                    volatile int fatfs_breakpoint = 0;  // Set breakpoint here to examine all results
                }
            }
        }
    }

    // Error case analysis
    volatile uint8_t error_analysis = 0;
    if(mount_result != FR_OK) error_analysis |= 0x01;      // Mount failed
    if(open_result != FR_OK) error_analysis |= 0x02;       // File create failed
    if(write_result != FR_OK) error_analysis |= 0x04;      // Write failed
    if(read_result != FR_OK) error_analysis |= 0x08;       // Read failed
    // error_analysis == 0 means no errors
}

// Quick FatFs test - enhanced with debugging for FR_NOT_RDY issue
void Quick_FatFs_Test(void)
{
    static FATFS fs;
    static FIL file;

    // Pre-check: Test diskio functions first
    volatile DSTATUS pre_init_status = disk_initialize(0);
    volatile DSTATUS pre_disk_status = disk_status(0);

    // Mount with immediate mount (1) to force disk access
    volatile FRESULT mount_status = f_mount(&fs, "", 1);

    // Post-mount: Check diskio status again
    volatile DSTATUS post_disk_status = disk_status(0);

    // Detailed debugging variables
    volatile uint8_t mount_error_code = (uint8_t)mount_status;  // FR_NOT_RDY = 3
    volatile uint8_t pre_init_code = (uint8_t)pre_init_status;   // RES_OK = 0
    volatile uint8_t pre_status_code = (uint8_t)pre_disk_status; // Check if RES_NOTRDY
    volatile uint8_t post_status_code = (uint8_t)post_disk_status;

    if(mount_status == FR_OK)
    {
        static char quick_data[] = "Quick test: 123\r\n";
        static char read_data[32];
        volatile UINT written = 0, read = 0;

        volatile FRESULT create_status = f_open(&file, "QUICK.TXT", FA_CREATE_ALWAYS | FA_WRITE);
        volatile uint8_t create_error_code = (uint8_t)create_status;

        if(create_status == FR_OK)
        {
            volatile FRESULT write_status = f_write(&file, quick_data, strlen(quick_data), &written);
            f_close(&file);

            if(write_status == FR_OK)
            {
                volatile FRESULT read_status = f_open(&file, "QUICK.TXT", FA_READ);
                if(read_status == FR_OK)
                {
                    f_read(&file, read_data, sizeof(read_data) - 1, &read);
                    f_close(&file);

                    // Clean up
               //     f_unlink("QUICK.TXT");
                }
            }
        }

        volatile uint8_t quick_fatfs_status = 0;
        if(mount_status == FR_OK) quick_fatfs_status |= 0x01;
        if(create_status == FR_OK) quick_fatfs_status |= 0x02;
        if(written > 0) quick_fatfs_status |= 0x04;
        if(read > 0) quick_fatfs_status |= 0x08;
        // quick_fatfs_status == 0x0F means quick FatFs test passed

        volatile int quick_fatfs_breakpoint = 0;  // Set breakpoint here
    }
    else
    {
        // Mount failed - examine why
        volatile int mount_failed_breakpoint = 0;  // Set breakpoint here to examine error codes
    }
}

// Test function for FatFsWrapper to isolate issues
// Test to demonstrate wrapper conflict
void Test_Wrapper_Conflict_Demo(void)
{
    // Step 1: Create a working FatFs mount (like your successful tests)
    static FATFS test_fs;
    volatile FRESULT test_mount = f_mount(&test_fs, "", 1);

    if(test_mount == FR_OK)
    {
        // Step 2: Create test file with working mount
        static FIL test_file;
        volatile FRESULT create_result = f_open(&test_file, "CONFLICT.TXT", FA_CREATE_ALWAYS | FA_WRITE);
        if(create_result == FR_OK)
        {
            const char* data = "Before wrapper conflict\r\n";
            UINT written = 0;
            f_write(&test_file, data, strlen(data), &written);
            f_close(&test_file);
        }

        // Step 3: Now simulate what happens when wrapper tries to mount
        // This represents the wrapper's internal FATFS object trying to mount
        static FATFS wrapper_fs;  // This simulates wrapper's fs_ member
        volatile FRESULT wrapper_mount = f_mount(&wrapper_fs, "", 1);

        // Step 4: Try to use original mount after wrapper mount
        volatile FRESULT reopen_result = f_open(&test_file, "CONFLICT.TXT", FA_READ);

        // Step 5: Check what happens to our original filesystem object
        volatile FRESULT original_mount_status = FR_OK;
        if(reopen_result != FR_OK) {
            // Try to remount original
            original_mount_status = f_mount(&test_fs, "", 1);
        }

        volatile uint8_t conflict_status = 0;
        if(test_mount == FR_OK) conflict_status |= 0x01;           // Original mount OK
        if(create_result == FR_OK) conflict_status |= 0x02;        // File creation OK
        if(wrapper_mount == FR_OK) conflict_status |= 0x04;        // Wrapper mount OK
        if(reopen_result == FR_OK) conflict_status |= 0x08;        // File still accessible
        if(original_mount_status == FR_OK) conflict_status |= 0x10; // Recovery possible

        // conflict_status analysis:
        // 0x1F = No conflict (unlikely)
        // 0x17 = Conflict occurred but recoverable
        // 0x07 = Conflict breaks file access

        volatile int conflict_breakpoint = 0;  // Set breakpoint here

        // Cleanup
        f_mount(NULL, "", 0);
    }
}

void Test_FatFsWrapper_Simple(void)
{
    // Use raw FatFs first to ensure it works
    static FATFS raw_fs;
    volatile FRESULT raw_mount = f_mount(&raw_fs, "", 1);

    if(raw_mount == FR_OK)
    {
        // Test 1: Create a file with raw FatFs
        static FIL raw_file;
        volatile FRESULT raw_create = f_open(&raw_file, "RAWTEST.TXT", FA_CREATE_ALWAYS | FA_WRITE);

        UINT written = 0;
        if(raw_create == FR_OK)
        {
            const char* test_data = "Raw FatFs test data\r\n";
            f_write(&raw_file, test_data, strlen(test_data), &written);
            f_close(&raw_file);
        }

        // Unmount raw FatFs
        f_mount(NULL, "", 0);

        // Now test with wrapper (if you have wrapper instance)
        // This is where wrapper would be tested
        // For now, just verify raw FatFs still works after unmount/remount

        // Remount to verify
        volatile FRESULT remount = f_mount(&raw_fs, "", 1);
        volatile uint8_t test_status = 0;
        if(raw_mount == FR_OK) test_status |= 0x01;
        if(raw_create == FR_OK) test_status |= 0x02;
        if(written > 0) test_status |= 0x04;
        if(remount == FR_OK) test_status |= 0x08;
        // test_status == 0x0F means all passed

        volatile int wrapper_test_breakpoint = 0;
    }
}
#endif

#endif
