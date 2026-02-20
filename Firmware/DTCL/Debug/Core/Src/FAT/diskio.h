
#ifndef _DISKIO

#define _READONLY	0	
#define _USE_IOCTL	1	

#include "integer.h"


enum FileReturn {FunctionOk=0,DiskError=1,DiskNotReady=2,FileNotFound=3,
                  PathNotFound=4,FileNotOpened=5,FileNotEnabled=6,NoFileSystem=7};


typedef BYTE	DSTATUS;

unsigned char ChkCF(void);
void RstCf(void);
void Delay(unsigned int,unsigned int);
unsigned char ChkCFRdyForCmd(void);
unsigned char ChkCFRdyForData(void);
void CfCmd(unsigned int,unsigned char,unsigned char);


typedef enum {
	RES_OK = 0,		
	RES_ERROR,		
	RES_WRPRT,		
	RES_NOTRDY,		
	RES_PARERR		
} DRESULT;
int assign_drives (int, int);
DSTATUS disk_initialize (BYTE);
DSTATUS disk_status (BYTE);
DRESULT disk_read (BYTE, BYTE*, DWORD, BYTE);
#if	_READONLY == 0
DRESULT disk_write (BYTE, const BYTE*, DWORD, BYTE);
#endif
DRESULT disk_ioctl (BYTE, BYTE, DWORD*);




#define STA_NOINIT		0x01	
#define STA_NODISK		0x02	
#define STA_PROTECT		0x04	

#define CTRL_SYNC			0	
#define GET_SECTOR_COUNT	1	
#define GET_SECTOR_SIZE		2	
#define GET_BLOCK_SIZE		3	
#define CTRL_ERASE_SECTOR	4	

#define CTRL_POWER			5	
#define CTRL_LOCK			6	
#define CTRL_EJECT			7	

#define MMC_GET_TYPE		10	
#define MMC_GET_CSD			11	
#define MMC_GET_CID			12	
#define MMC_GET_OCR			13	
#define MMC_GET_SDSTAT		14	

#define ATA_GET_REV			20	
#define ATA_GET_MODEL		21	
#define ATA_GET_SN			22	

#define NAND_FORMAT			30	


#define _DISKIO
#endif
