/*
 * Darin2Cartridge.c
 *
 *  Created on: Jul 29, 2023
 *      Author: Vijay
 */


#include "stm32f4xx_hal.h"
#include "main.h"
#include "Darin2Cart_Driver.h"

//uint16_t Address_Flash_Page = 0;

uint16_t F_RDY = C2RB0_Pin;
uint16_t F_ALE = C1A00_C2ALE_C3A00_Pin;
uint16_t F_CLE = C1A04_C2CLE_Pin;
uint16_t F_WP  = C1A01_C2nWP_C3A01_Pin;
uint16_t F_CE  = C2CE1_Pin;
uint16_t F_RD  = C2RE_Pin;
uint16_t F_WR  = C1A03_C2nWE_C3A03_Pin;

uint16_t M_D7  = C2DB7_C3DB7_INOUT_Pin;
uint16_t M_D6  = C2DB6_C3DB6_Pin;
uint16_t M_D5  = C2DB5_C3DB5_INOUT_Pin;
uint16_t M_D4  = C2DB4_C3DB4_Pin;
uint16_t M_D3  = C2DB3_C3DB3_Pin;
uint16_t M_D2  = C2DB2_C3DB2_Pin;
uint16_t M_D1  = C2DB1_C3DB1_Pin;
uint16_t M_D0  = C2DB0_C3DB0_Pin;

static const uint16_t CE_PINS[]  = { C2CE1_Pin, C2CE1_Pin, C2CE1_Pin, C2CE1_Pin };
static const uint16_t RDY_PINS[] = { C2RB0_Pin, C2RB0_Pin, C2RB0_Pin, C2RB0_Pin };
static const uint16_t SLT_PINS[] = { C2SLTS1_Pin, C2SLTS1_Pin, C2SLTS1_Pin, C2SLTS1_Pin };
static uint8_t SLT_STATUS[] = { 0, 0, 0, 0 };
static const uint16_t RED_LED[] = { LED1_Pin, LED1_Pin, LED1_Pin, LED1_Pin };
static const uint16_t GREEN_LED[] = { LED2_Pin, LED2_Pin, LED2_Pin, LED2_Pin };

//****************************************************************************
//PRE FLASH WRITE
//****************************************************************************

void pre_write_flash(CartridgeID id)		//pre setting of port pins before writing the data
{

   Configure_GPIO_IO_D2(Output);    //port P1 is declared as output port

   HAL_GPIO_WritePin(GPIOB, F_ALE, 0);		             //disable address latch enable pin of flash
   HAL_GPIO_WritePin(GPIOB, F_WP,  1);	                 //disable write protect pin of flash
   HAL_GPIO_WritePin(GPIOB, F_RD,  1);		 //disable output enable pin of flash
   HAL_GPIO_WritePin(GPIOB, F_WR,  1);	                 //disable write enable pin of flash
   HAL_GPIO_WritePin(GPIOC, F_CLE, 0);	                 //disable command latch enable pin of flash
   HAL_GPIO_WritePin(GPIOC, F_CE,  0);		 //activate the flash chip
}

