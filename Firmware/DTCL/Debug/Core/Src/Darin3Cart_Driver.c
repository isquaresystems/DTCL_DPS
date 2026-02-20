/*
 * Darin3Cartridge.c
 *
 *  Created on: 10-Aug-2023
 *      Author: Vijay
 */

#include "stm32f4xx_hal.h"
#include "main.h"
#include "Darin3Cart_Driver.h"

#define DATA_MASK_16  0xFFFFu

// ——————————————————————————————————————————————————————————————————————————————
// Configuration masks for pins PE0…PE15 (D0–D15)
// — Each pin has two bits in MODER/OSPEEDR/PUPDR:
//    00 = input    | 01 = output    | 10 = AF  | 11 = analog
// — We want either “all inputs” (00_00_…_00) or “all outputs” (01_01_…_01)
// — Mask covering bits 0–31 of each register:
#define D2_MODER_MASK    0xFFFFFFFFU
#define D2_OSPEEDR_MASK  0xFFFFFFFFU
#define D2_PUPDR_MASK    0xFFFFFFFFU

// — Pattern for setting outputs (01 in each 2-bit field) across 16 pins:
//   binary: 01_01_01_…_01 (16 times) = 0x55555555
#define D2_MODER_OUTPUT    0x55555555U
#define D2_OSPEEDR_LOW     0x55555555U
#define D2_PUPDR_FLOAT     0x00000000U

// — Pattern for inputs is all zeros
#define D2_MODER_INPUT     0x00000000U
#define D2_OSPEEDR_INPUT   0x00000000U
#define D2_PUPDR_INPUT     0x00000000U

uint16_t M_D7  = D07_Pin;
uint16_t M_D6  = DB6_Pin;
uint16_t M_D5  = DB5_Pin;
uint16_t M_D4  = DB4_Pin;
uint16_t M_D3  = DB3_Pin;
uint16_t M_D2  = DB2_Pin;
uint16_t M_D1  = DB1_Pin;
uint16_t M_D0  = DB0_Pin;

uint16_t CF_WE  = WE_Pin;
uint16_t CF_RST = RESET_Pin;
uint16_t CF_OE  = ATA_SEL_Pin;
uint16_t CF_CE[4]  = {CSO_1_Pin, CSO_2_Pin, CSO_3_Pin, CSO_4_Pin};
uint16_t CD2_SLT[4]  = {CD2_SLT_S1_Pin, CD2_SLT_S2_Pin, CD2_SLT_S3_Pin, CD2_SLT_S4_Pin};
static uint8_t SLT_STATUS[4]  = {1, 1, 1, 1};
static const uint16_t GREEN_LED[] = { LED1_Pin, LED3_Pin, LED5_Pin, LED7_Pin };
static const uint16_t RED_LED[] = { LED2_Pin, LED4_Pin, LED6_Pin, LED8_Pin };

uint16_t A0  = A00_Pin;
uint16_t A1  = A01_Pin;
uint16_t A2  = A02_Pin;
uint16_t A3  = A03_Pin;

uint16_t Address_Flash_Page1 = 0;

uint16_t sector_number = 0;

#define data_reg 	   0x00
#define feature        0x01
#define sector_count   0x02
#define sector_num     0x03
#define cyc_low        0x04
#define cyc_high       0x05
#define drive          0x06
#define command        0x07
#define status_reg     0x07

uint16_t get_CE_pin(CartridgeID id) {
    return CF_CE[id];
}

uint16_t get_CD2SLT_pin(CartridgeID id) {
    return CD2_SLT[id];
}

uint16_t get_D3_slt_status(CartridgeID id) {
    return SLT_STATUS[id];
}


