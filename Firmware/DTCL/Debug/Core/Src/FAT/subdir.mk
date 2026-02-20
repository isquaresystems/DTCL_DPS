################################################################################
# Automatically-generated file. Do not edit!
# Toolchain: GNU Tools for STM32 (10.3-2021.10)
################################################################################

# Add inputs and outputs from these tool invocations to the build variables 
C_SRCS += \
../Core/Src/FAT/diskio.c \
../Core/Src/FAT/ff.c \
../Core/Src/FAT/ffsystem.c \
../Core/Src/FAT/ffunicode.c 

C_DEPS += \
./Core/Src/FAT/diskio.d \
./Core/Src/FAT/ff.d \
./Core/Src/FAT/ffsystem.d \
./Core/Src/FAT/ffunicode.d 

OBJS += \
./Core/Src/FAT/diskio.o \
./Core/Src/FAT/ff.o \
./Core/Src/FAT/ffsystem.o \
./Core/Src/FAT/ffunicode.o 


# Each subdirectory must supply rules for building sources it contributes
Core/Src/FAT/%.o Core/Src/FAT/%.su Core/Src/FAT/%.cyclo: ../Core/Src/FAT/%.c Core/Src/FAT/subdir.mk
	arm-none-eabi-gcc "$<" -mcpu=cortex-m4 -std=gnu18 -g3 -DDEBUG -DUSE_HAL_DRIVER -DSTM32F411xE -c -I../USB_DEVICE/App -I../USB_DEVICE/Target -I../Core/Inc -I../Drivers/STM32F4xx_HAL_Driver/Inc -I../Drivers/STM32F4xx_HAL_Driver/Inc/Legacy -I../Middlewares/ST/STM32_USB_Device_Library/Core/Inc -I../Middlewares/ST/STM32_USB_Device_Library/Class/CDC/Inc -I../Drivers/CMSIS/Device/ST/STM32F4xx/Include -I../Drivers/CMSIS/Include -O0 -ffunction-sections -fdata-sections -Wall -fstack-usage -fcyclomatic-complexity -MMD -MP -MF"$(@:%.o=%.d)" -MT"$@"  -mfpu=fpv4-sp-d16 -mfloat-abi=hard -mthumb -o "$@"

clean: clean-Core-2f-Src-2f-FAT

clean-Core-2f-Src-2f-FAT:
	-$(RM) ./Core/Src/FAT/diskio.cyclo ./Core/Src/FAT/diskio.d ./Core/Src/FAT/diskio.o ./Core/Src/FAT/diskio.su ./Core/Src/FAT/ff.cyclo ./Core/Src/FAT/ff.d ./Core/Src/FAT/ff.o ./Core/Src/FAT/ff.su ./Core/Src/FAT/ffsystem.cyclo ./Core/Src/FAT/ffsystem.d ./Core/Src/FAT/ffsystem.o ./Core/Src/FAT/ffsystem.su ./Core/Src/FAT/ffunicode.cyclo ./Core/Src/FAT/ffunicode.d ./Core/Src/FAT/ffunicode.o ./Core/Src/FAT/ffunicode.su

.PHONY: clean-Core-2f-Src-2f-FAT