//**********************************************************************************
//Function To Write
//**********************************************************************************
void flash_write(const uint8_t* TempStorage, uint16_t dataLength, uint16_t Address_Flash_Page,CartridgeID id)
{
    HAL_GPIO_WritePin(GPIOC, F_CLE, 1);		   //activate the command latch enable
    write_port(0x80);					       //send read command 0x80 to port p1
    HAL_GPIO_WritePin(GPIOB, F_WR, 0);		   //write the write command into the flash
    HAL_GPIO_WritePin(GPIOB, F_WR, 1);   	   //so that write command is intiated
    HAL_GPIO_WritePin(GPIOC, F_CLE, 0);	       //disable command latch enable

    write_port(0x00);					       //send address 0x00 to port P1
	HAL_GPIO_WritePin(GPIOB, F_ALE, 1);		   //activate address latch enable signal of flash

	write_port(0x00);					       //send address 0x00 to port P1

	HAL_GPIO_WritePin(GPIOB, F_WR, 0);  	   //intiate write signal
	HAL_GPIO_WritePin(GPIOB, F_WR, 1);

	write_port(Address_Flash_Page);            //send lower order 8 bit page address to port P1

	HAL_GPIO_WritePin(GPIOB, F_WR, 0);		   //intiate write signal
	HAL_GPIO_WritePin(GPIOB, F_WR, 1);

	write_port(Address_Flash_Page >>8);        //send higher order 8 bit page address to port P1
	HAL_GPIO_WritePin(GPIOB, F_WR, 0);		   //initiate the write signal
	HAL_GPIO_WritePin(GPIOB, F_WR, 1);

	write_port(0x00);				           //send address 0x00 to port P1
	HAL_GPIO_WritePin(GPIOB, F_WR, 0);		   //initiate the write signal
	HAL_GPIO_WritePin(GPIOB, F_WR, 1);

	write_port(0xFF);  				           //intially set the bits of port P1
	HAL_GPIO_WritePin(GPIOB, F_ALE, 0);		   //disable address latch enable which indicates the end of write command for flash
	short_delay_us(1000);					           //wait for some delay

	int x=0;
    for(x = 0;	x<dataLength;	x++)           //if data counter 'x'<	last_page_size then
	{

		write_port(*TempStorage);                   //fetch the data from the XRAM loction
	   	HAL_GPIO_WritePin(GPIOB, F_WR, 0);			//enable write signal of flash
	   	HAL_GPIO_WritePin(GPIOB, F_WR, 1);			//disable write signal of flash
	   	TempStorage++;				                //increment the pointer of XRAM
	}

    if(dataLength < 512)	//if last page size is less than 512 then
    {
    for(x=0;x<(512-dataLength);x++)//fill the remaining by data 0xFF
      {
    	write_port(0xFF);			                //send data 0xFF through port P1
		HAL_GPIO_WritePin(GPIOB, F_WR, 0); 			//enable write signal
		HAL_GPIO_WritePin(GPIOB, F_WR, 1);			//disable write signal
      }
    }

    short_delay_us(1000);					                //call delay function

    HAL_GPIO_WritePin(GPIOC, F_CLE, 1);				//enable command latch enable
    write_port(0x10);			                    //initiate write command to flash so that the data from flash buffer

    HAL_GPIO_WritePin(GPIOB, F_WR, 0);				//enable write signal of flash
    HAL_GPIO_WritePin(GPIOB, F_WR, 1);   			//disable write signal
    HAL_GPIO_WritePin(GPIOC, F_CLE, 0); 			//disable command latch enable
    short_delay_us(1000);					                //call delay function
}
//****************************************************************************
//POST FLASH WRITE
//****************************************************************************

void post_write_flash(CartridgeID id)
{
   HAL_GPIO_WritePin(GPIOC, F_CE,  1); 			    //disable flash chip
   HAL_GPIO_WritePin(GPIOB, F_ALE, 0);				//disable address latch enable of flash
   HAL_GPIO_WritePin(GPIOB, F_WP,  1);				//disable write protect of flash
   HAL_GPIO_WritePin(GPIOB, F_RD,  1);	            //disable read enable of flash
   HAL_GPIO_WritePin(GPIOB, F_WR,  1);				//disable write of flash
   HAL_GPIO_WritePin(GPIOC, F_CLE, 0);				//disable command latch enable of flash

   Configure_GPIO_IO_D2(Input);
}


void pre_read_flash(CartridgeID id)
{
	Configure_GPIO_IO_D2(Output);                             //port P1 is declared as output port

	HAL_GPIO_WritePin(GPIOB, F_ALE, 0);		               //disable address latch enable pin of flash
	HAL_GPIO_WritePin(GPIOB, F_WP,  1);	                   //disable write protect pin of flash
	HAL_GPIO_WritePin(GPIOB, F_RD,  1);		               //disable output enable pin of flash
	HAL_GPIO_WritePin(GPIOB, F_WR,  1);	                   //disable write enable pin of flash
	HAL_GPIO_WritePin(GPIOC, F_CLE, 0);				       //disable command latch enable pin of flash
	HAL_GPIO_WritePin(GPIOC, F_CE,  0);		               //activate the flash chip
}
//****************************************************************************
//READ_FLASH
//****************************************************************************

