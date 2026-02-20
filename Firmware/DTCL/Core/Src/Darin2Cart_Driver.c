/**
 ******************************************************************************
 * @file    Darin2Cart_Driver.c
 * @brief   Darin-II NAND Flash Cartridge Driver Implementation
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

/* Includes ------------------------------------------------------------------*/
#include "stm32f4xx_hal.h"
#include "main.h"
#include "Darin2Cart_Driver.h"

/* Private Constants ---------------------------------------------------------*/

/* NAND Flash Control Pins */
uint16_t F_RDY = C2RB0_Pin;              /* Ready/Busy pin */
uint16_t F_ALE = C1A00_C2ALE_C3A00_Pin;   /* Address Latch Enable */
uint16_t F_CLE = C1A04_C2CLE_Pin;         /* Command Latch Enable */
uint16_t F_WP  = C1A01_C2nWP_C3A01_Pin;  /* Write Protect (active low) */
uint16_t F_CE  = C2CE1_Pin;              /* Chip Enable */
uint16_t F_RD  = C2RE_Pin;               /* Read Enable */
uint16_t F_WR  = C1A03_C2nWE_C3A03_Pin;  /* Write Enable (active low) */

/* NAND Flash Data Bus Pins (8-bit) */
uint16_t M_D7  = C2DB7_C3DB7_INOUT_Pin;  /* Data bit 7 */
uint16_t M_D6  = C2DB6_C3DB6_Pin;        /* Data bit 6 */
uint16_t M_D5  = C2DB5_C3DB5_INOUT_Pin;  /* Data bit 5 */
uint16_t M_D4  = C2DB4_C3DB4_Pin;        /* Data bit 4 */
uint16_t M_D3  = C2DB3_C3DB3_Pin;        /* Data bit 3 */
uint16_t M_D2  = C2DB2_C3DB2_Pin;        /* Data bit 2 */
uint16_t M_D1  = C2DB1_C3DB1_Pin;        /* Data bit 1 */
uint16_t M_D0  = C2DB0_C3DB0_Pin;        /* Data bit 0 */

/* Private Variables ---------------------------------------------------------*/

/* Cartridge slot pin mappings */
static const uint16_t CE_PINS[]    = { C2CE1_Pin, C2CE1_Pin, C2CE1_Pin, C2CE1_Pin };
static const uint16_t RDY_PINS[]   = { C2RB0_Pin, C2RB0_Pin, C2RB0_Pin, C2RB0_Pin };
static const uint16_t SLT_PINS[]   = { C2SLTS1_Pin, C2SLTS1_Pin, C2SLTS1_Pin, C2SLTS1_Pin };
static const uint16_t RED_LED[]    = { LED1_Pin, LED1_Pin, LED1_Pin, LED1_Pin };
static const uint16_t GREEN_LED[]  = { LED2_Pin, LED2_Pin, LED2_Pin, LED2_Pin };

/* Slot status tracking */
static uint8_t SLT_STATUS[] = { 0, 0, 0, 0 };

//****************************************************************************
//PRE FLASH WRITE
//****************************************************************************

void pre_write_flash(CartridgeID id)		//pre setting of port pins before writing the data
{

   Configure_GPIO_IO_D2(Output);    //port P1 is declared as output port

   GPIOB->BSRR = (uint32_t)F_ALE << 16;		             //disable address latch enable pin of flash
   GPIOB->BSRR = F_WP;	                 //disable write protect pin of flash
   GPIOB->BSRR = F_RD;		 //disable output enable pin of flash
   GPIOB->BSRR = F_WR;	                 //disable write enable pin of flash
   GPIOC->BSRR = (uint32_t)F_CLE << 16;	                 //disable command latch enable pin of flash
   GPIOC->BSRR = (uint32_t)F_CE << 16;		 //activate the flash chip
}

