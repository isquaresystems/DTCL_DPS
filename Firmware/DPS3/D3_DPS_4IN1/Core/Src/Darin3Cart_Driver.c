/*
 * Darin3Cartridge.c
 *
 *  Created on: 10-Aug-2023
 *      Author: Vijay
 */
#if 1
#include <stdint.h>
#include "stm32f4xx_ll_gpio.h"
#include "stm32f4xx_ll_bus.h"
#include "main.h"
#include "Darin3Cart_Driver.h"
#include <stdlib.h>

// Helper function to replace HAL_GPIO_WritePin
static inline void GPIO_WritePin(GPIO_TypeDef *GPIOx, uint16_t GPIO_Pin, uint8_t PinState)
{
    if (PinState)
    {
        LL_GPIO_SetOutputPin(GPIOx, GPIO_Pin);
    }
    else
    {
        LL_GPIO_ResetOutputPin(GPIOx, GPIO_Pin);
    }
}

// Helper function to replace GPIO_ReadPin
static inline uint8_t GPIO_ReadPin(GPIO_TypeDef *GPIOx, uint16_t GPIO_Pin)
{
    return LL_GPIO_IsInputPinSet(GPIOx, GPIO_Pin) ? 1 : 0;
}

uint16_t CF_WE  = WE_Pin;
uint16_t CF_RST = RESET_Pin;
uint16_t CF_OE  = ATA_SEL_Pin;
//uint16_t CF_CE  = CSO_1_Pin;

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

uint16_t M_D7  = DB7_Pin;
uint16_t M_D6  = DB6_Pin;
uint16_t M_D5  = DB5_Pin;
uint16_t M_D4  = DB4_Pin;
uint16_t M_D3  = DB3_Pin;
uint16_t M_D2  = DB2_Pin;
uint16_t M_D1  = DB1_Pin;
uint16_t M_D0  = DB0_Pin;

uint16_t CF_CE[4]  = {CSO_1_Pin, CSO_2_Pin, CSO_3_Pin, CSO_4_Pin};
uint16_t CD2_SLT[4]  = {CD2_SLT_S1_Pin, CD2_SLT_S2_Pin, CD2_SLT_S3_Pin, CD2_SLT_S4_Pin};
static uint8_t SLT_STATUS[4]  = {1, 1, 1, 1};
static const uint16_t GREEN_LED[] = { LED1_Pin, LED3_Pin, LED5_Pin, LED7_Pin };
static const uint16_t RED_LED[] = { LED2_Pin, LED4_Pin, LED6_Pin, LED8_Pin };

uint16_t get_CE_pin(CartridgeID id) {
    return CF_CE[id];
}

uint16_t get_CD2SLT_pin(CartridgeID id) {
    return CD2_SLT[id];
}

uint16_t get_D3_slt_status(CartridgeID id) {
    return SLT_STATUS[id];
}

void DataBus_SetOutput(void)
{
    // Ensure GPIOE clock is enabled
    LL_AHB1_GRP1_EnableClock(LL_AHB1_GRP1_PERIPH_GPIOE);

    // Configure each data bus pin as output using LL
    LL_GPIO_SetPinMode(GPIOE, M_D0, LL_GPIO_MODE_OUTPUT);
    LL_GPIO_SetPinOutputType(GPIOE, M_D0, LL_GPIO_OUTPUT_PUSHPULL);
    LL_GPIO_SetPinSpeed(GPIOE, M_D0, LL_GPIO_SPEED_FREQ_LOW);
    LL_GPIO_SetPinPull(GPIOE, M_D0, LL_GPIO_PULL_NO);

    LL_GPIO_SetPinMode(GPIOE, M_D1, LL_GPIO_MODE_OUTPUT);
    LL_GPIO_SetPinOutputType(GPIOE, M_D1, LL_GPIO_OUTPUT_PUSHPULL);
    LL_GPIO_SetPinSpeed(GPIOE, M_D1, LL_GPIO_SPEED_FREQ_LOW);
    LL_GPIO_SetPinPull(GPIOE, M_D1, LL_GPIO_PULL_NO);

    LL_GPIO_SetPinMode(GPIOE, M_D2, LL_GPIO_MODE_OUTPUT);
    LL_GPIO_SetPinOutputType(GPIOE, M_D2, LL_GPIO_OUTPUT_PUSHPULL);
    LL_GPIO_SetPinSpeed(GPIOE, M_D2, LL_GPIO_SPEED_FREQ_LOW);
    LL_GPIO_SetPinPull(GPIOE, M_D2, LL_GPIO_PULL_NO);

    LL_GPIO_SetPinMode(GPIOE, M_D3, LL_GPIO_MODE_OUTPUT);
    LL_GPIO_SetPinOutputType(GPIOE, M_D3, LL_GPIO_OUTPUT_PUSHPULL);
    LL_GPIO_SetPinSpeed(GPIOE, M_D3, LL_GPIO_SPEED_FREQ_LOW);
    LL_GPIO_SetPinPull(GPIOE, M_D3, LL_GPIO_PULL_NO);

    LL_GPIO_SetPinMode(GPIOE, M_D4, LL_GPIO_MODE_OUTPUT);
    LL_GPIO_SetPinOutputType(GPIOE, M_D4, LL_GPIO_OUTPUT_PUSHPULL);
    LL_GPIO_SetPinSpeed(GPIOE, M_D4, LL_GPIO_SPEED_FREQ_LOW);
    LL_GPIO_SetPinPull(GPIOE, M_D4, LL_GPIO_PULL_NO);

    LL_GPIO_SetPinMode(GPIOE, M_D5, LL_GPIO_MODE_OUTPUT);
    LL_GPIO_SetPinOutputType(GPIOE, M_D5, LL_GPIO_OUTPUT_PUSHPULL);
    LL_GPIO_SetPinSpeed(GPIOE, M_D5, LL_GPIO_SPEED_FREQ_LOW);
    LL_GPIO_SetPinPull(GPIOE, M_D5, LL_GPIO_PULL_NO);

    LL_GPIO_SetPinMode(GPIOE, M_D6, LL_GPIO_MODE_OUTPUT);
    LL_GPIO_SetPinOutputType(GPIOE, M_D6, LL_GPIO_OUTPUT_PUSHPULL);
    LL_GPIO_SetPinSpeed(GPIOE, M_D6, LL_GPIO_SPEED_FREQ_LOW);
    LL_GPIO_SetPinPull(GPIOE, M_D6, LL_GPIO_PULL_NO);

    LL_GPIO_SetPinMode(GPIOE, M_D7, LL_GPIO_MODE_OUTPUT);
    LL_GPIO_SetPinOutputType(GPIOE, M_D7, LL_GPIO_OUTPUT_PUSHPULL);
    LL_GPIO_SetPinSpeed(GPIOE, M_D7, LL_GPIO_SPEED_FREQ_LOW);
    LL_GPIO_SetPinPull(GPIOE, M_D7, LL_GPIO_PULL_NO);
}

