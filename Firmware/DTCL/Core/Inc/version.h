#ifndef VERSION_H
#define VERSION_H

// DTCL Firmware Version Components
// These can be overridden by Makefile compiler definitions
#ifndef FIRMWARE_VERSION_MAJOR
#define FIRMWARE_VERSION_MAJOR  1
#endif

#ifndef FIRMWARE_VERSION_MINOR  
#define FIRMWARE_VERSION_MINOR  5
#endif

// ASCII values for IspProtocol communication (automatically generated)
#define VERSION_MAJOR_ASCII     (0x30 + FIRMWARE_VERSION_MAJOR)
#define VERSION_MINOR_ASCII     (0x30 + FIRMWARE_VERSION_MINOR)
#define VERSION_DOT_ASCII       0x2E  // '.' character

// Helper macro for string conversion
#define STRINGIFY(x) #x
#define TOSTRING(x) STRINGIFY(x)

// Version string representations
#define VERSION_STRING          TOSTRING(FIRMWARE_VERSION_MAJOR) "." TOSTRING(FIRMWARE_VERSION_MINOR) "." TOSTRING(FIRMWARE_VERSION_PATCH)
#define VERSION_STRING_SHORT    TOSTRING(FIRMWARE_VERSION_MAJOR) "." TOSTRING(FIRMWARE_VERSION_MINOR)
#define VERSION_FILENAME_SUFFIX "V" TOSTRING(FIRMWARE_VERSION_MAJOR) "_" TOSTRING(FIRMWARE_VERSION_MINOR)

// Build information
#ifndef BUILD_DATE
#define BUILD_DATE __DATE__
#endif

#ifndef BUILD_TIME  
#define BUILD_TIME __TIME__
#endif

// Version array for IspProtocol (compatible with existing FirmwareVersion_SubCmdProcess)
#define FIRMWARE_VERSION_ARRAY { VERSION_MAJOR_ASCII, VERSION_DOT_ASCII, VERSION_MINOR_ASCII }

// Hardware identification
#define HARDWARE_TYPE "DTCL"
#define HARDWARE_DESCRIPTION "DTCL Unified Data Programming System"

// Legacy definitions for compatibility
#define FIRMWARE_VERSION_STRING VERSION_STRING_SHORT
#define FIRMWARE_NAME "DTCL"

#endif // VERSION_H