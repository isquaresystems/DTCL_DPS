using DTCL.JsonParser;
using DTCL.Log;
using DTCL.Messages;
using DTCL.Transport;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using IspProtocol;

namespace DTCL.Cartridges
{
    public class PerformanceCheck
    {
        ICart obj;
        // Darin2 obj2;
        // Darin1 obj1;
        public event EventHandler<CommandEventArgs> CommandInProgress2;
        public PerformanceCheck()
        {
        }

        /*public async Task<PCResult> ExecutePC(bool withCart, SlotInfo.CartType cartType, byte cartNo, ICart obj)
        {
            PCResult result = null;
            this.obj = obj;
            FileOperations.createDir(DTCLInfo.Instance.D1UploadFilePath);
            FileOperations.createDir(DTCLInfo.Instance.D2UploadFilePath);
            FileOperations.createDir(DTCLInfo.Instance.D3UploadFilePath);

            await LedState.DTCLAppCtrlLed();

            if (!withCart)
            {
                return await doLoopBackTest(cartNo);
            }

            switch (cartType)
            {
                case SlotInfo.CartType.Darin1:

                    FileOperations.createDir(DTCLInfo.Instance.D1UploadFilePath);
                    if (!FileOperations.IsFileExist(DTCLInfo.Instance.D1UploadFilePath + @"jmip.bin"))
                    {
                        FileOperations.Copy("D1\\jmip.bin", DTCLInfo.Instance.D3UploadFilePath + @"jmip.bin");
                    }

                    //obj = new Darin1();
                    result = await doD1PerformanceCheck(DTCLInfo.Instance.D1UploadFilePath, cartNo);
                    break;
                case SlotInfo.CartType.Darin2:

                    FileOperations.createDir(DTCLInfo.Instance.D2UploadFilePath);
                    if (!FileOperations.IsFileExist(DTCLInfo.Instance.D2UploadFilePath + @"DR.bin"))
                    {
                        FileOperations.Copy("D2\\DR.bin", DTCLInfo.Instance.D2UploadFilePath + @"DR.bin");
                    }

                    //obj = new Darin2();
                    result = await doD2PerformanceCheck(DTCLInfo.Instance.D2UploadFilePath, cartNo);
                    break;
                case SlotInfo.CartType.Darin3:

                    FileOperations.createDir(DTCLInfo.Instance.D3UploadFilePath);
                    if (!FileOperations.IsFileExist(DTCLInfo.Instance.D3UploadFilePath + @"DR.bin"))
                    {
                        FileOperations.Copy("D3\\DR.bin", DTCLInfo.Instance.D3UploadFilePath + @"DR.bin");
                    }

                    obj = new Darin3();
                    result = await doPerformanceCheck(DTCLInfo.Instance.D3UploadFilePath, cartNo);
                    break;

            }
            return result;
        }*/

        public async Task<PCResult> doLoopBackTest(byte cartNo)
        {
            var result = new PCResult();
            result.loopBackResult = "PASS";
            result.eraseResult = "PASS";
            result.writeResult = "PASS";
            result.readResult = "PASS";

            Log.Log.Info("Starting LoopBack Test");
            OnCommandProgress("LoopBack", Colors.DodgerBlue);

            // Capture actual test start time
            result.loopBackTestTime = DateTime.Now.ToString();

            if (await LedState.LoopBackTest(cartNo) == false)
                result.loopBackResult = "FAIL";

            OnCommandProgress("LoopBack", Colors.DarkGray);
            return result;
        }

        public async Task<PCResult> doD1PerformanceCheck(string msgPath, byte cartNo)
        {
            var result = new PCResult();
            result.loopBackResult = "PASS";
            result.eraseResult = "PASS";
            result.writeResult = "PASS";
            result.readResult = "PASS";

            var ret = 0;

            Log.Log.Info("Starting LoopBack Test");
            OnCommandProgress("LoopBack", Colors.DodgerBlue);

            // Capture actual test start time for LoopBack
            result.loopBackTestTime = DateTime.Now.ToString(); ;

            if (await LedState.LoopBackTest(cartNo) == false)
                result.loopBackResult = "FAIL";

            await LedState.LedBusySate(cartNo);

            Log.Log.Info("Starting PC Erase operation");

            OnCommandProgress("Erase", Colors.DodgerBlue);

            // Capture actual test start time for Erase
            result.eraseTestTime = DateTime.Now.ToString();

            var res = await obj.EraseCartFiles(null, cartNo);

            if (ret != returnCodes.DTCL_SUCCESS)
                result.eraseResult = "FAIL";

            await Task.Delay(100);

            Log.Log.Info("Starting PC Write operation");

            OnCommandProgress("Write", Colors.DodgerBlue);

            // Capture actual test start time for Write
            result.writeTestTime = DateTime.Now.ToString(); ;

            ret = await obj.WriteUploadFiles(HardwareInfo.Instance.CartUploadFilePath, null, cartNo, null);

            if (ret != returnCodes.DTCL_SUCCESS)
                result.writeResult = "FAIL";

            Log.Log.Info("Starting PC Read operation");

            OnCommandProgress("Read", Colors.DodgerBlue);

            // Capture actual test start time for Read
            result.readTestTime = DateTime.Now.ToString();

            ret = await obj.ReadDownloadFiles(HardwareInfo.Instance.CartDownloadFilePath, null, cartNo, null);

            if (ret != returnCodes.DTCL_SUCCESS)
                result.readResult = "FAIL";

            Log.Log.Info($"Start PC Compare");

            var compare = FileOperations.CompareFiles(System.IO.Path.Combine(HardwareInfo.Instance.CartDownloadFilePath, @"jmip.bin"), System.IO.Path.Combine(HardwareInfo.Instance.CartUploadFilePath, @"jmip.bin"));

            if (!compare)
            {
                result.readResult = "FAIL";
            }

            if (!compare)
            {
                result.readResult = "FAIL";
            }

            await Task.Delay(100);

            OnCommandProgress("", Colors.DodgerBlue);

            await LedState.LedIdleSate(cartNo);

            return result;
        }

