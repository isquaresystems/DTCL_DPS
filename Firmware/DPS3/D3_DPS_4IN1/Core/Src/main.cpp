/* USER CODE BEGIN Header */
/**
  ******************************************************************************
  * @file           : main.c
  * @brief          : Main program body
  ******************************************************************************
  * @attention
  *
  * Copyright (c) 2025 STMicroelectronics.
  * All rights reserved.
  *
  * This software is licensed under terms that can be found in the LICENSE file
  * in the root directory of this software component.
  * If no LICENSE file comes with this software, it is provided AS-IS.
  *
  ******************************************************************************
  */
/* USER CODE END Header */
/* Includes ------------------------------------------------------------------*/
extern "C" {
#include "main.h"
#include "usb_device.h"
#include "Darin3Cart_Driver.h"
#include "FAT/diskio.h"
}

#include "FAT/FatFsWrapperSingleton.h"
#include "Darin3.h"
#include "Protocol/IspCmdReceiveData.h"
#include "Protocol/IspCommandManager.h"
#include "Protocol/IspCmdReceiveData.h"
#include "Protocol/IspCmdTransmitData.h"
#include "Protocol/IspFramingUtils.h"
#include "Protocol/IspCmdControl.h"
#include "Protocol/SerialTransport.h"
// #include <memory>  // Removed to avoid STL dependencies

void SystemClock_Config(void);
static void MX_GPIO_Init(void);
extern "C" void Test_FatFsWrapperSingleton(void);

UsbIspTransport usbTransport;
IspCommandManager IspManager;
IspCmdReceiveData IspRx;
IspCmdTransmitData IspTx;
IspSubCommandProcessor subcmdProcess;
IspCmdControl IspCtrl;

void UpdateSlotLed()
{
	GuiCtrlLed_SubCmdProcess obj;
	if(obj.get_LedState() == 0)
	{
		UpdateD3SlotStatus();
		int itr=0;
		for (itr=0;itr<4;itr++)
		{
			if(get_D3_slt_status((CartridgeID)itr) == 0x03)
			{
				HAL_GPIO_WritePin(GPIOA, get_D3_Green_LedPins((CartridgeID)itr)
						, GPIO_PIN_SET); //GREEN-1
			}
			else
			{
				HAL_GPIO_WritePin(GPIOA, get_D3_Green_LedPins((CartridgeID)itr)
						, GPIO_PIN_RESET); //GREEN-1
			}
		}
	}
}

void BlinkLed_PA1_PA8(uint16_t delay)
{
    for(int i = 0; i < 3; i++)
    {
        // Toggle PA1-PA8
        GPIOA->ODR ^= 0x1FE;  // 0x1FE = bits 1-8
        HAL_Delay(delay);

        // Toggle again to return to original state
        GPIOA->ODR ^= 0x1FE;
        HAL_Delay(delay);
    }

    HAL_GPIO_WritePin(GPIOA, LED1_Pin|LED2_Pin|LED3_Pin|LED4_Pin
                              |LED5_Pin|LED6_Pin|LED7_Pin|LED8_Pin, GPIO_PIN_RESET);

}

extern "C" void Isp_forward_data(const uint8_t* data, uint32_t len)
{
	uint8_t payload[256];
	std::size_t payloadLen = 0;
	if (len == 0 || !data) return;
	if (IspFramingUtils::decodeFrame(data, len, payload, payloadLen))
	{
		if (payloadLen == 0) return;
		IspManager.handleData(&payload[0], payloadLen);
	}
}

/**
  * @brief  The application entry point.
  * @retval int
  */