void DataBus_SetInput(void)
{
    // Ensure GPIOE clock is enabled
    LL_AHB1_GRP1_EnableClock(LL_AHB1_GRP1_PERIPH_GPIOE);

    // Configure each data bus pin as input using LL
    LL_GPIO_SetPinMode(GPIOE, M_D0, LL_GPIO_MODE_INPUT);
    LL_GPIO_SetPinPull(GPIOE, M_D0, LL_GPIO_PULL_NO);

    LL_GPIO_SetPinMode(GPIOE, M_D1, LL_GPIO_MODE_INPUT);
    LL_GPIO_SetPinPull(GPIOE, M_D1, LL_GPIO_PULL_NO);

    LL_GPIO_SetPinMode(GPIOE, M_D2, LL_GPIO_MODE_INPUT);
    LL_GPIO_SetPinPull(GPIOE, M_D2, LL_GPIO_PULL_NO);

    LL_GPIO_SetPinMode(GPIOE, M_D3, LL_GPIO_MODE_INPUT);
    LL_GPIO_SetPinPull(GPIOE, M_D3, LL_GPIO_PULL_NO);

    LL_GPIO_SetPinMode(GPIOE, M_D4, LL_GPIO_MODE_INPUT);
    LL_GPIO_SetPinPull(GPIOE, M_D4, LL_GPIO_PULL_NO);

    LL_GPIO_SetPinMode(GPIOE, M_D5, LL_GPIO_MODE_INPUT);
    LL_GPIO_SetPinPull(GPIOE, M_D5, LL_GPIO_PULL_NO);

    LL_GPIO_SetPinMode(GPIOE, M_D6, LL_GPIO_MODE_INPUT);
    LL_GPIO_SetPinPull(GPIOE, M_D6, LL_GPIO_PULL_NO);

    LL_GPIO_SetPinMode(GPIOE, M_D7, LL_GPIO_MODE_INPUT);
    LL_GPIO_SetPinPull(GPIOE, M_D7, LL_GPIO_PULL_NO);
}

// DataBusDirection enum moved to header file


void DataBus_Configure(DataBusDirection direction)
{
    if(direction == DIR_OUTPUT)
    {
        DataBus_SetOutput();
    }
    else  // DIR_INPUT
    {
        DataBus_SetInput();
    }
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
 DataBus_Configure(DIR_INPUT);
 write_address_port(0);
 GPIO_WritePin(GPIOD,CF_WE,1);				//disable write signal
 GPIO_WritePin(GPIOB,CF_OE,1);				//disable output enable of compact flash
 GPIO_WritePin(GPIOD,get_CE_pin(id),0);
 GPIO_WritePin(GPIOD,CF_RST,1);				//Enable reset
 short_delay_us(50);
 GPIO_WritePin(GPIOD,CF_RST,0);				//disable reset
 short_delay_us(250);
}
//****************************************************************************
//COMMAND_FOR _READ
//****************************************************************************
void command_for_read(void)						 //function to intiate read command for compact flash
{
	DataBus_Configure(DIR_OUTPUT);					 //set the mode of port p1 as output port

 write_address_port(sector_count); 				 //address the register pointer of CF to sector counter register
 short_delay_us(5);
 DataBus_WriteByte( 0x01);								 //since single page is accessed at a time put value 0x01 into port p1
 short_delay_us(2);
 GPIO_WritePin(GPIOD, CF_WE, 0);	         //intiate write so that that value 0x01 is stored in
 short_delay_us(5);
 GPIO_WritePin(GPIOD, CF_WE, 1);			 //sector count register
 short_delay_us(10);

 write_address_port(sector_num);				 //address the register pointer of CF to sector number register
 short_delay_us(5);
 DataBus_WriteByte( sector_number);  					 //send the sector number from which you want to read the data
 short_delay_us(2);
 GPIO_WritePin(GPIOD, CF_WE, 0);			 //intiate the write signal
 short_delay_us(5);
 GPIO_WritePin(GPIOD, CF_WE, 1);
 short_delay_us(10);

 write_address_port(cyc_low);					 //address the register pointer of CF to cylinder low register
 short_delay_us(5);
 DataBus_WriteByte(Address_Flash_Page1);				 //send the cylinder low value from which you want to read the data
 short_delay_us(2);
 GPIO_WritePin(GPIOD, CF_WE, 0);			 //intiate the write signal
 short_delay_us(5);
 GPIO_WritePin(GPIOD, CF_WE, 1);
 short_delay_us(10);

 write_address_port(cyc_high);					 //address the register pointer of CF to cylinder high register
 short_delay_us(5);
 DataBus_WriteByte(Address_Flash_Page1  >> 8);			 //send the cylinder high value from which you want to read the data
 short_delay_us(2);
 GPIO_WritePin(GPIOD, CF_WE, 0);			 //intiate the write signal
 short_delay_us(5);
 GPIO_WritePin(GPIOD, CF_WE, 1);
 short_delay_us(10);

 write_address_port(drive);						 //address the register pointer of CF to drive register
 short_delay_us(5);
 DataBus_WriteByte(0xE0);								 //send the drive value from which you want to read the data
 short_delay_us(2);
 GPIO_WritePin(GPIOD, CF_WE, 0);			 //intiate the write signal
 short_delay_us(5);
 GPIO_WritePin(GPIOD, CF_WE, 1);
 short_delay_us(10);

 write_address_port(command);					 //address the register pointer of CF to comand register
 short_delay_us(5);
 DataBus_WriteByte(0x20);								 //send the command value from which you want to read the data
 short_delay_us(2);
 GPIO_WritePin(GPIOD, CF_WE, 0);			 //intiate the write signal
 short_delay_us(5);
 GPIO_WritePin(GPIOD, CF_WE, 1);
 short_delay_us(100);									 //longer delay after command
 DataBus_Configure(DIR_INPUT);					 //set the mode of port p1 as input port
}
//****************************************************************************
//POST_READ_COMPACT_FLASH
//****************************************************************************
void compact_flash_read(uint8_t *TempStorage,uint16_t datalength)
{
 DataBus_Configure(DIR_INPUT);				 //ready to access data from the CF
 short_delay_us(5);                         // Allow bus direction to settle
 write_address_port(data_reg) ;				 //address the register pointer to point to data regester
 short_delay_us(10);                        // Allow address to settle (increased delay)

 for (uint16_t x=0 ;x<datalength ;x++)		 //if data counter 'x'<512 then
 {
  GPIO_WritePin(GPIOB, CF_OE, 0);		 //activate the output enable of CF
  short_delay_us(2);                     // CF output enable setup time
  *TempStorage = DataBus_ReadByte();				 //read the data from the CF and store it in address specified by 'pread'
  GPIO_WritePin(GPIOB, CF_OE, 1);	     //disbale output enable of CF
  short_delay_us(2);                     // CF output disable hold time
  TempStorage++;							 //increment 'pread'
 }
}
//****************************************************************************
//POST_READ_COMPACT_FLASH
//****************************************************************************
void post_read_compact_flash(CartridgeID id)				 //reset the mode of all the ports to input
{
 GPIO_WritePin(GPIOD, get_CE_pin(id), 1);	    //disable compact flash chip
 GPIO_WritePin(GPIOD, CF_WE, 1);	    //disable write enable signal
 GPIO_WritePin(GPIOD, CF_RST,1);	    //disable reset signal of compact flash
 GPIO_WritePin(GPIOB, CF_OE, 1);	    //disable output enable of compact flash
}


