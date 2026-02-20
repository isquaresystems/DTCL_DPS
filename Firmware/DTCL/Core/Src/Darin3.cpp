#include "Darin3.h"

#include <string.h>
#include <stdlib.h>
#include "Protocol/safeBuffer.h"
#include "Protocol/IspProtocolDefs.h"

uint8_t GuiCtrlLed_SubCmdProcess::LED_CTRL_STATE = 0;

Darin3::Darin3() : readerOpen_(false), writerOpen_(false), completeReadSize_(0)
{
	FatFsWrapper& fs = FatFsWrapper::getInstance();
	if (!fs.isMounted())
	{
		fs.mount();
	}
	// register all your files once here
	registerAllKnownFiles();
}

Darin3::~Darin3()
{
	closeReadStream();
	closeWriteStream();
}

void Darin3::closeReadStream() {
	if (readerOpen_) {
		reader_.close();
		readerOpen_ = false;
	}
}

void Darin3::closeWriteStream() {
	if (writerOpen_) {
		writer_.close();
		writerOpen_ = false;
	}
}

// In Darin3.cpp

uint32_t Darin3::prepareForRx(const uint8_t* data,
                               const uint8_t  subcmd,
                               uint32_t       len)
{


    // Otherwise data[0] = file‐ID, data[1] = cartNo (unused here)
    uint8_t fileId = data[0];
    uint8_t cartID = data[1];

    // 1) Ensure filesystem is mounted
    FatFsWrapper& fs = FatFsWrapper::getInstance();
    fs.setCurrentCart(static_cast<CartridgeID>(cartID));
    if (!fs.isMounted()) {
        if (fs.mount() != FR_OK) {
            return 1;  // mount failure
        }
    }

    // If this is an ERASE command, nothing to receive:
    if (subcmd == static_cast<uint8_t>(IspSubCommand::D3_ERASE))
    {
    	FRESULT res = fs.deleteAllFiles("/");
    	return res;
    }

    // 2) (Re)open the write stream for this file, truncating it
    closeWriteStream();
    FRESULT result = writer_.open(fs, fileId, FatFsWrapper::FileStream::Mode::Write, /*truncate=*/true);
    if (result != FR_OK) {
        return 2;  // could not open file
    }
    if(len==0)
    {
       writer_.close();
	   writerOpen_ = false;
    }
    else
    writerOpen_ = true;

    // Ready to receive chunks
    return 0;
}


uint8_t Darin3::processRxData(const uint8_t* data,
                              const uint8_t  subcmd,
                              uint32_t       len)
{
    // If we never opened a writer, we can't write:
    if (!writerOpen_ || !writer_.isOpen()) {
        return 1;  // no active RX session
    }

    // A zero-length chunk signals “end of stream”
    if (len == 0) {
        writer_.sync();    // flush any buffered writes
        closeWriteStream();    // close file
        return 0;           // success
    }

    // Write this chunk
    UINT written = 0;
    FRESULT r = writer_.writeNext(data, static_cast<UINT>(len), written);
    if (r != FR_OK) {
        closeWriteStream();    // abort
        return static_cast<uint8_t>(r ? r : 2);
    }
    // Keep the writer open for next chunk
    return 0;
}


uint8_t Darin3::prepareDataToTx(const uint8_t* data,
                                const uint8_t  subcmd,
                                uint32_t&      outLen)
{
    uint8_t msgId  = data[0];
    uint8_t cartNo = data[1];
    outLen = 0;
    static uint32_t completeReadSize = 0;
    uint32_t FileSize=0;
    FatFsWrapper& fs = FatFsWrapper::getInstance();
    fs.fileSize(msgId, FileSize);

    if (subcmd == (uint8_t)IspSubCommand::D3_READ_FILES)
    {
        uint8_t packet[1024];
        size_t packetSize = fs.buildFilePacket(packet, sizeof(packet));
        SafeWriteToTxBuffer(packet, 0, packetSize);
        outLen = packetSize;
        return 0;
    }

    // 1) On the very first call for this msgId, open the read‐stream:
    if (!readerOpen_) {
        FRESULT result = reader_.open(fs, msgId, FatFsWrapper::FileStream::Mode::Read);
        if (result != FR_OK || !reader_.isOpen()) {
            closeReadStream();
            completeReadSize=0;
            return 1;            // nothing to send or error
        }
        readerOpen_ = true;
    }

    // 2) Read the next chunk, with up to N retries if we get 0 bytes
    const int   maxRetries = 20;
    int         attempts  = 0;
    UINT        actuallyRead = 0;
    FRESULT     r = FR_OK;

    do {
        r = reader_.readNext(txBuffer, 22400, actuallyRead);
        if (r != FR_OK) {
            // I/O error → abort entirely
            outLen = 0;
            completeReadSize=0;
            closeReadStream();
            return static_cast<uint8_t>(r);
        }
        ++attempts;
    } while (actuallyRead == 0 && attempts < maxRetries);

    // 3) If still zero after retries, treat as EOF
    if (actuallyRead == 0) {
        outLen = 0;
        closeReadStream();
        //return 1;  // signal "done" (or you could return a special retry‐exhausted code)
    }


    // 4) Otherwise report how many bytes to send
    outLen = actuallyRead;
    completeReadSize= completeReadSize + actuallyRead;
    if(completeReadSize>=FileSize)
    {
    	completeReadSize=0;
    	closeReadStream();
    }
    return 0;    // success
}



void Darin3::TestDarinIIIFlash()
{

}

void Darin3::registerAllKnownFiles()
{
    FatFsWrapper& fs = FatFsWrapper::getInstance();
    fs.registerFile(3,  "DR.bin");
    fs.registerFile(4,  "STR.bin");
    fs.registerFile(5,  "WP.bin");
    fs.registerFile(6,  "FPL.bin");
    fs.registerFile(7,  "THT.bin");
    fs.registerFile(8,  "SPJ.bin");
    fs.registerFile(9,  "RWR.bin");
    fs.registerFile(10, "IFFA_PRI.bin");
    fs.registerFile(11, "IFFA_SEC.bin");
    fs.registerFile(12, "IFFB_PRI.bin");
    fs.registerFile(13, "IFFB_SEC.bin");
    fs.registerFile(14, "INCOMKEY.bin");
    fs.registerFile(15, "INCOMCRY.bin");
    fs.registerFile(16, "INCOMMNE.bin");
    fs.registerFile(17, "MONT2.bin");
    fs.registerFile(18, "CMDS.bin");
    fs.registerFile(20, "MISSION1.bin");
    fs.registerFile(21, "MISSION2.bin");
    fs.registerFile(22, "UPDATE.bin");
    fs.registerFile(23, "USAGE.bin");
    fs.registerFile(24, "LRU.bin");
    fs.registerFile(25, "DLSPJ.bin");
    fs.registerFile(26, "DLRWR.bin");
    fs.registerFile(27, "TGT123.bin");
    fs.registerFile(28, "TGT456.bin");
    fs.registerFile(29, "TGT78.bin");
    fs.registerFile(30, "NAV.bin");
    fs.registerFile(31, "HPTSPT.bin");
    fs.registerFile(35, "pc.bin");
}




