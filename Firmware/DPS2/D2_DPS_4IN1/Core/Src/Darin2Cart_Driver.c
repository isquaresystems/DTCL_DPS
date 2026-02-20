/**
 ******************************************************************************
 * @file    Darin2Cart_Driver.c
 * @brief   Darin-II NAND Flash Cartridge Driver Implementation for DPS2 4-in-1
 * @version 1.0
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

/* Private Defines -----------------------------------------------------------*/

/* NAND Flash Control Pins */
#define F_CLE CLE_Pin      /* Command Latch Enable */
#define F_ALE ALE_Pin      /* Address Latch Enable */
#define F_WP  WP_Pin       /* Write Protect */
#define F_WR  WE_Pin       /* Write Enable */
#define F_RD  RE_Pin       /* Read Enable */

/* Private Variables ---------------------------------------------------------*/

/* NAND Flash Data Bus Pins (8-bit) */
uint16_t M_D7  = D07_Pin;  /* Data bit 7 */
uint16_t M_D6  = DB6_Pin;  /* Data bit 6 */
uint16_t M_D5  = DB5_Pin;  /* Data bit 5 */
uint16_t M_D4  = DB4_Pin;  /* Data bit 4 */
uint16_t M_D3  = DB3_Pin;  /* Data bit 3 */
uint16_t M_D2  = DB2_Pin;  /* Data bit 2 */
uint16_t M_D1  = DB1_Pin;  /* Data bit 1 */
uint16_t M_D0  = DB0_Pin;  /* Data bit 0 */

/* Cartridge Slot Pin Mappings - 4 slots */
static const uint16_t CE_PINS[]    = { CE1_Pin, CE2_Pin, CE3_Pin, CE4_Pin };        /* Chip Enable pins */
static const uint16_t RDY_PINS[]   = { RB1_Pin, RB2_Pin, RB3_Pin, RB4_Pin };        /* Ready/Busy pins */
static const uint16_t SLT_PINS[]   = { SLT_S1_Pin, SLT_S2_Pin, SLT_S3_Pin, SLT_S4_Pin };  /* Slot status pins */
static const uint16_t GREEN_LED[]  = { LED1_Pin, LED3_Pin, LED5_Pin, LED7_Pin };     /* Green LED pins */
static const uint16_t RED_LED[]    = { LED2_Pin, LED4_Pin, LED6_Pin, LED8_Pin };     /* Red LED pins */

/* Slot Status Tracking */
static uint8_t SLT_STATUS[] = { 1, 1, 1, 1 };

/* Private Function Prototypes -----------------------------------------------*/
static uint16_t get_ce_pin(CartridgeID id);
static uint16_t get_rdy_pin(CartridgeID id);
static uint16_t get_slt_pin(CartridgeID id);

/* Private Functions ---------------------------------------------------------*/

/**
 * @brief  Get Chip Enable pin for specified cartridge
 * @param  id: Cartridge identifier
 * @retval Pin number
 */
static uint16_t get_ce_pin(CartridgeID id)
{
    return CE_PINS[id];
}

/**
 * @brief  Get Ready/Busy pin for specified cartridge
 * @param  id: Cartridge identifier
 * @retval Pin number
 */
static uint16_t get_rdy_pin(CartridgeID id)
{
    return RDY_PINS[id];
}

/**
 * @brief  Get Slot Status pin for specified cartridge
 * @param  id: Cartridge identifier
 * @retval Pin number
 */
static uint16_t get_slt_pin(CartridgeID id)
{
    return SLT_PINS[id];
}

/* Public Functions ----------------------------------------------------------*/

/**
 * @brief  Get slot status for specified cartridge
 * @param  id: Cartridge identifier
 * @retval Slot status value
 */
uint16_t get_D2_slt_status(CartridgeID id)
{
    return SLT_STATUS[id];
}

/**
 * @brief  Get Green LED pin for specified cartridge
 * @param  id: Cartridge identifier
 * @retval LED pin number
 */
