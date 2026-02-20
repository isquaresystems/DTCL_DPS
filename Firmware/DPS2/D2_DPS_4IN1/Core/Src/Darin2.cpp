#include "Darin2.h"
#include <string.h>
#include <stdlib.h>
#include <stdio.h>
#include "Protocol/safeBuffer.h"
#include "Protocol/IspProtocolDefs.h"

uint8_t GuiCtrlLed_SubCmdProcess::LED_CTRL_STATE = 0;

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
            break;
        }

        uint8_t tempPage[512] = {0};
        flash_read(tempPage, readSize, addressFlashPage, m_cartID);
        post_read_flash(m_cartID);

        if (!SafeWriteToTxBuffer(tempPage, bufferOffset, readSize))
        {
            break;
        }

        bufferOffset += readSize;
        addressFlashPage++;
    }

    outLen = bufferOffset;

    return 0;
}

bool Darin2::TestDarinIIFlash(int startPage, int endPage, CartridgeID cartId)
{
    const int PAGE_SIZE = 512;
    uint8_t data[PAGE_SIZE];  // Move OUTSIDE the loop
    uint8_t rxBuffer2[PAGE_SIZE];

            for (int i = 0; i < PAGE_SIZE; ++i)
                data[i] = static_cast<uint8_t>(i & 0xFF);

        for (int page = startPage; page <= endPage; ++page)
        {
            pre_erase_flash(cartId);
            flash_erase(page, cartId);
            post_erase_flash(cartId);

            pre_write_flash(cartId);
            flash_write(data, PAGE_SIZE, page, cartId);
            post_write_flash(cartId);

            pre_read_flash(cartId);
            flash_read(rxBuffer2, PAGE_SIZE, page, cartId);
            post_read_flash(cartId);

            // Compare read data with expected
            for (int i = 0; i < PAGE_SIZE; ++i)
            {
                uint8_t expected = static_cast<uint8_t>(i & 0xFF);
                if (rxBuffer2[i] != expected)
                {
                    return false;
                }
            }
        }
    return true;
}