//****************************************************************************
//****************************************************************************
// COMPACT FLASH READ OPERATION
//****************************************************************************
//****************************************************************************
void read_compact_flash(uint8_t *TempStorage, CartridgeID id)//this initiates the complete read cycle for  compact flash
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
 Configure_GPIO_IO_D2(0);
 write_address_port(0);
 HAL_GPIO_WritePin(GPIOD,CF_WE,1);				//disable write signal
 HAL_GPIO_WritePin(GPIOB,CF_OE,1);				//disable output enable of compact flash
 HAL_GPIO_WritePin(GPIOD,get_CE_pin(id),0);
 HAL_GPIO_WritePin(GPIOD,CF_RST,1);				//Enable reset
 HAL_Delay(50);
 HAL_GPIO_WritePin(GPIOD,CF_RST,0);				//disable reset
 HAL_Delay(250);
}
//****************************************************************************
//COMMAND_FOR _READ
//****************************************************************************
void command_for_read(void)						 //function to intiate read command for compact flash
{
 Configure_GPIO_IO_D2(1);					 //set the mode of port p1 as output port
 write_address_port(sector_count); 				 //address the register pointer of CF to sector counter register
 write_port( 0x01);								 //since single page is accessed at a time put value 0x01 into port p1
 HAL_GPIO_WritePin(GPIOD, CF_WE, 0);	         //intiate write so that that value 0x01 is stored in
 HAL_GPIO_WritePin(GPIOD, CF_WE, 1);			 //sector count register
 write_address_port(sector_num);				 //address the register pointer of CF to sector number register
 write_port( sector_number);  					 //send the sector number from which you want to read the data
 HAL_GPIO_WritePin(GPIOD, CF_WE, 0);			 //intiate the write signal
 HAL_GPIO_WritePin(GPIOD, CF_WE, 1);
 write_address_port(cyc_low);					 //address the register pointer of CF to cylinder low register
 write_port(Address_Flash_Page1);				 //send the cylinder low value from which you want to read the data
 HAL_GPIO_WritePin(GPIOD, CF_WE, 0);			 //intiate the write signal
 HAL_GPIO_WritePin(GPIOD, CF_WE, 1);
 write_address_port(cyc_high);					 //address the register pointer of CF to cylinder high register
 write_port(Address_Flash_Page1  >> 8);			 //send the cylinder high value from which you want to read the data
 HAL_GPIO_WritePin(GPIOD, CF_WE, 0);			 //intiate the write signal
 HAL_GPIO_WritePin(GPIOD, CF_WE, 1);
 write_address_port(drive);						 //address the register pointer of CF to drive register
 write_port(0xE0);								 //send the drive value from which you want to read the data
 HAL_GPIO_WritePin(GPIOD, CF_WE, 0);			 //intiate the write signal
 HAL_GPIO_WritePin(GPIOD, CF_WE, 1);
 write_address_port(command);					 //address the register pointer of CF to comand register
 write_port(0x20);								 //send the command value from which you want to read the data
 HAL_GPIO_WritePin(GPIOD, CF_WE, 0);			 //intiate the write signal
 HAL_GPIO_WritePin(GPIOD, CF_WE, 1);
 HAL_Delay(5);									 //call delay function
 Configure_GPIO_IO_D2(0);					 //set the mode of port p1 as input port
}
//****************************************************************************
//POST_READ_COMPACT_FLASH
//****************************************************************************
void compact_flash_read(uint16_t *TempStorage,uint16_t datalength)
{
 write_address_port(data_reg) ;				 //address the register pointer to point to data regester
 Configure_GPIO_IO_D2(0);				 //ready to access data from the CF
 for (uint16_t x=0 ;x<datalength ;x++)		 //if data counter 'x'<512 then
 {
  HAL_GPIO_WritePin(GPIOB, CF_OE, 0);		 //activate the output enable of CF
  *TempStorage = Read_port();				 //read the data from the CF and store it in address specified by 'pread'
  HAL_GPIO_WritePin(GPIOB, CF_OE, 1);	     //disbale output enable of CF
  TempStorage++;							 //increment 'pread'
 }
}
//****************************************************************************
//POST_READ_COMPACT_FLASH
//****************************************************************************
void post_read_compact_flash(CartridgeID id)				 //reset the mode of all the ports to input
{
 HAL_GPIO_WritePin(GPIOD, get_CE_pin(id), 1);	    //disable compact flash chip
 HAL_GPIO_WritePin(GPIOD, CF_WE, 1);	    //disable write enable signal
 HAL_GPIO_WritePin(GPIOD, CF_RST,1);	    //disable reset signal of compact flash
 HAL_GPIO_WritePin(GPIOB, CF_OE, 1);	    //disable output enable of compact flash
}