        public async Task<PCResult> doD2PerformanceCheck(string msgPath, byte cartNo)
        {
            var result = new PCResult();
            result.loopBackResult = "PASS";
            result.eraseResult = "PASS";
            result.writeResult = "PASS";
            result.readResult = "PASS";

            var uMessageContainerObj = new UploadMessageInfoContainer();
            var uMessageParserObj = new JsonParser<UploadMessageInfoContainer>();
            uMessageContainerObj = uMessageParserObj.Deserialize("D2\\D2UploadMessageDetails.json");

            var ret = 0;

            Log.Log.Info("Starting LoopBack Test");
            OnCommandProgress("LoopBack", Colors.DodgerBlue);

            // Capture actual test start time for LoopBack
            result.loopBackTestTime = DateTime.Now.ToString();

            if (await LedState.LoopBackTest(cartNo) == false)
                result.loopBackResult = "FAIL";

            await LedState.LedBusySate(cartNo);
            var obj2 = new Darin2();
            obj2.InitializeUploadMessages(msgPath);
            obj2.InitializeDownloadMessages();

            obj2.allocate_space();

            DataHandlerIsp.Instance.totalDataProcessed = 0;

            ret = await obj2.ReadHeaderSpaceDetails(cartNo);

            if ((ret == returnCodes.DTCL_SUCCESS) || (ret == returnCodes.DTCL_BLANK_CARTRIDGE))
            {
                ret = obj2.InitUpdMsgWithHeaderSpaceDetails(ret);
            }

            Log.Log.Info("Starting PC Erase operation");

            OnCommandProgress("Erase", Colors.DodgerBlue);

            // Capture actual test start time for Erase
            result.eraseTestTime = DateTime.Now.ToString();

            var uMessageInfo = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(3);

            ret = await obj.EraseCartFiles(null, cartNo);

            if (ret != 0)
                result.eraseResult = "FAIL";

            await Task.Delay(500);

            Log.Log.Info("Starting PC Write operation");

            OnCommandProgress("Write", Colors.DodgerBlue);

            // Capture actual test start time for Write
            result.writeTestTime = DateTime.Now.ToString(); ;

            ret = obj2.ReadBlockDataFromFile(HardwareInfo.Instance.CartUploadFilePath, uMessageInfo, 1, 1, 60);

            if (ret != returnCodes.DTCL_SUCCESS)
            {
                Log.Log.Error($"Error reading block data for MessageID-{3}, Block {1}. Aborting.");
            }

            ret = await obj2.WriteD2BlockData(uMessageInfo.fsb, 1, 60, cartNo, null);

            if (ret != returnCodes.DTCL_SUCCESS)
            {
                Log.Log.Error($"Error writing block data for MessageID-{3}, Block {1}. Aborting.");
            }

            if (ret != returnCodes.DTCL_SUCCESS)
                result.writeResult = "FAIL";

            await Task.Delay(500);

            Log.Log.Info("Starting PC Read operation");

            OnCommandProgress("Read", Colors.DodgerBlue);

            // Capture actual test start time for Read
            result.readTestTime = DateTime.Now.ToString(); ;

            ret = await obj2.ReadD2BlockData(uMessageInfo.fsb, 1, 60, cartNo, null);
            var pageData = DataBlock.GetPageData(1, 60);

            FileOperations.WriteFileData(pageData, HardwareInfo.Instance.CartDownloadTempFilePath + "DR.bin", 0);

            if (ret != returnCodes.DTCL_SUCCESS)
                result.readResult = "FAIL";

            Log.Log.Info($"Start PC Compare");

            var compare = FileOperations.CompareFiles(System.IO.Path.Combine(HardwareInfo.Instance.CartUploadFilePath, uMessageInfo.FileName), System.IO.Path.Combine(HardwareInfo.Instance.CartDownloadTempFilePath, uMessageInfo.FileName));

            if (!compare)
            {
                result.readResult = "FAIL";
            }

            if (!compare)
            {
                result.readResult = "FAIL";
            }

            await Task.Delay(500);

            OnCommandProgress("", Colors.DodgerBlue);

            await LedState.LedIdleSate(cartNo);

            return result;
        }

