namespace IspProtocol
{
    public interface IIspCommandHandler
    {
        bool Match(byte cmd);
        void Execute(byte[] payload);
    }
}