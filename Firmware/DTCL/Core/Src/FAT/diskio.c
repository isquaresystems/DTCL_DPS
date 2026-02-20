#include <stdio.h>
#include "diskio.h"
#include "main.h"
#include "../Darin3Cart_Driver.h"
#include "stm32f4xx_hal.h"
#include <stdlib.h>

#define INT_CF	0
#define CF_IDE_BASE 0x00

#define ReadCmd        0x20 
#define WriteCmd       0x30 
#define EraseCmd       0xC0 
#define IdentifyCmd    0xEC 

unsigned char RdBuf[512];

#define DATA_REG 	   0x00
#define FEATURE_REG    0x01
#define SEC_COUNT_REG  0x02
#define SEC_NUM_REG    0x03
#define CYL_LOW_REG    0x04
#define CYL_HI_REG     0x05
#define DRV_HD_REG     0x06
#define COMMAND_REG    0x07
#define STATUS_REG     0x07

uint16_t CF_OE1  = C3OE_Pin;
CartridgeID m_CartId = 0;

//extern alt_u32 CF_TRANS_FIFO_BASE ;

DWORD get_fattime(void)
{
  return(0x00000000);  
}

DSTATUS disk_initialize(BYTE drv)
{
 DSTATUS stat;
 unsigned char Status;
    
    if(drv == INT_CF)
    {
       Status = ChkCF();
       if(Status == 0)
        { 
         stat = RES_OK;
        }
       else
        {
         stat = STA_NODISK;
        }
       RstCf();
       return stat;
    }
    else
    {
	  return STA_NOINIT;
    }
}

DSTATUS disk_status(BYTE drv)
{
    
    if(drv == INT_CF)
    {
      return(RES_OK);
    }
	return STA_NOINIT;
}

DRESULT disk_read(BYTE drv,BYTE *buff,DWORD sector,BYTE count)
{
DRESULT res;  
unsigned char Status;
unsigned char * BuffPtr = (unsigned char *)buff;
    
	if(drv == INT_CF)
    {
	  pre_read_compact_flash(m_CartId);

	  Status = compact_flash_ready();

	  if(!Status)
		  return RES_NOTRDY;

	  CfCmd(sector,ReadCmd,count);

	  Status = check_stat_of_compact_flash();
	  if(!Status)
	  	 return RES_NOTRDY;

	  compact_flash_read(&BuffPtr[0],512);
	  post_read_compact_flash(m_CartId);
      res = RES_OK;
    }
	else
	 res = RES_ERROR;

    return res;
}

#if _READONLY == 0
DRESULT disk_write(BYTE drv,const BYTE *buff,DWORD sector,BYTE count)
{
	DRESULT res;
	unsigned char Status;
	unsigned char * BuffPtr = (unsigned char *)buff;

	if(drv == INT_CF)
	{
		pre_write_compact_flash(m_CartId);
		Status = compact_flash_ready();

		if(!Status)
				  return RES_NOTRDY;

		CfCmd(sector, WriteCmd, count);

		Status = check_stat_of_compact_flash();

		if(!Status)
				  return RES_NOTRDY;

		compact_flash_write(&BuffPtr[0],512);
		post_write_compact_flash(m_CartId);

		res = RES_OK;
	}
	else
		 res = RES_ERROR;

	return res;
}
#endif

DRESULT disk_ioctl(BYTE drv,BYTE ctrl,DWORD *buff)
{
DRESULT res;  
unsigned int RdData;

unsigned char Status;
    if(drv == INT_CF)
    { 
      
      switch(ctrl)
      {
        case          CTRL_SYNC:
        
                                break;
        case   GET_SECTOR_COUNT:
        	                    /*read_compact_flash(&RdBuf[0]);
                                RdData = RdBuf[61] << 16;
                                *buff  = RdData | RdBuf[60];*/

        	                    pre_read_compact_flash(m_CartId);
        		                Status = compact_flash_ready();
        		                if(!Status)
        			              return RES_NOTRDY;

        		                CfCmd(0,IdentifyCmd,512);
        		                Status = check_stat_of_compact_flash();
        		                if(!Status)

        		  	               return RES_NOTRDY;

        		                compact_flash_read(&RdBuf[0],512);
        		                //RdData = (RdBuf[35] << 24) | (RdBuf[34] << 16) | (RdBuf[33] << 8) | (RdBuf[32] << 0);
        		                unsigned int word60 = RdBuf[120] | (RdBuf[121] << 8);  // Word 60: Lower 16 bits
        		                unsigned int word61 = RdBuf[122] | (RdBuf[123] << 8);  // Word 61: Upper 16 bits
        		                RdData = ((unsigned long)word61 << 16) | word60;

        		                *buff  = RdData ;
        		                //*buff  = 2047248 ; //This is Bad...
        		                post_read_compact_flash(m_CartId);

        	                    break;
        case    GET_SECTOR_SIZE:
                                *buff  = 512;
                                break;
        case     GET_BLOCK_SIZE:
                                *buff  = 512;
                                break;
        case  CTRL_ERASE_SECTOR:
                                *buff  = 512;
                                break;
        default:
                                *buff  = 0;
                                res = RES_ERROR;
                                return res;
                                break;                     
      }
    }
    res = RES_OK;
    return res;
}

