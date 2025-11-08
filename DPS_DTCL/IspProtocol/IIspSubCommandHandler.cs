using System;

namespace IspProtocol
{
    public interface IIspSubCommandHandler
    {
        long prepareForRx(byte[] data, byte subcmd, long length);
        uint processRxData(byte[] data, byte subcmd);
        byte[] prepareDataToTx(byte[] data, byte subcmd);
        byte[] FrameInternalPayload(byte cmd, byte subCmd, int totalSize, ushort[] parameters);

    }
}