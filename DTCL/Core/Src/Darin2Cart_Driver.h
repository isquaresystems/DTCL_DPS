// flash_driver.h
#ifndef DARIN2CART_DRIVER_H
#define DARIN2CART_DRIVER_H

#include <stdint.h>
#include "Darin3Cart_Driver.h"

#ifdef __cplusplus
extern "C" {
#endif

void flash_write(const uint8_t* buffer, uint16_t dataLength, uint16_t pageAddress, CartridgeID id);
void flash_read(uint8_t* buffer, uint16_t dataLength, uint16_t pageAddress, CartridgeID id);
unsigned char flash_erase(uint16_t pageAddress, CartridgeID id);
void flash_reset(CartridgeID id);
void flash_wait_ready(CartridgeID id);
void pre_write_flash(CartridgeID id);
void post_write_flash(CartridgeID id);
void pre_read_flash(CartridgeID id);
void post_read_flash(CartridgeID id);
void pre_erase_flash(CartridgeID id);
void post_erase_flash(CartridgeID id);
uint8_t flash_device_ID(CartridgeID id);
void Configure_GPIO_IO_D2(enum pinConfiuration io);
uint8_t Read_port();
void write_port(uint8_t data);
void short_delay_us(uint32_t us);
void UpdateD2SlotStatus();
uint16_t get_D2_slt_status(CartridgeID id);
uint16_t get_D1_slt_status(CartridgeID id);
uint16_t get_D2_Green_LedPins(CartridgeID id);
uint16_t get_D2_Red_LedPins(CartridgeID id) ;
void setGreenLed(CartridgeID id, uint8_t value);
void setRedLed(CartridgeID id, uint8_t value);
#ifdef __cplusplus
}
#endif

#endif // FLASH_DRIVER_H