//**********************************************************************************
//Function To Write
//**********************************************************************************
void flash_write(const uint8_t* TempStorage, uint16_t dataLength, uint16_t Address_Flash_Page,CartridgeID id)
{
    GPIOC->BSRR = F_CLE;		   //activate the command latch enable
    short_delay_us(1);  // CLE setup time
    write_port(0x80);					       //send read command 0x80 to port p1
    GPIOB->BSRR = (uint32_t)F_WR << 16;		   //write the write command into the flash
    short_delay_us(1);  // WE pulse width minimum
    GPIOB->BSRR = F_WR;   	   //so that write command is intiated
    short_delay_us(1);  // WE hold time
    GPIOC->BSRR = (uint32_t)F_CLE << 16;	       //disable command latch enable

    write_port(0x00);					       //send address 0x00 to port P1
	GPIOB->BSRR = F_ALE;		   //activate address latch enable signal of flash

	write_port(0x00);					       //send address 0x00 to port P1

	GPIOB->BSRR = (uint32_t)F_WR << 16;  	   //intiate write signal
	short_delay_us(1);  // WE pulse width minimum (25ns)
	GPIOB->BSRR = F_WR;

	write_port(Address_Flash_Page);            //send lower order 8 bit page address to port P1

	GPIOB->BSRR = (uint32_t)F_WR << 16;		   //intiate write signal
	short_delay_us(1);  // WE pulse width minimum (25ns)
	GPIOB->BSRR = F_WR;

	write_port(Address_Flash_Page >>8);        //send higher order 8 bit page address to port P1
	GPIOB->BSRR = (uint32_t)F_WR << 16;		   //initiate the write signal
	short_delay_us(1);  // WE pulse width minimum (25ns)
	GPIOB->BSRR = F_WR;

	write_port(0x00);				           //send address 0x00 to port P1
	GPIOB->BSRR = (uint32_t)F_WR << 16;		   //initiate the write signal
	short_delay_us(1);  // WE pulse width minimum (25ns)
	GPIOB->BSRR = F_WR;

	write_port(0xFF);  				           //intially set the bits of port P1
	GPIOB->BSRR = (uint32_t)F_ALE << 16;		   //disable address latch enable which indicates the end of write command for flash
	short_delay_us(1000);					           //wait for some delay

	int x=0;
    for(x = 0;	x<dataLength;	x++)           //if data counter 'x'<	last_page_size then
	{

		write_port(*TempStorage);                   //fetch the data from the XRAM loction
		short_delay_us(1);  // Data setup time (20ns)
	   	GPIOB->BSRR = (uint32_t)F_WR << 16;			//enable write signal of flash
	   	short_delay_us(1);  // WE pulse width minimum (25ns)
	   	GPIOB->BSRR = F_WR;			//disable write signal of flash
	   	TempStorage++;				                //increment the pointer of XRAM
	}

    if(dataLength < 512)	//if last page size is less than 512 then
    {
    for(x=0;x<(512-dataLength);x++)//fill the remaining by data 0xFF
      {
    	write_port(0xFF);			                //send data 0xFF through port P1
		GPIOB->BSRR = (uint32_t)F_WR << 16; 			//enable write signal
		GPIOB->BSRR = F_WR;			//disable write signal
      }
    }

    short_delay_us(1000);					                //call delay function

    GPIOC->BSRR = F_CLE;				//enable command latch enable
    short_delay_us(1);  // CLE setup time
    write_port(0x10);			                    //initiate write command to flash so that the data from flash buffer

    GPIOB->BSRR = (uint32_t)F_WR << 16;				//enable write signal of flash
    short_delay_us(1);  // WE pulse width minimum (25ns)
    GPIOB->BSRR = F_WR;   			//disable write signal
    GPIOC->BSRR = (uint32_t)F_CLE << 16; 			//disable command latch enable
    short_delay_us(1000);					                //call delay function
}
//****************************************************************************
//POST FLASH WRITE
//****************************************************************************

void post_write_flash(CartridgeID id)
{
   GPIOC->BSRR = F_CE; 			    //disable flash chip
   GPIOB->BSRR = (uint32_t)F_ALE << 16;				//disable address latch enable of flash
   GPIOB->BSRR = F_WP;				//disable write protect of flash
   GPIOB->BSRR = F_RD;	            //disable read enable of flash
   GPIOB->BSRR = F_WR;				//disable write of flash
   GPIOC->BSRR = (uint32_t)F_CLE << 16;				//disable command latch enable of flash

   Configure_GPIO_IO_D2(Input);
}


void pre_read_flash(CartridgeID id)
{
	Configure_GPIO_IO_D2(Output);                             //port P1 is declared as output port

	GPIOB->BSRR = (uint32_t)F_ALE << 16;		               //disable address latch enable pin of flash
	GPIOB->BSRR = F_WP;	                   //disable write protect pin of flash
	GPIOB->BSRR = F_RD;		               //disable output enable pin of flash
	GPIOB->BSRR = F_WR;	                   //disable write enable pin of flash
	GPIOC->BSRR = (uint32_t)F_CLE << 16;				       //disable command latch enable pin of flash
	GPIOC->BSRR = (uint32_t)F_CE << 16;		               //activate the flash chip
}
//****************************************************************************
//READ_FLASH
//****************************************************************************