        public async Task<PCResult> doPerformanceCheck(string msgPath, byte cartNo)
        {
            var result = new PCResult();
            result.loopBackResult = "PASS";
            result.eraseResult = "PASS";
            result.writeResult = "PASS";
            result.readResult = "PASS";

            var ret = 0;

            Log.Log.Info("Starting LoopBack Test");
            OnCommandProgress("LoopBack", Colors.DodgerBlue);

            // Capture actual test start time for LoopBack
            result.loopBackTestTime = DateTime.Now.ToString(); ;

            if (await LedState.LoopBackTest(cartNo) == false)
                result.loopBackResult = "FAIL";

            await LedState.LedBusySate(cartNo);

            var darin3 = new Darin3();
            darin3.InitializeUploadMessages(msgPath);
            Log.Log.Info("Starting PC Erase operation");

            OnCommandProgress("Erase", Colors.DodgerBlue);

            // Capture actual test start time for Erase
            result.eraseTestTime = DateTime.Now.ToString(); ;

            ret = await obj.EraseCartFiles(null, cartNo);

            if (ret != 0)
                result.eraseResult = "FAIL";

            var msg = darin3.uMessageContainerObj.FindMessageByFileName("DR.bin");

            Log.Log.Info("Starting PC Write operation");

            OnCommandProgress("Write", Colors.DodgerBlue);

            // Capture actual test start time for Write
            result.writeTestTime = DateTime.Now.ToString(); ;

            darin3.mMessageInfo = msg;
            darin3.mPath = HardwareInfo.Instance.CartUploadFilePath;
            // ret = await DataHandler.Instance.ExecuteWriteOperationAsync(HardwareInfo.Instance.CartUploadFilePath, HardwareInfo.D3_COMMAND_WRITE, msg, null);
            // ret = await obj.ExecuteWriteOperationAsync(HardwareInfo.Instance.CartUploadFilePath, msg, cartNo, null);

            byte[] cmdPayload = null;
            cmdPayload = obj.FrameInternalPayload((byte)IspCommand.TX_DATA, (byte)IspSubCommand.D3_WRITE, (int)msg.ActualFileSize, new ushort[] { (byte)msg.MsgID, (byte)cartNo });

            var res2 = await DataHandlerIsp.Instance.Execute(cmdPayload, null);

            Log.Log.Info($"PC Writing Done for cart:{cartNo} MsgID:{msg.MsgID} resp:{res2}");

            ret = (res2 == IspSubCmdResponse.SUCESS) ? returnCodes.DTCL_SUCCESS : returnCodes.DTCL_NO_RESPONSE;

            if (ret != returnCodes.DTCL_SUCCESS)
                result.writeResult = "FAIL";

            Log.Log.Info("Starting PC Read operation");

            OnCommandProgress("Read", Colors.DodgerBlue);

            // Capture actual test start time for Read
            result.readTestTime = DateTime.Now.ToString(); ;

            ret = await obj.ReadDownloadFiles(HardwareInfo.Instance.CartDownloadTempFilePath, null, cartNo, null, false);

            if (ret != returnCodes.DTCL_SUCCESS)
                result.readResult = "FAIL";

            Log.Log.Info($"Start PC Compare");

            var compare = FileOperations.CompareFiles(System.IO.Path.Combine(HardwareInfo.Instance.CartUploadFilePath, msg.FileName), System.IO.Path.Combine(HardwareInfo.Instance.CartDownloadTempFilePath, msg.FileName));

            if (!compare)
            {
                result.readResult = "FAIL";
            }

            OnCommandProgress("", Colors.DodgerBlue);

            await LedState.LedIdleSate(cartNo);

            return result;
        }

        public void OnCommandProgress(string name, Color color)
        {
            Application.Current.Dispatcher
                .Invoke(() =>
            {
                CommandInProgress2?.Invoke(this, new CommandEventArgs(name, color));
            });
        }
    }

    public class CommandEventArgs2 : EventArgs
    {
        public string commandName { get; }
        public Color commandColor { get; }

        public CommandEventArgs2(string _commandName, Color _commandColor)
        {
            commandName = _commandName;
            commandColor = _commandColor;
        }
    }
}