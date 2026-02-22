#include "Darin3.h"

#include <cstring>
#include <cstdio>
#include <cstdarg>
#include <stdlib.h>
#include "Protocol/safeBuffer.h"
#include "Protocol/IspProtocolDefs.h"

uint8_t GuiCtrlLed_SubCmdProcess::LED_CTRL_STATE = 0;

Darin3::Darin3() : readerOpen_(false), writerOpen_(false), completeReadSize_(0)
{
    SetCartNo(CARTRIDGE_1);  // Default to cartridge 1
    disk_initialize(0);
	FatFsWrapper& fs = FatFsWrapper::getInstance();
	if(fs.isMounted())
	{
		fs.unmount();
	}
    fs.mount();
}

Darin3::~Darin3()
{
	// Ensure all file handles are properly closed
	closeReadStream();
	closeWriteStream();
}

void Darin3::closeReadStream()
{
	if (readerOpen_) {
		reader_.close();
		readerOpen_ = false;
		completeReadSize_ = 0;  // Reset for next transfer
	}
}

void Darin3::closeWriteStream()
{
	if (writerOpen_) {
		writer_.sync();  // Flush any pending writes
		writer_.close();
		writerOpen_ = false;
	}
}

uint32_t Darin3::prepareForRx(const uint8_t* data,
                               const uint8_t  subcmd,
                               uint32_t       len)
{
    // Ensure any previous streams are properly closed
    closeWriteStream();
    closeReadStream();

    // Otherwise data[0] = file‐ID, data[1] = cartNo
    uint8_t fileId = data[0];
    uint8_t cartID = data[1]-1;
    m_cartID = (CartridgeID)cartID;
    SetCartNo(m_cartID);  // Set in diskio layer

    FatFsWrapper& fs = FatFsWrapper::getInstance();
    fs.setCurrentCart(cartID);  // Track cartridge (no unmount yet)
    if (!fs.isMounted()) {
        fs.mount();  // Mount if needed
    }

    // If this is an ERASE command, nothing to receive:
    if (subcmd == static_cast<uint8_t>(IspSubCommand::D3_ERASE))
    {
    	FRESULT res = fs.deleteAllFiles("/");
    	return res;
    }

    // 2) (Re)open the write stream for this file, truncating it
    FRESULT res = fs.openWriteStream(fileId, writer_, true);
    if (res != FR_OK) {
        writerOpen_ = false;
        return 2;  // could not open file
    }
    writerOpen_ = true;
    // Ready to receive chunks
    return 0;
}

uint8_t Darin3::processRxData(const uint8_t* data,
                              const uint8_t  subcmd,
                              uint32_t       len)
{
    static uint32_t totalWritten = 0;  // Track total bytes written
    static uint32_t chunkCount = 0;    // Track number of chunks received

    // If we never opened a writer, we can't write:
    if (!writerOpen_) {
        return 1;  // no active RX session
    }

    // A zero-length chunk signals "end of stream"
    if (len == 0) {
        closeWriteStream();  // Properly close and sync the file
        totalWritten = 0;    // Reset for next file
        chunkCount = 0;      // Reset chunk counter
        return 0;           // success
    }

    chunkCount++;  // Increment chunk counter

    // Write this chunk
    UINT written = 0;
    FRESULT r = writer_.writeNext(data, static_cast<UINT>(len), written);
    if (r != FR_OK) {
        closeWriteStream();  // Properly close on error
        totalWritten = 0;
        return static_cast<uint8_t>(r);  // Return exact error code
    }

    // Check if actual bytes written match requested
    if (written != len) {
        closeWriteStream();  // Partial write indicates problem
        totalWritten = 0;
        return 5;  // Partial write error code
    }

    totalWritten += written;
    // Keep the writer open for next chunk
    return 0;
}

