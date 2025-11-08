using System;

namespace IspProtocol
{
    public static class IspFramingUtils
    {
        public const byte StartByte = 0x7E;
        public const byte EndByte = 0x7F;

        public static byte[] EncodeFrame(byte[] payload)
        {
            var framed = new byte[payload.Length + 4];
            framed[0] = StartByte;
            framed[1] = (byte)payload.Length;
            Array.Copy(payload, 0, framed, 2, payload.Length);
            framed[2 + payload.Length] = ComputeCRC8(payload);
            framed[3 + payload.Length] = EndByte;
            return framed;
        }

        public static bool TryDecodeFrame(byte[] frame, out byte[] payload)
        {
            payload = null;

            if (frame.Length < 4 || frame[0] != StartByte || frame[frame.Length - 1] != EndByte)
                return false;

            var len = frame[1];

            if (len + 4 != frame.Length)
                return false;

            var data = new byte[len];
            Array.Copy(frame, 2, data, 0, len);
            var crc = frame[2 + len];

            if (crc != ComputeCRC8(data))
                return false;

            payload = data;
            return true;
        }

        static byte ComputeCRC8(byte[] data)
        {
            byte crc = 0x00;

            foreach (byte b in data)
            {
                crc ^= b;

                for (int i = 0; i < 8; i++)
                    crc = (byte)((crc & 0x80) != 0 ? (crc << 1) ^ 0x07 : (crc << 1));
            }

            return crc;
        }
    }
}