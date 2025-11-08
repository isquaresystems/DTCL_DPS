/*
 * Darin3Cartridge.c
 *
 *  Created on: 10-Aug-2023
 *      Author: Vijay
 */

#include "stm32f4xx_hal.h"
#include "main.h"
#include "Darin3Cart_Driver.h"

uint16_t CF_WE  = C3WE_Pin;
uint16_t CF_RST = C3RST_Pin;
uint16_t CF_OE  = C3OE_Pin;
uint16_t CF_CE  = C3CE1_Pin;

uint16_t A0  = C1A00_C2ALE_C3A00_Pin;
uint16_t A1  = C1A01_C2nWP_C3A01_Pin;
uint16_t A2  = C1A02_C3A02_Pin;
uint16_t A3  = C1A03_C2nWE_C3A03_Pin;

uint16_t Address_Flash_Page1 = 0;

uint16_t sector_number = 0;

static uint8_t SLT_STATUS2[] = { 0, 0, 0, 0 };

#define data_reg 	   0x00
#define feature        0x01
#define sector_count   0x02
#define sector_num     0x03
#define cyc_low        0x04
#define cyc_high       0x05
#define drive          0x06
#define command        0x07
#define status_reg     0x07
//****************************************************************************
//****************************************************************************
// COMPACT FLASH READ OPERATION
//****************************************************************************
//****************************************************************************
void read_compact_flash(uint8_t *TempStorage,CartridgeID id)//this initiates the complete read cycle for  compact flash
{
 pre_read_compact_flash(id);	                 //function to pre intialise the ports to compact flash
 compact_flash_ready();	                     //function to find wheather the compact flash is ready or not
 command_for_read();		                 //function to intiate read command for compact flash
 check_stat_of_compact_flash();              //function to check wheather the command is initiated properly or not
 compact_flash_read(TempStorage,512);		 //function to read the data from the compact flash and store in XRAM location
 post_read_compact_flash(id);                  //reset the mode of all the ports to input
}
//****************************************************************************
//PRE_READ_COMPACT_FLASH
//****************************************************************************
void pre_read_compact_flash(CartridgeID id)//function to pre intialise the ports to compact flash
{
 Configure_GPIO_IO_D2(Input);
 write_address_port(0);
 HAL_GPIO_WritePin(GPIOC,CF_WE,1);				//disable write signal
 HAL_GPIO_WritePin(GPIOA,CF_OE,1);				//disable output enable of compact flash
 HAL_GPIO_WritePin(GPIOB,CF_CE,0);
 HAL_GPIO_WritePin(GPIOE,CF_RST,1);				//Enable reset
 short_delay_us(500);
 HAL_GPIO_WritePin(GPIOE,CF_RST,0);				//disable reset
 short_delay_us(2500);
}
//****************************************************************************
//COMMAND_FOR _READ
//****************************************************************************
void command_for_read()						 //function to intiate read command for compact flash
{
 Configure_GPIO_IO_D2(Output);					 //set the mode of port p1 as output port
 write_address_port(sector_count); 				 //address the register pointer of CF to sector counter register
 write_port( 0x01);								 //since single page is accessed at a time put value 0x01 into port p1
 HAL_GPIO_WritePin(GPIOC, CF_WE, 0);	         //intiate write so that that value 0x01 is stored in
 HAL_GPIO_WritePin(GPIOC, CF_WE, 1);			 //sector count register
 write_address_port(sector_num);				 //address the register pointer of CF to sector number register
 write_port( sector_number);  					 //send the sector number from which you want to read the data
 HAL_GPIO_WritePin(GPIOC, CF_WE, 0);			 //intiate the write signal
 HAL_GPIO_WritePin(GPIOC, CF_WE, 1);
 write_address_port(cyc_low);					 //address the register pointer of CF to cylinder low register
 write_port(Address_Flash_Page1);				 //send the cylinder low value from which you want to read the data
 HAL_GPIO_WritePin(GPIOC, CF_WE, 0);			 //intiate the write signal
 HAL_GPIO_WritePin(GPIOC, CF_WE, 1);
 write_address_port(cyc_high);					 //address the register pointer of CF to cylinder high register
 write_port(Address_Flash_Page1  >> 8);			 //send the cylinder high value from which you want to read the data
 HAL_GPIO_WritePin(GPIOC, CF_WE, 0);			 //intiate the write signal
 HAL_GPIO_WritePin(GPIOC, CF_WE, 1);
 write_address_port(drive);						 //address the register pointer of CF to drive register
 write_port(0xE0);								 //send the drive value from which you want to read the data
 HAL_GPIO_WritePin(GPIOC, CF_WE, 0);			 //intiate the write signal
 HAL_GPIO_WritePin(GPIOC, CF_WE, 1);
 write_address_port(command);					 //address the register pointer of CF to comand register
 write_port(0x20);								 //send the command value from which you want to read the data
 HAL_GPIO_WritePin(GPIOC, CF_WE, 0);			 //intiate the write signal
 HAL_GPIO_WritePin(GPIOC, CF_WE, 1);
 short_delay_us(50);									 //call delay function
 Configure_GPIO_IO_D2(Input);					 //set the mode of port p1 as input port
}
//****************************************************************************
//POST_READ_COMPACT_FLASH
//****************************************************************************
void compact_flash_read(uint8_t *TempStorage,uint16_t datalength)
{
 write_address_port(data_reg) ;				 //address the register pointer to point to data regester
 Configure_GPIO_IO_D2(Input);				 //ready to access data from the CF
 for (uint16_t x=0 ;x<datalength ;x++)		 //if data counter 'x'<512 then
 {
  HAL_GPIO_WritePin(GPIOA, CF_OE, 0);		 //activate the output enable of CF
  *TempStorage = Read_port();				 //read the data from the CF and store it in address specified by 'pread'
  HAL_GPIO_WritePin(GPIOA, CF_OE, 1);	     //disbale output enable of CF
  TempStorage++;							 //increment 'pread'
 }
}
//****************************************************************************
//POST_READ_COMPACT_FLASH
//****************************************************************************
void post_read_compact_flash(CartridgeID id)				 //reset the mode of all the ports to input
{
 HAL_GPIO_WritePin(GPIOB, CF_CE, 1);	    //disable compact flash chip
 HAL_GPIO_WritePin(GPIOC, CF_WE, 1);	    //disable write enable signal
 HAL_GPIO_WritePin(GPIOE, CF_RST,1);	    //disable reset signal of compact flash
 HAL_GPIO_WritePin(GPIOA, CF_OE, 1);	    //disable output enable of compact flash
}


