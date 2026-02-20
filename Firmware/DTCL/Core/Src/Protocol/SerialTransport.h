/*
 * SerialTransport.h
 *
 *  Created on: Apr 13, 2025
 *      Author: HP-Admin
 */

#ifndef SRC_SERIALTRANSPORT_H_
#define SRC_SERIALTRANSPORT_H_


#pragma once
#include "IspTransportInterface.h"

class UsbIspTransport : public IspTransportInterface {
public:
    bool transmit(volatile const uint8_t* data, std::size_t len) override;
    const char* name() const override { return "USB CDC"; }
};


#endif /* SRC_SERIALTRANSPORT_H_ */
