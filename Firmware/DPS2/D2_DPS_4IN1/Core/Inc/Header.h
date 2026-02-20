/*
 * Header.h
 *
 *  Created on: Jul 20, 2023
 *      Author: Vijay
 */

#ifndef SRC_HEADER_H_
#define SRC_HEADER_H_

enum pinConfiuration {Input =0, Output};

/****** D1 Declarations********/
void Configure_GPIO_IO(enum pinConfiuration);
void RAM_Write(uint8_t*, uint16_t ,uint16_t);
void RAM_Read(uint8_t*, uint16_t ,uint16_t);
void Pre_RAM_Write();
void Post_RAM_Write();
void Pre_RAM_Read();
void Post_RAM_Read();
uint8_t D1_Cartridge_Check();
uint8_t LED_LoopBackTest();

void Configure_GPIO_IO_D2(enum pinConfiuration);
void Receive_File(void);
void State_Machine(void);
void processUSBData();

void pre_write_flash(void);		//pre setting of port pins before writing the data
void flash_write(uint8_t* TempStorage, uint16_t dataLength, uint16_t Address_Flash_Page);		    //function to read 512 byte from XRAM location and load it in flash
void post_write_flash(void);		//this function resets all the port pin so that the mode of the
void pre_read_flash(void);	//function to pre intialize the ports before reading
void flash_read(uint8_t *TempStorage, uint16_t dataLength, uint16_t Address_Flash_Page);	//this function initiates the read command to the flash
void post_read_flash(void);	//this function is used to reset the mode of all the port so that
void write_port(uint8_t data);
uint8_t Read_port();

void pre_erase_flash();
void post_erase_flash();
uint8_t flash_erase(uint16_t pageAddress);
void Receive_Setup(void);
uint8_t D2_Cartridge_Check(void);



#define MAX_BLOCK_SIZE_READ	64		//	Use the maximum read block size of 64 bytes
#define	FLASH_PAGE_SIZE	512	        //	Size of each flash page
#define	BLOCKS_PR_PAGE	FLASH_PAGE_SIZE/MAX_BLOCK_SIZE_READ  // 512/64 = 8
#define MAX_BLOCK_SIZE_WRITE 4096	//	Use the maximum write block size of 4096 bytes
#define	BLOCKS_PR_PAGE	FLASH_PAGE_SIZE/MAX_BLOCK_SIZE_READ  // 512/64 = 8
#define MAX_NUM_BYTES	FLASH_PAGE_SIZE*NUM_STG_PAGES
#define MAX_NUM_BLOCKS	BLOCKS_PR_PAGE*NUM_STG_PAGES

#endif /* SRC_HEADER_H_ */

