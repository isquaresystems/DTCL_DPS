// FatFsWrapper.h
#pragma once

#include "ff.h"
#include <string>
#include <vector>
#include <map>
#include <memory>

/**
 * @brief Wrapper around the FatFs library for basic file system operations.
 *        Allows registration of fixed file IDs mapped to file paths,
 *        scanning only registered files, one-shot I/O with offset,
 *        plus new sequential read/write streams.
 */
class FatFsWrapper {
public:
    struct FileInfo {
        int         id;
        std::string name;
        uint32_t    size;
        BYTE        attr;
    };

    FatFsWrapper() : mounted_(false) {}
    ~FatFsWrapper() { unmount(); }

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
    void registerFile(int id, const std::string& filePath) {
        idToPathMap_[id] = filePath;
        auto pos = filePath.find_last_of("/\\");
        std::string fname = (pos == std::string::npos
                             ? filePath
                             : filePath.substr(pos + 1));
        nameToIdMap_[fname] = id;
    }

    FRESULT scanFiles(const char* dirPath,
                      std::vector<FileInfo>& outFiles)
    {
        outFiles.clear();
        DIR dir;
        FILINFO fno;
        FRESULT res = f_opendir(&dir, dirPath);
        if (res != FR_OK) return res;
        for (;;) {
            res = f_readdir(&dir, &fno);
            if (res != FR_OK || fno.fname[0] == '\0') break;
            if (fno.fattrib & AM_DIR) continue;
            auto it = nameToIdMap_.find(fno.fname);
            if (it != nameToIdMap_.end()) {
                outFiles.push_back({ it->second,
                                     fno.fname,
                                     static_cast<uint32_t>(fno.fsize),
                                     fno.fattrib });
            }
        }
        f_closedir(&dir);
        return FR_OK;
    }

    // ─── NEW: Delete All Files ──────────────────────────────
    /**
     * @brief Deletes all files in the specified directory.
     * Does not delete subdirectories.
     * @param dirPath The path to the directory to clear.
     * @return FR_OK if all files were deleted successfully or directory was empty,
     * or the first FRESULT error encountered.
     */
    FRESULT deleteAllFiles(const char* dirPath) {
    	DIR dir;
    	FILINFO fno;
    	FRESULT res;

    	// Open the directory
    	res = f_opendir(&dir, dirPath);
    	if (res != FR_OK) {
    		// Return error if directory cannot be opened
    		return res;
    	}

    	// Iterate through directory entries
    	for (;;) {
    		res = f_readdir(&dir, &fno);
    		if (res != FR_OK || fno.fname[0] == '\0') {
    			// Break on error or end of directory
    			break;
    		}

    		// Skip current directory (.) and parent directory (..)
    		if (std::strcmp(fno.fname, ".") == 0 || std::strcmp(fno.fname, "..") == 0) {
    			continue;
    		}

    		// If it's a file, delete it
    		if (!(fno.fattrib & AM_DIR)) {
    			std::string filePath = std::string(dirPath) + "/" + fno.fname;
    			FRESULT unlink_res = f_unlink(filePath.c_str());
    			if (unlink_res != FR_OK) {
    				// If unlinking fails, close directory and return the error
    				f_closedir(&dir);
    				return unlink_res;
    			}
    		}
    	}

    	// Close the directory
    	f_closedir(&dir);
    	return FR_OK; // All files deleted successfully or directory was empty
    }

    // ─── One-shot Create/Delete/Stat ────────────────────────
    FRESULT createFile(int id, BYTE mode = FA_WRITE | FA_CREATE_ALWAYS) {
        auto path = getPathById(id);
        if (path.empty()) return FR_NO_FILE;
        FIL f;
        FRESULT r = f_open(&f, path.c_str(), mode);
        if (r == FR_OK) f_close(&f);
        return r;
    }
    FRESULT deleteFile(int id) {
        auto path = getPathById(id);
        return path.empty() ? FR_NO_FILE : f_unlink(path.c_str());
    }
    FRESULT fileSize(int id, uint32_t& size) {
        auto path = getPathById(id);
        if (path.empty()) return FR_NO_FILE;
        FILINFO fi;
        FRESULT r = f_stat(path.c_str(), &fi);
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
        auto path = getPathById(id);
        if (path.empty()) return FR_NO_FILE;
        FIL f;
        FRESULT r = f_open(&f, path.c_str(), FA_WRITE | FA_OPEN_ALWAYS);
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
        auto path = getPathById(id);
        if (path.empty()) return FR_NO_FILE;
        FIL f;
        FRESULT r = f_open(&f, path.c_str(), FA_READ);
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

    // ─── NEW: Sequential Read/Write Streams ─────────────────
    class FileStream {
    public:
        enum class Mode { Read, Write };

        /// Open and (optionally) truncate in write mode
        FileStream(FatFsWrapper& parent,
                   int            id,
                   Mode           mode,
                   bool           truncate = false)
          : fil_{}, open_{false}
        {
            if (!parent.isMounted()) parent.mount();
            auto path = parent.getPathById(id);
            if (path.empty()) return;
            BYTE flags = (mode == Mode::Read)
                          ? FA_READ
                          : (FA_WRITE | FA_OPEN_ALWAYS);
            if (mode == Mode::Write && truncate)
                flags |= FA_CREATE_ALWAYS;
            if (f_open(&fil_, path.c_str(), flags) == FR_OK) {
                open_ = true;
            }
        }

        ~FileStream() {
            if (open_) f_close(&fil_);
        }

        /// Read next chunk (advances internally)
        FRESULT readNext(void* buffer,
                         UINT  maxBytes,
                         UINT& bytesRead)
        {
            bytesRead = 0;
            if (!open_) return FR_INVALID_OBJECT;
            FRESULT r = f_read(&fil_, buffer, maxBytes, &bytesRead);
            return r;
        }

        /// Write next chunk (advances internally)
        FRESULT writeNext(const void* data,
                          UINT        bytesToWrite,
                          UINT&       bytesWritten)
        {
            bytesWritten = 0;
            if (!open_) return FR_INVALID_OBJECT;
            FRESULT r = f_write(&fil_, data, bytesToWrite, &bytesWritten);
            return r;
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

    /// Open for sequential reading
    std::unique_ptr<FileStream>
    openReadStream(int id) {
        return std::make_unique<FileStream>(*this, id, FileStream::Mode::Read);
    }

    /// Open for sequential writing (truncate if desired)
    std::unique_ptr<FileStream>
    openWriteStream(int id, bool truncate = false) {
        return std::make_unique<FileStream>(*this, id, FileStream::Mode::Write, truncate);
    }

private:
    FATFS                     fs_{};
    bool                      mounted_;
    std::map<int,std::string> idToPathMap_;
    std::map<std::string,int> nameToIdMap_;

    std::string getPathById(int id) const {
        auto it = idToPathMap_.find(id);
        return (it == idToPathMap_.end()) ? std::string() : it->second;
    }
};