uint16_t get_D2_Green_LedPins(CartridgeID id)
{
    return GREEN_LED[id];
}

/**
 * @brief  Get Red LED pin for specified cartridge
 * @param  id: Cartridge identifier
 * @retval LED pin number
 */
uint16_t get_D2_Red_LedPins(CartridgeID id)
{
    return RED_LED[id];
}

/**
 * @brief  Update cartridge slot status for all slots
 * @note   Reads slot pins and updates SLT_STATUS array
 *         0x02 = Cartridge present, 0x00 = No cartridge
 * @retval None
 */
void UpdateD2SlotStatus(void)
{
    for (int itr = 0; itr < 4; itr++)
    {
        if (0 == HAL_GPIO_ReadPin(GPIOC, get_slt_pin(itr)))
        {
            SLT_STATUS[itr] = 0x02;  /* Cartridge detected */
        }
        else
        {
            SLT_STATUS[itr] = 0x00;  /* No cartridge */
        }
    }
}


/**
 * @brief  Initialize flash for write operation
 * @param  id: Cartridge slot identifier
 * @retval None
 */
void pre_write_flash(CartridgeID id)
{

    /* Configure data bus as output */
    Configure_DataBus(1);

    /* Initialize control signals */
    HAL_GPIO_WritePin(GPIOD, F_ALE, 0);            /* Disable Address Latch Enable */
    HAL_GPIO_WritePin(GPIOD, F_WP,  1);            /* Disable Write Protect */
    HAL_GPIO_WritePin(GPIOD, F_RD,  1);            /* Disable Read Enable */
    HAL_GPIO_WritePin(GPIOD, F_WR,  1);            /* Disable Write Enable */
    HAL_GPIO_WritePin(GPIOD, F_CLE, 0);            /* Disable Command Latch Enable */
    HAL_GPIO_WritePin(GPIOD, get_ce_pin(id), 0);   /* Activate flash chip */
}

/**
 * @brief  Write data to NAND flash memory
 * @param  TempStorage: Pointer to data buffer
 * @param  dataLength: Number of bytes to write
 * @param  Address_Flash_Page: Page address in flash
 * @param  id: Cartridge slot identifier
 * @retval None
 */
void flash_write(const uint8_t* TempStorage, uint16_t dataLength, uint16_t Address_Flash_Page,CartridgeID id)
{
    HAL_GPIO_WritePin(GPIOD, F_CLE, 1);		   //activate the command latch enable
    write_port2(0x80);					       //send read command 0x80 to port p1
    HAL_GPIO_WritePin(GPIOD, F_WR, 0);		   //write the write command into the flash
    HAL_GPIO_WritePin(GPIOD, F_WR, 1);   	   //so that write command is intiated
    HAL_GPIO_WritePin(GPIOD, F_CLE, 0);	       //disable command latch enable

    write_port2(0x00);					       //send address 0x00 to port P1
	HAL_GPIO_WritePin(GPIOD, F_ALE, 1);		   //activate address latch enable signal of flash

	write_port2(0x00);					       //send address 0x00 to port P1

	HAL_GPIO_WritePin(GPIOD, F_WR, 0);  	   //intiate write signal
	HAL_GPIO_WritePin(GPIOD, F_WR, 1);

	write_port2(Address_Flash_Page);            //send lower order 8 bit page address to port P1

	HAL_GPIO_WritePin(GPIOD, F_WR, 0);		   //intiate write signal
	HAL_GPIO_WritePin(GPIOD, F_WR, 1);

	write_port2(Address_Flash_Page >>8);        //send higher order 8 bit page address to port P1
	HAL_GPIO_WritePin(GPIOD, F_WR, 0);		   //initiate the write signal
	HAL_GPIO_WritePin(GPIOD, F_WR, 1);

	write_port2(0x00);				           //send address 0x00 to port P1
	HAL_GPIO_WritePin(GPIOD, F_WR, 0);		   //initiate the write signal
	HAL_GPIO_WritePin(GPIOD, F_WR, 1);

	write_port2(0xFF);  				           //intially set the bits of port P1
	HAL_GPIO_WritePin(GPIOD, F_ALE, 0);		   //disable address latch enable which indicates the end of write command for flash
	short_delay_us(1000);					           //wait for some delay

	int x=0;
    for(x = 0;	x<dataLength;	x++)           //if data counter 'x'<	last_page_size then
	{

		write_port2(*TempStorage);                   //fetch the data from the XRAM loction
	   	HAL_GPIO_WritePin(GPIOD, F_WR, 0);			//enable write signal of flash
	   	HAL_GPIO_WritePin(GPIOD, F_WR, 1);			//disable write signal of flash
	   	TempStorage++;				                //increment the pointer of XRAM
	}

    if(dataLength < 512)	//if last page size is less than 512 then
    {
    for(x=0;x<(512-dataLength);x++)//fill the remaining by data 0xFF
      {
    	write_port2(0xFF);			                //send data 0xFF through port P1
		HAL_GPIO_WritePin(GPIOD, F_WR, 0); 			//enable write signal
		HAL_GPIO_WritePin(GPIOD, F_WR, 1);			//disable write signal
      }
    }

    short_delay_us(1000);					                //call delay function

    HAL_GPIO_WritePin(GPIOD, F_CLE, 1);				//enable command latch enable
    write_port2(0x10);			                    //initiate write command to flash so that the data from flash buffer

    HAL_GPIO_WritePin(GPIOD, F_WR, 0);				//enable write signal of flash
    HAL_GPIO_WritePin(GPIOD, F_WR, 1);   			//disable write signal
    HAL_GPIO_WritePin(GPIOD, F_CLE, 0); 			//disable command latch enable
    short_delay_us(1000);					                //call delay function
}
//****************************************************************************
//POST FLASH WRITE
//****************************************************************************

