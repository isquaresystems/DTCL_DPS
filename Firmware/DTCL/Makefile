##########################################################################################################################
# File created for DTCL VSCode project - Unified firmware for D2 and D3
##########################################################################################################################

# ------------------------------------------------
# Generic Makefile (based on gcc)
#
# ChangeLog :
#	2017-02-10 - Several enhancements + project update mode
#   2015-07-22 - first version
# ------------------------------------------------

######################################
# target
######################################
TARGET = DTCL

# Simple versioning: Default 1.0 or custom version
VERSION_MAJOR ?= 1
VERSION_MINOR ?= 0


# Always build versioned ELF
VERSION_SUFFIX = V$(VERSION_MAJOR)_$(VERSION_MINOR)
TARGET_VERSIONED = $(TARGET)_$(VERSION_SUFFIX)

# Auto-update VSCode JSON files with correct ELF filename
update-json:
ifeq ($(OS),Windows_NT)
	@echo "Updating VSCode configurations for version $(VERSION_MAJOR).$(VERSION_MINOR)..."
	@powershell -Command "if (Test-Path '.vscode/launch.json') { (Get-Content '.vscode/launch.json') -replace 'DTCL_V\d+_\d+\.elf', 'DTCL_V$(VERSION_MAJOR)_$(VERSION_MINOR).elf' -replace 'DTCL_VERSION\.elf', 'DTCL_V$(VERSION_MAJOR)_$(VERSION_MINOR).elf' | Set-Content '.vscode/launch.json' }"
	@powershell -Command "if (Test-Path '.vscode/tasks.json') { (Get-Content '.vscode/tasks.json') -replace 'DTCL_V\d+_\d+', 'DTCL_V$(VERSION_MAJOR)_$(VERSION_MINOR)' -replace 'DTCL_VERSION', 'DTCL_V$(VERSION_MAJOR)_$(VERSION_MINOR)' | Set-Content '.vscode/tasks.json' }"
	@powershell -Command "if (Test-Path '.vscode/settings.json') { (Get-Content '.vscode/settings.json') -replace 'DTCL_V\d+_\d+\.elf', 'DTCL_V$(VERSION_MAJOR)_$(VERSION_MINOR).elf' -replace 'DTCL_VERSION\.elf', 'DTCL_V$(VERSION_MAJOR)_$(VERSION_MINOR).elf' | Set-Content '.vscode/settings.json' }"
endif


######################################
# building variables
######################################
# debug build?
DEBUG = 1
# optimization
OPT = -Og


#######################################
# paths
#######################################
# Build path
BUILD_DIR = build

######################################
# source
######################################
# C sources
C_SOURCES =  \
Core/Src/stm32f4xx_it.c \
Core/Src/stm32f4xx_hal_msp.c \
Core/Src/Darin2Cart_Driver.c \
Core/Src/Darin3Cart_Driver.c \
USB_DEVICE/App/usb_device.c \
USB_DEVICE/App/usbd_desc.c \
USB_DEVICE/App/usbd_cdc_if.c \
USB_DEVICE/Target/usbd_conf.c \
Drivers/STM32F4xx_HAL_Driver/Src/stm32f4xx_hal_pcd.c \
Drivers/STM32F4xx_HAL_Driver/Src/stm32f4xx_hal_pcd_ex.c \
Drivers/STM32F4xx_HAL_Driver/Src/stm32f4xx_ll_usb.c \
Drivers/STM32F4xx_HAL_Driver/Src/stm32f4xx_hal_rcc.c \
Drivers/STM32F4xx_HAL_Driver/Src/stm32f4xx_hal_rcc_ex.c \
Drivers/STM32F4xx_HAL_Driver/Src/stm32f4xx_hal_flash.c \
Drivers/STM32F4xx_HAL_Driver/Src/stm32f4xx_hal_flash_ex.c \
Drivers/STM32F4xx_HAL_Driver/Src/stm32f4xx_hal_flash_ramfunc.c \
Drivers/STM32F4xx_HAL_Driver/Src/stm32f4xx_hal_gpio.c \
Drivers/STM32F4xx_HAL_Driver/Src/stm32f4xx_hal_dma_ex.c \
Drivers/STM32F4xx_HAL_Driver/Src/stm32f4xx_hal_dma.c \
Drivers/STM32F4xx_HAL_Driver/Src/stm32f4xx_hal_pwr.c \
Drivers/STM32F4xx_HAL_Driver/Src/stm32f4xx_hal_pwr_ex.c \
Drivers/STM32F4xx_HAL_Driver/Src/stm32f4xx_hal_cortex.c \
Drivers/STM32F4xx_HAL_Driver/Src/stm32f4xx_hal.c \
Drivers/STM32F4xx_HAL_Driver/Src/stm32f4xx_hal_exti.c \
Drivers/STM32F4xx_HAL_Driver/Src/stm32f4xx_hal_tim.c \
Drivers/STM32F4xx_HAL_Driver/Src/stm32f4xx_hal_tim_ex.c \
Core/Src/system_stm32f4xx.c \
Middlewares/ST/STM32_USB_Device_Library/Core/Src/usbd_core.c \
Middlewares/ST/STM32_USB_Device_Library/Core/Src/usbd_ctlreq.c \
Middlewares/ST/STM32_USB_Device_Library/Core/Src/usbd_ioreq.c \
Middlewares/ST/STM32_USB_Device_Library/Class/CDC/Src/usbd_cdc.c \
Core/Src/sysmem.c \
Core/Src/syscalls.c \
Core/Src/FAT/diskio.c \
Core/Src/FAT/ff.c \
Core/Src/FAT/ffsystem.c \
Core/Src/FAT/ffunicode.c

