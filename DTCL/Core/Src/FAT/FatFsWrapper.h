// FatFsWrapperSingleton.h - Embedded-friendly singleton wrapper for FatFS
#pragma once

#include "ff.h"
#include <stdint.h>
#include <stddef.h>
#include "../Darin3Cart_Driver.h"  // For CartridgeID

// Embedded-friendly string utilities
static inline int str_compare(const char* a, const char* b) {
    while (*a && (*a == *b)) { a++; b++; }
    return *a - *b;
}

static inline void str_copy(char* dest, const char* src, size_t n) {
    size_t i = 0;
    for (i = 0; i < n-1 && src[i]; i++) {
        dest[i] = src[i];
    }
    dest[i] = '\0';
}

static inline size_t str_length(const char* str) {
    size_t len = 0;
    while (str[len]) len++;
    return len;
}

static inline void mem_set(void* ptr, int value, size_t num) {
    uint8_t* p = (uint8_t*)ptr;
    for (size_t i = 0; i < num; i++) {
        p[i] = (uint8_t)value;
    }
}

/**
 * @brief Singleton wrapper around the FatFs library for basic file system operations.
 *        Embedded-friendly design without STL dependencies.
 */
class FatFsWrapper {
public:
    struct FileInfo {
        int      id;
        char     name[32];  // Fixed size instead of std::string
        uint32_t size;
        BYTE     attr;
    };

    // Singleton access
    static FatFsWrapper& getInstance() {
        static FatFsWrapper instance;
        return instance;
    }
    
    // Cart management
    void setCurrentCart(CartridgeID id) { currentCart_ = id; }
    CartridgeID getCurrentCart() const { return currentCart_; }

    // Constants
    static constexpr size_t MAX_REGISTERED_FILES = 64;
    static constexpr size_t MAX_PATH_LENGTH = 128;
    static constexpr size_t MAX_NAME_LENGTH = 32;

private:
    struct RegisteredFile {
        int  id;
        char path[MAX_PATH_LENGTH];
        char name[MAX_NAME_LENGTH];
    };

    FatFsWrapper() : mounted_(false), currentCart_(CARTRIDGE_1), fileCount_(0) {
        mem_set(registeredFiles_, 0, sizeof(registeredFiles_));
    }
    ~FatFsWrapper() { unmount(); }
    
    // Delete copy constructor and assignment operator
    FatFsWrapper(const FatFsWrapper&) = delete;
    FatFsWrapper& operator=(const FatFsWrapper&) = delete;

    FATFS fs_{};
    bool mounted_;
    CartridgeID currentCart_;
    RegisteredFile registeredFiles_[MAX_REGISTERED_FILES];
    size_t fileCount_;

public:
    struct FileEntry {
        int         id;
        const char* name;
    };
    
    static constexpr size_t kKnownFileCount = 29;  // Manual count of known files