void post_write_flash(CartridgeID id)
{
   HAL_GPIO_WritePin(GPIOD, get_ce_pin(id),  1); 			    //disable flash chip
   HAL_GPIO_WritePin(GPIOD, F_ALE, 0);				//disable address latch enable of flash
   HAL_GPIO_WritePin(GPIOD, F_WP,  1);				//disable write protect of flash
   HAL_GPIO_WritePin(GPIOD, F_RD,  1);	            //disable read enable of flash
   HAL_GPIO_WritePin(GPIOD, F_WR,  1);				//disable write of flash
   HAL_GPIO_WritePin(GPIOD, F_CLE, 0);				//disable command latch enable of flash

   Configure_DataBus(0);
}


void pre_read_flash(CartridgeID id)
{
	Configure_DataBus(1);                             //port P1 is declared as output port

	HAL_GPIO_WritePin(GPIOD, F_ALE, 0);		               //disable address latch enable pin of flash
	HAL_GPIO_WritePin(GPIOD, F_WP,  1);	                   //disable write protect pin of flash
	HAL_GPIO_WritePin(GPIOD, F_RD,  1);		               //disable output enable pin of flash
	HAL_GPIO_WritePin(GPIOD, F_WR,  1);	                   //disable write enable pin of flash
	HAL_GPIO_WritePin(GPIOD, F_CLE, 0);				       //disable command latch enable pin of flash
	HAL_GPIO_WritePin(GPIOD, get_ce_pin(id),  0);		               //activate the flash chip
}

//****************************************************************************
//POST_READ_FLASH
//****************************************************************************
void post_read_flash(CartridgeID id)
{
	HAL_GPIO_WritePin(GPIOD, get_ce_pin(id), 1); 			           //disable flash chip
	HAL_GPIO_WritePin(GPIOD, F_ALE, 0);		                   //disable address latch enable of flash
	HAL_GPIO_WritePin(GPIOD, F_WP, 1);				           //disable write protect of flash
	HAL_GPIO_WritePin(GPIOD, F_RD, 1);			   //disable read enable of flash
	HAL_GPIO_WritePin(GPIOD, F_WR, 1);				           //disable write of flash
	HAL_GPIO_WritePin(GPIOD, F_CLE, 0);				           //disable command latch enable of flash

	Configure_DataBus(0);
}