void flash_read(uint8_t *TempStorage, uint16_t dataLength, uint16_t Address_Flash_Page,CartridgeID id)
{

	GPIOC->BSRR = F_CLE;			//activate the command latch enable
	short_delay_us(1);  // CLE setup time

	write_port(0x00);				            //send read command 0x00 to port p1
	GPIOB->BSRR = (uint32_t)F_WR << 16;			//write the read command into the flash
	short_delay_us(1);  // WE pulse width minimum (25ns)
	GPIOB->BSRR = F_WR;   		//so that read command is intiated

	GPIOC->BSRR = (uint32_t)F_CLE << 16;		    //diable command latch enable
	GPIOB->BSRR = F_ALE;			//activate address latch enable signal of flash

	write_port(0x00);			                //send address 0x00 to port P1
	GPIOB->BSRR = (uint32_t)F_WR << 16;  		//intiate write signal
	short_delay_us(1);  // WE pulse width minimum (25ns)
	GPIOB->BSRR = F_WR;

	write_port(Address_Flash_Page);             //send lower order 8 bit page address to port P1
	GPIOB->BSRR = (uint32_t)F_WR << 16;			//intiate write signal
	short_delay_us(1);  // WE pulse width minimum (25ns)
	GPIOB->BSRR = F_WR;

	write_port(Address_Flash_Page >>8);	        //send higher order 8 bit page address to port P1
	GPIOB->BSRR = (uint32_t)F_WR << 16;			//initiate the write signal
	short_delay_us(1);  // WE pulse width minimum (25ns)
	GPIOB->BSRR = F_WR;

	write_port(0x00);				            //send address 0x00 to port P1
	GPIOB->BSRR = (uint32_t)F_WR << 16;			//intiate the write signal
	short_delay_us(1);  // WE pulse width minimum (25ns)
	GPIOB->BSRR = F_WR;
	GPIOB->BSRR = (uint32_t)F_ALE << 16;			//disable addresss latch enable which indicates
	write_port(0xFF);				            // the end of of the address write
	Configure_GPIO_IO_D2(Input);			        //change the mode of port P1 as input port so that the
	                                            //controller is now ready to recieve data from the port p1
	short_delay_us(5000);				                //call delay function
	for(uint16_t x = 0;	x<512;	x++)                    //if data counter(x)<512 then
	{
		GPIOB->BSRR = (uint32_t)F_RD << 16;		//activate the read enable signal of flash
		short_delay_us(1);  // RE pulse width minimum (25ns) + data access time
		*TempStorage = Read_port();
		GPIOB->BSRR = F_RD;		//disable read enable
		short_delay_us(1);  // RE hold time minimum (15ns)
		TempStorage++;			                            //increment XRAM pointer by one
	}
}
//****************************************************************************
//POST_READ_FLASH
//****************************************************************************
void post_read_flash(CartridgeID id)
{
	GPIOC->BSRR = F_CE; 			           //disable flash chip
	GPIOB->BSRR = (uint32_t)F_ALE << 16;		                   //disable address latch enable of flash
	GPIOB->BSRR = F_WP;				           //disable write protect of flash
	GPIOB->BSRR = F_RD;			   //disable read enable of flash
	GPIOB->BSRR = F_WR;				           //disable write of flash
	GPIOC->BSRR = (uint32_t)F_CLE << 16;				           //disable command latch enable of flash

	Configure_GPIO_IO_D2(Input);
}



void write_port(uint8_t data)
{
	if(data & 0x80) GPIOA->BSRR = M_D7; else GPIOA->BSRR = (uint32_t)M_D7 << 16;
	if(data & 0x40) GPIOC->BSRR = M_D6; else GPIOC->BSRR = (uint32_t)M_D6 << 16;
	if(data & 0x20) GPIOA->BSRR = M_D5; else GPIOA->BSRR = (uint32_t)M_D5 << 16;
	if(data & 0x10) GPIOC->BSRR = M_D4; else GPIOC->BSRR = (uint32_t)M_D4 << 16;
	if(data & 0x08) GPIOC->BSRR = M_D3; else GPIOC->BSRR = (uint32_t)M_D3 << 16;
	if(data & 0x04) GPIOD->BSRR = M_D2; else GPIOD->BSRR = (uint32_t)M_D2 << 16;
	if(data & 0x02) GPIOE->BSRR = M_D1; else GPIOE->BSRR = (uint32_t)M_D1 << 16;
	if(data & 0x01) GPIOB->BSRR = M_D0; else GPIOB->BSRR = (uint32_t)M_D0 << 16;
}

