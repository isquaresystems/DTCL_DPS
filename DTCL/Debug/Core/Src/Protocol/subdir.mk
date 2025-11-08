################################################################################
# Automatically-generated file. Do not edit!
# Toolchain: GNU Tools for STM32 (10.3-2021.10)
################################################################################

# Add inputs and outputs from these tool invocations to the build variables 
CPP_SRCS += \
../Core/Src/Protocol/SerialTransport.cpp \
../Core/Src/Protocol/VennaCmdControl.cpp \
../Core/Src/Protocol/VennaCmdReceiveData.cpp \
../Core/Src/Protocol/VennaCmdTransmitData.cpp \
../Core/Src/Protocol/VennaCommandManager.cpp \
../Core/Src/Protocol/VennaRingBuffer.cpp \
../Core/Src/Protocol/VennaSubCommandProcessor.cpp \
../Core/Src/Protocol/safeBuffer.cpp 

OBJS += \
./Core/Src/Protocol/SerialTransport.o \
./Core/Src/Protocol/VennaCmdControl.o \
./Core/Src/Protocol/VennaCmdReceiveData.o \
./Core/Src/Protocol/VennaCmdTransmitData.o \
./Core/Src/Protocol/VennaCommandManager.o \
./Core/Src/Protocol/VennaRingBuffer.o \
./Core/Src/Protocol/VennaSubCommandProcessor.o \
./Core/Src/Protocol/safeBuffer.o 

CPP_DEPS += \
./Core/Src/Protocol/SerialTransport.d \
./Core/Src/Protocol/VennaCmdControl.d \
./Core/Src/Protocol/VennaCmdReceiveData.d \
./Core/Src/Protocol/VennaCmdTransmitData.d \
./Core/Src/Protocol/VennaCommandManager.d \
./Core/Src/Protocol/VennaRingBuffer.d \
./Core/Src/Protocol/VennaSubCommandProcessor.d \
./Core/Src/Protocol/safeBuffer.d 


# Each subdirectory must supply rules for building sources it contributes
Core/Src/Protocol/%.o Core/Src/Protocol/%.su Core/Src/Protocol/%.cyclo: ../Core/Src/Protocol/%.cpp Core/Src/Protocol/subdir.mk
	arm-none-eabi-g++ "$<" -mcpu=cortex-m4 -std=gnu++14 -g3 -DDEBUG -DUSE_HAL_DRIVER -DSTM32F411xE -c -I../USB_DEVICE/App -I"F:/Work/Swave/DPS_4_IN_1/DPS_4_IN_1_Firmware/DTCL_Refactor/Core/Src/VennaProtocolCore" -I../USB_DEVICE/Target -I../Core/Inc -I../Drivers/STM32F4xx_HAL_Driver/Inc -I../Drivers/STM32F4xx_HAL_Driver/Inc/Legacy -I../Middlewares/ST/STM32_USB_Device_Library/Core/Inc -I../Middlewares/ST/STM32_USB_Device_Library/Class/CDC/Inc -I../Drivers/CMSIS/Device/ST/STM32F4xx/Include -I../Drivers/CMSIS/Include -O0 -ffunction-sections -fdata-sections -fno-exceptions -fno-rtti -fno-use-cxa-atexit -Wall -fstack-usage -fcyclomatic-complexity -MMD -MP -MF"$(@:%.o=%.d)" -MT"$@"  -mfpu=fpv4-sp-d16 -mfloat-abi=hard -mthumb -o "$@"

clean: clean-Core-2f-Src-2f-Protocol

clean-Core-2f-Src-2f-Protocol:
	-$(RM) ./Core/Src/Protocol/SerialTransport.cyclo ./Core/Src/Protocol/SerialTransport.d ./Core/Src/Protocol/SerialTransport.o ./Core/Src/Protocol/SerialTransport.su ./Core/Src/Protocol/VennaCmdControl.cyclo ./Core/Src/Protocol/VennaCmdControl.d ./Core/Src/Protocol/VennaCmdControl.o ./Core/Src/Protocol/VennaCmdControl.su ./Core/Src/Protocol/VennaCmdReceiveData.cyclo ./Core/Src/Protocol/VennaCmdReceiveData.d ./Core/Src/Protocol/VennaCmdReceiveData.o ./Core/Src/Protocol/VennaCmdReceiveData.su ./Core/Src/Protocol/VennaCmdTransmitData.cyclo ./Core/Src/Protocol/VennaCmdTransmitData.d ./Core/Src/Protocol/VennaCmdTransmitData.o ./Core/Src/Protocol/VennaCmdTransmitData.su ./Core/Src/Protocol/VennaCommandManager.cyclo ./Core/Src/Protocol/VennaCommandManager.d ./Core/Src/Protocol/VennaCommandManager.o ./Core/Src/Protocol/VennaCommandManager.su ./Core/Src/Protocol/VennaRingBuffer.cyclo ./Core/Src/Protocol/VennaRingBuffer.d ./Core/Src/Protocol/VennaRingBuffer.o ./Core/Src/Protocol/VennaRingBuffer.su ./Core/Src/Protocol/VennaSubCommandProcessor.cyclo ./Core/Src/Protocol/VennaSubCommandProcessor.d ./Core/Src/Protocol/VennaSubCommandProcessor.o ./Core/Src/Protocol/VennaSubCommandProcessor.su ./Core/Src/Protocol/safeBuffer.cyclo ./Core/Src/Protocol/safeBuffer.d ./Core/Src/Protocol/safeBuffer.o ./Core/Src/Protocol/safeBuffer.su

.PHONY: clean-Core-2f-Src-2f-Protocol