//****************************************************************************
//****************************************************************************
// COMPACT FLASH WRITE OPERATION
//****************************************************************************
//****************************************************************************
void write_compact_flash(uint8_t* TempStorage,CartridgeID id)	//this initiates the complete write cycle for compact flash
{
 pre_write_compact_flash(id);	                    //this function initiates pre port settings for CF write
 compact_flash_ready();		                    //this is used to check wheather CF is ready for write operation
 command_for_write();			                //this function is used to initiate write command to CF
 check_stat_of_compact_flash();                 //this function is used to check the status of write command
 compact_flash_write(TempStorage,512);		    //if command was sucessesful then start writing data to CF
 post_write_compact_flash(id);	                //this function used to reset the mode of ports
}
//****************************************************************************
//PRE_WRITE_COMPACT_FLASH
//****************************************************************************
void pre_write_compact_flash(CartridgeID id)              //this function initiates pre port settings for CF write
{
 Configure_GPIO_IO_D2(Input);
 write_address_port(0);
 HAL_GPIO_WritePin(GPIOC,CF_WE,1);				//disable write signal
 HAL_GPIO_WritePin(GPIOA,CF_OE,1);				//disable output enable of compact flash
 HAL_GPIO_WritePin(GPIOB,CF_CE,0);
 HAL_GPIO_WritePin(GPIOE,CF_RST,1);				//Enable reset
 short_delay_us(50);
 HAL_GPIO_WritePin(GPIOE,CF_RST,0);				//disable reset
 short_delay_us(2500);
}
//****************************************************************************
//COMPACT_FLASH_READY
//****************************************************************************
unsigned char compact_flash_ready()         //function to find wheather the compact flash is ready or not
{
 unsigned char reg_status;
 write_address_port(status_reg);                //send 0x07 to CF so that select status register of CF
 short_delay_us(50);			                        //call delay function
 unsigned int BusyCnt;

 BusyCnt = 0;
 do                   	                        //check for error if one it means previous
 {						                        //command was ended with error
  HAL_GPIO_WritePin(GPIOA,CF_OE,0);
  reg_status = Read_port();
  HAL_GPIO_WritePin(GPIOA,CF_OE,1);
  BusyCnt++;
 }while(((reg_status & 0x80) == 0x80) && (BusyCnt<5000));

 BusyCnt = 0;
 do                   	                        //check for error if one it means previous
 {						                        //command was ended with error
  HAL_GPIO_WritePin(GPIOA,CF_OE,0);
  reg_status = Read_port();
  HAL_GPIO_WritePin(GPIOA,CF_OE,1);
  BusyCnt++;
 }while(((reg_status & 0x40) == 0x00) && (BusyCnt<5000));

 BusyCnt = 0;
 do                   	                        //check for error if one it means previous
 {						                        //command was ended with error
  HAL_GPIO_WritePin(GPIOA,CF_OE,0);
  reg_status = Read_port();
  HAL_GPIO_WritePin(GPIOA,CF_OE,1);
  BusyCnt++;
 }while(((reg_status & 0x10) == 0x00) && (BusyCnt<5000));
 return reg_status;
}
//****************************************************************************
//COMMAND_FOR _WRITE
//****************************************************************************
void command_for_write()	                    //this function is used to initiate write command to CF
{
 Configure_GPIO_IO_D2(Output);	                //set the mode of port p1 as output port
 write_address_port(sector_count); 				//address the register pointer of CF to sector counter register
 write_port(0x01);								//since single page is accessed at a time put value 0x01 into port p1
 HAL_GPIO_WritePin(GPIOC, CF_WE, 0);    		//initiate write so that that value 0x01 is stored in
 HAL_GPIO_WritePin(GPIOC, CF_WE, 1);    		//sector count register
 write_address_port(sector_num);	    		//address the register pointer of CF to sector number register
 write_port(sector_number);			    		//send the sector number from which you want to read the data
 HAL_GPIO_WritePin(GPIOC, CF_WE, 0);    		//initiate the write signal
 HAL_GPIO_WritePin(GPIOC, CF_WE, 1);
 write_address_port(cyc_low);		    		//address the register pointer of CF to cylinder low register
 write_port(Address_Flash_Page1);				//send the cylinder low value from which you want to read the data
 HAL_GPIO_WritePin(GPIOC, CF_WE, 0);    		//initiate the write signal
 HAL_GPIO_WritePin(GPIOC, CF_WE, 1);
 write_address_port(cyc_high);		    		//address the register pointer of CF to cylinder high register
 write_port(Address_Flash_Page1 >> 8);			//send the cylinder high value from which you want to read the data
 HAL_GPIO_WritePin(GPIOC, CF_WE, 0);    		//initiate the write signal
 HAL_GPIO_WritePin(GPIOC, CF_WE, 1);
 write_address_port(drive);			    		//address the register pointer of CF to drive register
 write_port(0xE0);								//send the drive value from which you want to read the data
 HAL_GPIO_WritePin(GPIOC, CF_WE, 0);    		//initiate the write signal
 HAL_GPIO_WritePin(GPIOC, CF_WE, 1);
 write_address_port(command);					//address the register pointer of CF to comand register
 write_port(0x30);								//send the command value from which you want to WRITE the data
 HAL_GPIO_WritePin(GPIOC, CF_WE, 0);    		//initiate the write signal
 HAL_GPIO_WritePin(GPIOC, CF_WE, 1);
 Configure_GPIO_IO_D2(Input);
}
//****************************************************************************
//CHEAK_STATUS_FOR_COMPACT_FLASH
//****************************************************************************
unsigned char check_stat_of_compact_flash()	//function to check wheather the command is initiated properly or not
{
 unsigned char reg_status;
 write_address_port(status_reg);				//address the register pointer to point to status regester
 short_delay_us(50);								    //wait for some delay //if error then quit
 unsigned int BusyCnt;

 BusyCnt = 0;
 do
 {
  HAL_GPIO_WritePin(GPIOA,CF_OE,0);
  reg_status = Read_port();
  HAL_GPIO_WritePin(GPIOA,CF_OE,1);
  BusyCnt++;
 }while(((reg_status & 0x80) == 0x80)&& (BusyCnt < 500000));

 BusyCnt = 0;
 do
 {
  HAL_GPIO_WritePin(GPIOA,CF_OE,0);
  reg_status = Read_port();
  HAL_GPIO_WritePin(GPIOA,CF_OE,1);
  BusyCnt++;;
 }while(((reg_status & 0x40) == 0x00) && (BusyCnt < 500000));

 BusyCnt = 0;
 do
 {
  HAL_GPIO_WritePin(GPIOA,CF_OE,0);
  reg_status = Read_port();
  HAL_GPIO_WritePin(GPIOA,CF_OE,1);
  BusyCnt++;
 }while(((reg_status & 0x10) == 0x00) && (BusyCnt < 500000));

 BusyCnt = 0;
 do
 {
  HAL_GPIO_WritePin(GPIOA,CF_OE,0);
  reg_status = Read_port();
  HAL_GPIO_WritePin(GPIOA,CF_OE,1);
  BusyCnt++;
 }while(((reg_status & 0x08) == 0x00) && (BusyCnt < 500000));

 BusyCnt = 0;
 do
 {
  HAL_GPIO_WritePin(GPIOA,CF_OE,0);
  reg_status = Read_port();
  HAL_GPIO_WritePin(GPIOA,CF_OE,1);
  BusyCnt++;
 }while(((reg_status & 0x01) == 0x01) && (BusyCnt < 500000));
 return reg_status;
}
//****************************************************************************
//WRITE_COMPACT_FLASH
//****************************************************************************
void compact_flash_write(uint8_t* TempStorage,uint16_t datalength)	//if command was sussesful then start writing data to CF
{
 unsigned char busy;
 Configure_GPIO_IO_D2(Output);				//set the mode of port p1 as output port
 write_address_port(data_reg);				//address the register pointer to point to data regester
 short_delay_us(50);
 for (uint16_t x=0 ;x<datalength ;x++)      //if data counter value <last page size then
 {
  write_port(*TempStorage) ;			    //read the data from XRAM location load it into the CF
  HAL_GPIO_WritePin(GPIOC, CF_WE, 0);
  HAL_GPIO_WritePin(GPIOC, CF_WE, 1);	    //disable write signal of compact flash
  TempStorage++;					        //increment XRAM pointer
 }
 Configure_GPIO_IO_D2(Input);
 write_address_port(status_reg);
 HAL_GPIO_WritePin(GPIOA, CF_OE, 0);
 do                   	                    //check for error if one it means previous
 {						                    //command was ended with error
  busy = Read_port();
 }while((busy & 0x80) == 0x80);
 HAL_GPIO_WritePin(GPIOA, CF_OE, 1);
}
//****************************************************************************
//POST_WRITE_COMPACT_FLASH
//****************************************************************************
void post_write_compact_flash(CartridgeID id)
{
 HAL_GPIO_WritePin(GPIOB, CF_CE, 1);	    //disable compact flash chip
 HAL_GPIO_WritePin(GPIOC, CF_WE, 1);	    //disable write enable signal
 HAL_GPIO_WritePin(GPIOE, CF_RST,1);	    //disable reset signal of compact flash
 HAL_GPIO_WritePin(GPIOA, CF_OE, 1);	    //disable output enable of compact flash
}
//****************************************************************************
//****************************************************************************
//
//****************************************************************************
//****************************************************************************
uint8_t D3_Cartridge_Check(CartridgeID id)
{
 GPIO_InitTypeDef GPIO_InitStruct = {0};
 GPIO_InitStruct.Pin =  C3SLTS1_Pin;
 GPIO_InitStruct.Mode = GPIO_MODE_INPUT;
 GPIO_InitStruct.Pull = GPIO_PULLUP;
 HAL_GPIO_Init(GPIOD, &GPIO_InitStruct);
 return HAL_GPIO_ReadPin(GPIOD, C3SLTS1_Pin);
}