//****************************************************************************
//****************************************************************************
// COMPACT FLASH WRITE OPERATION
//****************************************************************************
//****************************************************************************
void write_compact_flash(uint8_t* TempStorage, CartridgeID id)	//this initiates the complete write cycle for compact flash
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
 DataBus_Configure(DIR_INPUT);
 write_address_port(0);
 GPIO_WritePin(GPIOD,CF_WE,1);				//disable write signal
 GPIO_WritePin(GPIOB,CF_OE,1);				//disable output enable of compact flash
 GPIO_WritePin(GPIOD,get_CE_pin(id),0);
 GPIO_WritePin(GPIOD,CF_RST,1);				//Enable reset
 short_delay_us(50);
 GPIO_WritePin(GPIOD,CF_RST,0);				//disable reset
 short_delay_us(250);
}
//****************************************************************************
//COMPACT_FLASH_READY
//****************************************************************************
unsigned char compact_flash_ready(void)         //function to find wheather the compact flash is ready or not
{
 unsigned char reg_status;
 DataBus_Configure(DIR_INPUT);               // Ensure data bus is input for reading status - MATCH ComprehensiveTest512
 write_address_port(status_reg);                //send 0x07 to CF so that select status register of CF - MATCH ComprehensiveTest512
 short_delay_us(10);			                        //MATCH ComprehensiveTest512 timing
 GPIO_WritePin(GPIOB,CF_OE,0);                   // MATCH ComprehensiveTest512 - simple single read
 reg_status = DataBus_ReadByte();                // MATCH ComprehensiveTest512
 GPIO_WritePin(GPIOB,CF_OE,1);                   // MATCH ComprehensiveTest512

 return reg_status;
}
//****************************************************************************
//COMMAND_FOR _WRITE
//****************************************************************************
void command_for_write()	                    //this function is used to initiate write command to CF
{
	DataBus_Configure(DIR_OUTPUT);		                //set the mode of port p1 as output port

 write_address_port(sector_count); 				//address the register pointer of CF to sector counter register
 short_delay_us(5);
 DataBus_WriteByte(0x01);								//since single page is accessed at a time put value 0x01 into port p1
 short_delay_us(2);
 GPIO_WritePin(GPIOD, CF_WE, 0);    		//initiate write so that that value 0x01 is stored in
 short_delay_us(5);
 GPIO_WritePin(GPIOD, CF_WE, 1);    		//sector count register
 short_delay_us(10);

 write_address_port(sector_num);	    		//address the register pointer of CF to sector number register
 short_delay_us(5);
 DataBus_WriteByte(sector_number);			    		//send the sector number from which you want to read the data
 short_delay_us(2);
 GPIO_WritePin(GPIOD, CF_WE, 0);    		//initiate the write signal
 short_delay_us(5);
 GPIO_WritePin(GPIOD, CF_WE, 1);
 short_delay_us(10);

 write_address_port(cyc_low);		    		//address the register pointer of CF to cylinder low register
 short_delay_us(5);
 DataBus_WriteByte(Address_Flash_Page1);				//send the cylinder low value from which you want to read the data
 short_delay_us(2);
 GPIO_WritePin(GPIOD, CF_WE, 0);    		//initiate the write signal
 short_delay_us(5);
 GPIO_WritePin(GPIOD, CF_WE, 1);
 short_delay_us(10);

 write_address_port(cyc_high);		    		//address the register pointer of CF to cylinder high register
 short_delay_us(5);
 DataBus_WriteByte(Address_Flash_Page1 >> 8);			//send the cylinder high value from which you want to read the data
 short_delay_us(2);
 GPIO_WritePin(GPIOD, CF_WE, 0);    		//initiate the write signal
 short_delay_us(5);
 GPIO_WritePin(GPIOD, CF_WE, 1);
 short_delay_us(10);

 write_address_port(drive);			    		//address the register pointer of CF to drive register
 short_delay_us(5);
 DataBus_WriteByte(0xE0);								//send the drive value from which you want to read the data
 short_delay_us(2);
 GPIO_WritePin(GPIOD, CF_WE, 0);    		//initiate the write signal
 short_delay_us(5);
 GPIO_WritePin(GPIOD, CF_WE, 1);
 short_delay_us(10);

 write_address_port(command);					//address the register pointer of CF to comand register
 short_delay_us(5);
 DataBus_WriteByte(0x30);								//send the command value from which you want to WRITE the data
 short_delay_us(2);
 GPIO_WritePin(GPIOD, CF_WE, 0);    		//initiate the write signal
 short_delay_us(5);
 GPIO_WritePin(GPIOD, CF_WE, 1);
 short_delay_us(100);                     //longer delay after command
 DataBus_Configure(DIR_INPUT);
}
//****************************************************************************
//CHEAK_STATUS_FOR_COMPACT_FLASH
//****************************************************************************
unsigned char check_stat_of_compact_flash(void)	//function to check wheather the command is initiated properly or not
{
 unsigned char reg_status;
 DataBus_Configure(DIR_INPUT);              // Ensure data bus is input for reading status
 short_delay_us(2);                         // Allow bus direction to settle
 write_address_port(status_reg);				//address the register pointer to point to status regester
 short_delay_us(100);								    //wait for command to be processed - MATCH ComprehensiveTest512
 unsigned int BusyCnt;

 // Wait for not busy (bit 7 = 0) and data ready/DRQ (bit 3 = 1) - EXACTLY like ComprehensiveTest512
 BusyCnt = 0;
 do
 {
  GPIO_WritePin(GPIOB,CF_OE,0);
  short_delay_us(2);                        // MATCH ComprehensiveTest512
  reg_status = DataBus_ReadByte();
  GPIO_WritePin(GPIOB,CF_OE,1);
  short_delay_us(50);                       // MATCH ComprehensiveTest512 timing
  BusyCnt++;
 }while(((reg_status & 0x80) != 0 || (reg_status & 0x08) == 0) && (BusyCnt < 50000)); // MATCH ComprehensiveTest512 timeout

 return reg_status;
}
//****************************************************************************
//WRITE_COMPACT_FLASH
//****************************************************************************
void compact_flash_write(uint8_t* TempStorage,uint16_t datalength)	//if command was sussesful then start writing data to CF
{
 unsigned char busy;
 DataBus_Configure(DIR_OUTPUT);					//set the mode of port p1 as output port
 write_address_port(data_reg);				//address the register pointer to point to data regester
 short_delay_us(10);                       // Allow address to settle
 for (uint16_t x=0 ;x<datalength ;x++)      //if data counter value <last page size then
 {
  DataBus_WriteByte(*TempStorage) ;			    //read the data from XRAM location load it into the CF
  short_delay_us(1);                     // Setup time
  GPIO_WritePin(GPIOD, CF_WE, 0);
  short_delay_us(2);                     // WE pulse width
  GPIO_WritePin(GPIOD, CF_WE, 1);	    //disable write signal of compact flash
  short_delay_us(1);                     // Hold time
  TempStorage++;					        //increment XRAM pointer
 }
 DataBus_Configure(DIR_INPUT);
 write_address_port(status_reg);
 short_delay_us(100);                      // Allow time for write completion
 GPIO_WritePin(GPIOB, CF_OE, 0);
 do                   	                    //check for error if one it means previous
 {						                    //command was ended with error
  busy = DataBus_ReadByte();
  short_delay_us(10);                    // Delay between status reads
 }while((busy & 0x80) == 0x80);
 GPIO_WritePin(GPIOB, CF_OE, 1);
}
//****************************************************************************
//POST_WRITE_COMPACT_FLASH
//****************************************************************************
void post_write_compact_flash(CartridgeID id)
{
 GPIO_WritePin(GPIOD, get_CE_pin(id), 1);	    //disable compact flash chip
 GPIO_WritePin(GPIOD, CF_WE, 1);	    //disable write enable signal
 GPIO_WritePin(GPIOD, CF_RST,1);	    //disable reset signal of compact flash
 GPIO_WritePin(GPIOB, CF_OE, 1);	    //disable output enable of compact flash
}
//****************************************************************************
//****************************************************************************
//
//****************************************************************************
//****************************************************************************
uint8_t D3_Cartridge_Check()
{
 // Configure POWER_CYCLE_1_Pin as input with pull-up
 LL_GPIO_SetPinMode(GPIOD, POWER_CYCLE_1_Pin, LL_GPIO_MODE_INPUT);
 LL_GPIO_SetPinPull(GPIOD, POWER_CYCLE_1_Pin, LL_GPIO_PULL_UP);
 return GPIO_ReadPin(GPIOD, POWER_CYCLE_1_Pin);
}