uint8_t Darin3::prepareDataToTx(const uint8_t* data,
                                const uint8_t  subcmd,
                                uint32_t&      outLen)
{
    uint32_t FileSize = 0;  // Declare at function scope

    // If data is nullptr, this is a continuation call - just read next chunk
    if (data == nullptr) {
        // Continue reading from already open file
        if (!readerOpen_) {
            outLen = 0;
            return 1; // No open file
        }
    }
    else {
        // Otherwise this is the initial call - extract parameters
        uint8_t msgId  = data[0];
        uint8_t cartNo = data[1] - 1;
        SetCartNo(static_cast<CartridgeID>(cartNo));  // Set in diskio layer

        FatFsWrapper& fs = FatFsWrapper::getInstance();
        fs.setCurrentCart(cartNo);  // Track cartridge (no unmount yet)
        if (!fs.isMounted()) {
            fs.mount();  // Mount if needed
        }

        outLen = 0;
        fs.fileSize(msgId, FileSize);

        if (subcmd == (uint8_t)IspSubCommand::D3_READ_FILES)
        {
            uint8_t packet[512];
            uint32_t packetSize = 0;
            FRESULT res = fs.buildFilePacket(packet, sizeof(packet), packetSize);
            if (res == FR_OK) {
                SafeWriteToTxBuffer(packet, 0, packetSize);
                outLen = packetSize;
            } else {
                outLen = 0;
            }
            return 0;
        }

        // 1) On the very first call for this msgId, open the read‐stream:
        if (!readerOpen_) {
            // Close any existing streams first
            closeReadStream();
            closeWriteStream();

            FRESULT res = fs.openReadStream(msgId, reader_);
            if (res != FR_OK) {
                readerOpen_ = false;
                completeReadSize_ = 0;
                return 1;            // nothing to send or error
            }
            readerOpen_ = true;
            completeReadSize_ = 0;   // Reset for new file
        }
    }
    // 2) Read the next chunk, with up to N retries if we get 0 bytes
    const int   maxRetries = 20;
    int         attempts  = 0;
    UINT        actuallyRead = 0;
    FRESULT     r = FR_OK;

    do {
        // Read up to MAX_BUF_SIZE (4KB) chunk from file
        r = reader_.readNext(txBuffer, MAX_BUF_SIZE, actuallyRead);
        if (r != FR_OK) {
            // I/O error → abort entirely and properly close
            outLen = 0;
            closeReadStream();  // Properly close the stream
            return static_cast<uint8_t>(r);
        }
        ++attempts;
    } while (actuallyRead == 0 && attempts < maxRetries);

    // 3) If still zero after retries, treat as EOF
    if (actuallyRead == 0) {
        outLen = 0;
        closeReadStream();  // Properly close the stream
        return 0;  // EOF reached - transmission complete
    }

    // 4) Otherwise report how many bytes to send
    outLen = actuallyRead;
    completeReadSize_ += actuallyRead;

    // 5) Check if we've read the entire file
    if(completeReadSize_ >= FileSize) {
        closeReadStream();  // Properly close - file completely read
    }

    return 0;    // success
}

void Darin3::TestDarinIIIFlash()
{
	uint8_t *data;
	uint8_t dat[512];
	data = (uint8_t *)malloc(512*sizeof(uint8_t));

	uint16_t i=0,j=0;
	for(i=0;i<512;i++,j++)
	{
		data[i] = j;
		if(j==255)
			j=0;
	}
	write_compact_flash(&data[0],CARTRIDGE_1);
	read_compact_flash(&dat[0],CARTRIDGE_1);
	free(data);
}

void Darin3::TesFATfS()
{
	FatFsWrapper& fs = FatFsWrapper::getInstance();
	if(fs.isMounted())
	{
		fs.unmount();
	}
	if (!fs.isMounted())
	{
		fs.mount();
	}

	const char filename1[] = "pc.bin";
	FRESULT res = fs.createFile(3);
	UINT bytesWritten=0;
	res = fs.writeFile(3, (const uint8_t*)filename1, 6, bytesWritten);
    if(!res)
    { // failed
        }
}