//****************************************************************************
//****************************************************************************
// COMPACT FLASH WRITE OPERATION
//****************************************************************************
//****************************************************************************
void write_compact_flash(uint16_t* TempStorage, CartridgeID id)	//this initiates the complete write cycle for compact flash
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
 Configure_GPIO_IO_D2(0);
 write_address_port(0);
 HAL_GPIO_WritePin(GPIOD,CF_WE,1);				//disable write signal
 HAL_GPIO_WritePin(GPIOB,CF_OE,1);				//disable output enable of compact flash
 HAL_GPIO_WritePin(GPIOD,get_CE_pin(id),0);
 HAL_GPIO_WritePin(GPIOD,CF_RST,1);				//Enable reset
 HAL_Delay(50);
 HAL_GPIO_WritePin(GPIOD,CF_RST,0);				//disable reset
 HAL_Delay(250);
}
//****************************************************************************
//COMPACT_FLASH_READY
//****************************************************************************
unsigned char compact_flash_ready(void)         //function to find wheather the compact flash is ready or not
{
 unsigned char reg_status;
 write_address_port(status_reg);                //send 0x07 to CF so that select status register of CF
 HAL_Delay(5);			                        //call delay function
 unsigned int BusyCnt;

 BusyCnt = 0;
 do                   	                        //check for error if one it means previous
 {						                        //command was ended with error
  HAL_GPIO_WritePin(GPIOB,CF_OE,0);
  reg_status = (unsigned char)Read_port();
  HAL_GPIO_WritePin(GPIOB,CF_OE,1);
  BusyCnt++;
 }while(((reg_status & 0x80) == 0x80) && (BusyCnt<5000));

 BusyCnt = 0;
 do                   	                        //check for error if one it means previous
 {						                        //command was ended with error
  HAL_GPIO_WritePin(GPIOB,CF_OE,0);
  reg_status = Read_port();
  HAL_GPIO_WritePin(GPIOB,CF_OE,1);
  BusyCnt++;
 }while(((reg_status & 0x40) == 0x00) && (BusyCnt<5000));

 BusyCnt = 0;
 do                   	                        //check for error if one it means previous
 {						                        //command was ended with error
  HAL_GPIO_WritePin(GPIOB,CF_OE,0);
  reg_status = Read_port();
  HAL_GPIO_WritePin(GPIOB,CF_OE,1);
  BusyCnt++;
 }while(((reg_status & 0x10) == 0x00) && (BusyCnt<5000));
 return reg_status;
}
//****************************************************************************
//COMMAND_FOR _WRITE
//****************************************************************************
void command_for_write()	                    //this function is used to initiate write command to CF
{
 Configure_GPIO_IO_D2(1);	                //set the mode of port p1 as output port
 write_address_port(sector_count); 				//address the register pointer of CF to sector counter register
 write_port(0x01);								//since single page is accessed at a time put value 0x01 into port p1
 HAL_GPIO_WritePin(GPIOD, CF_WE, 0);    		//initiate write so that that value 0x01 is stored in
 HAL_GPIO_WritePin(GPIOD, CF_WE, 1);    		//sector count register
 write_address_port(sector_num);	    		//address the register pointer of CF to sector number register
 write_port(sector_number);			    		//send the sector number from which you want to read the data
 HAL_GPIO_WritePin(GPIOD, CF_WE, 0);    		//initiate the write signal
 HAL_GPIO_WritePin(GPIOD, CF_WE, 1);
 write_address_port(cyc_low);		    		//address the register pointer of CF to cylinder low register
 write_port(Address_Flash_Page1);				//send the cylinder low value from which you want to read the data
 HAL_GPIO_WritePin(GPIOD, CF_WE, 0);    		//initiate the write signal
 HAL_GPIO_WritePin(GPIOD, CF_WE, 1);
 write_address_port(cyc_high);		    		//address the register pointer of CF to cylinder high register
 write_port(Address_Flash_Page1 >> 8);			//send the cylinder high value from which you want to read the data
 HAL_GPIO_WritePin(GPIOD, CF_WE, 0);    		//initiate the write signal
 HAL_GPIO_WritePin(GPIOD, CF_WE, 1);
 write_address_port(drive);			    		//address the register pointer of CF to drive register
 write_port(0xE0);								//send the drive value from which you want to read the data
 HAL_GPIO_WritePin(GPIOD, CF_WE, 0);    		//initiate the write signal
 HAL_GPIO_WritePin(GPIOD, CF_WE, 1);
 write_address_port(command);					//address the register pointer of CF to comand register
 write_port(0x30);								//send the command value from which you want to WRITE the data
 HAL_GPIO_WritePin(GPIOD, CF_WE, 0);    		//initiate the write signal
 HAL_GPIO_WritePin(GPIOD, CF_WE, 1);
 Configure_GPIO_IO_D2(0);
}
//****************************************************************************
//CHEAK_STATUS_FOR_COMPACT_FLASH
//****************************************************************************
unsigned char check_stat_of_compact_flash(void)	//function to check wheather the command is initiated properly or not
{
 unsigned char reg_status;
 write_address_port(status_reg);				//address the register pointer to point to status regester
 HAL_Delay(5);								    //wait for some delay //if error then quit
 unsigned int BusyCnt;

 BusyCnt = 0;
 do
 {
  HAL_GPIO_WritePin(GPIOB,CF_OE,0);
  reg_status = Read_port();
  HAL_GPIO_WritePin(GPIOB,CF_OE,1);
  BusyCnt++;
 }while(((reg_status & 0x80) == 0x80)&& (BusyCnt < 500000));

 BusyCnt = 0;
 do
 {
  HAL_GPIO_WritePin(GPIOB,CF_OE,0);
  reg_status = Read_port();
  HAL_GPIO_WritePin(GPIOB,CF_OE,1);
  BusyCnt++;;
 }while(((reg_status & 0x40) == 0x00) && (BusyCnt < 500000));

 BusyCnt = 0;
 do
 {
  HAL_GPIO_WritePin(GPIOB,CF_OE,0);
  reg_status = Read_port();
  HAL_GPIO_WritePin(GPIOB,CF_OE,1);
  BusyCnt++;
 }while(((reg_status & 0x10) == 0x00) && (BusyCnt < 500000));

 BusyCnt = 0;
 do
 {
  HAL_GPIO_WritePin(GPIOB,CF_OE,0);
  reg_status = Read_port();
  HAL_GPIO_WritePin(GPIOB,CF_OE,1);
  BusyCnt++;
 }while(((reg_status & 0x08) == 0x00) && (BusyCnt < 500000));

 BusyCnt = 0;
 do
 {
  HAL_GPIO_WritePin(GPIOB,CF_OE,0);
  reg_status = Read_port();
  HAL_GPIO_WritePin(GPIOB,CF_OE,1);
  BusyCnt++;
 }while(((reg_status & 0x01) == 0x01) && (BusyCnt < 500000));
 return reg_status;
}
//****************************************************************************
//WRITE_COMPACT_FLASH
//****************************************************************************
void compact_flash_write(uint16_t* TempStorage,uint16_t datalength)	//if command was sussesful then start writing data to CF
{
 unsigned char busy;
 Configure_GPIO_IO_D2(1);				//set the mode of port p1 as output port
 write_address_port(data_reg);				//address the register pointer to point to data regester
 HAL_Delay(5);
 for (uint16_t x=0 ;x<datalength ;x++)      //if data counter value <last page size then
 {
  write_port(*TempStorage) ;			    //read the data from XRAM location load it into the CF
  HAL_GPIO_WritePin(GPIOD, CF_WE, 0);
  HAL_GPIO_WritePin(GPIOD, CF_WE, 1);	    //disable write signal of compact flash
  TempStorage++;					        //increment XRAM pointer
 }
 Configure_GPIO_IO_D2(0);
 write_address_port(status_reg);
 HAL_GPIO_WritePin(GPIOB, CF_OE, 0);
 do                   	                    //check for error if one it means previous
 {						                    //command was ended with error
  busy = Read_port();
 }while((busy & 0x80) == 0x80);
 HAL_GPIO_WritePin(GPIOB, CF_OE, 1);
}
//****************************************************************************
//POST_WRITE_COMPACT_FLASH
//****************************************************************************
void post_write_compact_flash(CartridgeID id)
{
 HAL_GPIO_WritePin(GPIOD, get_CE_pin(id), 1);	    //disable compact flash chip
 HAL_GPIO_WritePin(GPIOD, CF_WE, 1);	    //disable write enable signal
 HAL_GPIO_WritePin(GPIOD, CF_RST,1);	    //disable reset signal of compact flash
 HAL_GPIO_WritePin(GPIOB, CF_OE, 1);	    //disable output enable of compact flash
}
//****************************************************************************
//****************************************************************************
//
//****************************************************************************
//****************************************************************************
uint8_t D3_Cartridge_Check(CartridgeID id)
{
   return HAL_GPIO_ReadPin(GPIOC, get_CD2SLT_pin(id));
}