unsigned char ChkCFRdyForCmd(void)
{
  unsigned char RdStatus = 0;
  unsigned int BusyCnt = 0;
  Configure_GPIO_IO_D2(0);
  
  write_address_port(STATUS_REG);			 //address the register pointer to point to status regester
  HAL_GPIO_WritePin(GPIOA, CF_OE1, 0);		 //intiate read signal
  short_delay_us(50);								 //wait for some delay

  RdStatus = Read_port();
  RdStatus = RdStatus & 0x01;

  if(RdStatus == 0x01)
  {
	  HAL_GPIO_WritePin(GPIOA, CF_OE1, 1);
      return(0);
  }

  RdStatus = Read_port();
  RdStatus = RdStatus & 0xF0;
  while(RdStatus != 0x50)
  {

	RdStatus = Read_port();
    RdStatus = RdStatus & 0xF0;
    BusyCnt++;
    if(BusyCnt == 500000)
    {
       HAL_GPIO_WritePin(GPIOA, CF_OE1, 1);
       return(0);
    }
  }
  HAL_GPIO_WritePin(GPIOA, CF_OE1, 1);
  return(1);
}

unsigned char ChkCFRdyForData(void)
{
  Configure_GPIO_IO_D2(0);
  unsigned char RdStatus = 0;
  unsigned int BusyCnt = 0;
  
  write_address_port(STATUS_REG);			 //address the register pointer to point to status regester
  HAL_GPIO_WritePin(GPIOA, CF_OE1, 0);		 //intiate read signal
  short_delay_us(50);								 //wait for some delay

  RdStatus = Read_port();

  RdStatus = RdStatus & 0x01;
  if(RdStatus == 0x01)
  {
	  HAL_GPIO_WritePin(GPIOA, CF_OE1, 1);
      return(0);
  }
  RdStatus = Read_port();
  RdStatus = RdStatus & 0xF8;

  while(RdStatus != 0x58)
  {
    RdStatus = Read_port();
    RdStatus = RdStatus & 0xF8;
    BusyCnt++;
    if(BusyCnt == 500000)
    {
       HAL_GPIO_WritePin(GPIOA, CF_OE1, 1);
       return (0);
    }
  }
  HAL_GPIO_WritePin(GPIOA, CF_OE1, 1);
  return(1);
}

void CfCmd(unsigned int LBA,unsigned char Cmd,unsigned char count)
{
   unsigned char SectorNo,CylinderLow,CylinderHigh;
   SectorNo     = LBA & 0x000000FF;
   CylinderLow  = LBA >> 8;
   CylinderLow  = CylinderLow & 0x000000FF;
   CylinderHigh = LBA >> 16; 
   CylinderHigh = CylinderHigh & 0x000000FF;
   
   generic_CF_CMD(SectorNo, Cmd, CylinderLow, CylinderHigh);
}

unsigned char ChkCF(void)
{
   unsigned char CfDetect = 0;
   //TODO: To be implemented
   //CfDetect = IORD_8DIRECT(CFDETECT_BASE,0x00);
   //CfDetect = (CfDetect & 0x01);
   return(CfDetect);
}

void RstCf(void)
{
	//TODO: To be implemented
   //IOWR_32DIRECT(CFNRST_BASE,0x00,0x00000000);
   HAL_GPIO_WritePin(GPIOA, C3_PWR_CYCLE_Pin, 0);
   short_delay_us(1000);
   HAL_GPIO_WritePin(GPIOA, C3_PWR_CYCLE_Pin, 1);
   short_delay_us(1000);
   //IOWR_32DIRECT(CFNRST_BASE,0x00,0xFFFFFFFF);
   //Delay(1000,1000);
}

void Delay(unsigned int a,unsigned int b)
{
  unsigned int x,y;
  for(x=0;x<a;x++)
  {
     for(y=0;y<b;y++)
     {
        
     }
  }  
}

void SetCartNo(CartridgeID id)
{
	m_CartId = id;
}



