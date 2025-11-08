#include "SerialTransport.h"
#include "usbd_cdc_if.h"

bool UsbVennaTransport::transmit(volatile const uint8_t* data, std::size_t len) {
    return CDC_Transmit_FS((uint8_t*)data, len) == USBD_OK;
}




