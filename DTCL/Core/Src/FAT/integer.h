
#ifndef _INTEGER
#define _INTEGER

#ifdef _WIN32	

#include <windows.h>
#include <tchar.h>

#else			

typedef int				INT;
typedef unsigned int	UINT;


typedef char			CHAR;
typedef unsigned char	UCHAR;
typedef unsigned char	BYTE;


typedef short			SHORT;
typedef unsigned short	USHORT;
typedef unsigned short	WORD;
typedef unsigned short	WCHAR;


typedef long			LONG;
typedef unsigned long	ULONG;
typedef unsigned long	DWORD;

#endif

#endif