//****************************************************************************
//
//****************************************************************************
void write_address_port(uint8_t data)
{
 GPIO_WritePin(GPIOD, A3, ((data & 0x08) >> 3));
 GPIO_WritePin(GPIOD, A2, ((data & 0x04) >> 2));
 GPIO_WritePin(GPIOD, A1, ((data & 0x02) >> 1));
 GPIO_WritePin(GPIOD, A0, (data & 0x01));
}
//****************************************************************************
//POST_READ_COMPACT_FLASH
//****************************************************************************
uint8_t Read_address_port()
{
 uint8_t ret =
 (GPIO_ReadPin(GPIOD, A3) << 3) |
 (GPIO_ReadPin(GPIOD, A2) << 2) |
 (GPIO_ReadPin(GPIOD, A1) << 1) |
 (GPIO_ReadPin(GPIOD, A0));
 return ret;
}
//****************************************************************************
//
//****************************************************************************
void generic_CF_CMD(unsigned char sector_num1,unsigned char Cmd, unsigned char CylinderLow, unsigned char CylinderHigh)	//this function is used to initiate write command to CF
{
	DataBus_Configure(DIR_OUTPUT);		                        //set the mode of port p1 as output port
 write_address_port(sector_count); 				        //address the register pointer of CF to sector counter register
 DataBus_WriteByte(0x01);						                //since single page is accessed at a time put value 0x01 into port p1
 GPIO_WritePin(GPIOD, CF_WE, 0);					//intiate write so that that value 0x01 is stored in
 GPIO_WritePin(GPIOD, CF_WE, 1);					//sector count register
 write_address_port(sector_num);				        //address the register pointer of CF to sector number register
 DataBus_WriteByte(sector_num1);			                    //send the sector number from which you want to read the data
 GPIO_WritePin(GPIOD, CF_WE, 0);					//intiate the write signal
 GPIO_WritePin(GPIOD, CF_WE, 1);
 write_address_port(cyc_low);					        //address the register pointer of CF to cylinder low register
 DataBus_WriteByte(CylinderLow);		                        //send the cylinder low value from which you want to read the data
 GPIO_WritePin(GPIOD, CF_WE, 0);					//intiate the write signal
 GPIO_WritePin(GPIOD, CF_WE, 1);
 write_address_port(cyc_high);					        //address the register pointer of CF to cylinder high register
 DataBus_WriteByte(CylinderHigh);	                            //send the cylinder high value from which you want to read the data
 GPIO_WritePin(GPIOD, CF_WE, 0);					//intiate the write signal
 GPIO_WritePin(GPIOD, CF_WE, 1);
 write_address_port(drive);					            //address the register pointer of CF to drive register
 DataBus_WriteByte(0xE0);						                //send the drive value from which you want to read the data
 GPIO_WritePin(GPIOD, CF_WE, 0);					//intiate the write signal
 GPIO_WritePin(GPIOD, CF_WE, 1);
 write_address_port(command);					        //address the register pointer of CF to comand register
 DataBus_WriteByte(Cmd);						                //send the command value from which you want to WRITE the data
 GPIO_WritePin(GPIOD, CF_WE, 0);					//intiate the write signal
 GPIO_WritePin(GPIOD, CF_WE, 1);
 DataBus_Configure(DIR_INPUT);
}