# C++ sources (DTCL includes both D2 and D3)
CXX_SOURCES = \
Core/Src/main.cpp \
Core/Src/cpp_minimal.cpp \
Core/Src/Darin2.cpp \
Core/Src/Darin3.cpp \
Core/Src/Protocol/SerialTransport.cpp \
Core/Src/Protocol/IspCmdControl.cpp \
Core/Src/Protocol/IspCmdReceiveData.cpp \
Core/Src/Protocol/IspCmdTransmitData.cpp \
Core/Src/Protocol/IspCommandManager.cpp \
Core/Src/Protocol/IspSubCommandProcessor.cpp \
Core/Src/Protocol/IspRingBuffer.cpp \
Core/Src/Protocol/safeBuffer.cpp

# ASM sources
ASM_SOURCES =  \
Core/Startup/startup_stm32f411vetx.s

# ASMM sources
ASMM_SOURCES =

# ASMMC sources
ASMMC_SOURCE =


#######################################
# binaries
#######################################
PREFIX = arm-none-eabi-
# The gcc compiler bin path can be either defined in make command via GCC_PATH variable (> make GCC_PATH=xxx)
# either it can be added to the PATH environment variable.
ifdef GCC_PATH
CC = $(GCC_PATH)/$(PREFIX)gcc
CXX = $(GCC_PATH)/$(PREFIX)g++
AS = $(GCC_PATH)/$(PREFIX)gcc -x assembler-with-cpp
CP = $(GCC_PATH)/$(PREFIX)objcopy
SZ = $(GCC_PATH)/$(PREFIX)size
else
CC = $(PREFIX)gcc
CXX = $(PREFIX)g++
AS = $(PREFIX)gcc -x assembler-with-cpp
CP = $(PREFIX)objcopy
SZ = $(PREFIX)size
endif
HEX = $(CP) -O ihex
BIN = $(CP) -O binary -S

#######################################
# CFLAGS
#######################################
# cpu
CPU = -mcpu=cortex-m4

# fpu
FPU = -mfpu=fpv4-sp-d16

# float-abi
FLOAT-ABI = -mfloat-abi=hard

# mcu
MCU = $(CPU) -mthumb $(FPU) $(FLOAT-ABI)

# macros for gcc
# AS defines
AS_DEFS =

# C defines
C_DEFS =  \
-DUSE_HAL_DRIVER \
-DSTM32F411xE \
-DFIRMWARE_VERSION_MAJOR=$(VERSION_MAJOR) \
-DFIRMWARE_VERSION_MINOR=$(VERSION_MINOR)


# AS includes
AS_INCLUDES =

# C includes
C_INCLUDES =  \
-IUSB_DEVICE/App \
-IUSB_DEVICE/Target \
-ICore/Inc \
-ICore/Src \
-ICore/Src/FAT \
-IDrivers/STM32F4xx_HAL_Driver/Inc \
-IDrivers/STM32F4xx_HAL_Driver/Inc/Legacy \
-IMiddlewares/ST/STM32_USB_Device_Library/Core/Inc \
-IMiddlewares/ST/STM32_USB_Device_Library/Class/CDC/Inc \
-IDrivers/CMSIS/Device/ST/STM32F4xx/Include \
-IDrivers/CMSIS/Include


# compile gcc flags
ASFLAGS = $(MCU) $(AS_DEFS) $(AS_INCLUDES) $(OPT) -Wall -fdata-sections -ffunction-sections

CFLAGS += $(MCU) $(C_DEFS) $(C_INCLUDES) $(OPT) -Wall -fdata-sections -ffunction-sections

CXXFLAGS += $(MCU) $(C_DEFS) $(C_INCLUDES) $(OPT) -Wall -fdata-sections -ffunction-sections -std=c++14 -fno-rtti -fno-exceptions

ifeq ($(DEBUG), 1)
CFLAGS += -g -gdwarf-2
CXXFLAGS += -g -gdwarf-2
endif


# Generate dependency information
CFLAGS += -MMD -MP -MF"$(@:%.o=%.d)"
CXXFLAGS += -MMD -MP -MF"$(@:%.o=%.d)"


#######################################
# LDFLAGS
#######################################
# link script
LDSCRIPT = STM32F411VETX_FLASH.ld