//****************************************************************************
//
//****************************************************************************
void write_address_port(uint8_t data)
{
 HAL_GPIO_WritePin(GPIOB, A3, ((data & 0x08) >> 3));
 HAL_GPIO_WritePin(GPIOE, A2, ((data & 0x04) >> 2));
 HAL_GPIO_WritePin(GPIOB, A1, ((data & 0x02) >> 1));
 HAL_GPIO_WritePin(GPIOB, A0, (data & 0x01));
}
//****************************************************************************
//POST_READ_COMPACT_FLASH
//****************************************************************************
uint8_t Read_address_port()
{
 uint8_t ret =
 (HAL_GPIO_ReadPin(GPIOB, A3) << 3) |
 (HAL_GPIO_ReadPin(GPIOE, A2) << 2) |
 (HAL_GPIO_ReadPin(GPIOB, A1) << 1) |
 (HAL_GPIO_ReadPin(GPIOB, A0));
 return ret;
}
//****************************************************************************
//
//****************************************************************************
void generic_CF_CMD(unsigned char sector_num1,unsigned char Cmd, unsigned char CylinderLow, unsigned char CylinderHigh)	//this function is used to initiate write command to CF
{
 Configure_GPIO_IO_D2(Output);	                        //set the mode of port p1 as output port
 write_address_port(sector_count); 				        //address the register pointer of CF to sector counter register
 write_port(0x01);						                //since single page is accessed at a time put value 0x01 into port p1
 HAL_GPIO_WritePin(GPIOC, CF_WE, 0);					//intiate write so that that value 0x01 is stored in
 HAL_GPIO_WritePin(GPIOC, CF_WE, 1);					//sector count register
 write_address_port(sector_num);				        //address the register pointer of CF to sector number register
 write_port(sector_num1);			                    //send the sector number from which you want to read the data
 HAL_GPIO_WritePin(GPIOC, CF_WE, 0);					//intiate the write signal
 HAL_GPIO_WritePin(GPIOC, CF_WE, 1);
 write_address_port(cyc_low);					        //address the register pointer of CF to cylinder low register
 write_port(CylinderLow);		                        //send the cylinder low value from which you want to read the data
 HAL_GPIO_WritePin(GPIOC, CF_WE, 0);					//intiate the write signal
 HAL_GPIO_WritePin(GPIOC, CF_WE, 1);
 write_address_port(cyc_high);					        //address the register pointer of CF to cylinder high register
 write_port(CylinderHigh);	                            //send the cylinder high value from which you want to read the data
 HAL_GPIO_WritePin(GPIOC, CF_WE, 0);					//intiate the write signal
 HAL_GPIO_WritePin(GPIOC, CF_WE, 1);
 write_address_port(drive);					            //address the register pointer of CF to drive register
 write_port(0xE0);						                //send the drive value from which you want to read the data
 HAL_GPIO_WritePin(GPIOC, CF_WE, 0);					//intiate the write signal
 HAL_GPIO_WritePin(GPIOC, CF_WE, 1);
 write_address_port(command);					        //address the register pointer of CF to comand register
 write_port(Cmd);						                //send the command value from which you want to WRITE the data
 HAL_GPIO_WritePin(GPIOC, CF_WE, 0);					//intiate the write signal
 HAL_GPIO_WritePin(GPIOC, CF_WE, 1);
 Configure_GPIO_IO_D2(Input);
}