//****************************************************************************
//READ_FLASH
//****************************************************************************

void flash_read(uint8_t *TempStorage, uint16_t dataLength, uint16_t Address_Flash_Page, CartridgeID id)
{

	HAL_GPIO_WritePin(GPIOD, F_CLE, 1);			//activate the command latch enable

	write_port2(0x00);				            //send read command 0x00 to port p1
	HAL_GPIO_WritePin(GPIOD, F_WR, 0);			//write the read command into the flash
	HAL_GPIO_WritePin(GPIOD, F_WR, 1);   		//so that read command is intiated

	HAL_GPIO_WritePin(GPIOD, F_CLE, 0);		    //diable command latch enable
	HAL_GPIO_WritePin(GPIOD, F_ALE, 1);			//activate address latch enable signal of flash

	write_port2(0x00);			                //send address 0x00 to port P1
	HAL_GPIO_WritePin(GPIOD, F_WR, 0);  		//intiate write signal
	HAL_GPIO_WritePin(GPIOD, F_WR, 1);

	write_port2(Address_Flash_Page);             //send lower order 8 bit page address to port P1
	HAL_GPIO_WritePin(GPIOD, F_WR, 0);			//intiate write signal
	HAL_GPIO_WritePin(GPIOD, F_WR, 1);

	write_port2(Address_Flash_Page >>8);	        //send higher order 8 bit page address to port P1
	HAL_GPIO_WritePin(GPIOD, F_WR, 0);			//initiate the write signal
	HAL_GPIO_WritePin(GPIOD, F_WR, 1);

	write_port2(0x00);				            //send address 0x00 to port P1
	HAL_GPIO_WritePin(GPIOD, F_WR, 0);			//intiate the write signal
	HAL_GPIO_WritePin(GPIOD, F_WR, 1);
	HAL_GPIO_WritePin(GPIOD, F_ALE, 0);			//disable addresss latch enable which indicates
	write_port2(0xFF);				            // the end of of the address write
	Configure_DataBus(0);			        //change the mode of port P1 as input port so that the
	                                            //controller is now ready to recieve data from the port p1
	short_delay_us(20000);				                //call delay function
	for(uint16_t x = 0;	x<512;	x++)                    //if data counter(x)<512 then
	{
		HAL_GPIO_WritePin(GPIOD, F_RD, 0);		//activate the read enable signal of flash
		*TempStorage = Read_port2();
		HAL_GPIO_WritePin(GPIOD, F_RD, 1);		//disable read enable
		TempStorage++;			                            //increment XRAM pointer by one
	}
}



/**
 * @brief  Write 8-bit data to the NAND flash data bus
 * @param  data: 8-bit data to write
 * @retval None
 */
void write_port2(uint8_t data)
{
    HAL_GPIO_WritePin(GPIOE, M_D7, ((data & 0x80) >> 7));
    HAL_GPIO_WritePin(GPIOE, M_D6, ((data & 0x40) >> 6));
    HAL_GPIO_WritePin(GPIOE, M_D5, ((data & 0x20) >> 5));
    HAL_GPIO_WritePin(GPIOE, M_D4, ((data & 0x10) >> 4));
    HAL_GPIO_WritePin(GPIOE, M_D3, ((data & 0x08) >> 3));
    HAL_GPIO_WritePin(GPIOE, M_D2, ((data & 0x04) >> 2));
    HAL_GPIO_WritePin(GPIOE, M_D1, ((data & 0x02) >> 1));
    HAL_GPIO_WritePin(GPIOE, M_D0, (data & 0x01));
}

/**
 * @brief  Read 8-bit data from the NAND flash data bus
 * @retval 8-bit data read from the bus
 */
