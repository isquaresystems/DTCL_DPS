#include "Darin2.h"
#include <string.h>
#include <stdlib.h>
#include <stdio.h>
#include "Protocol/safeBuffer.h"
#include "Protocol/IspProtocolDefs.h"

Darin2::Darin2() {

}

uint32_t Darin2::prepareForRx(const uint8_t* data, const uint8_t subcmd, uint32_t len)
{
    if (subcmd == (uint8_t)IspSubCommand::D2_ERASE)
    {
    	m_Address_Flash_Page = ((data[0] + 256 * data[1]));
    	m_cartID = static_cast<CartridgeID>(data[5] - 1);

    	pre_erase_flash(m_cartID);
    	for (int i = 0; i < 1024; ++i)
    	{
            int BlockAddress = ((i << 5));
    		unsigned char ans = flash_erase(BlockAddress, m_cartID);
    		if(ans)
    		 return ans;
    	}
    	post_erase_flash(m_cartID);
    	return 0;
    }
    else if(subcmd == (uint8_t)IspSubCommand::D2_ERASE_BLOCK)
    {
    	m_Address_Flash_Page = ((data[0] + 256 * data[1]));
    	m_cartID = static_cast<CartridgeID>(data[5] - 1);
    	pre_erase_flash(m_cartID);
    	unsigned char ans = flash_erase(m_Address_Flash_Page, m_cartID);
    	post_erase_flash(m_cartID);
        return ans;
    }
    else
    {
      m_Address_Flash_Page = ((data[0] + 256 * data[1]));
      m_NumBlocks = data[2];
      m_Last_Block_size = data[3] + 256 * data[4];
      m_cartID = static_cast<CartridgeID>(data[5] - 1);
      return 0;
    }
}

uint8_t Darin2::processRxData(const uint8_t* data, const uint8_t subcmd, uint32_t len)
{
    int rxOffset = 0;
    int currentPage = m_Address_Flash_Page;

    pre_erase_flash(m_cartID);
    flash_erase(currentPage, m_cartID);
    post_erase_flash(m_cartID);

    for (int i = 0; i < m_NumBlocks; ++i)
    {
        pre_write_flash(m_cartID);
        flash_write(&data[rxOffset], 512, currentPage, m_cartID);
        post_write_flash(m_cartID);
        currentPage++;
        rxOffset += 512;
    }

    if (m_Last_Block_size > 0)
    {
        pre_write_flash(m_cartID);
        flash_write(&data[rxOffset], m_Last_Block_size, currentPage, m_cartID);
        post_write_flash(m_cartID);
        rxOffset += m_Last_Block_size;
    }

    storedLength = rxOffset;

    return 0;

    //char msg[64];
    //std::sprintf(msg, "[D2] RX Done: Bytes=%d", storedLength);
   // SimpleLogger::getInstance().log(SimpleLogger::LOG_INFO, msg);
}

uint8_t Darin2::prepareDataToTx(const uint8_t* data, const uint8_t subcmd, uint32_t& outLen)
{
    int addressFlashPage = data[0] + (data[1] << 8);
    int numBlocks = data[2];
    int lastBlockSize = data[3] + (data[4] << 8);
    m_cartID = static_cast<CartridgeID>(data[5]-1);

    int totalBytesToRead = numBlocks * 512 + lastBlockSize;
    int totalFlashPages = (totalBytesToRead + 511) / 512;

    uint32_t bufferOffset = 0;

    for (int i = 0; i < totalFlashPages; ++i)
    {
        pre_read_flash(m_cartID);

        int readSize = 512;
        if (i == totalFlashPages - 1 && lastBlockSize > 0)
            readSize = lastBlockSize;

        if (bufferOffset + readSize > TX_BUFFER_SIZE)
        {
            // Log: TX buffer overflow
            break;
        }

        uint8_t tempPage[512] = {0};
        flash_read(tempPage, readSize, addressFlashPage, m_cartID);
        post_read_flash(m_cartID);

        if (!SafeWriteToTxBuffer(tempPage, bufferOffset, readSize))
        {
            // Log: SafeWriteToTxBuffer failed
            break;
        }

        bufferOffset += readSize;
        addressFlashPage++;
    }

    outLen = bufferOffset;

    //char msg[64];
    //std::sprintf(msg, "[D2] TX ready: Bytes=%lu", static_cast<unsigned long>(outLen));
    //SimpleLogger::getInstance().log(SimpleLogger::LOG_INFO, msg);

    return 0;
}

void Darin2::TestDarinIIFlash(int startPage, int endPage)
{
    const int PAGE_SIZE = 512;
    CartridgeID cartId = CARTRIDGE_2;

    const uint8_t fixedPatterns[] = { 0x00, 0xFF, 0xAA, 0x55 };
    const int numFixedPatterns = sizeof(fixedPatterns) / sizeof(fixedPatterns[0]);

    char logMsg[128];

    for (int p = 0; p < numFixedPatterns + 1; ++p)
    {
        uint8_t data[PAGE_SIZE];

        if (p < numFixedPatterns)
        {
            memset(data, fixedPatterns[p], PAGE_SIZE);
            sprintf(logMsg, "[D2_TEST] Running pattern: 0x%02X", fixedPatterns[p]);
        }
        else
        {
            for (int i = 0; i < PAGE_SIZE; ++i)
                data[i] = i & 0xFF;
            sprintf(logMsg, "[D2_TEST] Running pattern: INCR");
        }

        // Log: Pattern test message

        for (int page = startPage; page <= endPage; ++page)
        {
            pre_erase_flash(cartId);
            flash_erase(page, cartId);
            post_erase_flash(cartId);

            pre_write_flash(cartId);
            flash_write(data, PAGE_SIZE, page, cartId);
            post_write_flash(cartId);

            pre_read_flash(cartId);
            flash_read(rxBuffer, PAGE_SIZE, page, cartId);
            post_read_flash(cartId);

            bool mismatch = false;
            for (int i = 0; i < PAGE_SIZE; ++i)
            {
                uint8_t expected = (p < numFixedPatterns) ? fixedPatterns[p] : (i & 0xFF);
                if (rxBuffer[i] != expected)
                {
                    if (!mismatch)
                    {
                        //std::sprintf(logMsg,
                        //    "[D2_TEST] Mismatch! Page=%d Index=%d Expected=0x%02X Got=0x%02X",
                        //    page, i, expected, rxBuffer[i]);
                       // SimpleLogger::getInstance().log(SimpleLogger::LOG_CRITICAL, logMsg);
                        mismatch = true;
                    }
                }
            }

            if (!mismatch)
            {
               // const char* patternName = (p < numFixedPatterns) ? "FIXED" : "INCR";
                //std::sprintf(logMsg,
                //    "[D2_TEST] Page %d OK for pattern %s", page, patternName);
                //SimpleLogger::getInstance().log(SimpleLogger::LOG_INFO, logMsg);
            }
        }
    }
}