void UpdateD3SlotStatus()
{
	GPIO_InitTypeDef GPIO_InitStruct = {0};
	GPIO_InitStruct.Pin =  C3SLTS1_Pin;
	GPIO_InitStruct.Mode = GPIO_MODE_INPUT;
	GPIO_InitStruct.Pull = GPIO_PULLUP;
	HAL_GPIO_Init(GPIOD, &GPIO_InitStruct);

	if( 0 == HAL_GPIO_ReadPin(GPIOD, C3SLTS1_Pin))
	{
		SLT_STATUS2[0] = 0x03;SLT_STATUS2[1] = 0x03;SLT_STATUS2[2] = 0x03;SLT_STATUS2[3] = 0x03;
	}
	else
	{
		SLT_STATUS2[0] = 0x00;SLT_STATUS2[1] = 0x00;SLT_STATUS2[2] = 0x00;SLT_STATUS2[3] = 0x00;
	}
}

static const uint16_t RED_LED[] = { LED1_Pin, LED1_Pin, LED1_Pin, LED1_Pin };
static const uint16_t GREEN_LED[] = { LED2_Pin, LED2_Pin, LED2_Pin, LED2_Pin };

uint16_t get_D3_Green_LedPins(CartridgeID id) {
    return GREEN_LED[id];
}

