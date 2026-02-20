/* USER CODE BEGIN Header */
/**
  ******************************************************************************
  * @file           : main.h
  * @brief          : Header for main.c file.
  *                   This file contains the common defines of the application.
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

/* Define to prevent recursive inclusion -------------------------------------*/
#ifndef __MAIN_H
#define __MAIN_H

#ifdef __cplusplus
extern "C" {
#endif

/* Includes ------------------------------------------------------------------*/
#include "stm32f4xx_hal.h"

/* Private includes ----------------------------------------------------------*/
/* USER CODE BEGIN Includes */

/* USER CODE END Includes */

/* Exported types ------------------------------------------------------------*/
/* USER CODE BEGIN ET */

/* USER CODE END ET */

/* Exported constants --------------------------------------------------------*/
/* USER CODE BEGIN EC */

/* USER CODE END EC */

/* Exported macro ------------------------------------------------------------*/
/* USER CODE BEGIN EM */

/* USER CODE END EM */

/* Exported functions prototypes ---------------------------------------------*/
void Error_Handler(void);

/* USER CODE BEGIN EFP */

/* USER CODE END EFP */

/* Private defines -----------------------------------------------------------*/
#define DB2_Pin GPIO_PIN_2
#define DB2_GPIO_Port GPIOE
#define DB3_Pin GPIO_PIN_3
#define DB3_GPIO_Port GPIOE
#define DB4_Pin GPIO_PIN_4
#define DB4_GPIO_Port GPIOE
#define DB5_Pin GPIO_PIN_5
#define DB5_GPIO_Port GPIOE
#define DB6_Pin GPIO_PIN_6
#define DB6_GPIO_Port GPIOE
#define LED1_Pin GPIO_PIN_1
#define LED1_GPIO_Port GPIOA
#define LED2_Pin GPIO_PIN_2
#define LED2_GPIO_Port GPIOA
#define LED3_Pin GPIO_PIN_3
#define LED3_GPIO_Port GPIOA
#define LED4_Pin GPIO_PIN_4
#define LED4_GPIO_Port GPIOA
#define LED5_Pin GPIO_PIN_5
#define LED5_GPIO_Port GPIOA
#define LED6_Pin GPIO_PIN_6
#define LED6_GPIO_Port GPIOA
#define LED7_Pin GPIO_PIN_7
#define LED7_GPIO_Port GPIOA
#define CD2_SLT_S1_Pin GPIO_PIN_4
#define CD2_SLT_S1_GPIO_Port GPIOC
#define CD2_SLT_S2_Pin GPIO_PIN_5
#define CD2_SLT_S2_GPIO_Port GPIOC
#define LB1_Pin GPIO_PIN_1
#define LB1_GPIO_Port GPIOB
#define DB7_Pin GPIO_PIN_7
#define DB7_GPIO_Port GPIOE
#define POWER_CYCLE_3_Pin GPIO_PIN_10
#define POWER_CYCLE_3_GPIO_Port GPIOB
#define POWER_CYCLE_4_Pin GPIO_PIN_12
#define POWER_CYCLE_4_GPIO_Port GPIOB
#define ATA_SEL_Pin GPIO_PIN_15
#define ATA_SEL_GPIO_Port GPIOB
#define CS1_1_Pin GPIO_PIN_8
#define CS1_1_GPIO_Port GPIOD
#define WE_Pin GPIO_PIN_12
#define WE_GPIO_Port GPIOD
#define RESET_Pin GPIO_PIN_13
#define RESET_GPIO_Port GPIOD
#define POWER_CYCLE_1_Pin GPIO_PIN_15
#define POWER_CYCLE_1_GPIO_Port GPIOD
#define CD2_SLT_S3_Pin GPIO_PIN_6
#define CD2_SLT_S3_GPIO_Port GPIOC
#define CD2_SLT_S4_Pin GPIO_PIN_7
#define CD2_SLT_S4_GPIO_Port GPIOC
#define CD1_SLT_S1_Pin GPIO_PIN_8
#define CD1_SLT_S1_GPIO_Port GPIOC
#define LED8_Pin GPIO_PIN_8
#define LED8_GPIO_Port GPIOA
#define CSO_1_Pin GPIO_PIN_0
#define CSO_1_GPIO_Port GPIOD
#define CSO_2_Pin GPIO_PIN_1
#define CSO_2_GPIO_Port GPIOD
#define CSO_3_Pin GPIO_PIN_2
#define CSO_3_GPIO_Port GPIOD
#define CSO_4_Pin GPIO_PIN_3
#define CSO_4_GPIO_Port GPIOD
#define A00_Pin GPIO_PIN_4
#define A00_GPIO_Port GPIOD
#define A01_Pin GPIO_PIN_5
#define A01_GPIO_Port GPIOD
#define A02_Pin GPIO_PIN_6
#define A02_GPIO_Port GPIOD
#define A03_Pin GPIO_PIN_7
#define A03_GPIO_Port GPIOD
#define LB2_Pin GPIO_PIN_6
#define LB2_GPIO_Port GPIOB
#define LB3_Pin GPIO_PIN_7
#define LB3_GPIO_Port GPIOB
#define LB4_Pin GPIO_PIN_8
#define LB4_GPIO_Port GPIOB
#define POWER_CYCLE_2_Pin GPIO_PIN_9
#define POWER_CYCLE_2_GPIO_Port GPIOB
#define DB0_Pin GPIO_PIN_0
#define DB0_GPIO_Port GPIOE
#define DB1_Pin GPIO_PIN_1
#define DB1_GPIO_Port GPIOE

/* USER CODE BEGIN Private defines */

/* USER CODE END Private defines */

#ifdef __cplusplus
}
#endif

#endif /* __MAIN_H */
