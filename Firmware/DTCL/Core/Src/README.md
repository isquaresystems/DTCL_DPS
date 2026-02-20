# Venna Protocol (Framed + CRC8)

**Venna Protocol** is a modular, lightweight, and interface-agnostic communication protocol designed for embedded systems. This version adds robust framing support with CRC8 checksum, start and end markers — making it ideal for unreliable links like UART, radio, or BLE.

---

## ✅ Features

- 🔌 Interface-agnostic: USB, UART, BLE, etc.
- 🧱 Framed packet format: Start + Length + Payload + CRC8 + End
- 🔒 Reliable transmission with ACK/NACK + retry
- 📦 Variable-length payloads (1B to 100KB)
- 🔁 Command/subcommand support
- 🧠 Event-driven: No timers or timeouts required

---

## 🧱 Framed Packet Format

| Field     | Size      | Description                          |
|-----------|-----------|--------------------------------------|
| `START`   | 1 byte    | Frame start marker (`0x7E`)           |
| `LEN`     | 1 byte    | Length of payload                    |
| `PAYLOAD` | 1–255 B   | Header, data packet, or ACK/NACK     |
| `CRC8`    | 1 byte    | CRC8 checksum (poly `0x07`)           |
| `END`     | 1 byte    | Frame end marker (`0x7F`)             |

> All TX and RX packets use this frame structure.

---

## 📁 File Structure

```
VennaProtocol/
├── VennaProtocolDefs.h           # Commands, enums, responses
├── VennaProtocolPacket.h         # Command header structure
├── VennaRingBuffer.*             # Optional for UART receive buffering
├── VennaTransportInterface.h     # Abstract transport interface
├── VennaCommandHandler.h         # Base command class
├── VennaCommandManager.*         # Command router
├── VennaCmdReceiveData.*         # Handles RX command (0x55)
├── VennaCmdTransmitData.*        # Handles TX command (0x56)
├── VennaFramingUtils.h           # Frame encoding/decoding with CRC8
└── UsbVennaTransport.*           # Example USB CDC implementation
```

---

## 🔁 Protocol Flow

### RX Command (0x55)
1. Host sends framed CMD header (subcommand + length)
2. Host sends data fragments (max 60B chunks), each framed
3. Device replies with framed ACK/NACK after each packet
4. Device sends framed `ACK_DONE` after all received

### TX Command (0x56)
1. Host sends `0x56` to request data
2. Device sends framed packets, waits for ACK/NACK
3. Retries on NACK

---

## 🧪 Example Host Frame (Python Concept)

```python
def frame(payload: bytes) -> bytes:
    start = b'\x7E'
    end = b'\x7F'
    length = len(payload).to_bytes(1, 'big')
    crc = crc8(payload).to_bytes(1, 'big')
    return start + length + payload + crc + end
```

---

## 🔧 Integration Steps

### main.cpp

```cpp
UsbVennaTransport usbTransport;
VennaCmdReceiveData vennaRx;
VennaCmdTransmitData vennaTx;
VennaCommandManager vennaManager;

int main() {
    ...
    vennaRx.setTransport(&usbTransport);
    vennaTx.setTransport(&usbTransport);
    vennaManager.addHandler(&vennaRx);
    vennaManager.addHandler(&vennaTx);
    while (1);
}
```

### usbd_cdc_if.c

```c
extern void venna_forward_data(const uint8_t* data, uint32_t len);

int8_t CDC_Receive_FS(uint8_t* Buf, uint32_t *Len) {
    venna_forward_data(Buf, *Len);
    USBD_CDC_ReceivePacket(&hUsbDeviceFS);
    return USBD_OK;
}
```

### main.cpp wrapper

```cpp
extern "C" void venna_forward_data(const uint8_t* data, uint32_t len) {
    vennaManager.handleData(data, len);
}
```

---

## 🔐 CRC-8 Checksum

This version uses **CRC-8 with polynomial `0x07`** and init `0x00`, consistent with standards like SMBus, CRC-8-ATM.

---

## ✨ Future Improvements (optional)

- [ ] Escape `0x7E` and `0x7F` inside payloads
- [ ] Add CRC16 for stronger integrity
- [ ] Add encryption or authentication
- [ ] Bootloader-safe variant
- [ ] Timeout + watchdog recovery

---

## 👤 Author

**Vijay Bharath Reddy Venna**  
🛠 Embedded & Protocol Architect  
📧 isquaresysindia@gmail.com
🌐 https://vennasolutions.com

---

## 🪪 License

MIT License — free for commercial and non-commercial use.