uint8_t Read_port()
{
  uint8_t ret =
  ((GPIOA->IDR & M_D7) ? 0x80 : 0) |
  ((GPIOC->IDR & M_D6) ? 0x40 : 0) |
  ((GPIOA->IDR & M_D5) ? 0x20 : 0) |
  ((GPIOC->IDR & M_D4) ? 0x10 : 0) |
  ((GPIOC->IDR & M_D3) ? 0x08 : 0) |
  ((GPIOD->IDR & M_D2) ? 0x04 : 0) |
  ((GPIOE->IDR & M_D1) ? 0x02 : 0) |
  ((GPIOB->IDR & M_D0) ? 0x01 : 0);

  return ret;
}

void Configure_GPIO_IO_D2(enum pinConfiuration io)
{
	GPIO_InitTypeDef GPIO_InitStruct = {0};

  switch(io)
  {
   case 0 :

	   GPIO_InitStruct.Pin = C2DB2_C3DB2_Pin;
       GPIO_InitStruct.Mode = GPIO_MODE_INPUT;
       GPIO_InitStruct.Pull = GPIO_NOPULL;
       HAL_GPIO_Init(GPIOD, &GPIO_InitStruct);

       GPIO_InitStruct.Pin  = C2DB4_C3DB4_Pin|C2DB3_C3DB3_Pin|C2DB6_C3DB6_Pin;
       GPIO_InitStruct.Mode = GPIO_MODE_INPUT;
       GPIO_InitStruct.Pull = GPIO_NOPULL;
       HAL_GPIO_Init(GPIOC, &GPIO_InitStruct);

       GPIO_InitStruct.Pin  = C2DB7_C3DB7_INOUT_Pin| C2DB5_C3DB5_INOUT_Pin;
       GPIO_InitStruct.Mode = GPIO_MODE_INPUT;
       GPIO_InitStruct.Pull = GPIO_NOPULL;
       HAL_GPIO_Init(GPIOA, &GPIO_InitStruct);

       GPIO_InitStruct.Pin  = C2DB1_C3DB1_Pin;
       GPIO_InitStruct.Mode = GPIO_MODE_INPUT;
       GPIO_InitStruct.Pull = GPIO_NOPULL;
       HAL_GPIO_Init(GPIOE, &GPIO_InitStruct);

       GPIO_InitStruct.Pin  = C2DB0_C3DB0_Pin;
       GPIO_InitStruct.Mode = GPIO_MODE_INPUT;
       GPIO_InitStruct.Pull = GPIO_NOPULL;
       HAL_GPIO_Init(GPIOB, &GPIO_InitStruct);


    break;

   case 1:

	   GPIO_InitStruct.Pin = C2DB2_C3DB2_Pin;
	   GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
	   GPIO_InitStruct.Pull = GPIO_NOPULL;
	   GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
	   HAL_GPIO_Init(GPIOD, &GPIO_InitStruct);

	   GPIO_InitStruct.Pin  = C2DB4_C3DB4_Pin|C2DB3_C3DB3_Pin|C2DB6_C3DB6_Pin;
	   GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
	   GPIO_InitStruct.Pull = GPIO_NOPULL;
	   GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
	   HAL_GPIO_Init(GPIOC, &GPIO_InitStruct);

	   GPIO_InitStruct.Pin  = C2DB7_C3DB7_INOUT_Pin| C2DB5_C3DB5_INOUT_Pin;
	   GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
	   GPIO_InitStruct.Pull = GPIO_NOPULL;
	   GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
	   HAL_GPIO_Init(GPIOA, &GPIO_InitStruct);

	   GPIO_InitStruct.Pin  = C2DB1_C3DB1_Pin;
	   GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
	   GPIO_InitStruct.Pull = GPIO_NOPULL;
	   GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
	   HAL_GPIO_Init(GPIOE, &GPIO_InitStruct);

	   GPIO_InitStruct.Pin  = C2DB0_C3DB0_Pin;
	   GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
	   GPIO_InitStruct.Pull = GPIO_NOPULL;
	   GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
	   HAL_GPIO_Init(GPIOB, &GPIO_InitStruct);

	   break;
  }
}

