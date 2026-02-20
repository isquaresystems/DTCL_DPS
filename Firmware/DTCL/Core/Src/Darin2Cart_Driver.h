/**
 ******************************************************************************
 * @file    Darin2Cart_Driver.h
 * @brief   Darin-II NAND Flash Cartridge Driver Header
 * @version 3.7
 ******************************************************************************
 * @attention
 *
 * Copyright (c) 2023-2024 ISquare Systems
 * All rights reserved.
 *
 * This software is licensed under terms that can be found in the LICENSE file
 * in the root directory of this software component.
 * If no LICENSE file comes with this software, it is provided AS-IS.
 *
 ******************************************************************************
 */

#ifndef DARIN2CART_DRIVER_H
#define DARIN2CART_DRIVER_H

/* Includes ------------------------------------------------------------------*/
#include <stdint.h>
#include "Darin3Cart_Driver.h"

#ifdef __cplusplus
extern "C" {
#endif

/* Public Function Prototypes -----------------------------------------------*/

/** @defgroup DARIN2_FLASH_Operations Flash Memory Operations
 * @brief Core NAND flash operations for read, write, and erase
 * @{
 */
void flash_write(const uint8_t* buffer, uint16_t dataLength, uint16_t pageAddress, CartridgeID id);
void flash_read(uint8_t* buffer, uint16_t dataLength, uint16_t pageAddress, CartridgeID id);
unsigned char flash_erase(uint16_t pageAddress, CartridgeID id);
uint8_t flash_device_ID(CartridgeID id);
/**
 * @}
 */

/** @defgroup DARIN2_FLASH_Lifecycle Flash Operation Lifecycle
 * @brief Preparation and cleanup functions for flash operations
 * @{
 */
void pre_write_flash(CartridgeID id);
void post_write_flash(CartridgeID id);
void pre_read_flash(CartridgeID id);
void post_read_flash(CartridgeID id);
void pre_erase_flash(CartridgeID id);
void post_erase_flash(CartridgeID id);
/**
 * @}
 */

/** @defgroup DARIN2_GPIO_Control GPIO and Port Control
 * @brief Low-level GPIO and port manipulation functions
 * @{
 */
void Configure_GPIO_IO_D2(enum pinConfiuration io);
uint8_t Read_port(void);
void write_port(uint8_t data);
void short_delay_us(uint32_t us);
/**
 * @}
 */

/** @defgroup DARIN2_Status_Control Status and LED Control
 * @brief Cartridge status monitoring and LED control functions
 * @{
 */
void UpdateD2SlotStatus(void);
uint16_t get_D2_slt_status(CartridgeID id);
uint16_t get_D1_slt_status(CartridgeID id);
uint16_t get_D2_Green_LedPins(CartridgeID id);
uint16_t get_D2_Red_LedPins(CartridgeID id);
void setGreenLed(CartridgeID id, uint8_t value);
void setRedLed(CartridgeID id, uint8_t value);
/**
 * @}
 */

#ifdef __cplusplus
}
#endif

#endif /* DARIN2CART_DRIVER_H */
