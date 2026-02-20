##########################################################################################################################
# File created for D2_DPS_4IN1 VSCode project based on D3_DPS_4IN1 template
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
TARGET = D2_DPS_4IN1

# Simple versioning: Default 1.0 or custom version
VERSION_MAJOR ?= 1
VERSION_MINOR ?= 0


# Always build versioned ELF
VERSION_SUFFIX = V$(VERSION_MAJOR)_$(VERSION_MINOR)
TARGET_VERSIONED = $(TARGET)_$(VERSION_SUFFIX)

# Auto-update VSCode JSON files with correct ELF filename
update-json:
	@echo "Updating VSCode configurations for version $(VERSION_MAJOR).$(VERSION_MINOR)..."
	@powershell -Command "if (Test-Path '.vscode/launch.json') { (Get-Content '.vscode/launch.json') -replace 'D2_DPS_4IN1_V\d+_\d+\.elf', 'D2_DPS_4IN1_V$(VERSION_MAJOR)_$(VERSION_MINOR).elf' -replace 'D2_DPS_4IN1_VERSION\.elf', 'D2_DPS_4IN1_V$(VERSION_MAJOR)_$(VERSION_MINOR).elf' | Set-Content '.vscode/launch.json' }"
	@powershell -Command "if (Test-Path '.vscode/tasks.json') { (Get-Content '.vscode/tasks.json') -replace 'D2_DPS_4IN1_V\d+_\d+', 'D2_DPS_4IN1_V$(VERSION_MAJOR)_$(VERSION_MINOR)' -replace 'D2_DPS_4IN1_VERSION', 'D2_DPS_4IN1_V$(VERSION_MAJOR)_$(VERSION_MINOR)' | Set-Content '.vscode/tasks.json' }"
	@powershell -Command "if (Test-Path '.vscode/settings.json') { (Get-Content '.vscode/settings.json') -replace 'D2_DPS_4IN1_V\d+_\d+\.elf', 'D2_DPS_4IN1_V$(VERSION_MAJOR)_$(VERSION_MINOR).elf' -replace 'D2_DPS_4IN1_VERSION\.elf', 'D2_DPS_4IN1_V$(VERSION_MAJOR)_$(VERSION_MINOR).elf' | Set-Content '.vscode/settings.json' }"


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
Core/Src/syscalls.c

# C++ sources (D2 specific - no FAT filesystem)
CXX_SOURCES = \
Core/Src/main.cpp \
Core/Src/cpp_minimal.cpp \
Core/Src/Darin2.cpp \
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

# Legacy build targets (for backward compatibility)
$(BUILD_DIR)/$(TARGET).elf: $(BUILD_DIR)/$(TARGET_VERSIONED).elf
	cp $< $@

$(BUILD_DIR)/$(TARGET).hex: $(BUILD_DIR)/$(TARGET_VERSIONED).hex
	cp $< $@

$(BUILD_DIR)/$(TARGET).bin: $(BUILD_DIR)/$(TARGET_VERSIONED).bin
	cp $< $@

$(BUILD_DIR):
	mkdir $@

#######################################
# build the application
#######################################
# default action: build version-specific targets with JSON update
all: update-json $(BUILD_DIR)/$(TARGET_VERSIONED).elf $(BUILD_DIR)/$(TARGET_VERSIONED).hex $(BUILD_DIR)/$(TARGET_VERSIONED).bin
	@echo "Built versioned firmware: $(TARGET_VERSIONED).elf (Version $(VERSION_MAJOR).$(VERSION_MINOR))"

# legacy build (for backward compatibility)
legacy: $(BUILD_DIR)/$(TARGET).elf $(BUILD_DIR)/$(TARGET).hex $(BUILD_DIR)/$(TARGET).bin

.PHONY: all clean legacy update-json

#######################################
# clean up
#######################################
clean:
	cmd /c "if exist $(BUILD_DIR) rmdir /s /q $(BUILD_DIR)"

#######################################
# dependencies
#######################################
-include $(wildcard $(BUILD_DIR)/*.d)

# *** EOF ***