//****************************************************************************
//
//****************************************************************************
void write_address_port(uint8_t data)
{
 HAL_GPIO_WritePin(GPIOD, A3, ((data & 0x08) >> 3));
 HAL_GPIO_WritePin(GPIOD, A2, ((data & 0x04) >> 2));
 HAL_GPIO_WritePin(GPIOD, A1, ((data & 0x02) >> 1));
 HAL_GPIO_WritePin(GPIOD, A0, (data & 0x01));
}
//****************************************************************************
//POST_READ_COMPACT_FLASH
//****************************************************************************
uint8_t Read_address_port()
{
 uint8_t ret =
 (HAL_GPIO_ReadPin(GPIOD, A3) << 3) |
 (HAL_GPIO_ReadPin(GPIOD, A2) << 2) |
 (HAL_GPIO_ReadPin(GPIOD, A1) << 1) |
 (HAL_GPIO_ReadPin(GPIOD, A0));
 return ret;
}
//****************************************************************************
//
//****************************************************************************
void generic_CF_CMD(unsigned char sector_num1,unsigned char Cmd, unsigned char CylinderLow, unsigned char CylinderHigh)	//this function is used to initiate write command to CF
{
 Configure_GPIO_IO_D2(1);	                        //set the mode of port p1 as output port
 write_address_port(sector_count); 				        //address the register pointer of CF to sector counter register
 write_port(0x01);						                //since single page is accessed at a time put value 0x01 into port p1
 HAL_GPIO_WritePin(GPIOD, CF_WE, 0);					//intiate write so that that value 0x01 is stored in
 HAL_GPIO_WritePin(GPIOD, CF_WE, 1);					//sector count register
 write_address_port(sector_num);				        //address the register pointer of CF to sector number register
 write_port(sector_num1);			                    //send the sector number from which you want to read the data
 HAL_GPIO_WritePin(GPIOD, CF_WE, 0);					//intiate the write signal
 HAL_GPIO_WritePin(GPIOD, CF_WE, 1);
 write_address_port(cyc_low);					        //address the register pointer of CF to cylinder low register
 write_port(CylinderLow);		                        //send the cylinder low value from which you want to read the data
 HAL_GPIO_WritePin(GPIOD, CF_WE, 0);					//intiate the write signal
 HAL_GPIO_WritePin(GPIOD, CF_WE, 1);
 write_address_port(cyc_high);					        //address the register pointer of CF to cylinder high register
 write_port(CylinderHigh);	                            //send the cylinder high value from which you want to read the data
 HAL_GPIO_WritePin(GPIOD, CF_WE, 0);					//intiate the write signal
 HAL_GPIO_WritePin(GPIOD, CF_WE, 1);
 write_address_port(drive);					            //address the register pointer of CF to drive register
 write_port(0xE0);						                //send the drive value from which you want to read the data
 HAL_GPIO_WritePin(GPIOD, CF_WE, 0);					//intiate the write signal
 HAL_GPIO_WritePin(GPIOD, CF_WE, 1);
 write_address_port(command);					        //address the register pointer of CF to comand register
 write_port(Cmd);						                //send the command value from which you want to WRITE the data
 HAL_GPIO_WritePin(GPIOD, CF_WE, 0);					//intiate the write signal
 HAL_GPIO_WritePin(GPIOD, CF_WE, 1);
 Configure_GPIO_IO_D2(0);
}



