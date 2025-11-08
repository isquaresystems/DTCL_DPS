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
extern "C" {
#include "main.h"
#include "usb_device.h"
#include "Darin3Cart_Driver.h"
#include "Darin2Cart_Driver.h"
#include "FAT/diskio.h"
}

#include "FAT/FatFsWrapper.h"
#include "Darin3.h"
#include "Darin2.h"
#include "Protocol/IspCmdReceiveData.h"
#include "Protocol/IspCommandManager.h"
#include "Protocol/IspCmdReceiveData.h"
#include "Protocol/IspCmdTransmitData.h"
#include "Protocol/IspFramingUtils.h"
#include "Protocol/IspCmdControl.h"
#include "Protocol/SerialTransport.h"

void SystemClock_Config(void);
static void MX_GPIO_Init(void);
extern "C" void Test_FatFsWrapperSingleton(void);

UsbIspTransport usbTransport;
IspCommandManager IspManager;
IspCmdReceiveData IspRx;
IspCmdTransmitData IspTx;
IspSubCommandProcessor subcmdProcess;
IspCmdControl IspCtrl;

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

void BlinkLed(uint16_t delay)
{
  for(int i=0;i<3;i++)
  {
   HAL_GPIO_WritePin(GPIOB, LED1_Pin, GPIO_PIN_SET);
   HAL_GPIO_WritePin(GPIOB, LED2_Pin, GPIO_PIN_SET);
   HAL_Delay(delay);
   HAL_GPIO_WritePin(GPIOB, LED1_Pin, GPIO_PIN_RESET);
   HAL_GPIO_WritePin(GPIOB, LED2_Pin, GPIO_PIN_RESET);
   HAL_Delay(delay);
  }
}