uint8_t Read_port2(void)
{
    uint8_t ret =
        (HAL_GPIO_ReadPin(GPIOE, M_D7) << 7) |
        (HAL_GPIO_ReadPin(GPIOE, M_D6) << 6) |
        (HAL_GPIO_ReadPin(GPIOE, M_D5) << 5) |
        (HAL_GPIO_ReadPin(GPIOE, M_D4) << 4) |
        (HAL_GPIO_ReadPin(GPIOE, M_D3) << 3) |
        (HAL_GPIO_ReadPin(GPIOE, M_D2) << 2) |
        (HAL_GPIO_ReadPin(GPIOE, M_D1) << 1) |
        (HAL_GPIO_ReadPin(GPIOE, M_D0));

    return ret;
}

void Configure_DataBus(int io)
{
  switch(io)
  {
   case 0 :
	   GPIOE->MODER &= ~0x0000FFFF;
    break;

   case 1:
	   GPIOE->MODER = (GPIOE->MODER & ~0x0000FFFF) | 0x00005555;
	   break;
  }
}

void pre_erase_flash(CartridgeID id)
{
	Configure_DataBus(1);                             //port P1 is declared as output port

	HAL_GPIO_WritePin(GPIOD, F_ALE, 0);		             //disable address latch enable pin of flash
	HAL_GPIO_WritePin(GPIOD, F_WP,  1);	                 //disable write protect pin of flash
	HAL_GPIO_WritePin(GPIOD, F_RD,  1);		             //disable output enable pin of flash
	HAL_GPIO_WritePin(GPIOD, F_WR,  1);	                 //disable write enable pin of flash
	HAL_GPIO_WritePin(GPIOD, F_CLE, 0);	                 //disable command latch enable pin of flash
	HAL_GPIO_WritePin(GPIOD, get_ce_pin(id),  0);		 //activate the flash chip

}

unsigned char flash_device_ID(CartridgeID id)
{
	HAL_GPIO_WritePin(GPIOD, F_CLE, 1);		   //activate the command latch enable
	short_delay_us(1000);
	write_port2(0x90);					       //send read command 0x80 to port p1
	HAL_GPIO_WritePin(GPIOD, F_WR, 0);		   //write the write command into the flash
	short_delay_us(1000);
	HAL_GPIO_WritePin(GPIOD, F_WR, 1);   	   //so that write command is intiated
	HAL_GPIO_WritePin(GPIOD, F_CLE, 0);	       //disable command latch enable
	short_delay_us(1000);

	HAL_GPIO_WritePin(GPIOD, F_ALE, 1);		   //activate address latch enable signal of flash
	short_delay_us(1000);

	write_port2((0x00) & 0xFF);                //send lower order 8 bit page address to port P1
	HAL_GPIO_WritePin(GPIOD, F_WR, 0);		   //intiate write signal
	short_delay_us(1000);
	HAL_GPIO_WritePin(GPIOD, F_WR, 1);

	HAL_GPIO_WritePin(GPIOD, F_ALE, 0);		   //activate address latch enable signal of flash


	Configure_DataBus(0);
	short_delay_us(1000);
	HAL_GPIO_WritePin(GPIOD, F_RD, 0);
	short_delay_us(1000);
	unsigned char result = Read_port2();
	HAL_GPIO_WritePin(GPIOD, F_RD, 1);
	short_delay_us(1000);

	HAL_GPIO_WritePin(GPIOD, F_RD, 0);
	short_delay_us(1000);
	result = Read_port2();
	HAL_GPIO_WritePin(GPIOD, F_RD, 1);
	short_delay_us(1000);

	return result;

}
//****************************************************************************
//FLASH ERASE
//****************************************************************************

