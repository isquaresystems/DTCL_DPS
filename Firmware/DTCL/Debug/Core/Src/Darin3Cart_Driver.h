/*
 * Darin3Cart_Driver.h
 *
 *  Created on: Jul 12, 2025
 *      Author: HP-Admin
 */

#ifndef SRC_DARIN3CART_DRIVER_H_
#define SRC_DARIN3CART_DRIVER_H_

typedef enum {
    CARTRIDGE_1 = 0,
    CARTRIDGE_2,
    CARTRIDGE_3,
    CARTRIDGE_4
} CartridgeID;

#ifdef __cplusplus
extern "C" {
#endif

uint16_t get_CE_pin(CartridgeID id);
void read_compact_flash(uint8_t *TempStorage, CartridgeID id);
void pre_read_compact_flash(CartridgeID id);
void command_for_read(void);
void post_write_compact_flash(CartridgeID id);
void compact_flash_read(uint16_t *TempStorage,uint16_t datalength);
void write_compact_flash(uint16_t* TempStorage, CartridgeID id);
void pre_write_compact_flash(CartridgeID id);
unsigned char compact_flash_ready(void);
void command_for_write();
unsigned char check_stat_of_compact_flash(void);
void compact_flash_write(uint16_t* TempStorage,uint16_t datalength);
void post_read_compact_flash(CartridgeID id);
uint8_t D3_Cartridge_Check(CartridgeID id);
void write_address_port(uint8_t data);
uint8_t Read_address_port();
void generic_CF_CMD(unsigned char sector_num1,unsigned char Cmd, unsigned char CylinderLow, unsigned char CylinderHigh);
static void Configure_GPIO_IO_D2(uint8_t output_enable);
void TesCompactFlashDriver(CartridgeID id);
static void write_port(uint16_t data);
static uint16_t Read_port();
void UpdateD3SlotStatus();


uint16_t get_D3_Green_LedPins(CartridgeID id);
uint16_t get_D3_Red_LedPins(CartridgeID id);
uint16_t get_CD2SLT_pin(CartridgeID id);
uint16_t get_D3_slt_status(CartridgeID id);
void setGreenLed(CartridgeID id, uint8_t value);
void setRedLed(CartridgeID id, uint8_t value);

#ifdef __cplusplus
}
#endif
#endif /* SRC_DARIN3CART_DRIVER_H_ */
