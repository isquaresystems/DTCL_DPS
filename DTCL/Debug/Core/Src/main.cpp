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
#include "main.h"
#include "usb_device.h"
#include "Darin3Cart_Driver.h"

#include "Darin3.h"
#include "Protocol/VennaCmdReceiveData.h"
#include "Protocol/VennaCommandManager.h"
#include "Protocol/VennaCmdReceiveData.h"
#include "Protocol/VennaCmdTransmitData.h"
#include "Protocol/VennaFramingUtils.h"
#include "Protocol/VennaCmdControl.h"
#include "Protocol/SerialTransport.h"
#include "Protocol/Logger.h"
#include <memory>

/* Private includes ----------------------------------------------------------*/
/* USER CODE BEGIN Includes */

/* USER CODE END Includes */

/* Private typedef -----------------------------------------------------------*/
/* USER CODE BEGIN PTD */

/* USER CODE END PTD */

/* Private define ------------------------------------------------------------*/
/* USER CODE BEGIN PD */

/* USER CODE END PD */

/* Private macro -------------------------------------------------------------*/
/* USER CODE BEGIN PM */

/* USER CODE END PM */

/* Private variables ---------------------------------------------------------*/

/* USER CODE BEGIN PV */

/* USER CODE END PV */

/* Private function prototypes -----------------------------------------------*/
void SystemClock_Config(void);
static void MX_GPIO_Init(void);

UsbVennaTransport usbTransport;
VennaCommandManager vennaManager;
VennaCmdReceiveData vennaRx;
VennaCmdTransmitData vennaTx;
VennaSubCommandProcessor subcmdProcess;
VennaCmdControl vennaCtrl;
//extern VennaCommandManager vennaManager;

extern "C" void venna_forward_data(const uint8_t* data, uint32_t len)
{
	uint8_t payload[256];
	std::size_t payloadLen = 0;
	if (len == 0 || !data) return;
	if (VennaFramingUtils::decodeFrame(data, len, payload, payloadLen))
	{
		if (payloadLen == 0) return;
		vennaManager.handleData(&payload[0], payloadLen);
	}
}

void BlinkLed(uint16_t delay)
{
  for(int i=0;i<3;i++)
  {
	  GPIOA->ODR = (GPIOA->ODR & 0x1FF00U) | 0x1FF;

      HAL_Delay(delay);

      GPIOA->ODR = (GPIOA->ODR & 0x1FF00U) | 0x00;

      HAL_Delay(delay);

  }

  /*HAL_GPIO_WritePin(GPIOA, LED1_Pin, GPIO_PIN_SET); //GREEN-1
  HAL_GPIO_WritePin(GPIOA, LED2_Pin, GPIO_PIN_SET); //RED-1
  HAL_GPIO_WritePin(GPIOA, LED3_Pin, GPIO_PIN_SET); //GREEN-2
  HAL_GPIO_WritePin(GPIOA, LED4_Pin, GPIO_PIN_SET); //RED-2
  HAL_GPIO_WritePin(GPIOA, LED5_Pin, GPIO_PIN_SET); //GREEN-3
  HAL_GPIO_WritePin(GPIOA, LED6_Pin, GPIO_PIN_SET); //RED-3
  HAL_GPIO_WritePin(GPIOA, LED7_Pin, GPIO_PIN_SET); //GREEN-4
  HAL_GPIO_WritePin(GPIOA, LED8_Pin, GPIO_PIN_SET); //RED-4*/
}