unsigned char flash_erase(uint16_t Address_Flash_Page, CartridgeID id)
{
	unsigned char result = 0x00;
	unsigned char answer;
	uint32_t timeout_count = 0;
	const uint32_t timeout_limit = 5000; // 5000 iterations × 1000us = 5 seconds

    HAL_GPIO_WritePin(GPIOD, F_CLE, 1);		             //activate the command latch enable
    write_port2(0x60);					                 //send read command 0x80 to port p1
    HAL_GPIO_WritePin(GPIOD, F_WR, 0);		             //write the write command into the flash
    HAL_GPIO_WritePin(GPIOD, F_WR, 1);   	             //so that write command is intiated
    HAL_GPIO_WritePin(GPIOD, F_CLE, 0);	                 //disable command latch enable

	HAL_GPIO_WritePin(GPIOD, F_ALE, 1);		             //activate address latch enable signal of flash
	short_delay_us(1000);

	write_port2((Address_Flash_Page) & 0xFF);            //send lower order 8 bit page address to port P1
	HAL_GPIO_WritePin(GPIOD, F_WR, 0);		             //intiate write signal
	HAL_GPIO_WritePin(GPIOD, F_WR, 1);
	short_delay_us(1000);

	write_port2((Address_Flash_Page >>8) & 0xFF);        //send higher order 8 bit page address to port P1
	HAL_GPIO_WritePin(GPIOD, F_WR, 0);		             //initiate the write signal
	HAL_GPIO_WritePin(GPIOD, F_WR, 1);
	short_delay_us(1000);

	write_port2(0x00);				                     //send address 0x00 to port P1
	HAL_GPIO_WritePin(GPIOD, F_WR, 0);		             //initiate the write signal
	HAL_GPIO_WritePin(GPIOD, F_WR, 1);
	short_delay_us(1000);

	HAL_GPIO_WritePin(GPIOD, F_ALE, 0);


	HAL_GPIO_WritePin(GPIOD, F_CLE, 1);
	short_delay_us(1000);
	write_port2(0xD0);
	HAL_GPIO_WritePin(GPIOD, F_WR, 0);
    HAL_GPIO_WritePin(GPIOD, F_WR, 1);
    HAL_GPIO_WritePin(GPIOD, F_CLE, 0);
    short_delay_us(1000);

    //rdy - with 5 second timeout
    while(HAL_GPIO_ReadPin(GPIOC, get_rdy_pin(id)) != 1)
    {
        short_delay_us(1000);  // 1ms delay per iteration
        timeout_count++;
        if(timeout_count >= timeout_limit)
        {
            return 0xFF;  // timeout error
			//break;
        }
    }

    write_port2(0x70);
    short_delay_us(1000);
    HAL_GPIO_WritePin(GPIOD, F_CLE, 1);
    HAL_GPIO_WritePin(GPIOD, F_WR, 0);
    short_delay_us(1000);
    HAL_GPIO_WritePin(GPIOD, F_WR, 1);
    HAL_GPIO_WritePin(GPIOD, F_CLE, 0);

    Configure_DataBus(0);
    HAL_GPIO_WritePin(GPIOD, F_RD, 0);
    short_delay_us(1000);
    result = Read_port2();
    HAL_GPIO_WritePin(GPIOD, F_RD, 1);
    Configure_DataBus(1);

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

void post_erase_flash(CartridgeID id)			    //function deactivate control lines of flash
 {
	HAL_GPIO_WritePin(GPIOD, get_ce_pin(id),  1); 	//disable flash chip
	HAL_GPIO_WritePin(GPIOD, F_ALE, 0);				//disable address latch enable of flash
	HAL_GPIO_WritePin(GPIOD, F_WP,  1);				//disable write protect of flash
	HAL_GPIO_WritePin(GPIOD, F_RD,  1);	            //disable read enable of flash
	HAL_GPIO_WritePin(GPIOD, F_WR,  1);				//disable write of flash
	HAL_GPIO_WritePin(GPIOD, F_CLE, 0);				//disable command latch enable of flash

	Configure_DataBus(0);
 }

/**
 * @brief  Microsecond delay function using NOP instructions
 * @param  us: Delay time in microseconds (approximate)
 * @note   Timing depends on CPU clock frequency
 * @retval None
 */
void short_delay_us(uint32_t us)
{
    for (volatile uint32_t i = 0; i < us * 8; i++)
    {
        __NOP();  /* One NOP ~1 cycle at system clock */
    }
}

/**
 * @brief  Control green LED for specified cartridge
 * @param  id: Cartridge identifier
 * @param  value: 1 to turn ON, 0 to turn OFF
 * @retval None
 */
void setGreenLed(CartridgeID id, uint8_t value)
{
    if (value == 1)
    {
        HAL_GPIO_WritePin(GPIOA, get_D2_Green_LedPins(id), GPIO_PIN_SET);
    }
    else
    {
        HAL_GPIO_WritePin(GPIOA, get_D2_Green_LedPins(id), GPIO_PIN_RESET);
    }
}
/**
 * @brief  Control red LED for specified cartridge
 * @param  id: Cartridge identifier
 * @param  value: 1 to turn ON, 0 to turn OFF
 * @retval None
 */
void setRedLed(CartridgeID id, uint8_t value)
{
    if (value == 1)
    {
        HAL_GPIO_WritePin(GPIOA, get_D2_Red_LedPins(id), GPIO_PIN_SET);
    }
    else
    {
        HAL_GPIO_WritePin(GPIOA, get_D2_Red_LedPins(id), GPIO_PIN_RESET);
    }
}

/**
 * @brief  Blink both LEDs for specified cartridge
 * @param  id: Cartridge identifier
 * @param  value: Number of blinks (0 = stop)
 * @retval None
 */
void slotLedBlink(CartridgeID id, uint8_t value)
{
	while(value)
	{
		HAL_GPIO_WritePin(GPIOA, get_D2_Red_LedPins(id), GPIO_PIN_SET);
		HAL_GPIO_WritePin(GPIOA, get_D2_Green_LedPins(id), GPIO_PIN_SET);
		short_delay_us(500000);
		HAL_GPIO_WritePin(GPIOA, get_D2_Red_LedPins(id), GPIO_PIN_RESET);
		HAL_GPIO_WritePin(GPIOA, get_D2_Green_LedPins(id), GPIO_PIN_RESET);
		short_delay_us(500000);
		--value;
	}
}

uint8_t LedLoopBack(uint8_t value)
{
	uint8_t data = 0, data2 = 0;
	while(value)
	{
		GPIOA->ODR = (GPIOA->ODR & 0x1FF00U) | 0x1FF;

		if(HAL_GPIO_ReadPin(GPIOB, LB1_Pin))
			data = data | 0x01;
		if(HAL_GPIO_ReadPin(GPIOB, LB2_Pin))
			data = data | 0x02;
		if(HAL_GPIO_ReadPin(GPIOB, LB3_Pin))
			data = data | 0x04;
		if(HAL_GPIO_ReadPin(GPIOB, LB4_Pin))
			data = data | 0x08;

		short_delay_us(500000);

		 GPIOA->ODR ^= 0x1FE;
		short_delay_us(500000);

		if(HAL_GPIO_ReadPin(GPIOB, LB1_Pin))
			data2 = data2 | 0x01;
		if(HAL_GPIO_ReadPin(GPIOB, LB2_Pin))
			data2 = data2 | 0x02;
		if(HAL_GPIO_ReadPin(GPIOB, LB3_Pin))
			data2 = data2 | 0x04;
		if(HAL_GPIO_ReadPin(GPIOB, LB4_Pin))
			data2 = data2 | 0x08;


		--value;
	}
	if((data == 0x0F ) && (data2 == 0))
		return 0;
	else
		return 1;
}

/**
 * @brief  Blink all LEDs simultaneously
 * @param  value: Continue blinking while non-zero
 * @note   This function blocks while blinking
 * @retval None
 */
void BlinkAllLed(uint8_t value)
{
    while (value)
    {
        /* Turn ON all LEDs */
        GPIOA->ODR = (GPIOA->ODR & 0x1FF00U) | 0x1FF;

        short_delay_us(500000);  /* 500ms ON time */

        /* Turn OFF all LEDs */
        GPIOA->ODR = (GPIOA->ODR & 0x1FF00U) | 0x00;

        short_delay_us(500000);  /* 500ms OFF time */
    }
}
