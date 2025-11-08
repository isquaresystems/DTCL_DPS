#pragma once
#include <cstdint>
#include <cstddef>

class IspFramingUtils {
public:
    static constexpr uint8_t START_BYTE = 0x7E;
    static constexpr uint8_t END_BYTE   = 0x7F;

    // Wrap a payload into a framed packet using CRC8
    static std::size_t encodeFrame(const uint8_t* payload, std::size_t len, volatile uint8_t* outBuf, std::size_t outMax) {
        if (outMax < len + 4) return 0;
        outBuf[0] = START_BYTE;
        outBuf[1] = static_cast<uint8_t>(len);
        for (std::size_t i = 0; i < len; ++i) {
            outBuf[2 + i] = payload[i];
        }
        outBuf[2 + len] = computeCRC8(payload, len);
        outBuf[3 + len] = END_BYTE;
        return len + 4;
    }

    // Decode and validate a framed packet
    static bool decodeFrame(const uint8_t* frame, std::size_t frameLen, uint8_t* outPayload, std::size_t& outLen) {
        if (frameLen < 4 || frame[0] != START_BYTE || frame[frameLen - 1] != END_BYTE) return false;
        std::size_t len = frame[1];
        if (len + 4 != frameLen) return false;

        uint8_t crc = frame[2 + len];
        if (crc != computeCRC8(&frame[2], len)) return false;

        for (std::size_t i = 0; i < len; ++i) {
            outPayload[i] = frame[2 + i];
        }
        outLen = len;
        return true;
    }

private:
    // Standard CRC-8 (polynomial 0x07, init 0x00)
    static uint8_t computeCRC8(const uint8_t* data, std::size_t len) {
        uint8_t crc = 0x00;
        for (std::size_t i = 0; i < len; ++i) {
            crc ^= data[i];
            for (int j = 0; j < 8; ++j) {
                if (crc & 0x80)
                    crc = (crc << 1) ^ 0x07;
                else
                    crc <<= 1;
            }
        }
        return crc;
    }
};