void flash_read(uint8_t *TempStorage, uint16_t dataLength, uint16_t Address_Flash_Page,CartridgeID id)
{

	HAL_GPIO_WritePin(GPIOC, F_CLE, 1);			//activate the command latch enable

	write_port(0x00);				            //send read command 0x00 to port p1
	HAL_GPIO_WritePin(GPIOB, F_WR, 0);			//write the read command into the flash
	HAL_GPIO_WritePin(GPIOB, F_WR, 1);   		//so that read command is intiated

	HAL_GPIO_WritePin(GPIOC, F_CLE, 0);		    //diable command latch enable
	HAL_GPIO_WritePin(GPIOB, F_ALE, 1);			//activate address latch enable signal of flash

	write_port(0x00);			                //send address 0x00 to port P1
	HAL_GPIO_WritePin(GPIOB, F_WR, 0);  		//intiate write signal
	HAL_GPIO_WritePin(GPIOB, F_WR, 1);

	write_port(Address_Flash_Page);             //send lower order 8 bit page address to port P1
	HAL_GPIO_WritePin(GPIOB, F_WR, 0);			//intiate write signal
	HAL_GPIO_WritePin(GPIOB, F_WR, 1);

	write_port(Address_Flash_Page >>8);	        //send higher order 8 bit page address to port P1
	HAL_GPIO_WritePin(GPIOB, F_WR, 0);			//initiate the write signal
	HAL_GPIO_WritePin(GPIOB, F_WR, 1);

	write_port(0x00);				            //send address 0x00 to port P1
	HAL_GPIO_WritePin(GPIOB, F_WR, 0);			//intiate the write signal
	HAL_GPIO_WritePin(GPIOB, F_WR, 1);
	HAL_GPIO_WritePin(GPIOB, F_ALE, 0);			//disable addresss latch enable which indicates
	write_port(0xFF);				            // the end of of the address write
	Configure_GPIO_IO_D2(Input);			        //change the mode of port P1 as input port so that the
	                                            //controller is now ready to recieve data from the port p1
	short_delay_us(5000);				                //call delay function
	for(uint16_t x = 0;	x<512;	x++)                    //if data counter(x)<512 then
	{
		HAL_GPIO_WritePin(GPIOB, F_RD, 0);		//activate the read enable signal of flash
		*TempStorage = Read_port();
		HAL_GPIO_WritePin(GPIOB, F_RD, 1);		//disable read enable
		TempStorage++;			                            //increment XRAM pointer by one
	}
}
//****************************************************************************
//POST_READ_FLASH
//****************************************************************************
void post_read_flash(CartridgeID id)
{
	HAL_GPIO_WritePin(GPIOC, F_CE, 1); 			           //disable flash chip
	HAL_GPIO_WritePin(GPIOB, F_ALE, 0);		                   //disable address latch enable of flash
	HAL_GPIO_WritePin(GPIOB, F_WP, 1);				           //disable write protect of flash
	HAL_GPIO_WritePin(GPIOB, F_RD, 1);			   //disable read enable of flash
	HAL_GPIO_WritePin(GPIOB, F_WR, 1);				           //disable write of flash
	HAL_GPIO_WritePin(GPIOC, F_CLE, 0);				           //disable command latch enable of flash

	Configure_GPIO_IO_D2(Input);
}



void write_port(uint8_t data)
{
	HAL_GPIO_WritePin(GPIOA, M_D7, ((data & 0x80) >> 7));
	HAL_GPIO_WritePin(GPIOC, M_D6, ((data & 0x40) >> 6));
	HAL_GPIO_WritePin(GPIOA, M_D5, ((data & 0x20) >> 5));
	HAL_GPIO_WritePin(GPIOC, M_D4, ((data & 0x10) >> 4));
	HAL_GPIO_WritePin(GPIOC, M_D3, ((data & 0x08) >> 3));
	HAL_GPIO_WritePin(GPIOD, M_D2, ((data & 0x04) >> 2));
	HAL_GPIO_WritePin(GPIOE, M_D1, ((data & 0x02) >> 1));
	HAL_GPIO_WritePin(GPIOB, M_D0, (data & 0x01));
}

