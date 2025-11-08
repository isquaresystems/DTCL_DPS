using DTCL.Log;
using System;
using System.Threading.Tasks;
using System.Windows.Media;
using IspProtocol;

namespace DTCL.Cartridges
{
    public interface ICart : IIspSubCommandHandler
    {
        Task<int> WriteUploadFiles(string msgPath, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile, byte cartNo, IProgress<int> progress);
        Task<int> EraseCartFiles(IProgress<int> progress, byte cartNo, bool trueErase = false);

        Task<int> EraseCartPCFiles(IProgress<int> progress, byte cartNo, bool trueErase = false);
        Task<int> ReadDownloadFiles(string msgPath, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile, byte cartNo, IProgress<int> progress, bool checkHeaderInfo = true);
        Task<int> CopyCartFiles(string msgPath, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile, Func<string, string> displayUserStatus, byte masterCartNo, byte[] slaveCartNo, IProgress<int> progress);
        Task<int> CompareCartFiles(Func<string, string, CustomMessageBox.MessageBoxResult> handleUserConfirmation, Func<string, string> displayUserStatus, byte masterCartNo, byte[] slaveCartNo, IProgress<int> progress);
        Task<int> Format(IProgress<int> progress, byte cartNo);
        Task<PCResult> ExecutePC(bool withCart, CartType cartType, byte cartNo);

        event EventHandler<CommandEventArgs> CommandInProgress;
    }

    public class CommandEventArgs : EventArgs
    {
        public string commandName { get; }
        public Color commandColor { get; }

        public CommandEventArgs(string _commandName, Color _commandColor)
        {
            commandName = _commandName;
            commandColor = _commandColor;
        }
    }
}