int main(void)
{

  HAL_Init();
  SystemClock_Config();
  MX_GPIO_Init();
  MX_USB_DEVICE_Init();
  HAL_GPIO_WritePin(GPIOD, POWER_CYCLE_1_Pin, GPIO_PIN_SET); //power on compact flash
  HAL_GPIO_WritePin(GPIOB, POWER_CYCLE_2_Pin, GPIO_PIN_SET); //power on compact flash
  HAL_GPIO_WritePin(GPIOB, POWER_CYCLE_3_Pin, GPIO_PIN_SET); //power on compact flash
  HAL_GPIO_WritePin(GPIOB, POWER_CYCLE_4_Pin, GPIO_PIN_SET); //power on compact flash

  IspRx.setTransport(&usbTransport);
  IspTx.setTransport(&usbTransport);
  IspRx.setSubProcessor(&subcmdProcess);
  IspTx.setSubProcessor(&subcmdProcess);
  IspManager.addHandler(&IspRx);
  IspManager.addHandler(&IspTx);
  IspManager.setBoardID(IspBoardId::DPS3_4_IN_1);

  // Now IspCmdControl is STL-free and can be enabled
  IspCtrl.setTransport(&usbTransport);
  IspCtrl.setSubProcessor(&subcmdProcess);
  IspManager.addHandler(&IspCtrl);

  // Create objects using static allocation (embedded-friendly, no memory leaks)
  static Darin3 darin3Obj;

  // Create static handler objects to avoid memory leaks
  static FirmwareVersion_SubCmdProcess firmwareVersionHandler;
  static BoardID_SubCmdProcess boardIdHandler;
  static GuiCtrlLed_SubCmdProcess guiCtrlLedHandler;
  static GreenLed_SubCmdProcess greenLedHandler;
  static RedLed_SubCmdProcess redLedHandler;
  static CartStatus_SubCmdProcess cartStatusHandler;
  static Erase_SubCmdProcess eraseHandler;
  static Format_SubCmdProcess formatHandler;
  static BlinkAllLed_SubCmdProcess blinkAllLedHandler;
  static SlotLedBlink_SubCmdProcess slotLedBlinkHandler;
  static LedLoopBack_SubCmdProcess loopbackTestHandler;
  static D3_Power_Cycle_SubCmdProcess powerCycleHandler;

  // Register control command handlers using static objects (no memory leaks)
  IspCtrl.registerSubCmdHandlers(&firmwareVersionHandler);
  IspCtrl.registerSubCmdHandlers(&boardIdHandler);
  IspCtrl.registerSubCmdHandlers(&guiCtrlLedHandler);
  IspCtrl.registerSubCmdHandlers(&greenLedHandler);
  IspCtrl.registerSubCmdHandlers(&redLedHandler);
  IspCtrl.registerSubCmdHandlers(&cartStatusHandler);
  IspCtrl.registerSubCmdHandlers(&eraseHandler);
  IspCtrl.registerSubCmdHandlers(&formatHandler);
  IspCtrl.registerSubCmdHandlers(&blinkAllLedHandler);
  IspCtrl.registerSubCmdHandlers(&slotLedBlinkHandler);
  IspCtrl.registerSubCmdHandlers(&loopbackTestHandler);
  IspCtrl.registerSubCmdHandlers(&powerCycleHandler);


  // Register Darin3 handlers directly with subcmdProcess (using static object address)
  subcmdProcess.registerHandler(static_cast<uint8_t>(IspSubCommand::D3_WRITE), &darin3Obj); //Write
  subcmdProcess.registerHandler(static_cast<uint8_t>(IspSubCommand::D3_READ), &darin3Obj); //Read
  subcmdProcess.registerHandler(static_cast<uint8_t>(IspSubCommand::D3_ERASE), &darin3Obj); //Erase
  subcmdProcess.registerHandler(static_cast<uint8_t>(IspSubCommand::D3_READ_FILES), &darin3Obj); //Read

  BlinkLed_PA1_PA8(350);

  while (1)
  {


    UpdateSlotLed();
    // ===== CHOOSE YOUR TEST =====
    // Option 1: Original working driver test
    //TesCompactFlashDriver(CARTRIDGE_1);

    // Option 2: Quick diskio test (commented out - PASSED)
    //Quick_DiskIO_Test();

    // Option 3: Comprehensive diskio test (commented out - PASSED)
    //Test_DiskIO_Functions();

    // Option 4: Quick FatFs test (uncomment to use)
     //Quick_FatFs_Test();

    // Option 5: Comprehensive FatFs test (uncomment to use)
    //Test_FatFs_Functions();

    // Option 6: Test FatFs wrapper isolation (uncomment to use)
    //Test_FatFsWrapper_Simple();

    // Option 7: Test conflict demonstration (uncomment to use)
    //Test_Wrapper_Conflict_Demo();

    // Option 8: Test singleton wrapper (uncomment to use)
    //Test_FatFsWrapperSingleton();

    // Option 9: Test simple wrapper (uncomment to use)
    //Test_SimpleFatFsWrapper();

    // Option 10: Original comprehensive CF test (uncomment to use)
    //ComprehensiveTest512(CARTRIDGE_1);


  }
  /* USER CODE END 3 */
}

