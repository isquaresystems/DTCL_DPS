// FatFsWrapperSingletonTest.cpp - Test for the simplified singleton FatFsWrapper
extern "C" {
#include "diskio.h"
#include "ff.h"
#include <string.h>
}
#include "FatFsWrapperSingleton.h"
#if 0
extern "C" void Test_FatFsWrapperSingleton(void)
{
    // Step 1: Initialize diskio (required for FatFs to work)
    disk_initialize(0);
    
    // Step 2: Get singleton instance
    FatFsWrapper& wrapper = FatFsWrapper::getInstance();
    
    // Step 3: Mount the filesystem
    volatile FRESULT mount_result = wrapper.mount();
    
    if (mount_result == FR_OK) {
        // Step 4: Test deleteAllFiles functionality (clean start)
        volatile FRESULT delete_all_result = wrapper.deleteAllFiles("/");
        
        // Step 5: Test file creation by ID
        volatile FRESULT create_result = wrapper.createFile(3);  // DR.BIN
        
        // Step 6: Test file write by ID
        const char test_data[] = "Hello from FatFsWrapper Singleton!\r\nThis is a test file.\r\n";
        UINT written = 0;
        volatile FRESULT write_result = wrapper.writeFile(3, test_data, strlen(test_data), written);
        
        // Step 7: Test file read by ID
        char read_buffer[128] = {0};
        UINT read_count = 0;
        volatile FRESULT read_result = wrapper.readFile(3, read_buffer, sizeof(read_buffer) - 1, read_count);
        read_buffer[read_count] = '\0';  // Null terminate
        
        // Step 8: Verify read data matches written data
        volatile bool data_match = (read_count == strlen(test_data) && 
                                   strcmp(read_buffer, test_data) == 0);
        
        // Step 9: Test file size
        uint32_t file_size = 0;
        volatile FRESULT size_result = wrapper.fileSize(3, file_size);
        volatile bool size_correct = (file_size == strlen(test_data));
        
        // Step 10: Test stream write operations
        volatile bool stream_write_ok = false;
        {
            FatFsWrapper::FileStream writeStream;
            if (wrapper.openStream(writeStream, 4, true, true)) {  // STR.BIN, write, truncate
                const char* stream_data1 = "Stream line 1\r\n";
                const char* stream_data2 = "Stream line 2\r\n";
                const char* stream_data3 = "Stream line 3\r\n";
                
                UINT sw1 = 0, sw2 = 0, sw3 = 0;
                FRESULT sr1 = writeStream.writeNext(stream_data1, strlen(stream_data1), sw1);
                FRESULT sr2 = writeStream.writeNext(stream_data2, strlen(stream_data2), sw2);
                FRESULT sr3 = writeStream.writeNext(stream_data3, strlen(stream_data3), sw3);
                
                stream_write_ok = (sr1 == FR_OK && sr2 == FR_OK && sr3 == FR_OK);
                writeStream.sync();  // Flush to disk
                writeStream.close();
            }
        }
        
        // Step 11: Test stream read operations
        volatile bool stream_read_ok = false;
        char stream_buffer[256] = {0};
        {
            FatFsWrapper::FileStream readStream;
            if (wrapper.openStream(readStream, 4, false)) {  // STR.BIN, read
                UINT chunk_read = 0;
                FRESULT sr = readStream.readNext(stream_buffer, sizeof(stream_buffer) - 1, chunk_read);
                stream_buffer[chunk_read] = '\0';
                stream_read_ok = (sr == FR_OK && chunk_read > 0);
                readStream.close();
            }
        }
        
        // Step 12: Test file scanning
        FatFsWrapper::FileInfo scanned_files[MAX_SCANNED_FILES];
        size_t file_count = 0;
        volatile FRESULT scan_result = wrapper.scanFiles("/", scanned_files, MAX_SCANNED_FILES, file_count);
        
        // Step 13: Test buildFilePacket
        uint8_t packet[FILE_PACKET_SIZE];
        volatile int packet_result = wrapper.buildFilePacket(packet, sizeof(packet));
        volatile bool packet_ok = (packet_result == FILE_PACKET_SIZE && packet[0] == file_count);
        
        // Step 14: Test write with offset
        const char append_data[] = "APPENDED DATA";
        UINT append_written = 0;
        volatile FRESULT append_result = wrapper.writeFile(3, append_data, strlen(append_data), 
                                                           append_written, strlen(test_data));
        
        // Step 15: Test read with offset
        char offset_buffer[32] = {0};
        UINT offset_read = 0;
        volatile FRESULT offset_read_result = wrapper.readFile(3, offset_buffer, sizeof(offset_buffer) - 1, 
                                                               offset_read, strlen(test_data));
        offset_buffer[offset_read] = '\0';
        volatile bool offset_data_match = (strcmp(offset_buffer, append_data) == 0);
        
        // Step 16: Test delete file
        volatile FRESULT delete_result = wrapper.deleteFile(3);  // Delete DR.BIN
        
        // Step 17: Verify file is deleted
        uint32_t deleted_size = 0;
        volatile FRESULT deleted_check = wrapper.fileSize(3, deleted_size);
        volatile bool delete_verified = (deleted_check == FR_NO_FILE);
        
        // Step 18: Test remount (should unmount first then mount again)
        volatile FRESULT remount_result = wrapper.mount();
        
        // Results summary
        volatile uint8_t test_status = 0;
        if (mount_result == FR_OK) test_status |= 0x01;        // Mount OK
        if (create_result == FR_OK) test_status |= 0x02;       // Create OK
        if (write_result == FR_OK && written > 0) test_status |= 0x04;  // Write OK
        if (read_result == FR_OK && data_match) test_status |= 0x08;    // Read OK
        if (size_result == FR_OK && size_correct) test_status |= 0x10;  // Size OK
        if (stream_write_ok) test_status |= 0x20;              // Stream write OK
        if (stream_read_ok) test_status |= 0x40;               // Stream read OK
        if (scan_result == FR_OK && file_count >= 2) test_status |= 0x80;  // Scan OK
        
        volatile uint8_t test_status2 = 0;
        if (packet_ok) test_status2 |= 0x01;                   // Packet build OK
        if (append_result == FR_OK && offset_data_match) test_status2 |= 0x02;  // Offset write/read OK
        if (delete_result == FR_OK && delete_verified) test_status2 |= 0x04;    // Delete OK
        if (remount_result == FR_OK) test_status2 |= 0x08;     // Remount OK
        
        // test_status == 0xFF && test_status2 == 0x0F means all tests passed
        volatile int test_breakpoint = 0;  // Set breakpoint here to check results
        
        // Cleanup
        wrapper.unmount();
    }
    else {
        // Mount failed
        volatile int mount_failed_breakpoint = 0;  // Debug point
    }
}
#endif