void pre_erase_flash(CartridgeID id)
{
	Configure_GPIO_IO_D2(Output);    //port P1 is declared as output port

	GPIOB->BSRR = (uint32_t)F_ALE << 16;		             //disable address latch enable pin of flash
	GPIOB->BSRR = F_WP;	                 //disable write protect pin of flash
	GPIOB->BSRR = F_RD;		 //disable output enable pin of flash
	GPIOB->BSRR = F_WR;	                 //disable write enable pin of flash
	GPIOC->BSRR = (uint32_t)F_CLE << 16;	                 //disable command latch enable pin of flash
	GPIOC->BSRR = (uint32_t)F_CE << 16;		 //activate the flash chip

	GPIO_InitTypeDef GPIO_InitStruct = {0};
	GPIO_InitStruct.Pin = C2RB0_Pin;
	GPIO_InitStruct.Mode = GPIO_MODE_INPUT;
	GPIO_InitStruct.Pull = GPIO_NOPULL;
	HAL_GPIO_Init(GPIOA, &GPIO_InitStruct);

}

unsigned char flash_device_ID(CartridgeID id)
{
	GPIOC->BSRR = F_CLE;		   //activate the command latch enable
	short_delay_us(1000);
	write_port(0x90);					       //send read command 0x80 to port p1
	GPIOB->BSRR = (uint32_t)F_WR << 16;		   //write the write command into the flash
	short_delay_us(1000);
	GPIOB->BSRR = F_WR;   	   //so that write command is intiated
	GPIOC->BSRR = (uint32_t)F_CLE << 16;	       //disable command latch enable
	short_delay_us(1000);

	GPIOB->BSRR = F_ALE;		   //activate address latch enable signal of flash
	short_delay_us(1000);

	write_port((0x00) & 0xFF);            //send lower order 8 bit page address to port P1
	GPIOB->BSRR = (uint32_t)F_WR << 16;		   //intiate write signal
	short_delay_us(1000);
	GPIOB->BSRR = F_WR;

	GPIOB->BSRR = (uint32_t)F_ALE << 16;		   //activate address latch enable signal of flash


	Configure_GPIO_IO_D2(Input);
	short_delay_us(1000);
	GPIOB->BSRR = (uint32_t)F_RD << 16;
	short_delay_us(1000);
	unsigned char result = Read_port();
	GPIOB->BSRR = F_RD;
	short_delay_us(1000);

	GPIOB->BSRR = (uint32_t)F_RD << 16;
	short_delay_us(1000);
	result = Read_port();
	GPIOB->BSRR = F_RD;
	short_delay_us(1000);

	return result;

}
//****************************************************************************
//FLASH ERASE
//****************************************************************************

