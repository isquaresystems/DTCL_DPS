// FatFsWrapperSingleton.h - Simplified singleton wrapper for FatFs without STL
#pragma once

#include "ff.h"
#include <stdint.h>
#include <string.h>
#include "diskio.h"

// Maximum number of files we can track
#define MAX_SCANNED_FILES 32
#define MAX_PATH_LENGTH 256
#define FILE_PACKET_SIZE 1024

/**
 * @brief Simplified singleton wrapper around FatFs for embedded systems.
 *        No STL dependencies, uses only static memory allocation.
 */
class FatFsWrapper {
public:
    struct FileInfo {
        int         id;
        char        name[14];  // 8.3 filename + null terminator
        uint32_t    size;
        BYTE        attr;
    };

    struct FileEntry {
        int         id;
        const char* name;
    };
    
    // Known files list
    static const FileEntry kKnownFiles[];
    static const size_t kKnownFileCount;

    // Singleton access
    static FatFsWrapper& getInstance() {
        static FatFsWrapper instance;
        return instance;
    }
    
    // Smart cartridge switching with automatic unmount and disk reinitialization
    void setCurrentCart(int cartId) {
        if (cartId != currentCartId_) {
            if (mounted_) {
                unmount();  // Unmount if switching carts while mounted
            }
            currentCartId_ = cartId;
            // Force disk reinitialization for the new cartridge
            forceCartridgeReinit((CartridgeID)cartId);
        }
    }
    
    int getCurrentCart() const { return currentCartId_; }

    // Delete copy constructor and assignment
    FatFsWrapper(const FatFsWrapper&) = delete;
    FatFsWrapper& operator=(const FatFsWrapper&) = delete;

    // Basic operations
    FRESULT mount();
    FRESULT unmount();
    bool isMounted() const { return mounted_; }

    // File operations by ID
    FRESULT createFile(int id, BYTE mode = FA_WRITE | FA_CREATE_ALWAYS);
    FRESULT deleteFile(int id);
    FRESULT fileSize(int id, uint32_t& size);
    
    // Read/Write operations
    FRESULT writeFile(int id, const void* data, UINT bytesToWrite, UINT& bytesWritten, FSIZE_t offset = 0);
    FRESULT readFile(int id, void* buffer, UINT bytesToRead, UINT& bytesRead, FSIZE_t offset = 0);
    
    // Directory operations
    FRESULT deleteAllFiles(const char* dirPath);
    FRESULT scanFiles(const char* dirPath, FileInfo* outFiles, size_t maxFiles, size_t& fileCount);
    
    // Utility functions
    int buildFilePacket(uint8_t* packet, size_t packetSize);
    FRESULT buildFilePacket(uint8_t* packet, size_t packetSize, uint32_t& actualSize);
    static int lookupFileId(const char* filename);
    
    // Cartridge reinitialization helper
    void forceCartridgeReinit(CartridgeID id);
    
    // Format
    FRESULT format(const TCHAR* path = "", BYTE fmt = FM_ANY, UINT au = 0);

    // Simple stream operations (no dynamic allocation)
    class FileStream {
    public:
        FileStream() : open_(false), id_(-1) {}
        
        bool open(FatFsWrapper& wrapper, int id, bool forWrite, bool truncate = false);
        void close();
        
        FRESULT readNext(void* buffer, UINT maxBytes, UINT& bytesRead);
        FRESULT writeNext(const void* data, UINT bytesToWrite, UINT& bytesWritten);
        FRESULT sync();
        
        bool isOpen() const { return open_; }
        int getId() const { return id_; }

    private:
        FIL  fil_;
        bool open_;
        int  id_;
    };
    
    // Get a file stream (caller must manage the FileStream object)
    bool openStream(FileStream& stream, int id, bool forWrite, bool truncate = false) {
        return stream.open(*this, id, forWrite, truncate);
    }
    
    // Convenience methods for read/write streams
    FRESULT openReadStream(int id, FileStream& stream) {
        if (stream.open(*this, id, false, false)) {
            return FR_OK;
        }
        return FR_INVALID_PARAMETER;
    }
    
    FRESULT openWriteStream(int id, FileStream& stream, bool truncate = false) {
        if (stream.open(*this, id, true, truncate)) {
            return FR_OK;
        }
        return FR_INVALID_PARAMETER;
    }

private:
    FatFsWrapper() : mounted_(false), currentCartId_(-1) {}
    ~FatFsWrapper() { unmount(); }

    FATFS internalFs_;
    bool mounted_;
    int currentCartId_;  // Track current cartridge (-1 = none)
    
    // Helper to get filename by ID
    const char* getFilenameById(int id) const;
};