# libraries
LIBS = -lc -lm -lnosys -lstdc++
LIBDIR =
LDFLAGS = $(MCU) -specs=nano.specs -T$(LDSCRIPT) $(LIBDIR) $(LIBS) -Wl,-Map=$(BUILD_DIR)/$(TARGET).map,--cref -Wl,--gc-sections

# default action: update JSON then build versioned files
all: update-json $(BUILD_DIR)/$(TARGET_VERSIONED).elf $(BUILD_DIR)/$(TARGET_VERSIONED).hex $(BUILD_DIR)/$(TARGET_VERSIONED).bin

# Legacy target names (for backward compatibility)
legacy: $(BUILD_DIR)/$(TARGET).elf $(BUILD_DIR)/$(TARGET).hex $(BUILD_DIR)/$(TARGET).bin


#######################################
# build the application
#######################################
# list of objects
OBJECTS = $(addprefix $(BUILD_DIR)/,$(notdir $(C_SOURCES:.c=.o)))
vpath %.c $(sort $(dir $(C_SOURCES)))
# list of C++ program objects
OBJECTS += $(addprefix $(BUILD_DIR)/,$(notdir $(CXX_SOURCES:.cpp=.o)))
vpath %.cpp $(sort $(dir $(CXX_SOURCES)))
# list of ASM program objects
OBJECTS += $(addprefix $(BUILD_DIR)/,$(notdir $(ASM_SOURCES:.s=.o)))
vpath %.s $(sort $(dir $(ASM_SOURCES)))
OBJECTS += $(addprefix $(BUILD_DIR)/,$(notdir $(ASMM_SOURCES:.S=.o)))
vpath %.S $(sort $(dir $(ASMM_SOURCES)))
OBJECTS += $(addprefix $(BUILD_DIR)/,$(notdir $(ASMMC_SOURCES:.c=.o)))
vpath %.S $(sort $(dir $(ASMMC_SOURCES)))

$(BUILD_DIR)/%.o: %.c Makefile | $(BUILD_DIR)
	$(CC) -c $(CFLAGS) -Wa,-a,-ad,-alms=$(BUILD_DIR)/$(notdir $(<:.c=.lst)) $< -o $@

$(BUILD_DIR)/%.o: %.cpp Makefile | $(BUILD_DIR)
	$(CXX) -c $(CXXFLAGS) $< -o $@

$(BUILD_DIR)/%.o: %.s Makefile | $(BUILD_DIR)
	$(AS) -c $(CFLAGS) $< -o $@
$(BUILD_DIR)/%.o: %.S Makefile | $(BUILD_DIR)
	$(AS) -c $(CFLAGS) $< -o $@

# Versioned build targets
$(BUILD_DIR)/$(TARGET_VERSIONED).elf: $(OBJECTS) Makefile
	$(CC) $(OBJECTS) $(LDFLAGS) -o $@
	$(SZ) $@
	@echo "Built firmware: $(TARGET_VERSIONED).elf (Version $(VERSION_MAJOR).$(VERSION_MINOR))"

$(BUILD_DIR)/$(TARGET_VERSIONED).hex: $(BUILD_DIR)/$(TARGET_VERSIONED).elf | $(BUILD_DIR)
	$(HEX) $< $@
	
$(BUILD_DIR)/$(TARGET_VERSIONED).bin: $(BUILD_DIR)/$(TARGET_VERSIONED).elf | $(BUILD_DIR)
	$(BIN) $< $@

# Legacy build targets (without version)
$(BUILD_DIR)/$(TARGET).elf: $(OBJECTS) Makefile
	$(CC) $(OBJECTS) $(LDFLAGS) -o $@
	$(SZ) $@

$(BUILD_DIR)/$(TARGET).hex: $(BUILD_DIR)/$(TARGET).elf | $(BUILD_DIR)
	$(HEX) $< $@
	
$(BUILD_DIR)/$(TARGET).bin: $(BUILD_DIR)/$(TARGET).elf | $(BUILD_DIR)
	$(BIN) $< $@
	
$(BUILD_DIR):
ifeq ($(OS),Windows_NT)
	-mkdir $@ 2>nul || echo "Build directory exists"
else
	mkdir $@
endif

#######################################
# Aliases for common targets
#######################################
hex: $(BUILD_DIR)/$(TARGET_VERSIONED).hex
bin: $(BUILD_DIR)/$(TARGET_VERSIONED).bin

#######################################
# clean up
#######################################
clean:
ifeq ($(OS),Windows_NT)
	-rmdir /s /q $(BUILD_DIR) 2>nul || echo "Build directory cleaned"
else
	-rm -fR $(BUILD_DIR)
endif

#######################################
# flash/program STM32
#######################################
flash: $(BUILD_DIR)/$(TARGET_VERSIONED).elf
	openocd -f interface/stlink.cfg -f target/stm32f4x.cfg -c "program $(BUILD_DIR)/$(TARGET_VERSIONED).elf verify reset exit"

#######################################
# dependencies
#######################################
-include $(wildcard $(BUILD_DIR)/*.d)

# *** EOF ***