unsigned char flash_erase(uint16_t Address_Flash_Page,CartridgeID id)
{
	unsigned char result = 0x00;
	unsigned char answer;

    GPIOC->BSRR = F_CLE;		   //activate the command latch enable
    short_delay_us(1);  // CLE setup time
    write_port(0x60);					       //send read command 0x80 to port p1
    GPIOB->BSRR = (uint32_t)F_WR << 16;		   //write the write command into the flash
    short_delay_us(1);  // WE pulse width minimum (25ns)
    GPIOB->BSRR = F_WR;   	   //so that write command is intiated
    GPIOC->BSRR = (uint32_t)F_CLE << 16;	       //disable command latch enable

	GPIOB->BSRR = F_ALE;		   //activate address latch enable signal of flash
	short_delay_us(1000);

	write_port((Address_Flash_Page) & 0xFF);            //send lower order 8 bit page address to port P1
	GPIOB->BSRR = (uint32_t)F_WR << 16;		   //intiate write signal
	GPIOB->BSRR = F_WR;
	short_delay_us(1000);

	write_port((Address_Flash_Page >>8) & 0xFF);        //send higher order 8 bit page address to port P1
	GPIOB->BSRR = (uint32_t)F_WR << 16;		   //initiate the write signal
	GPIOB->BSRR = F_WR;
	short_delay_us(1000);

	write_port(0x00);				           //send address 0x00 to port P1
	GPIOB->BSRR = (uint32_t)F_WR << 16;		   //initiate the write signal
	GPIOB->BSRR = F_WR;
	short_delay_us(1000);

	GPIOB->BSRR = (uint32_t)F_ALE << 16;


	GPIOC->BSRR = F_CLE;
	short_delay_us(1000);
	write_port(0xD0);
	GPIOB->BSRR = (uint32_t)F_WR << 16;
    GPIOB->BSRR = F_WR;
    GPIOC->BSRR = (uint32_t)F_CLE << 16;
    short_delay_us(1000);

    //rdy
    while((GPIOA->IDR & F_RDY) == 0);

    write_port(0x70);
    short_delay_us(1000);
    GPIOC->BSRR = F_CLE;
    GPIOB->BSRR = (uint32_t)F_WR << 16;
    short_delay_us(1000);
    GPIOB->BSRR = F_WR;
    GPIOC->BSRR = (uint32_t)F_CLE << 16;

    Configure_GPIO_IO_D2(Input);
    GPIOB->BSRR = (uint32_t)F_RD << 16;
    short_delay_us(1000);
    result = Read_port();
    GPIOB->BSRR = F_RD;
    Configure_GPIO_IO_D2(Output);

    if((result & 0x01)==0x01)
    {
       answer = 0xFF; //error
    }
    else
    {
      answer = 0x00; //success
    }
    return answer;
}

void post_erase_flash(CartridgeID id)			 //function deactivate control lines of flash
 {
	GPIOC->BSRR = F_CE; 			    //disable flash chip
	GPIOB->BSRR = (uint32_t)F_ALE << 16;				//disable address latch enable of flash
	GPIOB->BSRR = F_WP;				//disable write protect of flash
	GPIOB->BSRR = F_RD;	            //disable read enable of flash
	GPIOB->BSRR = F_WR;				//disable write of flash
	GPIOC->BSRR = (uint32_t)F_CLE << 16;				//disable command latch enable of flash

	Configure_GPIO_IO_D2(Input);
 }

void setGreenLed(CartridgeID id, uint8_t value)
{
	if(value ==1)
		GPIOB->BSRR = get_D2_Green_LedPins(id);
	else
		GPIOB->BSRR = (uint32_t)get_D2_Green_LedPins(id) << 16;
}
void setRedLed(CartridgeID id, uint8_t value)
{
	if(value ==1)
		GPIOB->BSRR = get_D2_Red_LedPins(id);
	else
		GPIOB->BSRR = (uint32_t)get_D2_Red_LedPins(id) << 16;
}

uint16_t get_ce_pin(CartridgeID id) {
    return CE_PINS[id];
}

uint16_t get_rdy_pin(CartridgeID id) {
    return RDY_PINS[id];
}

uint16_t get_slt_pin(CartridgeID id) {
    return SLT_PINS[id];
}

uint16_t get_D2_slt_status(CartridgeID id) {
    return SLT_STATUS[id];
}

uint16_t get_D1_slt_status(CartridgeID id) {
    return 0;
}

uint16_t get_D2_Green_LedPins(CartridgeID id) {
    return GREEN_LED[id];
}

uint16_t get_D2_Red_LedPins(CartridgeID id) {
    return RED_LED[id];
}

void UpdateD2SlotStatus()
{
	GPIO_InitTypeDef GPIO_InitStruct = {0};

		GPIO_InitStruct.Pin =  C2SLTS1_Pin;
		GPIO_InitStruct.Mode = GPIO_MODE_INPUT;
		       GPIO_InitStruct.Pull = GPIO_PULLUP;
			   HAL_GPIO_Init(GPIOD, &GPIO_InitStruct);

	if((GPIOD->IDR & get_slt_pin(CARTRIDGE_1)) == 0)
	{
		SLT_STATUS[0] = 0x02;SLT_STATUS[1] = 0x02;SLT_STATUS[2] = 0x02;SLT_STATUS[3] = 0x02;
	}
	else
		{
			SLT_STATUS[0] = 0x00;SLT_STATUS[1] = 0x00;SLT_STATUS[2] = 0x00;SLT_STATUS[3] = 0x00;
		}
}

void short_delay_us(uint32_t us) {
    for (volatile uint32_t i = 0; i < us * 8; i++) {
        __NOP();  // One NOP ~1 cycle
    }
}
