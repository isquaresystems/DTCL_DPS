#include "Darin3.h"

#include <cstring>
#include <stdlib.h>
#include "Protocol/safeBuffer.h"
#include "Protocol/logger.h"
#include "Protocol/VennaProtocolDefs.h"
#include <memory>    // for std::shared_ptr

Darin3::Darin3(std::shared_ptr<FatFsWrapper> fs) : fs(std::move(fs))
{
	std::memset(flashData, 0, SIZE);
	if (!fs->isMounted())
	{
		fs->mount();
	}
	// register all your files once here
	registerAllKnownFiles();
}

Darin3::Darin3()
{
	std::memset(flashData, 0, SIZE);
}

// In Darin3.cpp

uint32_t Darin3::prepareForRx(const uint8_t* data,
                               const uint8_t  subcmd,
                               uint32_t       len)
{


    // Otherwise data[0] = file‐ID, data[1] = cartNo (unused here)
    uint8_t fileId = data[0];

    // 1) Ensure filesystem is mounted
    if (!fs->isMounted()) {
        if (fs->mount() != FR_OK) {
            return 1;  // mount failure
        }
    }

    // If this is an ERASE command, nothing to receive:
    if (subcmd == static_cast<uint8_t>(VennaSubCommand::D3_ERASE))
    {
    	FRESULT res = fs->deleteAllFiles("/");
    	return res;
    }

    // 2) (Re)open the write stream for this file, truncating it
    writer_ = fs->openWriteStream(fileId, /*truncate=*/true);
    if (!writer_ || !writer_->isOpen()) {
        writer_.reset();
        return 2;  // could not open file
    }

    // Ready to receive chunks
    return 0;
}


uint8_t Darin3::processRxData(const uint8_t* data,
                              const uint8_t  subcmd,
                              uint32_t       len)
{
    // If we never opened a writer, we can’t write:
    if (!writer_) {
        return 1;  // no active RX session
    }

    // A zero-length chunk signals “end of stream”
    if (len == 0) {
        writer_->sync();    // flush any buffered writes
        writer_.reset();    // close file
        return 0;           // success
    }

    // Write this chunk
    UINT written = 0;
    FRESULT r = writer_->writeNext(data, static_cast<UINT>(len), written);
    if (r != FR_OK) {
        writer_.reset();    // abort
        return static_cast<uint8_t>(r ? r : 2);
    }
    // Keep the writer open for next chunk
    return 0;
}


// In Darin3.cpp
uint8_t Darin3::prepareDataToTx(const uint8_t* data,
                                const uint8_t  subcmd,
                                uint32_t&      outLen)
{
    uint8_t msgId  = data[0];
    uint8_t cartNo = data[1];
    outLen = 0;

    // 1) On the very first call for this msgId, open the read‐stream:
    if (!reader_) {
        reader_ = fs->openReadStream(msgId);
        if (!reader_ || !reader_->isOpen()) {
            reader_.reset();
            return 1;            // nothing to send or error
        }
    }

    // 2) Read the next chunk
    UINT actuallyRead = 0;
    FRESULT r = reader_->readNext(txBuffer, TX_BUFFER_SIZE, actuallyRead);
    if (r != FR_OK) {
        // I/O error → abort
        outLen = 0;
        reader_.reset();
        return static_cast<uint8_t>(r);
    }

    // 3) If EOF (0 bytes), close and signal “done”
    if (actuallyRead == 0) {
        outLen = 0;
        reader_.reset();
        //return 1;
    }

    // 4) Otherwise report how many bytes to send
    outLen = actuallyRead;
    return 0;    // success
}


void Darin3::TestDarinIIIFlash()
{

}

void Darin3::registerAllKnownFiles()
{
    fs->registerFile(3,  "DR.bin");
    fs->registerFile(4,  "STR.bin");
    fs->registerFile(5,  "WP.bin");
    fs->registerFile(6,  "FPL.bin");
    fs->registerFile(7,  "THT.bin");
    fs->registerFile(8,  "SPJ.bin");
    fs->registerFile(9,  "RWR.bin");
    fs->registerFile(10, "IFFA_PRI.bin");
    fs->registerFile(11, "IFFA_SEC.bin");
    fs->registerFile(12, "IFFB_PRI.bin");
    fs->registerFile(13, "IFFB_SEC.bin");
    fs->registerFile(14, "INCOMKEY.bin");
    fs->registerFile(15, "INCOMCRY.bin");
    fs->registerFile(16, "INCOMMNE.bin");
    fs->registerFile(17, "MonT2.bin");
    fs->registerFile(18, "CMDS.bin");
    // note: you skipped 19 in your list
    fs->registerFile(20, "MISSION1.bin");
    fs->registerFile(21, "MISSION2.bin");
    fs->registerFile(22, "UPDATE.bin");
    fs->registerFile(23, "USAGE.bin");
    fs->registerFile(24, "LRU.bin");
    fs->registerFile(25, "DLSPJ.bin");
    fs->registerFile(26, "DLRWR.bin");
    fs->registerFile(27, "TGT123.bin");
    fs->registerFile(28, "TGT456.bin");
    fs->registerFile(29, "TGT78.bin");
    fs->registerFile(30, "NAV.bin");
    fs->registerFile(31, "HPTSPT.bin");
    fs->registerFile(100,"pc.bin");
}