/// Configure the entire PE0–PE15 block as inputs or outputs
static void Configure_GPIO_IO_D2(uint8_t output_enable)
{
    // 1) Make sure GPIOE clock is enabled
    __HAL_RCC_GPIOE_CLK_ENABLE();

    if (output_enable)
    {
        // PE0–PE15 = push-pull outputs, low speed, floating
        GPIOE->MODER   = (GPIOE->MODER   & ~D2_MODER_MASK)   | D2_MODER_OUTPUT;
        GPIOE->OSPEEDR = (GPIOE->OSPEEDR & ~D2_OSPEEDR_MASK) | D2_OSPEEDR_LOW;
        GPIOE->PUPDR   = (GPIOE->PUPDR   & ~D2_PUPDR_MASK)   | D2_PUPDR_FLOAT;
    }
    else
    {
        // PE0–PE15 = inputs (floating)
        GPIOE->MODER   = (GPIOE->MODER   & ~D2_MODER_MASK)   | D2_MODER_INPUT;
        GPIOE->OSPEEDR = (GPIOE->OSPEEDR & ~D2_OSPEEDR_MASK) | D2_OSPEEDR_INPUT;
        GPIOE->PUPDR   = (GPIOE->PUPDR   & ~D2_PUPDR_MASK)   | D2_PUPDR_INPUT;
    }
}