// Write with setup/hold time
void DataBus_WriteByte(uint8_t data)
{
    // Set data on bus with proper setup time
    GPIOE->BSRR = (0xFF << 16) | data;

    // Setup time delay - CompactFlash typically needs 30ns minimum
    short_delay_us(1);  // 1 microsecond should be plenty
}

// Read with proper timing
uint8_t DataBus_ReadByte(void)
{
    uint8_t data;

    // Access time delay - CompactFlash typically needs 50-100ns
    short_delay_us(1);  // 1 microsecond for safety

    // Read data twice for stability (in case of bus capacitance)
    data = (uint8_t)(GPIOE->IDR & 0xFF);
    short_delay_us(1);
    data = (uint8_t)(GPIOE->IDR & 0xFF);

    return data;
}

void UpdateD3SlotStatus()
{
	for(int itr=0;itr<4;itr++)
	{
		if( 0 == GPIO_ReadPin(GPIOC, CD2_SLT[itr]))
		{
			SLT_STATUS[itr] = 0x03;
		}
		else
		{
			SLT_STATUS[itr] = 0x00;
		}
	}
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
	GPIO_WritePin(GPIOA, get_D3_Green_LedPins(id), 1);
	else
		GPIO_WritePin(GPIOA, get_D3_Green_LedPins(id), 0);
}
void setRedLed(CartridgeID id, uint8_t value)
{
	if(value ==1)
	GPIO_WritePin(GPIOA, get_D3_Red_LedPins(id), 1);
	else
		GPIO_WritePin(GPIOA, get_D3_Red_LedPins(id), 0);
}

void short_delay_us(uint32_t us) {
    for (volatile uint32_t i = 0; i < us * 8; i++) {
        __NOP();  // One NOP ~1 cycle
    }
}

void slotLedBlink(CartridgeID id, uint8_t value)
{
    uint16_t red_pin = get_D3_Red_LedPins(id);
    uint16_t green_pin = get_D3_Green_LedPins(id);

    while(value)
    {
        GPIOA->BSRR = red_pin;                      // Set red LED
        GPIOA->BSRR = green_pin;                    // Set green LED
        short_delay_us(500000);
        GPIOA->BSRR = (red_pin << 16);              // Reset red LED
        GPIOA->BSRR = (green_pin << 16);            // Reset green LED
        short_delay_us(500000);
        --value;
    }
}

uint8_t LedLoopBack(uint8_t value)
{
    uint8_t data = 0, data2 = 0;
    while(value)
    {
        GPIOA->ODR = (GPIOA->ODR & 0x1FE00U) | 0x1FF;

        if(GPIOB->IDR & LB1_Pin)
            data = data | 0x01;
        if(GPIOB->IDR & LB2_Pin)
            data = data | 0x02;
        if(GPIOB->IDR & LB3_Pin)
            data = data | 0x04;
        if(GPIOB->IDR & LB4_Pin)
            data = data | 0x08;

        short_delay_us(500000);

        GPIOA->ODR = (GPIOA->ODR & 0x1FE00U) | 0x00;
        short_delay_us(500000);

        if(GPIOB->IDR & LB1_Pin)
            data2 = data2 | 0x01;
        if(GPIOB->IDR & LB2_Pin)
            data2 = data2 | 0x02;
        if(GPIOB->IDR & LB3_Pin)
            data2 = data2 | 0x04;
        if(GPIOB->IDR & LB4_Pin)
            data2 = data2 | 0x08;

        --value;
    }
    if((data == 0x0F ) && (data2 == 0))
        return 0;
    else
        return 1;
}

void BlinkAllLed(uint8_t value)
{
	while(value)
	{
		// Toggle PA1-PA8
		GPIOA->ODR ^= 0x1FE;  // 0x1FE = bits 1-8
		short_delay_us(500000);

		// Toggle again to return to original state
		GPIOA->ODR ^= 0x1FE;
		short_delay_us(500000);
	}
}

