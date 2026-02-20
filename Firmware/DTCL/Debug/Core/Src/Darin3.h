#pragma once
#include "Protocol/IVennaSubCommandHandler.h"
#include "Darin3Cart_Driver.h"
#include "stm32f4xx_hal.h"
#include "main.h"
#include <stdint.h>
#include <string>
#include <vector>
#include "FAT/FatFsWrapper.h"
#include <memory>    // for std::shared_ptr


class Darin3 : public IVennaSubCommandHandler {
public:
	Darin3();
	Darin3(std::shared_ptr<FatFsWrapper> fs);
	uint32_t prepareForRx(const uint8_t* data, const uint8_t subcmd,uint32_t len) override;
	uint8_t processRxData(const uint8_t* data, const uint8_t subcmd, uint32_t len) override;
    uint8_t prepareDataToTx(const uint8_t* data, const uint8_t subcmd, uint32_t& outLen) override;
    void TestDarinIIIFlash();
    void registerAllKnownFiles();

private:
    static constexpr uint32_t SIZE = 10240;
    uint8_t flashData[SIZE];
    uint32_t storedLength = 0;
    #define Output 1
    #define Input  0

    int m_Address_Flash_Page;
    int m_NumBlocks;
    int m_Last_Block_size;
    CartridgeID m_cartID;
    uint16_t m_D3_No_of_files;
    std::shared_ptr<FatFsWrapper> fs;
    std::unique_ptr<FatFsWrapper::FileStream> reader_;
    std::unique_ptr<FatFsWrapper::FileStream> writer_;

};

class ReadD3FileInfo_SubCmdProcess : public IVennaSubCommandHandler {
public:

	ReadD3FileInfo_SubCmdProcess(){};
	ReadD3FileInfo_SubCmdProcess(std::shared_ptr<FatFsWrapper> fs) : fs_(std::move(fs))
	{
		if (!fs_->isMounted()) {
			fs_->mount();
		}
	}

	virtual uint16_t processCmdReq(uint8_t* reqData) override
	{
		DecodeCmdReq(reqData);
		//uint8_t slot_No = rxBuffer[0];
		//uint8_t pin_state = rxBuffer[1];
		//setRedLed(CartridgeID(slot_No-1), pin_state);
		// scan and build ID array…
		std::vector<FatFsWrapper::FileInfo> files;
		fs_->scanFiles("/", files);

		size_t count = 0;
		//uint8_t* ids = buildFileIdArray(files, count);
		auto packet = buildFileIdVectorWithSize(files);

		ushort txLen;
		if (count) {
			txLen = EnocdeCmdRes(
					(uint8_t)VennaSubCommand::D3_READ,
					packet.data(),
					static_cast<ushort>(packet.size())
			);
		} else {
			txLen = EnocdeCmdRes(
					static_cast<uint8_t>(getSubCmd()),
					nullptr, 0
			);
		}
		//delete[] ids;
		return txLen;
	};
	virtual VennaSubCommand getSubCmd() override
	{
		return VennaSubCommand::D3_READ;
	};

	std::vector<uint8_t> buildFileIdVectorWithSize(
	    const std::vector<FatFsWrapper::FileInfo>& files)
	{
	    size_t count = files.size();
	    //if (count == 0) {
	    //    return {};
	    //}

	    std::vector<uint8_t> out;
	    out.reserve(1 + count * 5);

	    // Byte 0 = file count
	    out.push_back(static_cast<uint8_t>(count & 0xFF));

	    // For each file: push ID then 4-byte big-endian size
	    for (auto& fi : files) {
	        out.push_back(static_cast<uint8_t>(fi.id & 0xFF));
	        uint32_t sz = fi.size;
	        out.push_back(static_cast<uint8_t>((sz >> 24) & 0xFF));
	        out.push_back(static_cast<uint8_t>((sz >> 16) & 0xFF));
	        out.push_back(static_cast<uint8_t>((sz >>  8) & 0xFF));
	        out.push_back(static_cast<uint8_t>( sz        & 0xFF));
	    }

	    return out;
	};

private:
	std::shared_ptr<FatFsWrapper>  fs_;
};

class Erase_SubCmdProcess : public IVennaSubCommandHandler {
public:

	Erase_SubCmdProcess(){};
	Erase_SubCmdProcess(std::shared_ptr<FatFsWrapper> fs) : fs_(std::move(fs))
	{
		if (!fs_->isMounted()) {
			fs_->mount();
		}
	}

	virtual uint16_t processCmdReq(uint8_t* reqData) override
	{
		DecodeCmdReq(reqData);
		FRESULT res = fs_->deleteAllFiles("/");
		uint8_t result[1] = {static_cast<uint8_t>(res)};
		ushort txLen = EnocdeCmdRes((uint8_t)VennaSubCommand::D3_READ, &result[0], 1 );
		return txLen;
	};
	virtual VennaSubCommand getSubCmd() override
	{
		return VennaSubCommand::D3_ERASE;
	};

private:
	std::shared_ptr<FatFsWrapper>  fs_;
};