void TesCompactFlashDriver(CartridgeID id)
{
	uint8_t *data;
	uint8_t TxdataBuff[512];
	data = (uint8_t *)malloc(512*sizeof(uint8_t));

	uint16_t i=0,j=0;
	for(i=0;i<512;i++,j++)
	{
		data[i] = j;
		if(j==255)
			j=0;
	}
	write_compact_flash(&data[0], id);
	read_compact_flash(&TxdataBuff[0], id);
}

static void write_port(uint16_t data)
{
    GPIOE->ODR = (GPIOE->ODR & ~DATA_MASK_16) | ((uint32_t)data & DATA_MASK_16);
}

static uint16_t Read_port(void)
{
    return (uint16_t)(GPIOE->IDR & DATA_MASK_16);
}

void UpdateD3SlotStatus()
{
	SLT_STATUS[0] = HAL_GPIO_ReadPin(GPIOC, CD2_SLT[CARTRIDGE_1]);
	SLT_STATUS[1] = HAL_GPIO_ReadPin(GPIOC, CD2_SLT[CARTRIDGE_2]);
	SLT_STATUS[2] = HAL_GPIO_ReadPin(GPIOC, CD2_SLT[CARTRIDGE_3]);
	SLT_STATUS[3] = HAL_GPIO_ReadPin(GPIOC, CD2_SLT[CARTRIDGE_4]);
}

uint16_t get_D3_Green_LedPins(CartridgeID id) {
    return GREEN_LED[id];
}

uint16_t get_D3_Red_LedPins(CartridgeID id) {
    return RED_LED[id];
}

void setGreenLed(CartridgeID id, uint8_t value)
{
	if(value ==1)
	HAL_GPIO_WritePin(GPIOA, get_D3_Green_LedPins(id)
			    				   , GPIO_PIN_SET);
	else
		HAL_GPIO_WritePin(GPIOA, get_D3_Green_LedPins(id)
					    				   , GPIO_PIN_RESET);
}
void setRedLed(CartridgeID id, uint8_t value)
{
	if(value ==1)
	HAL_GPIO_WritePin(GPIOA, get_D3_Red_LedPins(id)
			    				   , GPIO_PIN_SET);
	else
		HAL_GPIO_WritePin(GPIOA, get_D3_Red_LedPins(id)
					    				   , GPIO_PIN_RESET);
}
