// FatFsWrapperSingleton.cpp - Simple implementation without STL
#include "FatFsWrapperSingleton.h"
#include <stdio.h>  // for snprintf

// Define the known files list
const FatFsWrapper::FileEntry FatFsWrapper::kKnownFiles[] = {
    {  3, "DR.BIN"       },
    {  4, "STR.BIN"      },
    {  5, "WP.BIN"       },
    {  6, "FPL.BIN"      },
    {  7, "THT.BIN"      },
    {  8, "SPJ.BIN"      },
    {  9, "RWR.BIN"      },
    { 10, "IFFA_PRI.BIN" },
    { 11, "IFFA_SEC.BIN" },
    { 12, "IFFB_PRI.BIN" },
    { 13, "IFFB_SEC.BIN" },
    { 14, "INCOMKEY.BIN" },
    { 15, "INCOMCRY.BIN" },
    { 16, "INCOMMNE.BIN" },
    { 17, "MonT2.BIN"    },
    { 18, "CMDS.BIN"     },
    { 20, "MISSION1.BIN" },
    { 21, "MISSION2.BIN" },
    { 22, "UPDATE.BIN"   },
    { 23, "USAGE.BIN"    },
    { 24, "LRU.BIN"      },
    { 25, "DLSPJ.BIN"    },
    { 26, "DLRWR.BIN"    },
    { 27, "TGT123.BIN"   },
    { 28, "TGT456.BIN"   },
    { 29, "TGT78.BIN"    },
    { 30, "NAV.BIN"      },
    { 31, "HPTSPT.BIN"   },
    { 35, "pc.BIN"       },
};

const size_t FatFsWrapper::kKnownFileCount = sizeof(kKnownFiles) / sizeof(kKnownFiles[0]);

int FatFsWrapper::lookupFileId(const char* filename) {
    if (!filename) return -1;

    for (size_t i = 0; i < kKnownFileCount; ++i) {
        if (strcmp(kKnownFiles[i].name, filename) == 0) {
            return kKnownFiles[i].id;
        }
    }
    return -1;
}

const char* FatFsWrapper::getFilenameById(int id) const {
    for (size_t i = 0; i < kKnownFileCount; ++i) {
        if (kKnownFiles[i].id == id) {
            return kKnownFiles[i].name;
        }
    }
    return nullptr;
}

FRESULT FatFsWrapper::mount() {
    // If already mounted, unmount first
    if (mounted_) {
        f_mount(nullptr, "", 0);
        mounted_ = false;
    }

    // Now mount
    FRESULT r = f_mount(&internalFs_, "", 1);
    mounted_ = (r == FR_OK);
    return r;
}

FRESULT FatFsWrapper::unmount() {
    if (mounted_) {
        FRESULT r = f_mount(nullptr, "", 0);
        mounted_ = false;
        return r;
    }
    return FR_OK;
}

void FatFsWrapper::forceCartridgeReinit(CartridgeID id) {
    // Force complete disk reinitialization for cartridge switching
    ForceCartridgeReinit(id);
}

FRESULT FatFsWrapper::createFile(int id, BYTE mode) {
    const char* filename = getFilenameById(id);
    if (!filename) return FR_NO_FILE;
    if (!mounted_ && mount() != FR_OK) return FR_NOT_READY;

    FIL f;
    FRESULT r = f_open(&f, filename, mode);
    if (r == FR_OK) f_close(&f);
    return r;
}

FRESULT FatFsWrapper::deleteFile(int id) {
    const char* filename = getFilenameById(id);
    if (!filename) return FR_NO_FILE;
    if (!mounted_ && mount() != FR_OK) return FR_NOT_READY;

    return f_unlink(filename);
}

FRESULT FatFsWrapper::fileSize(int id, uint32_t& size) {
    const char* filename = getFilenameById(id);
    if (!filename) return FR_NO_FILE;
    if (!mounted_ && mount() != FR_OK) return FR_NOT_READY;

    FILINFO fi;
    FRESULT r = f_stat(filename, &fi);
    if (r == FR_OK) size = fi.fsize;
    return r;
}

FRESULT FatFsWrapper::writeFile(int id, const void* data, UINT bytesToWrite, UINT& bytesWritten, FSIZE_t offset) {
    if (!data && bytesToWrite > 0) return FR_INVALID_PARAMETER;
    const char* filename = getFilenameById(id);
    if (!filename) return FR_NO_FILE;
    if (!mounted_ && mount() != FR_OK) return FR_NOT_READY;

    FIL f;
    FRESULT r = f_open(&f, filename, FA_WRITE | FA_OPEN_ALWAYS);
    if (r != FR_OK) return r;

    if ((r = f_lseek(&f, offset)) == FR_OK) {
        r = f_write(&f, data, bytesToWrite, &bytesWritten);
    }
    f_close(&f);
    return r;
}

FRESULT FatFsWrapper::readFile(int id, void* buffer, UINT bytesToRead, UINT& bytesRead, FSIZE_t offset) {
    if (!buffer && bytesToRead > 0) return FR_INVALID_PARAMETER;
    const char* filename = getFilenameById(id);
    if (!filename) return FR_NO_FILE;
    if (!mounted_ && mount() != FR_OK) return FR_NOT_READY;

    FIL f;
    FRESULT r = f_open(&f, filename, FA_READ);
    if (r != FR_OK) return r;

    if ((r = f_lseek(&f, offset)) == FR_OK) {
        r = f_read(&f, buffer, bytesToRead, &bytesRead);
    }
    f_close(&f);
    return r;
}