void UpdateSlotLed()
{
	GuiCtrlLed_SubCmdProcess obj;
	if(obj.get_LedState() == 0)
	{
		UpdateD3SlotStatus();
		UpdateD2SlotStatus();
		int itr=0;
		for (itr=0;itr<1;itr++)
		{
			if(get_D3_slt_status((CartridgeID)itr) == 3 || get_D2_slt_status((CartridgeID)itr) == 2)
			{
				HAL_GPIO_WritePin(GPIOB, get_D3_Green_LedPins((CartridgeID)itr)
						, GPIO_PIN_SET); //GREEN-1
			}
			else
			{
				HAL_GPIO_WritePin(GPIOB, get_D3_Green_LedPins((CartridgeID)itr)
						, GPIO_PIN_RESET); //GREEN-1
			}
		}
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

  BlinkLed(300);
  
	IspRx.setTransport(&usbTransport);
	IspTx.setTransport(&usbTransport);
	IspRx.setSubProcessor(&subcmdProcess);
	IspTx.setSubProcessor(&subcmdProcess);

	IspManager.addHandler(&IspRx);
	IspManager.addHandler(&IspTx);

	IspManager.setBoardID(IspBoardId::DTCL);


	IspCtrl.setTransport(&usbTransport);
	IspCtrl.setSubProcessor(&subcmdProcess);

	IspManager.addHandler(&IspCtrl);

	static FirmwareVersion_SubCmdProcess firmwareVersionHandler;
	static BoardID_SubCmdProcess boardIdHandler;
	static GuiCtrlLed_SubCmdProcess guiCtrlLedHandler;
	static GreenLed_SubCmdProcess greenLedHandler;
	static RedLed_SubCmdProcess redLedHandler;
	static CartStatus_SubCmdProcess cartStatusHandler;
	static LedLoopBack_SubCmdProcess loopbackTestHandler;
  static Erase_SubCmdProcess eraseHandler;
  static Format_SubCmdProcess formatHandler;
  static D3_Power_Cycle_SubCmdProcess powerCycleHandler;

	// Register control command handlers using static objects
	IspCtrl.registerSubCmdHandlers(&firmwareVersionHandler);
	IspCtrl.registerSubCmdHandlers(&boardIdHandler);
	IspCtrl.registerSubCmdHandlers(&guiCtrlLedHandler);
	IspCtrl.registerSubCmdHandlers(&greenLedHandler);
	IspCtrl.registerSubCmdHandlers(&redLedHandler);
	IspCtrl.registerSubCmdHandlers(&cartStatusHandler);
	IspCtrl.registerSubCmdHandlers(&loopbackTestHandler);
  IspCtrl.registerSubCmdHandlers(&eraseHandler);
  IspCtrl.registerSubCmdHandlers(&formatHandler);
  IspCtrl.registerSubCmdHandlers(&powerCycleHandler);


	HAL_GPIO_WritePin(GPIOA, C3_PWR_CYCLE_Pin, GPIO_PIN_SET);
	HAL_Delay(100);
	static Darin3 darin3Obj;
	subcmdProcess.registerHandler(static_cast<uint8_t>(IspSubCommand::D3_WRITE), &darin3Obj); //Write
	subcmdProcess.registerHandler(static_cast<uint8_t>(IspSubCommand::D3_READ), &darin3Obj); //Read
	subcmdProcess.registerHandler(static_cast<uint8_t>(IspSubCommand::D3_READ_FILES), &darin3Obj); //Read
	subcmdProcess.registerHandler(static_cast<uint8_t>(IspSubCommand::D3_ERASE), &darin3Obj); //Erase
	subcmdProcess.registerHandler(static_cast<uint8_t>(IspSubCommand::D3_FORMAT), &darin3Obj); //Format

	static Darin2 darin2Obj;

	// Register Darin2 handlers directly with subcmdProcess (using static object address)
	subcmdProcess.registerHandler(static_cast<uint8_t>(IspSubCommand::D2_READ), &darin2Obj); //Read
	subcmdProcess.registerHandler(static_cast<uint8_t>(IspSubCommand::D2_WRITE), &darin2Obj); //Write
	subcmdProcess.registerHandler(static_cast<uint8_t>(IspSubCommand::D2_ERASE_BLOCK), &darin2Obj); //Erase Block
	subcmdProcess.registerHandler(static_cast<uint8_t>(IspSubCommand::D2_ERASE), &darin2Obj); //Erase

	HAL_GPIO_WritePin(GPIOB, LED1_Pin, GPIO_PIN_RESET);
	HAL_GPIO_WritePin(GPIOB, LED2_Pin, GPIO_PIN_RESET);

	while (1)
	{
		UpdateSlotLed();
	}

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
  HAL_GPIO_WritePin(GPIOC, C1A04_C2CLE_Pin|C1A06_C2RBA_Pin|C2CE1_Pin|C2DB3_C3DB3_Pin
                          |C2DB4_C3DB4_Pin|C2DB6_C3DB6_Pin|C1DB4_Pin|C1DB6_Pin
                          |C1DB7_Pin|C3WE_Pin|C1SLTS4_C2SLTS4_C3SLTS4_Pin|C1WE_C3RST_Pin
                          |C1SLTS1_Pin, GPIO_PIN_RESET);

  /*Configure GPIO pin Output Level */
  HAL_GPIO_WritePin(GPIOA, C2DB5_C3DB5_INOUT_Pin|C2DB7_C3DB7_INOUT_Pin|C3OE_Pin|C2RB0_Pin
                          |C1A05_Pin|C1A07_Pin|C3_PWR_CYCLE_Pin|C1A09_Pin, GPIO_PIN_RESET);

  /*Configure GPIO pin Output Level */
  HAL_GPIO_WritePin(GPIOB, LED6_Pin|C3CE1_Pin|C1A03_C2nWE_C3A03_Pin|C1A00_C2ALE_C3A00_Pin
                          |C1A01_C2nWP_C3A01_Pin|C1A10_Pin|C2DB0_C3DB0_Pin|C2RE_Pin
                          |LED1_Pin|C1CE1_Pin|LED2_Pin, GPIO_PIN_RESET);

  /*Configure GPIO pin Output Level */
  HAL_GPIO_WritePin(GPIOE, LED8_Pin|LED5_Pin|C1CE3_C2CE3_C3CE3_Pin|LED7_Pin
                          |C1A02_C3A02_Pin|C1CE2_C2CE2_C3CE2_Pin|C3RST_Pin|C2DB1_C3DB1_Pin
                          |C1CE4_C2CE4_C3CE4_Pin|LED4_Pin|LED3_Pin, GPIO_PIN_RESET);

  /*Configure GPIO pin Output Level */
  HAL_GPIO_WritePin(GPIOD, C1DB0_C2RB2_C3IORDYS2_Pin|C2DB2_C3DB2_Pin|C1DB2_C2RB4_C3IORDYS4_Pin|BOARD_ID1_Pin
                          |C1DB5_Pin|C3SLTS1_Pin|C1DB1_C2RB3_C3IORDYS3_Pin|BOARD_ID0_Pin
                          |C1MIRS_Pin|C1A08_Pin|C2SLTS1_Pin|C3IORDY1_Pin
                          |C1SLTS2_C2SLTS2_C3SLTS2_Pin|C1SLTS3_C2SLTS3_C3SLTS3_Pin|C1DB3_Pin|C1OE_Pin, GPIO_PIN_RESET);

  /*Configure GPIO pins : LED1_LOOPBACK_Pin LED4_LOOPBACK_Pin */
  GPIO_InitStruct.Pin = LED1_LOOPBACK_Pin|LED4_LOOPBACK_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_INPUT;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  HAL_GPIO_Init(GPIOE, &GPIO_InitStruct);

  /*Configure GPIO pins : INT1_Pin INT2_Pin */
  GPIO_InitStruct.Pin = INT1_Pin|INT2_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_EVT_RISING;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  HAL_GPIO_Init(GPIOE, &GPIO_InitStruct);

  /*Configure GPIO pins : C1A04_C2CLE_Pin C1A06_C2RBA_Pin C2CE1_Pin C2DB3_C3DB3_Pin
                           C2DB4_C3DB4_Pin C2DB6_C3DB6_Pin C1DB4_Pin C1DB6_Pin
                           C1DB7_Pin C3WE_Pin C1SLTS4_C2SLTS4_C3SLTS4_Pin C1WE_C3RST_Pin
                           C1SLTS1_Pin */
  GPIO_InitStruct.Pin = C1A04_C2CLE_Pin|C1A06_C2RBA_Pin|C2CE1_Pin|C2DB3_C3DB3_Pin
                          |C2DB4_C3DB4_Pin|C2DB6_C3DB6_Pin|C1DB4_Pin|C1DB6_Pin
                          |C1DB7_Pin|C3WE_Pin|C1SLTS4_C2SLTS4_C3SLTS4_Pin|C1WE_C3RST_Pin
                          |C1SLTS1_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
  HAL_GPIO_Init(GPIOC, &GPIO_InitStruct);

  /*Configure GPIO pin : PA0 */
  GPIO_InitStruct.Pin = GPIO_PIN_0;
  GPIO_InitStruct.Mode = GPIO_MODE_EVT_RISING;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  HAL_GPIO_Init(GPIOA, &GPIO_InitStruct);

  /*Configure GPIO pins : C2DB5_C3DB5_INOUT_Pin C2DB7_C3DB7_INOUT_Pin C3OE_Pin C2RB0_Pin
                           C1A05_Pin C1A07_Pin C3_PWR_CYCLE_Pin C1A09_Pin */
  GPIO_InitStruct.Pin = C2DB5_C3DB5_INOUT_Pin|C2DB7_C3DB7_INOUT_Pin|C3OE_Pin|C2RB0_Pin
                          |C1A05_Pin|C1A07_Pin|C3_PWR_CYCLE_Pin|C1A09_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
  HAL_GPIO_Init(GPIOA, &GPIO_InitStruct);

  /*Configure GPIO pins : LED6_Pin C3CE1_Pin C1A03_C2nWE_C3A03_Pin C1A00_C2ALE_C3A00_Pin
                           C1A01_C2nWP_C3A01_Pin C1A10_Pin C2DB0_C3DB0_Pin C2RE_Pin
                           LED1_Pin C1CE1_Pin LED2_Pin */
  GPIO_InitStruct.Pin = LED6_Pin|C3CE1_Pin|C1A03_C2nWE_C3A03_Pin|C1A00_C2ALE_C3A00_Pin
                          |C1A01_C2nWP_C3A01_Pin|C1A10_Pin|C2DB0_C3DB0_Pin|C2RE_Pin
                          |LED1_Pin|C1CE1_Pin|LED2_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
  HAL_GPIO_Init(GPIOB, &GPIO_InitStruct);

  /*Configure GPIO pins : LED8_Pin LED5_Pin C1CE3_C2CE3_C3CE3_Pin LED7_Pin
                           C1A02_C3A02_Pin C1CE2_C2CE2_C3CE2_Pin C3RST_Pin C2DB1_C3DB1_Pin
                           C1CE4_C2CE4_C3CE4_Pin LED4_Pin LED3_Pin */
  GPIO_InitStruct.Pin = LED8_Pin|LED5_Pin|C1CE3_C2CE3_C3CE3_Pin|LED7_Pin
                          |C1A02_C3A02_Pin|C1CE2_C2CE2_C3CE2_Pin|C3RST_Pin|C2DB1_C3DB1_Pin
                          |C1CE4_C2CE4_C3CE4_Pin|LED4_Pin|LED3_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
  HAL_GPIO_Init(GPIOE, &GPIO_InitStruct);

  /*Configure GPIO pins : C1DB0_C2RB2_C3IORDYS2_Pin C2DB2_C3DB2_Pin C1DB2_C2RB4_C3IORDYS4_Pin BOARD_ID1_Pin
                           C1DB5_Pin C3SLTS1_Pin C1DB1_C2RB3_C3IORDYS3_Pin BOARD_ID0_Pin
                           C1MIRS_Pin C1A08_Pin C2SLTS1_Pin C3IORDY1_Pin
                           C1SLTS2_C2SLTS2_C3SLTS2_Pin C1SLTS3_C2SLTS3_C3SLTS3_Pin C1DB3_Pin C1OE_Pin */
  GPIO_InitStruct.Pin = C1DB0_C2RB2_C3IORDYS2_Pin|C2DB2_C3DB2_Pin|C1DB2_C2RB4_C3IORDYS4_Pin|BOARD_ID1_Pin
                          |C1DB5_Pin|C3SLTS1_Pin|C1DB1_C2RB3_C3IORDYS3_Pin|BOARD_ID0_Pin
                          |C1MIRS_Pin|C1A08_Pin|C2SLTS1_Pin|C3IORDY1_Pin
                          |C1SLTS2_C2SLTS2_C3SLTS2_Pin|C1SLTS3_C2SLTS3_C3SLTS3_Pin|C1DB3_Pin|C1OE_Pin;
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

#ifdef  USE_FULL_ASSERT
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