/******************** below are test function in case CF not working ********************/
#if 0
// New comprehensive 512-byte test function
void ComprehensiveTest512(CartridgeID id)
{
	uint8_t write_data[512];
	uint8_t read_data[512];

	// Initialize test pattern - 0, 1, 2, ... 255, 0, 1, 2, ... (repeating)
	for(int i = 0; i < 512; i++)
	{
		write_data[i] = i & 0xFF;  // Same as i % 256
	}

	// Proper CF initialization sequence
	DataBus_Configure(DIR_INPUT);
	write_address_port(0);
	GPIO_WritePin(GPIOD, CF_WE, 1);        // Disable write signal
	GPIO_WritePin(GPIOB, CF_OE, 1);        // Disable output enable
	GPIO_WritePin(GPIOD, get_CE_pin(id), 0); // Enable chip select for this cartridge
	GPIO_WritePin(GPIOD, CF_RST, 1);       // Enable reset
	short_delay_us(100);
	GPIO_WritePin(GPIOD, CF_RST, 0);       // Disable reset
	short_delay_us(2000);  // Wait for CF to initialize

	// Check initial status
	DataBus_Configure(DIR_INPUT);
	write_address_port(status_reg);
	short_delay_us(10);
	GPIO_WritePin(GPIOB, CF_OE, 0);
	volatile uint8_t initial_status = DataBus_ReadByte();
	GPIO_WritePin(GPIOB, CF_OE, 1);

	// === WRITE OPERATION ===
	DataBus_Configure(DIR_OUTPUT);

	// Set up write command for sector 0
	write_address_port(sector_count);
	short_delay_us(5);
	DataBus_WriteByte(0x01);  // 1 sector
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(10);

	write_address_port(sector_num);
	short_delay_us(5);
	DataBus_WriteByte(0x00);  // Sector 0
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(10);

	write_address_port(cyc_low);
	short_delay_us(5);
	DataBus_WriteByte(0x00);  // Cylinder low
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(10);

	write_address_port(cyc_high);
	short_delay_us(5);
	DataBus_WriteByte(0x00);  // Cylinder high
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(10);

	write_address_port(drive);
	short_delay_us(5);
	DataBus_WriteByte(0xE0);  // Drive/head
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(10);

	write_address_port(command);
	short_delay_us(5);
	DataBus_WriteByte(0x30);  // Write sectors command
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(100);  // Wait for command acceptance

	// Wait for CF to be ready for data (DRQ = 1)
	DataBus_Configure(DIR_INPUT);
	write_address_port(status_reg);
	short_delay_us(100);

	volatile uint8_t write_status;
	int timeout = 50000;
	do {
		GPIO_WritePin(GPIOB, CF_OE, 0);
		short_delay_us(2);
		write_status = DataBus_ReadByte();
		GPIO_WritePin(GPIOB, CF_OE, 1);
		short_delay_us(50);
		timeout--;
	} while (((write_status & 0x80) != 0 || (write_status & 0x08) == 0) && timeout > 0);

	// Write all 512 bytes
	DataBus_Configure(DIR_OUTPUT);
	write_address_port(data_reg);
	short_delay_us(10);

	for (int i = 0; i < 512; i++) {
		DataBus_WriteByte(write_data[i]);
		short_delay_us(1);
		GPIO_WritePin(GPIOD, CF_WE, 0);
		short_delay_us(2);
		GPIO_WritePin(GPIOD, CF_WE, 1);
		short_delay_us(1);
	}

	// Wait for write completion
	DataBus_Configure(DIR_INPUT);
	write_address_port(status_reg);
	short_delay_us(1000);

	volatile uint8_t write_complete_status;
	timeout = 100000;
	do {
		GPIO_WritePin(GPIOB, CF_OE, 0);
		short_delay_us(2);
		write_complete_status = DataBus_ReadByte();
		GPIO_WritePin(GPIOB, CF_OE, 1);
		short_delay_us(100);
		timeout--;
	} while ((write_complete_status & 0x80) != 0 && timeout > 0);

	// === READ OPERATION ===
	DataBus_Configure(DIR_OUTPUT);

	// Set up read command for sector 0
	write_address_port(sector_count);
	short_delay_us(5);
	DataBus_WriteByte(0x01);  // 1 sector
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(10);

	write_address_port(sector_num);
	short_delay_us(5);
	DataBus_WriteByte(0x00);  // Sector 0
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(10);

	write_address_port(cyc_low);
	short_delay_us(5);
	DataBus_WriteByte(0x00);  // Cylinder low
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(10);

	write_address_port(cyc_high);
	short_delay_us(5);
	DataBus_WriteByte(0x00);  // Cylinder high
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(10);

	write_address_port(drive);
	short_delay_us(5);
	DataBus_WriteByte(0xE0);  // Drive/head
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(10);

	write_address_port(command);
	short_delay_us(5);
	DataBus_WriteByte(0x20);  // Read sectors command
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(100);

	// Wait for read command to complete and data ready
	DataBus_Configure(DIR_INPUT);
	write_address_port(status_reg);
	short_delay_us(500);

	volatile uint8_t read_status;
	timeout = 50000;
	do {
		GPIO_WritePin(GPIOB, CF_OE, 0);
		short_delay_us(2);
		read_status = DataBus_ReadByte();
		GPIO_WritePin(GPIOB, CF_OE, 1);
		short_delay_us(50);
		timeout--;
	} while (((read_status & 0x80) != 0 || (read_status & 0x08) == 0) && timeout > 0);

	// Read all 512 bytes
	write_address_port(data_reg);
	short_delay_us(10);

	for (int i = 0; i < 512; i++) {
		GPIO_WritePin(GPIOB, CF_OE, 0);
		short_delay_us(2);
		read_data[i] = DataBus_ReadByte();
		GPIO_WritePin(GPIOB, CF_OE, 1);
		short_delay_us(2);
	}

	// Verify results - check key positions
	volatile uint8_t result_0 = read_data[0];    // Should be 0
	volatile uint8_t result_1 = read_data[1];    // Should be 1
	volatile uint8_t result_10 = read_data[10];  // Should be 10
	volatile uint8_t result_100 = read_data[100]; // Should be 100
	volatile uint8_t result_255 = read_data[255]; // Should be 255
	volatile uint8_t result_256 = read_data[256]; // Should be 0 (pattern repeats)
	volatile uint8_t result_511 = read_data[511]; // Should be 255

	// Count matches for verification
	int matches = 0;
	for (int i = 0; i < 512; i++) {
		if (read_data[i] == write_data[i]) {
			matches++;
		}
	}
	volatile int total_matches = matches;  // Should be 512 for perfect match

	// Status summary
	volatile uint8_t final_status = read_status;

	//int breakpoint_here = 0;  // Set breakpoint here to examine all results
}