/**
  * @brief System Clock Configuration
  * @retval None
  */
void SystemClock_Config(void)
{
  RCC_OscInitTypeDef RCC_OscInitStruct = {0};
  RCC_ClkInitTypeDef RCC_ClkInitStruct = {0};

  /** Configure the main internal regulator output voltage
  */
  __HAL_RCC_PWR_CLK_ENABLE();
  __HAL_PWR_VOLTAGESCALING_CONFIG(PWR_REGULATOR_VOLTAGE_SCALE1);

  /** Initializes the RCC Oscillators according to the specified parameters
  * in the RCC_OscInitTypeDef structure.
  */
  RCC_OscInitStruct.OscillatorType = RCC_OSCILLATORTYPE_HSE;
  RCC_OscInitStruct.HSEState = RCC_HSE_ON;
  RCC_OscInitStruct.PLL.PLLState = RCC_PLL_ON;
  RCC_OscInitStruct.PLL.PLLSource = RCC_PLLSOURCE_HSE;
  RCC_OscInitStruct.PLL.PLLM = 4;
  RCC_OscInitStruct.PLL.PLLN = 192;
  RCC_OscInitStruct.PLL.PLLP = RCC_PLLP_DIV4;
  RCC_OscInitStruct.PLL.PLLQ = 8;
  if (HAL_RCC_OscConfig(&RCC_OscInitStruct) != HAL_OK)
  {
    Error_Handler();
  }

  /** Initializes the CPU, AHB and APB buses clocks
  */
  RCC_ClkInitStruct.ClockType = RCC_CLOCKTYPE_HCLK|RCC_CLOCKTYPE_SYSCLK
                              |RCC_CLOCKTYPE_PCLK1|RCC_CLOCKTYPE_PCLK2;
  RCC_ClkInitStruct.SYSCLKSource = RCC_SYSCLKSOURCE_PLLCLK;
  RCC_ClkInitStruct.AHBCLKDivider = RCC_SYSCLK_DIV1;
  RCC_ClkInitStruct.APB1CLKDivider = RCC_HCLK_DIV4;
  RCC_ClkInitStruct.APB2CLKDivider = RCC_HCLK_DIV1;

  if (HAL_RCC_ClockConfig(&RCC_ClkInitStruct, FLASH_LATENCY_3) != HAL_OK)
  {
    Error_Handler();
  }
}

/**
  * @brief GPIO Initialization Function
  * @param None
  * @retval None
  */
