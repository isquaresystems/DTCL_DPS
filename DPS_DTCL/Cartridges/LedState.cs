using DTCL.Transport;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using IspProtocol;

namespace DTCL.Cartridges
{
    public class LedStateChangedEventArgs : EventArgs
    {
        public int CartNo { get; }
        public bool IsBusy { get; }

        public LedStateChangedEventArgs(int cartNo, bool isBusy)
        {
            CartNo = cartNo;
            IsBusy = isBusy;
        }
    }
    public static class LedState
    {
        public static int timeout = 50;

        public static event EventHandler<LedStateChangedEventArgs> LedStateChanged;
        public async static Task<bool> DTCLAppCtrlLed()
        {
            Log.Log.Info("Start DTCLAppCtrlLed");

            ushort len = 1;
            byte[] txData = { (byte)IspCommand.COMMAND_REQUEST, (byte)IspSubCommand.GUI_CTRL_LED, (byte)(len >> 8), (byte)(len & 0xFF), 0x01 };
            var data = await DataHandlerIsp.Instance.ExecuteCMD(txData, 8, timeout);

            Log.Log.Info("Start DTCLAppCtrlLed Done");

            if (data != null)
                return true;
            else
                return false;
        }

        public async static Task<bool> FirmwareCtrlLed()
        {
            Log.Log.Info("Start FirmwareCtrlLed");

            ushort len = 1;
            byte[] txData = { (byte)IspCommand.COMMAND_REQUEST, (byte)IspSubCommand.GUI_CTRL_LED, (byte)(len >> 8), (byte)(len & 0xFF), 0x00 };
            var data = await DataHandlerIsp.Instance.ExecuteCMD(txData, 8, timeout);

            Log.Log.Info("Start FirmwareCtrlLed Done");

            if (data != null)
                return true;
            else
                return false;
        }

        public async static Task GreenLedOn(int cartNo)
        {
            ushort len = 2;
            byte[] txData = { (byte)IspCommand.COMMAND_REQUEST, (byte)IspSubCommand.GREEN_LED, (byte)(len >> 8), (byte)(len & 0xFF), (byte)cartNo, 0x01 };
            var data = await DataHandlerIsp.Instance.ExecuteCMD(txData, 8, timeout);
        }

        public async static Task RedLedOn(int cartNo)
        {
            ushort len = 2;
            byte[] txData = { (byte)IspCommand.COMMAND_REQUEST, (byte)IspSubCommand.RED_LED, (byte)(len >> 8), (byte)(len & 0xFF), (byte)cartNo, 0x01 };
            var data = await DataHandlerIsp.Instance.ExecuteCMD(txData, 8, timeout);
        }

        public async static Task GreenLedOff(int cartNo)
        {
            ushort len = 2;
            byte[] txData = { (byte)IspCommand.COMMAND_REQUEST, (byte)IspSubCommand.GREEN_LED, (byte)(len >> 8), (byte)(len & 0xFF), (byte)cartNo, 0x00 };
            var data = await DataHandlerIsp.Instance.ExecuteCMD(txData, 8, timeout);
        }

        public async static Task RedLedOff(int cartNo)
        {
            ushort len = 2;
            byte[] txData = { (byte)IspCommand.COMMAND_REQUEST, (byte)IspSubCommand.RED_LED, (byte)(len >> 8), (byte)(len & 0xFF), (byte)cartNo, 0x00 };
            var data = await DataHandlerIsp.Instance.ExecuteCMD(txData, 8, timeout);
        }

        public async static Task<byte[]> GetVersionNumber()
        {
            ushort len = 0;
            byte[] txData = { (byte)IspCommand.COMMAND_REQUEST, (byte)IspSubCommand.FIRMWARE_VERSION, (byte)(len >> 8), (byte)(len & 0xFF) };
            var data = await DataHandlerIsp.Instance.ExecuteCMD(txData, 11, 500);
            return data;
        }

        public async static Task LedBusySate(int cartNo)
        {
            await RedLedOn(cartNo);
            await GreenLedOff(cartNo);

            // Fire event for busy state (red)
            LedStateChanged?.Invoke(null, new LedStateChangedEventArgs(cartNo, true));
        }

        public async static Task LedIdleSate(int cartNo)
        {
            await GreenLedOn(cartNo);
            await RedLedOff(cartNo);

            // Fire event for idle state (blue)
            LedStateChanged?.Invoke(null, new LedStateChangedEventArgs(cartNo, false));
        }

        public async static Task SlotLedBlink(byte cartNo)
        {
            ushort len = 2;
            byte[] txData = { (byte)IspCommand.COMMAND_REQUEST, (byte)IspSubCommand.SLOT_LED_BLINK, (byte)(len >> 8), (byte)(len & 0xFF), cartNo, 0x03 };
            var data = await DataHandlerIsp.Instance.ExecuteCMD(txData, 8, 8000);
        }

        public async static Task BlinkAllLed(byte itr)
        {
            ushort len = 1;
            byte[] txData = { (byte)IspCommand.COMMAND_REQUEST, (byte)IspSubCommand.BLINK_ALL_LED, (byte)(len >> 8), (byte)(len & 0xFF), itr };
            var data = await DataHandlerIsp.Instance.ExecuteCMD(txData, 7, timeout);
        }

        public static async Task<bool> LoopBackTest(byte cartNo)
        {
            ushort len = 1;
            byte[] txData = { (byte)IspCommand.COMMAND_REQUEST, (byte)IspSubCommand.LOOPBACK_TEST, (byte)(len >> 8), (byte)(len & 0xFF), 0x03 };
            var data = await DataHandlerIsp.Instance.ExecuteCMD(txData, 9, 8000);

            if (data != null && data[0] == 0)
                return true;
            else
                return false;
        }

        public static async Task<bool> LoopBackTestAll()
        {
            ushort len = 1;
            byte[] txData = { (byte)IspCommand.COMMAND_REQUEST, (byte)IspSubCommand.LOOPBACK_TEST, (byte)(len >> 8), (byte)(len & 0xFF), 0x03 };
            var data = await DataHandlerIsp.Instance.ExecuteCMD(txData, 9, 8000);

            if (data != null && data[0] == 0)
                return true;
            else
                return false;
        }
    }
}