void TesCompactFlashDriver(CartridgeID id)
{
	uint8_t write_data[512];
	uint8_t read_data[512];

	// Create test pattern: 0, 1, 2, ..., 255, 0, 1, 2, ... (repeating)
	uint16_t i=0,j=0;
	for(i=0;i<512;i++,j++)
	{
		write_data[i] = j;
		if(j==255)
			j=0;
	}

	// Clear read buffer to ensure we're reading new data
	for(i=0;i<512;i++)
	{
		read_data[i] = 0xFF;  // Fill with 0xFF to distinguish from actual data
	}

	// Test complete write cycle using existing functions
	write_compact_flash(&write_data[0], id);

	// Small delay between write and read operations
	short_delay_us(5000);  // 5ms delay to ensure write completion

	// Check CF status after write before attempting read
	DataBus_Configure(DIR_INPUT);
	write_address_port(status_reg);
	short_delay_us(10);
	GPIO_WritePin(GPIOB, CF_OE, 0);
	short_delay_us(2);
	volatile uint8_t status_after_write = DataBus_ReadByte();
	GPIO_WritePin(GPIOB, CF_OE, 1);

	// Test complete read cycle using existing functions
	read_compact_flash(&read_data[0], id);

	// Check CF status after read
	DataBus_Configure(DIR_INPUT);
	write_address_port(status_reg);
	short_delay_us(10);
	GPIO_WritePin(GPIOB, CF_OE, 0);
	short_delay_us(2);
	volatile uint8_t status_after_read = DataBus_ReadByte();
	GPIO_WritePin(GPIOB, CF_OE, 1);

	// Verification: check key positions in the data
	volatile uint8_t result_0 = read_data[0];    // Should be 0
	volatile uint8_t result_1 = read_data[1];    // Should be 1
	volatile uint8_t result_10 = read_data[10];  // Should be 10
	volatile uint8_t result_100 = read_data[100]; // Should be 100
	volatile uint8_t result_255 = read_data[255]; // Should be 255
	volatile uint8_t result_256 = read_data[256]; // Should be 0 (pattern repeats)
	volatile uint8_t result_511 = read_data[511]; // Should be 255

	// Count successful matches for overall verification
	int matches = 0;
	int mismatches = 0;
	for (i = 0; i < 512; i++) {
		if (read_data[i] == write_data[i]) {
			matches++;
		} else {
			mismatches++;
		}
	}

	// Summary results for debugging
	volatile int total_matches = matches;     // Should be 512 for perfect operation
	volatile int total_mismatches = mismatches; // Should be 0 for perfect operation

	// Check for specific failure patterns
	volatile uint8_t first_mismatch_pos = 0xFF;
	volatile uint8_t first_mismatch_expected = 0xFF;
	volatile uint8_t first_mismatch_actual = 0xFF;

	for (i = 0; i < 512; i++) {
		if (read_data[i] != write_data[i]) {
			first_mismatch_pos = i;
			first_mismatch_expected = write_data[i];
			first_mismatch_actual = read_data[i];
			break;  // Found first mismatch
		}
	}

	// Status check: Test if all functions completed successfully
	volatile uint8_t test_status = 0;
	if (total_matches == 512) {
		test_status = 0xAA;  // Perfect success
	} else if (total_matches > 400) {
		test_status = 0x55;  // Mostly working
	} else if (total_matches > 0) {
		test_status = 0x33;  // Partial success
	} else {
		test_status = 0x00;  // Complete failure
	}

	//int breakpoint_here = 0;  // Set breakpoint here to examine all results
}