FRESULT FatFsWrapper::deleteAllFiles(const char* dirPath) {
    if (!dirPath) return FR_INVALID_PARAMETER;
    if (!mounted_ && mount() != FR_OK) return FR_NOT_READY;

    DIR dir;
    FILINFO fno;
    FRESULT res;
    char filepath[MAX_PATH_LENGTH];

    res = f_opendir(&dir, dirPath);
    if (res != FR_OK) return res;

    for (;;) {
        res = f_readdir(&dir, &fno);
        if (res != FR_OK || fno.fname[0] == '\0') break;

        if (strcmp(fno.fname, ".") == 0 || strcmp(fno.fname, "..") == 0) continue;

        if (!(fno.fattrib & AM_DIR)) {
            snprintf(filepath, sizeof(filepath), "%s/%s", dirPath, fno.fname);
            FRESULT unlink_res = f_unlink(filepath);
            if (unlink_res != FR_OK) {
                f_closedir(&dir);
                return unlink_res;
            }
        }
    }

    f_closedir(&dir);
    return FR_OK;
}

FRESULT FatFsWrapper::scanFiles(const char* dirPath, FileInfo* outFiles, size_t maxFiles, size_t& fileCount) {
    fileCount = 0;
    if (!dirPath || !outFiles) return FR_INVALID_PARAMETER;
    if (!mounted_ && mount() != FR_OK) return FR_NOT_READY;

    DIR dir;
    FILINFO fno;
    FRESULT res = f_opendir(&dir, dirPath);
    if (res != FR_OK) return res;

    for (;;) {
        res = f_readdir(&dir, &fno);
        if (res != FR_OK || fno.fname[0] == '\0') break;
        if (fno.fattrib & AM_DIR) continue;

        int id = lookupFileId(fno.fname);
        if (id >= 0 && fileCount < maxFiles) {
            outFiles[fileCount].id = id;
            strncpy(outFiles[fileCount].name, fno.fname, sizeof(outFiles[fileCount].name) - 1);
            outFiles[fileCount].name[sizeof(outFiles[fileCount].name) - 1] = '\0';
            outFiles[fileCount].size = fno.fsize;
            outFiles[fileCount].attr = fno.fattrib;
            fileCount++;
        }
    }

    f_closedir(&dir);
    return FR_OK;
}

int FatFsWrapper::buildFilePacket(uint8_t* packet, size_t packetSize) {
   // Ensure mounted
    if (!isMounted()) mount();

    FileInfo files[MAX_SCANNED_FILES];
    size_t fileCount = 0;
    scanFiles("/", files, MAX_SCANNED_FILES, fileCount);

    memset(packet, 0, packetSize);

    // First byte: number of files
    packet[0] = (uint8_t)(fileCount > 255 ? 255 : fileCount);

    size_t offset = 1;
    for (size_t i = 0; i < fileCount && offset + 18 < packetSize; i++) {
        // 14 bytes for filename
        memcpy(packet + offset, files[i].name, 14);
        offset += 14;

        // 4 bytes for size (little-endian)
        packet[offset++] = (uint8_t)(files[i].size & 0xFF);
        packet[offset++] = (uint8_t)((files[i].size >> 8) & 0xFF);
        packet[offset++] = (uint8_t)((files[i].size >> 16) & 0xFF);
        packet[offset++] = (uint8_t)((files[i].size >> 24) & 0xFF);
    }

    return 512;
}

FRESULT FatFsWrapper::buildFilePacket(uint8_t* packet, size_t packetSize, uint32_t& actualSize) {

    int result = buildFilePacket(packet, packetSize);
    if (result < 0) {
        actualSize = 0;
        return FR_INVALID_PARAMETER;
    }

    actualSize = (uint32_t)result;
    return FR_OK;
}

FRESULT FatFsWrapper::format(const TCHAR* path, BYTE fmt, UINT au) {
    static BYTE work[FF_MAX_SS];
    MKFS_PARM opt = { fmt, 0, 0, 0, au };
    return f_mkfs(path, &opt, work, sizeof(work));
}

// FileStream implementation
bool FatFsWrapper::FileStream::open(FatFsWrapper& wrapper, int id, bool forWrite, bool truncate) {
    if (open_) close();

    const char* filename = wrapper.getFilenameById(id);
    if (!filename) return false;

    if (!wrapper.isMounted() && wrapper.mount() != FR_OK) return false;

    BYTE flags;
    if (forWrite) {
        if (truncate) {
            flags = FA_WRITE | FA_CREATE_ALWAYS;  // Create new or truncate existing
        } else {
            flags = FA_WRITE | FA_OPEN_ALWAYS;    // Create if not exists, append if exists
        }
    } else {
        flags = FA_READ;
    }

    if (f_open(&fil_, filename, flags) == FR_OK) {
        open_ = true;
        id_ = id;
        return true;
    }
    return false;
}

void FatFsWrapper::FileStream::close() {
    if (open_) {
        f_close(&fil_);
        open_ = false;
        id_ = -1;
    }
}

FRESULT FatFsWrapper::FileStream::readNext(void* buffer, UINT maxBytes, UINT& bytesRead) {
    bytesRead = 0;
    if (!open_ || !buffer) return FR_INVALID_OBJECT;
    return f_read(&fil_, buffer, maxBytes, &bytesRead);
}

FRESULT FatFsWrapper::FileStream::writeNext(const void* data, UINT bytesToWrite, UINT& bytesWritten) {
    bytesWritten = 0;
    if (!open_ || !data) return FR_INVALID_OBJECT;
    return f_write(&fil_, data, bytesToWrite, &bytesWritten);
}

FRESULT FatFsWrapper::FileStream::sync() {
    if (!open_) return FR_INVALID_OBJECT;
    return f_sync(&fil_);
}