uint8_t Read_port()
{
  uint8_t ret =
  (HAL_GPIO_ReadPin(GPIOA, M_D7) << 7) |
  (HAL_GPIO_ReadPin(GPIOC, M_D6) << 6) |
  (HAL_GPIO_ReadPin(GPIOA, M_D5) << 5) |
  (HAL_GPIO_ReadPin(GPIOC, M_D4) << 4) |
  (HAL_GPIO_ReadPin(GPIOC, M_D3) << 3) |
  (HAL_GPIO_ReadPin(GPIOD, M_D2) << 2) |
  (HAL_GPIO_ReadPin(GPIOE, M_D1) << 1) |
  (HAL_GPIO_ReadPin(GPIOB, M_D0));

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

	HAL_GPIO_WritePin(GPIOB, F_ALE, 0);		             //disable address latch enable pin of flash
	HAL_GPIO_WritePin(GPIOB, F_WP,  1);	                 //disable write protect pin of flash
	HAL_GPIO_WritePin(GPIOB, F_RD,  1);		 //disable output enable pin of flash
	HAL_GPIO_WritePin(GPIOB, F_WR,  1);	                 //disable write enable pin of flash
	HAL_GPIO_WritePin(GPIOC, F_CLE, 0);	                 //disable command latch enable pin of flash
	HAL_GPIO_WritePin(GPIOC, F_CE,  0);		 //activate the flash chip

	GPIO_InitTypeDef GPIO_InitStruct = {0};
	GPIO_InitStruct.Pin = C2RB0_Pin;
	GPIO_InitStruct.Mode = GPIO_MODE_INPUT;
	GPIO_InitStruct.Pull = GPIO_NOPULL;
	HAL_GPIO_Init(GPIOA, &GPIO_InitStruct);

}