// Simple test to check what we're actually reading
void SimpleReadTest(CartridgeID id)
{
	uint8_t test_data[10];

	// Proper CF initialization sequence
	DataBus_Configure(DIR_INPUT);
	write_address_port(0);
	GPIO_WritePin(GPIOD, CF_WE, 1);        // Disable write signal
	GPIO_WritePin(GPIOB, CF_OE, 1);        // Disable output enable
	GPIO_WritePin(GPIOD, get_CE_pin(id), 0); // Enable chip select for this cartridge
	GPIO_WritePin(GPIOD, CF_RST, 1);       // Enable reset
	short_delay_us(50);
	GPIO_WritePin(GPIOD, CF_RST, 0);       // Disable reset
	short_delay_us(1000);  // Wait longer for CF to initialize

	// Check if CF is ready before doing anything
	DataBus_Configure(DIR_INPUT);
	write_address_port(status_reg);
	short_delay_us(10);
	GPIO_WritePin(GPIOB, CF_OE, 0);
	volatile uint8_t initial_status = DataBus_ReadByte();
	GPIO_WritePin(GPIOB, CF_OE, 1);

	// Wait for CF to be ready (bit 7 = 0, bit 6 = 1)
	int timeout = 10000;
	while (((initial_status & 0x80) != 0 || (initial_status & 0x40) == 0) && timeout > 0) {
		short_delay_us(10);
		GPIO_WritePin(GPIOB, CF_OE, 0);
		initial_status = DataBus_ReadByte();
		GPIO_WritePin(GPIOB, CF_OE, 1);
		timeout--;
	}

	// Write a simple pattern to sector 0
	DataBus_Configure(DIR_OUTPUT);

	// Set up write command manually for sector 0
	write_address_port(sector_count);
	short_delay_us(5);
	DataBus_WriteByte(0x01);  // 1 sector
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(10);

	write_address_port(sector_num);
	short_delay_us(5);
	DataBus_WriteByte(0x00);  // Sector 0
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(10);

	write_address_port(cyc_low);
	short_delay_us(5);
	DataBus_WriteByte(0x00);  // Cylinder low
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(10);

	write_address_port(cyc_high);
	short_delay_us(5);
	DataBus_WriteByte(0x00);  // Cylinder high
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(10);

	write_address_port(drive);
	short_delay_us(5);
	DataBus_WriteByte(0xE0);  // Drive/head
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(10);

	write_address_port(command);
	short_delay_us(5);
	DataBus_WriteByte(0x30);  // Write sectors command
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(50);  // Longer delay after command

	// Wait for CF to accept the write command and be ready for data
	DataBus_Configure(DIR_INPUT);
	write_address_port(status_reg);
	short_delay_us(100);  // Longer delay for command to be processed

	volatile uint8_t write_status;
	timeout = 50000;  // Increased timeout
	do {
		GPIO_WritePin(GPIOB, CF_OE, 0);
		short_delay_us(2);
		write_status = DataBus_ReadByte();
		GPIO_WritePin(GPIOB, CF_OE, 1);
		short_delay_us(50);  // Longer delay between status checks
		timeout--;
	} while (((write_status & 0x80) != 0 || (write_status & 0x08) == 0) && timeout > 0);  // Wait for not busy and DRQ

	DataBus_Configure(DIR_OUTPUT);

	// Write 10 test bytes
	write_address_port(data_reg);
	short_delay_us(10);  // Allow address to settle
	for (int i = 0; i < 10; i++) {
		DataBus_WriteByte(i + 0x41);  // Write 'A', 'B', 'C', etc.
		short_delay_us(2);  // Setup time
		GPIO_WritePin(GPIOD, CF_WE, 0);
		short_delay_us(5);  // WE pulse width
		GPIO_WritePin(GPIOD, CF_WE, 1);
		short_delay_us(2);  // Hold time
	}

	// Fill rest of sector with 0xFF
	for (int i = 10; i < 512; i++) {
		DataBus_WriteByte(0xFF);
		short_delay_us(1);  // Setup time
		GPIO_WritePin(GPIOD, CF_WE, 0);
		short_delay_us(2);  // WE pulse width
		GPIO_WritePin(GPIOD, CF_WE, 1);
		short_delay_us(1);  // Hold time
	}

	// Wait for write to complete - check status
	DataBus_Configure(DIR_INPUT);
	write_address_port(status_reg);
	short_delay_us(1000);  // Give more time for write to complete

	volatile uint8_t write_complete_status;
	timeout = 100000;  // Much longer timeout for write completion
	do {
		GPIO_WritePin(GPIOB, CF_OE, 0);
		short_delay_us(2);
		write_complete_status = DataBus_ReadByte();
		GPIO_WritePin(GPIOB, CF_OE, 1);
		short_delay_us(100);  // Longer delay between checks
		timeout--;
	} while ((write_complete_status & 0x80) != 0 && timeout > 0);  // Wait for not busy

	// Now issue read command
	DataBus_Configure(DIR_OUTPUT);

	write_address_port(sector_count);
	short_delay_us(5);
	DataBus_WriteByte(0x01);  // 1 sector
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(10);

	write_address_port(sector_num);
	short_delay_us(5);
	DataBus_WriteByte(0x00);  // Sector 0
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(10);

	write_address_port(cyc_low);
	short_delay_us(5);
	DataBus_WriteByte(0x00);  // Cylinder low
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(10);

	write_address_port(cyc_high);
	short_delay_us(5);
	DataBus_WriteByte(0x00);  // Cylinder high
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(10);

	write_address_port(drive);
	short_delay_us(5);
	DataBus_WriteByte(0xE0);  // Drive/head
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(10);

	write_address_port(command);
	short_delay_us(5);
	DataBus_WriteByte(0x20);  // Read sectors command
	short_delay_us(2);
	GPIO_WritePin(GPIOD, CF_WE, 0);
	short_delay_us(5);
	GPIO_WritePin(GPIOD, CF_WE, 1);
	short_delay_us(50);  // Longer delay after command

	// Wait for read command to complete and data to be ready
	DataBus_Configure(DIR_INPUT);
	write_address_port(status_reg);
	short_delay_us(500);  // Allow time for read command to be processed

	volatile uint8_t read_status;
	timeout = 50000;  // Increased timeout for read
	do {
		GPIO_WritePin(GPIOB, CF_OE, 0);
		short_delay_us(2);
		read_status = DataBus_ReadByte();
		GPIO_WritePin(GPIOB, CF_OE, 1);
		short_delay_us(50);  // Longer delay between status checks
		timeout--;
	} while (((read_status & 0x80) != 0 || (read_status & 0x08) == 0) && timeout > 0);  // Wait for not busy and DRQ

	// Check status
	write_address_port(status_reg);
	short_delay_us(5);
	GPIO_WritePin(GPIOB, CF_OE, 0);
	volatile uint8_t status_after_read_cmd = DataBus_ReadByte();
	GPIO_WritePin(GPIOB, CF_OE, 1);

	// Now read the data
	write_address_port(data_reg);
	short_delay_us(5);

	for (int i = 0; i < 10; i++) {
		GPIO_WritePin(GPIOB, CF_OE, 0);
		short_delay_us(2);
		test_data[i] = DataBus_ReadByte();
		GPIO_WritePin(GPIOB, CF_OE, 1);
		short_delay_us(2);
	}

	// Check our results
	volatile uint8_t result1 = test_data[0];  // Should be 0x41 ('A')
	volatile uint8_t result2 = test_data[1];  // Should be 0x42 ('B')
	volatile uint8_t result3 = test_data[2];  // Should be 0x43 ('C')

	//int breakpoint_here = 0;  // Set breakpoint here to examine values
}
#endif

#endif