void UpdateSlotLed()
{
	GuiCtrlLed_SubCmdProcess obj;
	if(obj.get_LedState() == 0)
	{
		UpdateD3SlotStatus();
		int itr=0;
		for (itr=0;itr<4;itr++)
		{
			if(get_D3_slt_status((CartridgeID)itr) == 0)
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

/* USER CODE BEGIN PFP */

/* USER CODE END PFP */

/* Private user code ---------------------------------------------------------*/
/* USER CODE BEGIN 0 */

/* USER CODE END 0 */

/**
  * @brief  The application entry point.
  * @retval int
  */
int main(void)
{
	HAL_GPIO_WritePin(GPIOD, PWR_CYCLE_SLT1_Pin , GPIO_PinState::GPIO_PIN_SET); //power on compact flash
	HAL_GPIO_WritePin(GPIOB, PWR_CYCLE_SLT2_Pin , GPIO_PinState::GPIO_PIN_SET); //power on compact flash
	HAL_GPIO_WritePin(GPIOB, PWR_CYCLE_SLT3_Pin , GPIO_PinState::GPIO_PIN_SET); //power on compact flash
	HAL_GPIO_WritePin(GPIOB, PWR_CYCLE_SLT4_Pin , GPIO_PinState::GPIO_PIN_SET); //power on compact flash

	HAL_Init();
	SystemClock_Config();
	MX_GPIO_Init();
	MX_USB_DEVICE_Init();

	vennaRx.setTransport(&usbTransport);
	vennaTx.setTransport(&usbTransport);
	vennaRx.setSubProcessor(&subcmdProcess);
	vennaTx.setSubProcessor(&subcmdProcess);

	vennaManager.addHandler(&vennaRx);
	vennaManager.addHandler(&vennaTx);

	vennaManager.setBoardID(VennaBoardId::DPS_4_IN_1);


	vennaCtrl.setTransport(&usbTransport);
	vennaCtrl.setSubProcessor(&subcmdProcess);

	vennaManager.addHandler(&vennaCtrl);

	vennaCtrl.registerSubCmdHandlers(std::make_unique<FirmwareVersion_SubCmdProcess>());
	vennaCtrl.registerSubCmdHandlers(std::make_unique<BoardID_SubCmdProcess>());
	vennaCtrl.registerSubCmdHandlers(std::make_unique<GuiCtrlLed_SubCmdProcess>());
	vennaCtrl.registerSubCmdHandlers(std::make_unique<GreenLed_SubCmdProcess>());
	vennaCtrl.registerSubCmdHandlers(std::make_unique<RedLed_SubCmdProcess>());
	vennaCtrl.registerSubCmdHandlers(std::make_unique<CartStatus_SubCmdProcess>());
	vennaCtrl.registerSubCmdHandlers(std::make_unique<ReadD3FileInfo_SubCmdProcess>());
	vennaCtrl.registerSubCmdHandlers(std::make_unique<Erase_SubCmdProcess>());

	auto darin3Obj = std::make_unique<Darin3>();
	subcmdProcess.registerHandler(static_cast<uint8_t>(VennaSubCommand::D3_WRITE), darin3Obj.get()); //Write
	subcmdProcess.registerHandler(static_cast<uint8_t>(VennaSubCommand::D3_READ), darin3Obj.get()); //Read
	subcmdProcess.registerHandler(static_cast<uint8_t>(VennaSubCommand::D3_ERASE), darin3Obj.get()); //Erase
	subcmdProcess.registerHandler(static_cast<uint8_t>(VennaSubCommand::D3_FORMAT), darin3Obj.get()); //Erase

	BlinkLed(300);

	//SimpleLogger::getInstance().log(SimpleLogger::LOG_INFO, "Start\n");

	//darin2Obj->TestDarinIIFlash(0, 10);

	//const char txData[] = "Hello from STM32 using Venna Protocol!";
	//vennaTx.setDataToSend((const uint8_t*)txData, strlen(txData));

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
  HAL_GPIO_WritePin(GPIOE, DB2_Pin|DB3_Pin|DB4_Pin|DB5_Pin
                          |DB6_Pin|D07_Pin|DB8_Pin|DB9_Pin
                          |DB10_Pin|DB11_Pin|DB12_Pin|DB13_Pin
                          |DB14_Pin|DB15_Pin|DB0_Pin|DB1_Pin, GPIO_PIN_RESET);

  /*Configure GPIO pin Output Level */
  HAL_GPIO_WritePin(GPIOA, LED1_Pin|LED2_Pin|LED3_Pin|LED4_Pin
                          |LED5_Pin|LED6_Pin|LED7_Pin|LED8_Pin, GPIO_PIN_RESET);

  /*Configure GPIO pin Output Level */
  HAL_GPIO_WritePin(GPIOB, PWR_CYCLE_SLT3_Pin|PWR_CYCLE_SLT4_Pin|IOWR_Pin|DMACK_Pin
                          |ATA_SEL_Pin|PWR_CYCLE_SLT2_Pin, GPIO_PIN_RESET);

  /*Configure GPIO pin Output Level */
  HAL_GPIO_WritePin(GPIOD, CS1_1_Pin|CS1_2_Pin|CS1_3_Pin|CS1_4_Pin
                          |WE_Pin|RESET_Pin|IORD_Pin|PWR_CYCLE_SLT1_Pin
                          |CSO_1_Pin|CSO_2_Pin|CSO_3_Pin|CSO_4_Pin
                          |A00_Pin|A01_Pin|A02_Pin|A03_Pin, GPIO_PIN_RESET);

  /*Configure GPIO pins : DB2_Pin DB3_Pin DB4_Pin DB5_Pin
                           DB6_Pin D07_Pin DB8_Pin DB9_Pin
                           DB10_Pin DB11_Pin DB12_Pin DB13_Pin
                           DB14_Pin DB15_Pin DB0_Pin DB1_Pin */
  GPIO_InitStruct.Pin = DB2_Pin|DB3_Pin|DB4_Pin|DB5_Pin
                          |DB6_Pin|D07_Pin|DB8_Pin|DB9_Pin
                          |DB10_Pin|DB11_Pin|DB12_Pin|DB13_Pin
                          |DB14_Pin|DB15_Pin|DB0_Pin|DB1_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
  HAL_GPIO_Init(GPIOE, &GPIO_InitStruct);

  /*Configure GPIO pins : DMARQ_Pin IORDY_S1_Pin IORDY_S2_Pin IORDY_S3_Pin
                           IORDY_S4_Pin CD2_SLT_S1_Pin CD2_SLT_S2_Pin CD2_SLT_S3_Pin
                           CD2_SLT_S4_Pin CD1_SLT_S1_Pin CD1_SLT_S2_Pin CD1_SLT_S3_Pin
                           CD1_SLT_S4_Pin INTRQ_Pin */
  GPIO_InitStruct.Pin = DMARQ_Pin|IORDY_S1_Pin|IORDY_S2_Pin|IORDY_S3_Pin
                          |IORDY_S4_Pin|CD2_SLT_S1_Pin|CD2_SLT_S2_Pin|CD2_SLT_S3_Pin
                          |CD2_SLT_S4_Pin|CD1_SLT_S1_Pin|CD1_SLT_S2_Pin|CD1_SLT_S3_Pin
                          |CD1_SLT_S4_Pin|INTRQ_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_INPUT;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  HAL_GPIO_Init(GPIOC, &GPIO_InitStruct);

  /*Configure GPIO pins : LED1_Pin LED2_Pin LED3_Pin LED4_Pin
                           LED5_Pin LED6_Pin LED7_Pin LED8_Pin */
  GPIO_InitStruct.Pin = LED1_Pin|LED2_Pin|LED3_Pin|LED4_Pin
                          |LED5_Pin|LED6_Pin|LED7_Pin|LED8_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
  HAL_GPIO_Init(GPIOA, &GPIO_InitStruct);

  /*Configure GPIO pins : IOCS16_Pin LB1_Pin LB2_Pin LB3_Pin
                           LB4_Pin */
  GPIO_InitStruct.Pin = IOCS16_Pin|LB1_Pin|LB2_Pin|LB3_Pin
                          |LB4_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_INPUT;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  HAL_GPIO_Init(GPIOB, &GPIO_InitStruct);

  /*Configure GPIO pins : PWR_CYCLE_SLT3_Pin PWR_CYCLE_SLT4_Pin IOWR_Pin DMACK_Pin
                           ATA_SEL_Pin PWR_CYCLE_SLT2_Pin */
  GPIO_InitStruct.Pin = PWR_CYCLE_SLT3_Pin|PWR_CYCLE_SLT4_Pin|IOWR_Pin|DMACK_Pin
                          |ATA_SEL_Pin|PWR_CYCLE_SLT2_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
  HAL_GPIO_Init(GPIOB, &GPIO_InitStruct);

  /*Configure GPIO pins : CS1_1_Pin CS1_2_Pin CS1_3_Pin CS1_4_Pin
                           WE_Pin RESET_Pin IORD_Pin PWR_CYCLE_SLT1_Pin
                           CSO_1_Pin CSO_2_Pin CSO_3_Pin CSO_4_Pin
                           A00_Pin A01_Pin A02_Pin A03_Pin */
  GPIO_InitStruct.Pin = CS1_1_Pin|CS1_2_Pin|CS1_3_Pin|CS1_4_Pin
                          |WE_Pin|RESET_Pin|IORD_Pin|PWR_CYCLE_SLT1_Pin
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