static void MX_GPIO_Init(void)
{
  GPIO_InitTypeDef GPIO_InitStruct = {0};
  /* USER CODE BEGIN MX_GPIO_Init_1 */

  /* USER CODE END MX_GPIO_Init_1 */

  /* GPIO Ports Clock Enable */
  __HAL_RCC_GPIOE_CLK_ENABLE();
  __HAL_RCC_GPIOC_CLK_ENABLE();
  __HAL_RCC_GPIOH_CLK_ENABLE();
  __HAL_RCC_GPIOA_CLK_ENABLE();
  __HAL_RCC_GPIOB_CLK_ENABLE();
  __HAL_RCC_GPIOD_CLK_ENABLE();

  /*Configure GPIO pin Output Level */
  HAL_GPIO_WritePin(GPIOE, DB2_Pin|DB3_Pin|DB4_Pin|DB5_Pin
                          |DB6_Pin|DB7_Pin|DB0_Pin|DB1_Pin, GPIO_PIN_RESET);

  /*Configure GPIO pin Output Level */
  HAL_GPIO_WritePin(GPIOA, LED1_Pin|LED2_Pin|LED3_Pin|LED4_Pin
                          |LED5_Pin|LED6_Pin|LED7_Pin|LED8_Pin, GPIO_PIN_RESET);

  /*Configure GPIO pin Output Level */
  HAL_GPIO_WritePin(GPIOB, POWER_CYCLE_3_Pin|POWER_CYCLE_4_Pin|ATA_SEL_Pin|POWER_CYCLE_2_Pin, GPIO_PIN_RESET);

  /*Configure GPIO pin Output Level */
  HAL_GPIO_WritePin(GPIOD, CS1_1_Pin|WE_Pin|RESET_Pin|POWER_CYCLE_1_Pin
                          |CSO_1_Pin|CSO_2_Pin|CSO_3_Pin|CSO_4_Pin
                          |A00_Pin|A01_Pin|A02_Pin|A03_Pin, GPIO_PIN_RESET);

  /*Configure GPIO pins : DB2_Pin DB3_Pin DB4_Pin DB5_Pin
                           DB6_Pin DB7_Pin DB0_Pin DB1_Pin */
  GPIO_InitStruct.Pin = DB2_Pin|DB3_Pin|DB4_Pin|DB5_Pin
                          |DB6_Pin|DB7_Pin|DB0_Pin|DB1_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
  HAL_GPIO_Init(GPIOE, &GPIO_InitStruct);

  /*Configure GPIO pins : LED1_Pin LED2_Pin LED3_Pin LED4_Pin
                           LED5_Pin LED6_Pin LED7_Pin LED8_Pin */
  GPIO_InitStruct.Pin = LED1_Pin|LED2_Pin|LED3_Pin|LED4_Pin
                          |LED5_Pin|LED6_Pin|LED7_Pin|LED8_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
  HAL_GPIO_Init(GPIOA, &GPIO_InitStruct);

  /*Configure GPIO pins : CD2_SLT_S1_Pin CD2_SLT_S2_Pin CD2_SLT_S3_Pin CD2_SLT_S4_Pin
                           CD1_SLT_S1_Pin */
  GPIO_InitStruct.Pin = CD2_SLT_S1_Pin|CD2_SLT_S2_Pin|CD2_SLT_S3_Pin|CD2_SLT_S4_Pin
                          |CD1_SLT_S1_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_INPUT;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  HAL_GPIO_Init(GPIOC, &GPIO_InitStruct);

  /*Configure GPIO pins : LB1_Pin LB2_Pin LB3_Pin LB4_Pin */
  GPIO_InitStruct.Pin = LB1_Pin|LB2_Pin|LB3_Pin|LB4_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_INPUT;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  HAL_GPIO_Init(GPIOB, &GPIO_InitStruct);

  /*Configure GPIO pins : POWER_CYCLE_3_Pin POWER_CYCLE_4_Pin ATA_SEL_Pin POWER_CYCLE_2_Pin */
  GPIO_InitStruct.Pin = POWER_CYCLE_3_Pin|POWER_CYCLE_4_Pin|ATA_SEL_Pin|POWER_CYCLE_2_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
  HAL_GPIO_Init(GPIOB, &GPIO_InitStruct);

  /*Configure GPIO pins : CS1_1_Pin WE_Pin RESET_Pin POWER_CYCLE_1_Pin
                           CSO_1_Pin CSO_2_Pin CSO_3_Pin CSO_4_Pin
                           A00_Pin A01_Pin A02_Pin A03_Pin */
  GPIO_InitStruct.Pin = CS1_1_Pin|WE_Pin|RESET_Pin|POWER_CYCLE_1_Pin
                          |CSO_1_Pin|CSO_2_Pin|CSO_3_Pin|CSO_4_Pin
                          |A00_Pin|A01_Pin|A02_Pin|A03_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
  HAL_GPIO_Init(GPIOD, &GPIO_InitStruct);

  /* USER CODE BEGIN MX_GPIO_Init_2 */

  /* USER CODE END MX_GPIO_Init_2 */
}

/* USER CODE BEGIN 4 */

/* USER CODE END 4 */

/**
  * @brief  This function is executed in case of error occurrence.
  * @retval None
  */
void Error_Handler(void)
{
  /* USER CODE BEGIN Error_Handler_Debug */
  /* User can add his own implementation to report the HAL error return state */
  __disable_irq();
  while (1)
  {
  }
  /* USER CODE END Error_Handler_Debug */
}
#ifdef USE_FULL_ASSERT
/**
  * @brief  Reports the name of the source file and the source line number
  *         where the assert_param error has occurred.
  * @param  file: pointer to the source file name
  * @param  line: assert_param error line source number
  * @retval None
  */
void assert_failed(uint8_t *file, uint32_t line)
{
  /* USER CODE BEGIN 6 */
  /* User can add his own implementation to report the file name and line number,
     ex: printf("Wrong parameters value: file %s on line %d\r\n", file, line) */
  /* USER CODE END 6 */
}
#endif /* USE_FULL_ASSERT */