uint16_t get_D3_Red_LedPins(CartridgeID id) {
    return RED_LED[id];
}

uint16_t get_D3_slt_status(CartridgeID id) {
    return SLT_STATUS2[id];
}

uint8_t LedLoopBack(uint8_t value)
{
    uint8_t data = 0, data2 = 0;
    while(value)
    {
        HAL_GPIO_WritePin(GPIOB, LED1_Pin, 0x01);
        HAL_GPIO_WritePin(GPIOB, LED2_Pin, 0x01);

        if(GPIOE->IDR & LED1_LOOPBACK_Pin)
            data = data | 0x01;
        if(GPIOE->IDR & LED4_LOOPBACK_Pin)
            data = data | 0x02;
  
        short_delay_us(500000);

        HAL_GPIO_WritePin(GPIOB, LED1_Pin, 0x00);
        HAL_GPIO_WritePin(GPIOB, LED2_Pin, 0x00);

        short_delay_us(500000);

        if(GPIOE->IDR & LED1_LOOPBACK_Pin)
            data2 = data2 | 0x01;
        if(GPIOE->IDR & LED4_LOOPBACK_Pin)
            data2 = data2 | 0x02;

        --value;
    }
    if((data == 0x03 ) && (data2 == 0))
        return 0;
    else
        return 1;
}
