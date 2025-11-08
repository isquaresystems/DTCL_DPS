namespace IspProtocol
{
    public interface IIspTransport
    {
        void Transmit(byte[] data);
    }
}