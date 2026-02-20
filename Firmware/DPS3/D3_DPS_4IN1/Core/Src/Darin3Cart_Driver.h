/*
 * Darin3Cart_Driver.h
 *
 *  Created on: Sep 20, 2025
 *      Author: HP-Admin
 */

#ifndef SRC_DARIN3CART_DRIVER_H_
#define SRC_DARIN3CART_DRIVER_H_

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef enum {
    CARTRIDGE_1 = 0,
    CARTRIDGE_2,
    CARTRIDGE_3,
    CARTRIDGE_4
} CartridgeID;

typedef enum {
    DIR_INPUT = 0,
    DIR_OUTPUT = 1
} DataBusDirection;

void read_compact_flash(uint8_t *TempStorage, CartridgeID id);
void pre_read_compact_flash(CartridgeID id);
void command_for_read(void);
void compact_flash_read(uint8_t *TempStorage,uint16_t datalength);
void post_read_compact_flash(CartridgeID id);
void write_compact_flash(uint8_t* TempStorage, CartridgeID id);
void pre_write_compact_flash(CartridgeID id);
unsigned char compact_flash_ready(void);
void command_for_write(void);
unsigned char check_stat_of_compact_flash(void);
void compact_flash_write(uint8_t* TempStorage,uint16_t datalength);
void post_write_compact_flash(CartridgeID id);
uint8_t D3_Cartridge_Check(void);
void write_address_port(uint8_t data);
uint8_t Read_address_port(void);
void generic_CF_CMD(unsigned char sector_num1,unsigned char Cmd, unsigned char CylinderLow, unsigned char CylinderHigh);
void TesCompactFlashDriver(CartridgeID id);
void SimpleReadTest(CartridgeID id);
void ComprehensiveTest512(CartridgeID id);
void DataBus_Configure(DataBusDirection direction);
void DataBus_SetOutput(void);
void DataBus_SetInput(void);
void DataBus_WriteByte(uint8_t data);
uint8_t DataBus_ReadByte(void);

void UpdateD3SlotStatus(void);

uint16_t get_CE_pin(CartridgeID id);
uint16_t get_D3_Green_LedPins(CartridgeID id);
uint16_t get_D3_Red_LedPins(CartridgeID id);
uint16_t get_CD2SLT_pin(CartridgeID id);
uint16_t get_D3_slt_status(CartridgeID id);
void setGreenLed(CartridgeID id, uint8_t value);
void setRedLed(CartridgeID id, uint8_t value);
void short_delay_us(uint32_t us);
void slotLedBlink(CartridgeID id, uint8_t value);
uint8_t LedLoopBack(uint8_t value);
void BlinkAllLed(uint8_t value);

#ifdef __cplusplus
}
#endif
#endif /* SRC_DARIN3CART_DRIVER_H_ */
