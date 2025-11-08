/* USER CODE BEGIN Header */
/**
  ******************************************************************************
  * @file           : main.h
  * @brief          : Header for main.c file.
  *                   This file contains the common defines of the application.
  ******************************************************************************
  * @attention
  *
  * Copyright (c) 2023 STMicroelectronics.
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
#define LED1_LOOPBACK_Pin GPIO_PIN_2
#define LED1_LOOPBACK_GPIO_Port GPIOE
#define LED4_LOOPBACK_Pin GPIO_PIN_3
#define LED4_LOOPBACK_GPIO_Port GPIOE
#define INT1_Pin GPIO_PIN_4
#define INT1_GPIO_Port GPIOE
#define INT2_Pin GPIO_PIN_5
#define INT2_GPIO_Port GPIOE
#define PC14_OSC32_IN_Pin GPIO_PIN_14
#define PC14_OSC32_IN_GPIO_Port GPIOC
#define PC15_OSC32_OUT_Pin GPIO_PIN_15
#define PC15_OSC32_OUT_GPIO_Port GPIOC
#define PH0_OSC_IN_Pin GPIO_PIN_0
#define PH0_OSC_IN_GPIO_Port GPIOH
#define PH1_OSC_OUT_Pin GPIO_PIN_1
#define PH1_OSC_OUT_GPIO_Port GPIOH
#define C1A04_C2CLE_Pin GPIO_PIN_0
#define C1A04_C2CLE_GPIO_Port GPIOC
#define C1A06_C2RBA_Pin GPIO_PIN_1
#define C1A06_C2RBA_GPIO_Port GPIOC
#define C2CE1_Pin GPIO_PIN_2
#define C2CE1_GPIO_Port GPIOC
#define C2DB3_C3DB3_Pin GPIO_PIN_3
#define C2DB3_C3DB3_GPIO_Port GPIOC
#define C2DB5_C3DB5_INOUT_Pin GPIO_PIN_1
#define C2DB5_C3DB5_INOUT_GPIO_Port GPIOA
#define C2DB7_C3DB7_INOUT_Pin GPIO_PIN_2
#define C2DB7_C3DB7_INOUT_GPIO_Port GPIOA
#define C3OE_Pin GPIO_PIN_3
#define C3OE_GPIO_Port GPIOA
#define C2RB0_Pin GPIO_PIN_4
#define C2RB0_GPIO_Port GPIOA
#define C1A05_Pin GPIO_PIN_5
#define C1A05_GPIO_Port GPIOA
#define C1A07_Pin GPIO_PIN_6
#define C1A07_GPIO_Port GPIOA
#define C3_PWR_CYCLE_Pin GPIO_PIN_7
#define C3_PWR_CYCLE_GPIO_Port GPIOA
#define C2DB4_C3DB4_Pin GPIO_PIN_4
#define C2DB4_C3DB4_GPIO_Port GPIOC
#define C2DB6_C3DB6_Pin GPIO_PIN_5
#define C2DB6_C3DB6_GPIO_Port GPIOC
#define LED6_Pin GPIO_PIN_0
#define LED6_GPIO_Port GPIOB
#define C3CE1_Pin GPIO_PIN_1
#define C3CE1_GPIO_Port GPIOB
#define LED8_Pin GPIO_PIN_7
#define LED8_GPIO_Port GPIOE
#define LED5_Pin GPIO_PIN_8
#define LED5_GPIO_Port GPIOE
#define C1CE3_C2CE3_C3CE3_Pin GPIO_PIN_9
#define C1CE3_C2CE3_C3CE3_GPIO_Port GPIOE
#define LED7_Pin GPIO_PIN_10
#define LED7_GPIO_Port GPIOE
#define C1A02_C3A02_Pin GPIO_PIN_11
#define C1A02_C3A02_GPIO_Port GPIOE
#define C1CE2_C2CE2_C3CE2_Pin GPIO_PIN_12
#define C1CE2_C2CE2_C3CE2_GPIO_Port GPIOE
#define C3RST_Pin GPIO_PIN_13
#define C3RST_GPIO_Port GPIOE
#define C2DB1_C3DB1_Pin GPIO_PIN_14
#define C2DB1_C3DB1_GPIO_Port GPIOE
#define C1CE4_C2CE4_C3CE4_Pin GPIO_PIN_15
#define C1CE4_C2CE4_C3CE4_GPIO_Port GPIOE
#define C1A03_C2nWE_C3A03_Pin GPIO_PIN_10
#define C1A03_C2nWE_C3A03_GPIO_Port GPIOB
#define C1A00_C2ALE_C3A00_Pin GPIO_PIN_12
#define C1A00_C2ALE_C3A00_GPIO_Port GPIOB
#define C1A01_C2nWP_C3A01_Pin GPIO_PIN_13
#define C1A01_C2nWP_C3A01_GPIO_Port GPIOB
#define C1A10_Pin GPIO_PIN_14
#define C1A10_GPIO_Port GPIOB
#define C2DB0_C3DB0_Pin GPIO_PIN_15
#define C2DB0_C3DB0_GPIO_Port GPIOB
#define C1DB0_C2RB2_C3IORDYS2_Pin GPIO_PIN_8
#define C1DB0_C2RB2_C3IORDYS2_GPIO_Port GPIOD
#define C2DB2_C3DB2_Pin GPIO_PIN_9
#define C2DB2_C3DB2_GPIO_Port GPIOD
#define C1DB2_C2RB4_C3IORDYS4_Pin GPIO_PIN_10
#define C1DB2_C2RB4_C3IORDYS4_GPIO_Port GPIOD
#define BOARD_ID1_Pin GPIO_PIN_11
#define BOARD_ID1_GPIO_Port GPIOD
#define C1DB5_Pin GPIO_PIN_12
#define C1DB5_GPIO_Port GPIOD
#define C3SLTS1_Pin GPIO_PIN_13
#define C3SLTS1_GPIO_Port GPIOD
#define C1DB1_C2RB3_C3IORDYS3_Pin GPIO_PIN_14
#define C1DB1_C2RB3_C3IORDYS3_GPIO_Port GPIOD
#define BOARD_ID0_Pin GPIO_PIN_15
#define BOARD_ID0_GPIO_Port GPIOD
#define C1DB4_Pin GPIO_PIN_6
#define C1DB4_GPIO_Port GPIOC
#define C1DB6_Pin GPIO_PIN_7
#define C1DB6_GPIO_Port GPIOC
#define C1DB7_Pin GPIO_PIN_8
#define C1DB7_GPIO_Port GPIOC
#define C3WE_Pin GPIO_PIN_9
#define C3WE_GPIO_Port GPIOC
#define C1A09_Pin GPIO_PIN_8
#define C1A09_GPIO_Port GPIOA
#define VBUS_FS_Pin GPIO_PIN_9
#define VBUS_FS_GPIO_Port GPIOA
#define OTG_FS_ID_Pin GPIO_PIN_10
#define OTG_FS_ID_GPIO_Port GPIOA
#define OTG_FS_DM_Pin GPIO_PIN_11
#define OTG_FS_DM_GPIO_Port GPIOA
#define OTG_FS_DP_Pin GPIO_PIN_12
#define OTG_FS_DP_GPIO_Port GPIOA
#define SWDIO_Pin GPIO_PIN_13
#define SWDIO_GPIO_Port GPIOA
#define SWCLK_Pin GPIO_PIN_14
#define SWCLK_GPIO_Port GPIOA
#define C1SLTS4_C2SLTS4_C3SLTS4_Pin GPIO_PIN_10
#define C1SLTS4_C2SLTS4_C3SLTS4_GPIO_Port GPIOC
#define C1WE_C3RST_Pin GPIO_PIN_11
#define C1WE_C3RST_GPIO_Port GPIOC
#define C1SLTS1_Pin GPIO_PIN_12
#define C1SLTS1_GPIO_Port GPIOC
#define C1MIRS_Pin GPIO_PIN_0
#define C1MIRS_GPIO_Port GPIOD
#define C1A08_Pin GPIO_PIN_1
#define C1A08_GPIO_Port GPIOD
#define C2SLTS1_Pin GPIO_PIN_2
#define C2SLTS1_GPIO_Port GPIOD
#define C3IORDY1_Pin GPIO_PIN_3
#define C3IORDY1_GPIO_Port GPIOD
#define C1SLTS2_C2SLTS2_C3SLTS2_Pin GPIO_PIN_4
#define C1SLTS2_C2SLTS2_C3SLTS2_GPIO_Port GPIOD
#define C1SLTS3_C2SLTS3_C3SLTS3_Pin GPIO_PIN_5
#define C1SLTS3_C2SLTS3_C3SLTS3_GPIO_Port GPIOD
#define C1DB3_Pin GPIO_PIN_6
#define C1DB3_GPIO_Port GPIOD
#define C1OE_Pin GPIO_PIN_7
#define C1OE_GPIO_Port GPIOD
#define SWO_Pin GPIO_PIN_3
#define SWO_GPIO_Port GPIOB
#define C2RE_Pin GPIO_PIN_6
#define C2RE_GPIO_Port GPIOB
#define LED1_Pin GPIO_PIN_7
#define LED1_GPIO_Port GPIOB
#define C1CE1_Pin GPIO_PIN_8
#define C1CE1_GPIO_Port GPIOB
#define LED2_Pin GPIO_PIN_9
#define LED2_GPIO_Port GPIOB
#define LED4_Pin GPIO_PIN_0
#define LED4_GPIO_Port GPIOE
#define LED3_Pin GPIO_PIN_1
#define LED3_GPIO_Port GPIOE

/* USER CODE BEGIN Private defines */

/* USER CODE END Private defines */

#ifdef __cplusplus
}
#endif

#endif /* __MAIN_H */