unsigned char flash_device_ID(CartridgeID id)
{
	HAL_GPIO_WritePin(GPIOC, F_CLE, 1);		   //activate the command latch enable
	short_delay_us(1000);
	write_port(0x90);					       //send read command 0x80 to port p1
	HAL_GPIO_WritePin(GPIOB, F_WR, 0);		   //write the write command into the flash
	short_delay_us(1000);
	HAL_GPIO_WritePin(GPIOB, F_WR, 1);   	   //so that write command is intiated
	HAL_GPIO_WritePin(GPIOC, F_CLE, 0);	       //disable command latch enable
	short_delay_us(1000);

	HAL_GPIO_WritePin(GPIOB, F_ALE, 1);		   //activate address latch enable signal of flash
	short_delay_us(1000);

	write_port((0x00) & 0xFF);            //send lower order 8 bit page address to port P1
	HAL_GPIO_WritePin(GPIOB, F_WR, 0);		   //intiate write signal
	short_delay_us(1000);
	HAL_GPIO_WritePin(GPIOB, F_WR, 1);

	HAL_GPIO_WritePin(GPIOB, F_ALE, 0);		   //activate address latch enable signal of flash


	Configure_GPIO_IO_D2(Input);
	short_delay_us(1000);
	HAL_GPIO_WritePin(GPIOB, F_RD, 0);
	short_delay_us(1000);
	unsigned char result = Read_port();
	HAL_GPIO_WritePin(GPIOB, F_RD, 1);
	short_delay_us(1000);

	HAL_GPIO_WritePin(GPIOB, F_RD, 0);
	short_delay_us(1000);
	result = Read_port();
	HAL_GPIO_WritePin(GPIOB, F_RD, 1);
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

    HAL_GPIO_WritePin(GPIOC, F_CLE, 1);		   //activate the command latch enable
    write_port(0x60);					       //send read command 0x80 to port p1
    HAL_GPIO_WritePin(GPIOB, F_WR, 0);		   //write the write command into the flash
    HAL_GPIO_WritePin(GPIOB, F_WR, 1);   	   //so that write command is intiated
    HAL_GPIO_WritePin(GPIOC, F_CLE, 0);	       //disable command latch enable

	HAL_GPIO_WritePin(GPIOB, F_ALE, 1);		   //activate address latch enable signal of flash
	short_delay_us(1000);

	write_port((Address_Flash_Page) & 0xFF);            //send lower order 8 bit page address to port P1
	HAL_GPIO_WritePin(GPIOB, F_WR, 0);		   //intiate write signal
	HAL_GPIO_WritePin(GPIOB, F_WR, 1);
	short_delay_us(1000);

	write_port((Address_Flash_Page >>8) & 0xFF);        //send higher order 8 bit page address to port P1
	HAL_GPIO_WritePin(GPIOB, F_WR, 0);		   //initiate the write signal
	HAL_GPIO_WritePin(GPIOB, F_WR, 1);
	short_delay_us(1000);

	write_port(0x00);				           //send address 0x00 to port P1
	HAL_GPIO_WritePin(GPIOB, F_WR, 0);		   //initiate the write signal
	HAL_GPIO_WritePin(GPIOB, F_WR, 1);
	short_delay_us(1000);

	HAL_GPIO_WritePin(GPIOB, F_ALE, 0);


	HAL_GPIO_WritePin(GPIOC, F_CLE, 1);
	short_delay_us(1000);
	write_port(0xD0);
	HAL_GPIO_WritePin(GPIOB, F_WR, 0);
    HAL_GPIO_WritePin(GPIOB, F_WR, 1);
    HAL_GPIO_WritePin(GPIOC, F_CLE, 0);
    short_delay_us(1000);

    //rdy
    while(HAL_GPIO_ReadPin(GPIOA, F_RDY) !=1 );

    write_port(0x70);
    short_delay_us(1000);
    HAL_GPIO_WritePin(GPIOC, F_CLE, 1);
    HAL_GPIO_WritePin(GPIOB, F_WR, 0);
    short_delay_us(1000);
    HAL_GPIO_WritePin(GPIOB, F_WR, 1);
    HAL_GPIO_WritePin(GPIOC, F_CLE, 0);

    Configure_GPIO_IO_D2(Input);
    HAL_GPIO_WritePin(GPIOB, F_RD, 0);
    short_delay_us(1000);
    result = Read_port();
    HAL_GPIO_WritePin(GPIOB, F_RD, 1);
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
	HAL_GPIO_WritePin(GPIOC, F_CE,  1); 			    //disable flash chip
	HAL_GPIO_WritePin(GPIOB, F_ALE, 0);				//disable address latch enable of flash
	HAL_GPIO_WritePin(GPIOB, F_WP,  1);				//disable write protect of flash
	HAL_GPIO_WritePin(GPIOB, F_RD,  1);	            //disable read enable of flash
	HAL_GPIO_WritePin(GPIOB, F_WR,  1);				//disable write of flash
	HAL_GPIO_WritePin(GPIOC, F_CLE, 0);				//disable command latch enable of flash

	Configure_GPIO_IO_D2(Input);
 }

void setGreenLed(CartridgeID id, uint8_t value)
{
	if(value ==1)
	HAL_GPIO_WritePin(GPIOB, get_D2_Green_LedPins(id)
			    				   , GPIO_PIN_SET);
	else
		HAL_GPIO_WritePin(GPIOB, get_D2_Green_LedPins(id), GPIO_PIN_RESET);
}
void setRedLed(CartridgeID id, uint8_t value)
{
	if(value ==1)
	HAL_GPIO_WritePin(GPIOB, get_D2_Red_LedPins(id)
			    				   , GPIO_PIN_SET);
	else
		HAL_GPIO_WritePin(GPIOB, get_D2_Red_LedPins(id)
					    				   , GPIO_PIN_RESET);
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

	if( 0 == HAL_GPIO_ReadPin(GPIOD, get_slt_pin(CARTRIDGE_1)))
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