    static const FileEntry* getKnownFiles() {
        static const FileEntry kKnownFiles[] = {
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
        return kKnownFiles;
    }

    static int lookupFileId(const char* filename) {
        const FileEntry* kKnownFiles = getKnownFiles();
    	for (size_t i = 0; i < kKnownFileCount; ++i) {
    		if (str_compare(kKnownFiles[i].name, filename) == 0) {
    			return kKnownFiles[i].id;
    		}
    	}
    	return -1;  // not found
    }

    // ─── Mount / Unmount ────────────────────────────────────
    FRESULT mount() {
        if (!mounted_) {
            FRESULT r = f_mount(&fs_, "", 1);
            mounted_ = (r == FR_OK);
            return r;
        }
        return FR_OK;
    }
    
    FRESULT unmount() {
        if (mounted_) {
            FRESULT r = f_mount(nullptr, "", 1);
            mounted_ = false;
            return r;
        }
        return FR_OK;
    }
    
    bool isMounted() const { return mounted_; }

    // ─── File Registration & Scanning ───────────────────────
    void registerFile(int id, const char* filePath) {
        if (fileCount_ >= MAX_REGISTERED_FILES) return;  // Array full
        
        registeredFiles_[fileCount_].id = id;
        str_copy(registeredFiles_[fileCount_].path, filePath, sizeof(registeredFiles_[fileCount_].path));
        
        // Extract filename from path
        const char* lastSlash = filePath;
        const char* temp = filePath;
        while (*temp) {
            if (*temp == '/' || *temp == '\\') {
                lastSlash = temp + 1;
            }
            temp++;
        }
        str_copy(registeredFiles_[fileCount_].name, lastSlash, sizeof(registeredFiles_[fileCount_].name));
        fileCount_++;
    }

    FRESULT scanFiles(const char* dirPath, FileInfo* outFiles, size_t maxFiles, size_t& fileCount) {
    	fileCount = 0;
    	DIR dir;
    	FILINFO fno;
    	FRESULT res = f_opendir(&dir, dirPath);
    	if (res != FR_OK) return res;
    	
    	for (;;) {
    		res = f_readdir(&dir, &fno);
    		if (res != FR_OK || fno.fname[0] == '\0') break;
    		if (fno.fattrib & AM_DIR) continue;
    		if (fileCount >= maxFiles) break;  // Array full
    		
    		char safeName[256];
    		str_copy(safeName, fno.fname, sizeof(safeName));

    		// lookup fixed list
    		int id = lookupFileId(safeName);
    		if (id >= 0) {
    			outFiles[fileCount].id = id;
    			str_copy(outFiles[fileCount].name, safeName, sizeof(outFiles[fileCount].name));
    			outFiles[fileCount].size = static_cast<uint32_t>(fno.fsize);
    			outFiles[fileCount].attr = fno.fattrib;
    			fileCount++;
    		}
            else{
                // Unknown file, skip
                int breakpoint;
                continue;
            }
    	}
    	f_closedir(&dir);
    	return FR_OK;
    }

    /**
     * @brief Build a packet of files (fixed size array instead of vector)
     */
    size_t buildFilePacket(uint8_t* packet, size_t maxSize) {
    	// Ensure filesystem is mounted
    	if (!isMounted()) mount();

    	// Automatically scan the root directory for known files
    	FileInfo files[64];  // Fixed array size
    	size_t fileCount;
    	scanFiles("/", files, 64, fileCount);

    	// Decide total packet length (fixed), e.g. 1024 bytes
    	constexpr size_t PACKET_SIZE = 1024;
    	if (maxSize < PACKET_SIZE) return 0;  // Buffer too small

    	size_t packetIndex = 0;

    	// 1st byte: number of files (max 255)
    	packet[packetIndex++] = static_cast<uint8_t>(fileCount);

    	for (size_t i = 0; i < fileCount && i < 64; i++) {
    		// Next 14 bytes: filename (ASCII), pad with 0 if shorter, truncate if longer
    		size_t nameLen = str_length(files[i].name);
    		if (nameLen > 14) nameLen = 14;
    		
    		for (size_t j = 0; j < nameLen; j++) {
    			packet[packetIndex++] = files[i].name[j];
    		}
    		for (size_t j = nameLen; j < 14; j++) {
    			packet[packetIndex++] = 0;  // Pad with zeros
    		}

    		// Next 4 bytes: file size, little-endian
    		uint32_t sz = files[i].size;
    		for (int j = 0; j < 4; ++j) {
    			packet[packetIndex++] = static_cast<uint8_t>((sz >> (8 * j)) & 0xFF);
    		}
    	}

    	// Pad the rest of the packet with zeros
    	while (packetIndex < PACKET_SIZE) {
    		packet[packetIndex++] = 0;
    	}

    	return PACKET_SIZE;
    }

    // ─── Delete All Files ──────────────────────────────────
    FRESULT deleteAllFiles(const char* dirPath) {
    	DIR dir;
    	FILINFO fno;
    	FRESULT res;

    	// Open the directory
    	res = f_opendir(&dir, dirPath);
    	if (res != FR_OK) {
    		return res;
    	}

    	// Iterate through directory entries
    	for (;;) {
    		res = f_readdir(&dir, &fno);
    		if (res != FR_OK || fno.fname[0] == '\0') {
    			break;
    		}

    		// Skip current directory (.) and parent directory (..)
    		if (str_compare(fno.fname, ".") == 0 || str_compare(fno.fname, "..") == 0) {
    			continue;
    		}

    		// If it's a file, delete it
    		if (!(fno.fattrib & AM_DIR)) {
    			char filePath[256];
    			str_copy(filePath, dirPath, sizeof(filePath));
    			size_t dirLen = str_length(filePath);
    			if (dirLen > 0 && filePath[dirLen-1] != '/') {
    				if (dirLen < sizeof(filePath) - 1) {
    					filePath[dirLen] = '/';
    					filePath[dirLen + 1] = '\0';
    				}
    			}
    			// Append filename (careful about buffer overflow)
    			size_t remaining = sizeof(filePath) - str_length(filePath) - 1;
    			if (remaining > 0) {
    				str_copy(filePath + str_length(filePath), fno.fname, remaining);
    			}
    			
    			FRESULT unlink_res = f_unlink(filePath);
    			if (unlink_res != FR_OK) {
    				f_closedir(&dir);
    				return unlink_res;
    			}
    		}
    	}

    	f_closedir(&dir);
    	return FR_OK;
    }

    // ─── One-shot Create/Delete/Stat ────────────────────────
    FRESULT createFile(int id, BYTE mode = FA_WRITE | FA_CREATE_ALWAYS) {
        const char* path = getPathById(id);
        if (!path) return FR_NO_FILE;
        FIL f;
        FRESULT r = f_open(&f, path, mode);
        if (r == FR_OK) f_close(&f);
        return r;
    }
    
    FRESULT deleteFile(int id) {
        const char* path = getPathById(id);
        return path ? f_unlink(path) : FR_NO_FILE;
    }
    
    FRESULT fileSize(int id, uint32_t& size) {
        const char* path = getPathById(id);
        if (!path) return FR_NO_FILE;
        FILINFO fi;
        FRESULT r = f_stat(path, &fi);
        if (r == FR_OK) size = static_cast<uint32_t>(fi.fsize);
        return r;
    }

    // ─── One-shot Write with Offset ─────────────────────────
    FRESULT writeFile(int        id,
                      const void* data,
                      UINT        bytesToWrite,
                      UINT&       bytesWritten,
                      FSIZE_t     offset = 0)
    {
        const char* path = getPathById(id);
        if (!path) return FR_NO_FILE;
        FIL f;
        FRESULT r = f_open(&f, path, FA_WRITE | FA_OPEN_ALWAYS);
        if (r != FR_OK) return r;
        if ((r = f_lseek(&f, offset)) == FR_OK) {
            r = f_write(&f, data, bytesToWrite, &bytesWritten);
        }
        f_close(&f);
        return r;
    }

    // ─── One-shot Read with Offset ──────────────────────────
    FRESULT readFile(int      id,
                     void*    buffer,
                     UINT     bytesToRead,
                     UINT&    bytesRead,
                     FSIZE_t  offset = 0)
    {
        const char* path = getPathById(id);
        if (!path) return FR_NO_FILE;
        FIL f;
        FRESULT r = f_open(&f, path, FA_READ);
        if (r != FR_OK) return r;
        if ((r = f_lseek(&f, offset)) == FR_OK) {
            r = f_read(&f, buffer, bytesToRead, &bytesRead);
        }
        f_close(&f);
        return r;
    }

    // ─── Format ─────────────────────────────────────────────
    FRESULT format(const TCHAR* path = "",
                   BYTE         fmt  = FM_ANY,
                   UINT         au   = 0)
    {
        static BYTE work[FF_MAX_SS];
        MKFS_PARM opt = { fmt, 0, 0, 0, au };
        return f_mkfs(path, &opt, work, sizeof(work));
    }

    // ─── Stack-allocated FileStream (instead of unique_ptr) ─────────────────
    class FileStream {
    public:
        enum class Mode { Read, Write };

        FileStream() : fil_{}, open_{false} {}

        /// Open and (optionally) truncate in write mode
        FRESULT open(FatFsWrapper& parent, int id, Mode mode, bool truncate = false) {
            if (open_) close();
            
            if (!parent.isMounted()) parent.mount();
            const char* path = parent.getPathById(id);
            if (!path) return FR_NO_FILE;
            
            BYTE flags = (mode == Mode::Read) ? FA_READ : (FA_WRITE | FA_OPEN_ALWAYS);
            if (mode == Mode::Write && (truncate == true))
                flags |= FA_CREATE_ALWAYS;
                
            FRESULT result = f_open(&fil_, path, flags);
            if (result == FR_OK) {
                open_ = true;
            }
            return result;
        }

        void close() {
            if (open_) {
                f_close(&fil_);
                open_ = false;
            }
        }

        ~FileStream() { close(); }

        /// Read next chunk (advances internally)
        FRESULT readNext(void* buffer, UINT maxBytes, UINT& bytesRead) {
            bytesRead = 0;
            if (!open_) return FR_INVALID_OBJECT;
            return f_read(&fil_, buffer, maxBytes, &bytesRead);
        }

        /// Write next chunk (advances internally)
        FRESULT writeNext(const void* data, UINT bytesToWrite, UINT& bytesWritten) {
            bytesWritten = 0;
            if (!open_) return FR_INVALID_OBJECT;
            return f_write(&fil_, data, bytesToWrite, &bytesWritten);
        }

        /// Flush any pending writes
        FRESULT sync() {
            if (!open_) return FR_INVALID_OBJECT;
            return f_sync(&fil_);
        }

        bool isOpen() const { return open_; }

    private:
        FIL  fil_;
        bool open_;
    };

private:
    const char* getPathById(int id) const {
        for (size_t i = 0; i < fileCount_; i++) {
            if (registeredFiles_[i].id == id) {
                return registeredFiles_[i].path;
            }
        }
        return nullptr;
    }
};

