using DTCL.JsonParser;
using DTCL.Log;
using DTCL.Messages;
using DTCL.Transport;
using IspProtocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace DTCL.Cartridges
{
    public class Darin3 : ICart
    {
        public IMessageInfoContainer uMessageContainerObj;
        public IMessageInfoContainer dMessageContainerObj;
        public JsonParser<UploadMessageInfoContainer> uMessageParserObj;
        public JsonParser<DownloadMessageInfoContainer> dMessageParserObj;

        public event EventHandler<CommandEventArgs> CommandInProgress;

        public static List<FileEntry> files;

        public string mPath = "";
        public IMessageInfo mMessageInfo;
        public Darin3()
        {
        }

        public int cartType { get; set; }

        public bool InitializeUploadMessages(string msgPath)
        {
            uMessageContainerObj = new UploadMessageInfoContainer();

            uMessageParserObj = new JsonParser<UploadMessageInfoContainer>();

            uMessageContainerObj = uMessageParserObj.Deserialize("D3\\D3UploadMessageDetails.json");

            foreach (var msg in uMessageContainerObj.MessageInfoList)
            {
                msg.Nob = 0;
                msg.HeaderFileSize = 0;
                msg.ActualFileSize = 0;
                msg.ActualFileNOB = 0;
                msg.ActualFileLastPageSize = 0;
                msg.ActualFilePageSize = 0;
                msg.ActualFileNoOfPages = 0;
                msg.ActualFileNoOfPagesLastBlock = 0;
                msg.isDefinedInHeader = false;
                msg.isFileValid = false;
                msg.isFileExists = false;
                msg.PreFixedNoOfBlocks = 0;
                msg.NoOfBlocks = 0;
            }

            var res = HeaderInfo.UpdateMessageInfoWithHeaderData(CartType.Darin3, msgPath, uMessageContainerObj);

            var totalSize = 0;

            foreach (var msg in uMessageContainerObj.MessageInfoList)
                totalSize = totalSize + msg.ActualFileSize;

            var serializedJson = uMessageParserObj.Serialize((UploadMessageInfoContainer)uMessageContainerObj);

            File.WriteAllText("D3\\D3UploadMessageDetails.json", serializedJson);

            Log.Log.Info("Upload Messages Initialized");

            return res;
        }

        public void InitializeDownloadMessages()
        {
            FileOperations.deleteAndCreateDir(HardwareInfo.Instance.D3DownloadTempFilePath);

            uMessageContainerObj = new UploadMessageInfoContainer();
            uMessageParserObj = new JsonParser<UploadMessageInfoContainer>();
            uMessageContainerObj = uMessageParserObj.Deserialize("D3\\D3UploadMessageDetails.json");

            dMessageContainerObj = new DownloadMessageInfoContainer();

            dMessageParserObj = new JsonParser<DownloadMessageInfoContainer>();

            dMessageContainerObj = dMessageParserObj.Deserialize("D3\\D3DownloadMessageDetails.json");

            Log.Log.Info("Download Messages Initialized");
        }

        public bool UpdateDownloadMessages(string destPath, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile)
        {
            var res = HeaderInfo.UpdateMessageInfoWithHeaderData(CartType.Darin3, HardwareInfo.Instance.D3DownloadTempFilePath, dMessageContainerObj);
            var headerPresent = true;

            if (!res)
            {
                var shouldContinue = handleInvalidFile("Header_Missing_Msg2", "");

                if (shouldContinue == CustomMessageBox.MessageBoxResult.No)
                {
                    Log.Log.Warning($"User chose to stop operation due to missing header file");
                    return false;
                }
                else
                {
                    headerPresent = false;
                    var fs = new FileStream(HardwareInfo.Instance.D3DownloadTempFilePath + "DR.bin", FileMode.OpenOrCreate, FileAccess.Write);
                    fs.Close();
                }
            }

            foreach (var msg in dMessageContainerObj.MessageInfoList)
            {
                if ((msg.isUploadFile) && headerPresent)
                {
                    if (msg.isDefinedInHeader)
                    {
                        if (msg.isFileValid)
                        {
                            FileOperations.Copy(HardwareInfo.Instance.D3DownloadTempFilePath + msg.FileName, destPath + msg.FileName);
                        }
                        else
                        {
                            if (msg.FileName.Contains("fpl") && msg.FileName.Any(char.IsDigit))
                            {
                                continue;
                            }

                            if (msg.isFileValid == false)
                            {
                                handleInvalidFile("Invalid_FileSize_Msg", msg.FileName);
                            }

                            var shouldContinue = handleInvalidFile("Header_Compliance_Msg", "");

                            if (shouldContinue == CustomMessageBox.MessageBoxResult.No)
                            {
                                Log.Log.Warning($"User chose to stop operation due to invalid file: {msg.FileName}");
                                return false;
                            }
                            else
                            {
                                FileOperations.Copy(HardwareInfo.Instance.D3DownloadTempFilePath + msg.FileName, destPath + msg.FileName);
                            }
                        }
                    }
                }
                else
                {
                    FileOperations.Copy(HardwareInfo.Instance.D3DownloadTempFilePath + msg.FileName, destPath + msg.FileName);
                }
            }

            if (FileOperations.IsFileExist(HardwareInfo.Instance.D3DownloadTempFilePath + @"DR.bin"))
            {
                FileOperations.Copy(HardwareInfo.Instance.D3DownloadTempFilePath + @"DR.bin", destPath + @"DR.bin");
            }

            var serializedJson = dMessageParserObj.Serialize((DownloadMessageInfoContainer)dMessageContainerObj);

            File.WriteAllText("D3\\D3DownloadMessageDetails.json", serializedJson);

            return true;
        }

        public async Task<int> WriteUploadFiles(string msgPath, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile, byte cartNo, IProgress<int> progress)
        {
            var res = await powerCycle();

            if (!res)
                return returnCodes.DTCL_BAD_BLOCK;

            var ret = InitializeUploadMessages(msgPath);

            if (!ret)
                return returnCodes.DTCL_MISSING_HEADER;

            Log.Log.Info("Starting erase operation");
   
            byte[] txData = { (byte)IspCommand.COMMAND_REQUEST, (byte)IspSubCommand.D3_ERASE, 0, 1, cartNo };
            var data = await DataHandlerIsp.Instance.ExecuteCMD(txData, (byte)IspSubCmdRespLen.D3_ERASE, 10000);

            if ((data == null) || (data[0] != 0))
            {
                Log.Log.Error("Erase Operation Failed");
                return -1;
            }

            var result = CopyValidFilesToTempFolder(msgPath, handleInvalidFile);

            if (result != returnCodes.DTCL_SUCCESS)
                return result;

            result = await PerformUploadOperation(HardwareInfo.Instance.D3UploadTempFilePath, cartNo, progress);

            if (result != returnCodes.DTCL_SUCCESS)
                return result;

            result = await ReadDownloadFiles(HardwareInfo.Instance.D3DownloadTempFilePath, handleInvalidFile, cartNo, progress, false);

            if (result != returnCodes.DTCL_SUCCESS)
                return result;

            Log.Log.Info($"Start Comparing");

            result = FileOperations.compareDir(HardwareInfo.Instance.D3UploadTempFilePath, HardwareInfo.Instance.D3DownloadTempFilePath);

            Log.Log.Info($"Comparing Done");

            return result;
        }

        public async Task<int> WriteUploadFiles_ForCopy(string msgPath, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile, byte cartNo, IProgress<int> progress, bool checkHeader = false)
        {
            var res = await powerCycle();

            if (!res)
                return returnCodes.DTCL_BAD_BLOCK;

            Log.Log.Info("Starting erase operation");
            byte[] txData = { (byte)IspCommand.COMMAND_REQUEST, (byte)IspSubCommand.D3_ERASE, 0, 1, cartNo };
            var data = await DataHandlerIsp.Instance.ExecuteCMD(txData, (byte)IspSubCmdRespLen.D3_ERASE, 1000);

            if ((data == null) || (data[0] != 0))
            {
                Log.Log.Error("Erase Operation Failed");
                return -1;
            }

            int result;

            result = await PerformUploadOperation_copy(msgPath, cartNo, progress);

            result = await PerformUploadOperation_DwnFiles(msgPath, cartNo, progress);

            if (result != returnCodes.DTCL_SUCCESS)
                return result;

            result = await ReadDownloadFiles(HardwareInfo.Instance.D3DownloadTempFilePath, handleInvalidFile, cartNo, progress, false);

            if (result != returnCodes.DTCL_SUCCESS)
                return result;

            Log.Log.Info($"Start Comparing");

            result = FileOperations.compareDir(msgPath, HardwareInfo.Instance.D3DownloadTempFilePath);
            
            Log.Log.Info($"Comparing Done");

            return result;
        }

        async Task<int> PerformUploadOperation(string msgPath, byte cartNo, IProgress<int> progress)
        {
            Log.Log.Info("Start Uploading files to Cartridge");

            var writeResult = 0;

            // Get all files in the temp folder
            var Files = Directory.GetFiles(msgPath);

            foreach (var filePath in Files)
            {
                var fileName = Path.GetFileName(filePath);

                Log.Log.Info($"Start Uploading File: {fileName}");

                var msg = uMessageContainerObj.MessageInfoList.FirstOrDefault(m => m.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));

                if (msg != null && msg.isDefinedInHeader)
                {
                    writeResult = await ExecuteWriteOperationAsync(msgPath, msg, cartNo, progress);

                    if (writeResult != 0)
                    {
                        Log.Log.Error($"Write operation result: {writeResult}");
                        Log.Log.Error("Uploading Error");
                        return writeResult;
                    }
                    else
                    {
                        Log.Log.Info($"Successfully Uploaded File: {fileName}");
                    }
                }
                else if (msg != null)
                {
                    Log.Log.Info($"writing 0kb File: {fileName}, not defined in Header");

                    writeResult = await ExecuteWriteOperationAsync(msgPath, msg, cartNo, progress);

                    if (writeResult != 0)
                    {
                        Log.Log.Error($"Write operation result: {writeResult}");
                        Log.Log.Error("Uploading Error");
                        return writeResult;
                    }
                    else
                    {
                        Log.Log.Info($"Successfully Uploaded 0kb File: {fileName}");
                    }
                }
            }

            Log.Log.Info("Uploading Done");
            return writeResult;
        }

        async Task<int> PerformUploadOperation_copy(string msgPath, byte cartNo, IProgress<int> progress)
        {
            Log.Log.Info("Start Uploading files to Cartridge");

            var writeResult = 0;

            // Get all files in the temp folder
            var Files = Directory.GetFiles(msgPath);

            foreach (var filePath in Files)
            {
                var fileName = Path.GetFileName(filePath);

                Log.Log.Info($"Start Uploading File: {fileName}");

                var msg = uMessageContainerObj.MessageInfoList.FirstOrDefault(m => m.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));

                if (msg != null)
                {
                    var fs = new FileStream(msgPath + fileName, FileMode.Open, FileAccess.Read);
                    var len = fs.Length;
                    fs.Close();

                    writeResult = await ExecuteWriteOperationAsync(msgPath, msg, cartNo, progress);

                    if (writeResult != 0)
                    {
                        Log.Log.Error($"Write operation result: {writeResult}");
                        Log.Log.Error("Uploading Error");
                        return writeResult;
                    }
                    else
                    {
                        Log.Log.Info($"Successfully Uploaded File: {fileName}");
                    }

                }
                else
                {
                    Log.Log.Info($"Skipping File: {fileName}, not defined in Header");
                }
            }

            Log.Log.Info("Uploading Done");
            return writeResult;
        }

        public async Task<int> PerformUploadOperation_DwnFiles(string msgPath, byte cartNo, IProgress<int> progress)
        {
            Log.Log.Info("Start Uploading download files to Cartridge");

            var writeResult = 0;

            // Get all files in the temp folder
            var Files = Directory.GetFiles(msgPath);

            foreach (var filePath in Files)
            {
                var fileName = Path.GetFileName(filePath);

                Log.Log.Info($"Start Uploading File: {fileName}");

                var msg = dMessageContainerObj.MessageInfoList.FirstOrDefault(m => m.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));

                if ((msg != null) && (msg.isUploadFile == false))
                {
                    var fs = new FileStream(msgPath + msg.FileName, FileMode.Open);
                    msg.ActualFileSize = (int)fs.Length;
                    fs.Close();
                    msg.ActualFileNOB = msg.ActualFileSize / 64;
                    msg.ActualFileLastPageSize = msg.ActualFileSize - (msg.ActualFileNOB * 64);

                    switch (msg.FileName.ToLower())
                    {
                        case "dr.bin":
                            msg.MsgID = 3;
                            break;
                        case "str.bin":
                            msg.MsgID = 4;
                            break;
                        case "wp.bin":
                            msg.MsgID = 5;
                            break;
                        case "fpl.bin":
                            msg.MsgID = 6;
                            break;
                        case "tht.bin":
                            msg.MsgID = 7;
                            break;

                        case "mission1.bin":
                            msg.MsgID = 20;
                            break;
                        case "mission2.bin":
                            msg.MsgID = 21;
                            break;
                        case "update.bin":
                            msg.MsgID = 22;
                            break;
                        case "usage.bin":
                            msg.MsgID = 23;
                            break;
                        case "lru.bin":
                            msg.MsgID = 24;
                            break;
                        case "dlspj.bin":
                            msg.MsgID = 25;
                            break;
                        case "dlrwr.bin":
                            msg.MsgID = 26;
                            break;
                        case "tgt123.bin":
                            msg.MsgID = 27;
                            break;
                        case "tgt456.bin":
                            msg.MsgID = 28;
                            break;
                        case "tgt78.bin":
                            msg.MsgID = 29;
                            break;
                        case "nav.bin":
                            msg.MsgID = 30;
                            break;
                        case "hptspt.bin":
                            msg.MsgID = 31;
                            break;
                        case "pc.bin":
                            msg.MsgID = 100;
                            break;
                        default:
                            msg.MsgID = -1; // Assign a default value if no match is found
                            break;
                    }

                    var retry = 2;

                    while (retry > 0)
                    {
                        var fs2 = new FileStream(msgPath + fileName, FileMode.Open, FileAccess.Read);
                        var len = fs2.Length;
                        fs2.Close();

                        // if (len != 0)
                        // {
                        writeResult = await ExecuteWriteOperationAsync(msgPath, msg, cartNo, progress);

                        if (writeResult != 0)
                        {
                            Log.Log.Error($"Write operation result: {writeResult}");
                            Log.Log.Error("Uploading Error");
                            return writeResult;
                        }
                        else
                        {
                            Log.Log.Info($"Successfully Uploaded File: {fileName}");
                        }
 
                        if (writeResult != 0)
                        {
                            Log.Log.Error($"Write operation result: {writeResult}");
                            Log.Log.Error("Uploading Error");
                            retry--;
                            Log.Log.Error("retry Uploading");

                            if (retry <= 0)
                                return writeResult;
                        }
                        else
                        {
                            Log.Log.Info($"Successfully Uploaded File: {fileName}"); retry = 0;
                        }
                    }
                }
                else
                {
                    Log.Log.Info($"Skipping File: {fileName}, not defined in Header");
                }
            }

            Log.Log.Info("Uploading Done");
            return writeResult;
        }

        int CopyValidFilesToTempFolder(string msgPath, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile)
        {
            Log.Log.Info("Starting Copy of valid files to temp folder");

            FileOperations.deleteAndCreateDir(HardwareInfo.Instance.D3UploadTempFilePath);

            foreach (var msg in uMessageContainerObj.MessageInfoList)
            {
                if (msg.isDefinedInHeader)
                {
                    var sourceFilePath = Path.Combine(msgPath, msg.FileName);
                    var destinationFilePath = Path.Combine(HardwareInfo.Instance.D3UploadTempFilePath, msg.FileName);

                    if ((msg.isFileValid) && (msg.isFileExists))
                    {
                        try
                        {
                            FileOperations.Copy(sourceFilePath, destinationFilePath);
                        }
                        catch (Exception ex)
                        {
                            Log.Log.Error($"Error copying file {msg.FileName}: {ex.Message}");
                        }
                    }
                    else
                    {
                        if (msg.FileName.Contains("fpl") && msg.FileName.Any(char.IsDigit))
                        {
                            continue;
                        }

                        CustomMessageBox.MessageBoxResult shouldContinue;

                        if (msg.isFileExists == false)
                        {
                            handleInvalidFile("Missing_File", msg.FileName);
                        }
                        else if (msg.isFileValid == false)
                        {
                            handleInvalidFile("Invalid_File_Msg", msg.FileName);
                        }

                        shouldContinue = handleInvalidFile("Header_Compliance_Msg", "");

                        if (shouldContinue == CustomMessageBox.MessageBoxResult.No)
                        {
                            Log.Log.Warning($"User chose to stop operation due to an error: {msg.FileName}");
                            return returnCodes.DTCL_CMD_ABORT;
                        }

                        if (msg.isFileExists)
                        {
                            FileOperations.Copy(sourceFilePath, destinationFilePath);
                        }
                        else
                        {
                            var fs = new FileStream(destinationFilePath, FileMode.OpenOrCreate, FileAccess.Write);
                            msg.isFileExists = true;
                            fs.Close();
                        }
                    }
                }
                else
                {
                    if (msg.FileName.Contains("fpl") && msg.FileName.Any(char.IsDigit))
                    {
                        continue;
                    }

                    var destinationFilePath = Path.Combine(HardwareInfo.Instance.D3UploadTempFilePath, msg.FileName);
                    var sourceFilePath = Path.Combine(msgPath, msg.FileName);
                    Log.Log.Debug($"creating 0kb {msg.FileName} as its not defined in header");
                    var fs = new FileStream(destinationFilePath, FileMode.OpenOrCreate, FileAccess.Write);
                    fs.Close();
                }
            }

            Log.Log.Info("Copy of valid files to temp folder done");
            return returnCodes.DTCL_SUCCESS;
        }

        public int CompareUploadFiles()
        {
            Log.Log.Info($"Start Comparing");

            var ret = FileOperations.compareDir(HardwareInfo.Instance.D3UploadTempFilePath, HardwareInfo.Instance.D3DownloadTempFilePath);

            Log.Log.Info($"Compare Result: {ret}");
            return ret;
        }

        public async Task<int> EraseCartFiles(IProgress<int> progress, byte cartNo, bool trueErase = false)
        {
            var res = await powerCycle();

            if (!res)
                return returnCodes.DTCL_BAD_BLOCK;

            byte[] txData = { (byte)IspCommand.COMMAND_REQUEST, (byte)IspSubCommand.D3_ERASE, 0, 1, cartNo };
            var data = await DataHandlerIsp.Instance.ExecuteCMD(txData, (byte)IspSubCmdRespLen.D3_ERASE, 20000);

            if ((data == null) || (data[0] != 0))
            {
                Log.Log.Error("Erase Operation Failed");
                return -1;
            }

            return 0;
        }

        public async Task<int> EraseCartPCFiles(IProgress<int> progress, byte cartNo, bool trueErase = false)
        {
            var res = await powerCycle();

            if (!res)
                return returnCodes.DTCL_BAD_BLOCK;

            return 0;
        }

        public async Task<int> ReadDownloadFiles(string msgPath, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile, byte cartNo, IProgress<int> progress, bool checkHeaderInfo = true)
        {
            if (files != null)
                files.Clear();

            var res = await powerCycle();

            if (!res)
                return returnCodes.DTCL_BAD_BLOCK;

            Log.Log.Info("Starting download operation");

            InitializeDownloadMessages();

            FileOperations.deleteAndCreateDir(HardwareInfo.Instance.D3DownloadFilePath);

            mPath = HardwareInfo.Instance.D3DownloadTempFilePath;

            try
            {
                var cmdPayload = FrameInternalPayload((byte)IspCommand.RX_DATA, (byte)IspSubCommand.D3_READ_FILES, 1024, new ushort[] { (byte)0, (byte)cartNo });

                Log.Log.Info($"[EVT4001] Initiating Read FileDetails from cart {cartNo}");

                var result = await DataHandlerIsp.Instance.Execute(cmdPayload, progress);

                if (result != IspSubCmdResponse.SUCESS)
                    return returnCodes.DTCL_BAD_BLOCK;

                if (files.Count == 0)
                {
                    Log.Log.Info("D3 Cartridge is Blank");
                    return returnCodes.DTCL_BLANK_CARTRIDGE;
                }

                foreach (var file in files)
                {
                    IMessageInfo msg;

                    if (file.FileName.ToLower() == "DR.BIN".ToLower())
                        msg = uMessageContainerObj.FindMessageByFileName(file.FileName);
                    else
                        msg = dMessageContainerObj.FindMessageByFileName(file.FileName);

                    switch (msg.FileName.ToLower())
                    {
                        case "dr.bin":
                            msg.MsgID = 3;
                            break;
                        case "str.bin":
                            msg.MsgID = 4;
                            break;
                        case "wp.bin":
                            msg.MsgID = 5;
                            break;
                        case "fpl.bin":
                            msg.MsgID = 6;
                            break;
                        case "tht.bin":
                            msg.MsgID = 7;
                            break;

                        case "spj.bin":
                            msg.MsgID = 8;
                            break;
                        case "rwr.bin":
                            msg.MsgID = 9;
                            break;

                        case "iffa_pri.bin":
                            msg.MsgID = 10;
                            break;
                        case "iffa_sec.bin":
                            msg.MsgID = 11;
                            break;
                        case "iffb_pri.bin":
                            msg.MsgID = 12;
                            break;
                        case "iffb_sec.bin":
                            msg.MsgID = 13;
                            break;
                        case "incomkey.bin":
                            msg.MsgID = 14;
                            break;
                        case "incomcry.bin":
                            msg.MsgID = 15;
                            break;
                        case "incommne.bin":
                            msg.MsgID = 16;
                            break;
                        case "mont2.bin":
                            msg.MsgID = 17;
                            break;


                        case "cmds.bin":
                            msg.MsgID = 18;
                            break;

                        case "mission1.bin":
                            msg.MsgID = 20;
                            break;
                        case "mission2.bin":
                            msg.MsgID = 21;
                            break;
                        case "update.bin":
                            msg.MsgID = 22;
                            break;
                        case "usage.bin":
                            msg.MsgID = 23;
                            break;
                        case "lru.bin":
                            msg.MsgID = 24;
                            break;
                        case "dlspj.bin":
                            msg.MsgID = 25;
                            break;
                        case "dlrwr.bin":
                            msg.MsgID = 26;
                            break;
                        case "tgt123.bin":
                            msg.MsgID = 27;
                            break;
                        case "tgt456.bin":
                            msg.MsgID = 28;
                            break;
                        case "tgt78.bin":
                            msg.MsgID = 29;
                            break;
                        case "nav.bin":
                            msg.MsgID = 30;
                            break;
                        case "hptspt.bin":
                            msg.MsgID = 31;
                            break;
                        case "pc.bin":
                            msg.MsgID = 100;
                            break;
                        default:
                            msg.MsgID = -1; // Assign a default value if no match is found
                            break;
                    }

                    var fileName = msg.FileName;
                    if (string.IsNullOrEmpty(fileName)) return returnCodes.DTCL_FILE_NOT_FOUND;

                    Log.Log.Info($"Start Downloading File: {fileName} Size: {file.FileSize}");

                    var res2 = await ProcessFileDownload(msg, (int)file.FileSize, cartNo, progress);

                    if (res2 != IspSubCmdResponse.SUCESS)
                    {
                        Log.Log.Error($"Error downloading file {fileName}: {res2}");
                        return returnCodes.DTCL_FILE_NOT_FOUND;
                    }
                    else
                        Log.Log.Info($"File: {fileName} Done");
                }

                if (checkHeaderInfo == true)
                {
                    if (UpdateDownloadMessages(msgPath, handleInvalidFile) == false)
                        return returnCodes.DTCL_CMD_ABORT;

                    CopyAllMessages(msgPath);
                }
                else
                {
                    CopyAllMessages(msgPath);
                }

                return returnCodes.DTCL_SUCCESS;
            }
            catch (Exception ex)
            {
                Log.Log.Error("Error during download file operation", ex);
                return returnCodes.DTCL_FILE_NOT_FOUND;
            }
        }

        public void CopyAllMessages(string destPath)
        {
            if (destPath.Equals(HardwareInfo.Instance.D3DownloadTempFilePath))
                return;

            InitializeUploadMessages(HardwareInfo.Instance.D3DownloadTempFilePath);

            foreach (var msg in dMessageContainerObj.MessageInfoList)
            {
                if (msg.FileName.Contains("fpl") && msg.FileName.Any(char.IsDigit))
                {
                    continue;
                }

                if (!File.Exists(HardwareInfo.Instance.D3DownloadTempFilePath + msg.FileName))
                {
                    var fs = new FileStream(HardwareInfo.Instance.D3DownloadTempFilePath + msg.FileName, FileMode.OpenOrCreate, FileAccess.Write);
                    fs.Close();
                }

                FileOperations.Copy(HardwareInfo.Instance.D3DownloadTempFilePath + msg.FileName, destPath + msg.FileName);

            }

            foreach (var msg in uMessageContainerObj.MessageInfoList)
            {
                FileOperations.Copy(HardwareInfo.Instance.D3DownloadTempFilePath + msg.FileName, destPath + msg.FileName);
            }
         }

        public void CopyUploadMessages(string destPath)
        {
            if (destPath.Equals(HardwareInfo.Instance.D3DownloadTempFilePath))
                return;

            InitializeUploadMessages(HardwareInfo.Instance.D3DownloadTempFilePath);

            foreach (var msg in uMessageContainerObj.MessageInfoList)
            {
                FileOperations.Copy(HardwareInfo.Instance.D3DownloadTempFilePath + msg.FileName, destPath + msg.FileName);
            }
        }

        public async Task<int> CopyCartFiles(string msgPath, Func<string, string, CustomMessageBox.MessageBoxResult> handleUserConfirmation, Func<string, string> displayUserStatus, byte masterSlot, byte[] slaveSlot, IProgress<int> progress)
        {
            Log.Log.Info("Starting copy operation");

            displayUserStatus("Copy_Inprogess_Msg");

            FileOperations.deleteAndCreateDir(msgPath);

            var result = await ReadDownloadFiles(msgPath, handleUserConfirmation, masterSlot, progress, true);

            if (result != returnCodes.DTCL_SUCCESS)
                return result;
            else
                CopyAllMessages(msgPath);

            if (HardwareInfo.Instance.BoardId != "DTCL")
                await LedState.LedIdleSate(masterSlot);

            displayUserStatus("Copy_Overwrite_Msg3");

            for (int itr = 0; itr < slaveSlot.Length; itr++)
            {
                if (slaveSlot[itr] == 0)
                    continue;

                if (HardwareInfo.Instance.BoardId == "DTCL")
                {
                    Log.Log.Info("waiting for cart change");
                    displayUserStatus("Copy_Overwrite_Msg");

                    var cts = new CancellationTokenSource();
                    var changed = await HardwareInfo.Instance.WaitForCartChangeAsync(slaveSlot[itr], cts.Token);

                    if (changed)
                    {
                        Log.Log.Info("Cart changed! Performing Copy action...");
                        // Handle cart change (e.g., stop operation or refresh data)
                    }
                    else
                    {
                        Log.Log.Error("Monitoring canceled or timed out.");
                    }

                    // Cancel monitoring after done
                    cts.Cancel();

                    var shouldContinue = handleUserConfirmation("Slave_CartDetected_Msg", "");

                    if (shouldContinue == CustomMessageBox.MessageBoxResult.Cancel)
                    {
                        Log.Log.Warning($"User chose to stop operation");
                        return returnCodes.DTCL_CMD_ABORT;
                    }
                }
                else
                {
                    await LedState.LedIdleSate(masterSlot);
                    await LedState.LedBusySate(slaveSlot[itr]);
                }

                displayUserStatus("Copy_Inprogess_slave_Msg");

                result = await WriteUploadFiles_ForCopy(msgPath, handleUserConfirmation, slaveSlot[itr], progress);

                Log.Log.Info($"copy operation done with {result}");

                if (HardwareInfo.Instance.BoardId != "DTCL")
                {
                    if (result == returnCodes.DTCL_SUCCESS)
                    {
                        var info = handleUserConfirmation("Copy_completed_Msg", " for Slot-" + slaveSlot[itr].ToString());
                    }
                    else
                    {
                        var info = handleUserConfirmation("Copy_Failed_Msg", " for Slot-" + slaveSlot[itr].ToString());
                        return returnCodes.DTCL_NO_RESPONSE;
                    }

                    await LedState.LedIdleSate(slaveSlot[itr]);
                }
            }

            return result;
        }

        (int[] fileSizes, int[] msgIds) ExtractFileSizesAndMsgIds(byte[] dataPacket)
        {
            if (dataPacket == null || dataPacket.Length < 1)
                throw new ArgumentException("Packet is null or empty", nameof(dataPacket));

            int noOfFiles = dataPacket[0];
            var expectedLength = 1 + noOfFiles * 5;

            if (dataPacket.Length < expectedLength)
            {
                throw new ArgumentException(
                   $"Packet too short: expected at least {expectedLength} bytes, got {dataPacket.Length}",
                   nameof(dataPacket));
            }

            var fileSizes = new int[noOfFiles];
            var msgIds = new int[noOfFiles];

            for (int i = 0; i < noOfFiles; i++)
            {
                var baseIndex = 1 + i * 5;
                msgIds[i] = dataPacket[baseIndex];

                fileSizes[i] = (dataPacket[baseIndex + 1] << 24)
                             | (dataPacket[baseIndex + 2] << 16)
                             | (dataPacket[baseIndex + 3] << 8)
                             | dataPacket[baseIndex + 4];
            }

            return (fileSizes, msgIds);
        }

        public class FileEntry
        {
            public string FileName { get; set; }
            public uint FileSize { get; set; }
        }

        /// <summary>
        /// Decode the file‐packet generated by FatFsWrapper::buildFilePacket().
        /// </summary>
        /// <param name="packet">
        /// [0] = file count (N)  
        /// then N×([1–14] name ASCII, zero‐padded; [15–18] size little‐endian)
        /// </param>
        public static List<FileEntry> DecodeFilePacket(byte[] packet)
        {
            var list = new List<FileEntry>();

            if (packet == null || packet.Length < 1)
                return list;

            var offset = 0;
            var fileCount = packet[offset++];

            // each record is 14 + 4 = 18 bytes
            for (int i = 0; i < fileCount; i++)
            {
                // protect against malformed packets
                if (offset + 18 > packet.Length)
                    break;

                // read 14-byte ASCII name, strip trailing NULs
                var name = Encoding.ASCII
                    .GetString(packet, offset, 14)
                    .TrimEnd('\0');

                offset += 14;

                // read 4-byte little-endian size
                var size = BitConverter.ToUInt32(packet, offset);
                offset += 4;

                list.Add(new FileEntry
                {
                    FileName = name,
                    FileSize = size
                });
            }

            return list;
        }

        // Retrieve file name from the cartridge
        // private async Task<string> GetFileNameFromCartridge()
        // {
        //     //byte[] dataPacket = await //DataHandler.Instance.GetResponse(13, "read", null);
        // if (dataPacket == null) return null;

        // return Encoding.ASCII.GetString(dataPacket).Split('\0')[0];
        // }

        // Process the file download and write the data to disk
        async Task<IspSubCmdResponse> ProcessFileDownload(IMessageInfo msg, int fileSize, byte cartNo, IProgress<int> progress)
        {
            FileOperations.createDir(HardwareInfo.Instance.D3DownloadTempFilePath);

            var FileName = Path.Combine(HardwareInfo.Instance.D3DownloadTempFilePath, msg.FileName);

            FileOperations.deleteFile(FileName);

            if (fileSize == 0)
            {
                var fs = new FileStream(FileName, FileMode.OpenOrCreate, FileAccess.Write);
                fs.Close();
                return IspSubCmdResponse.SUCESS;
            }

            msg.ActualFileSize = fileSize;

            mMessageInfo = msg;

            var cmdPayload = FrameInternalPayload((byte)IspCommand.RX_DATA, (byte)IspSubCommand.D3_READ, fileSize, new ushort[] { (byte)msg.MsgID, (byte)cartNo });

            Log.Log.Info($"[EVT4001] Initiating Read: File={msg.FileName}, Size={fileSize}");

            var res = await DataHandlerIsp.Instance.Execute(cmdPayload, progress);

            return res;
        }

        public async Task<int> Format(IProgress<int> progress, byte cartNo)
        {
            var res = await powerCycle();

            if (!res)
                return returnCodes.DTCL_BAD_BLOCK;

            Log.Log.Info("Start Format");

            var len = 1;
            byte[] txData = { (byte)IspCommand.COMMAND_REQUEST, (byte)IspSubCommand.D3_FORMAT, (byte)(len >> 8), (byte)(len & 0xFF), cartNo };
            var data = await DataHandlerIsp.Instance.ExecuteCMD(txData, (byte)IspSubCmdRespLen.D3_FORMAT, 20000);

            Log.Log.Info("Format Completed");

            if (data != null)
                return data[0];
            else
                return -1;
        }

        public async Task<bool> powerCycle()
        {
            Log.Log.Info("Power Cycle Start");
            var len = 0;
            byte[] txData = { (byte)IspCommand.COMMAND_REQUEST, (byte)IspSubCommand.D3_POWER_CYCLE, (byte)(len >> 8), (byte)(len & 0xFF) };
            var res = await DataHandlerIsp.Instance.ExecuteCMD(txData, (byte)IspSubCmdRespLen.D3_POWER_CYCLE, 20000);
            await Task.Delay(100);
            Log.Log.Info("Power Cycle Done");
            return true;
        }

        public async Task<int> CompareCartFiles(Func<string, string, CustomMessageBox.MessageBoxResult> handleUserConfirmation, Func<string, string> displayUserStatus, byte masterSlot, byte[] slaveSlot, IProgress<int> progress)
        {
            Log.Log.Info("Starting compare operation");

            FileOperations.deleteAndCreateDir(HardwareInfo.Instance.D3Compare1FilePath);
            FileOperations.deleteAndCreateDir(HardwareInfo.Instance.D3Compare2FilePath);

            displayUserStatus("Compare_Reading_Ist_cart_Msg");

            var result = await ReadDownloadFiles(HardwareInfo.Instance.D3Compare1FilePath, handleUserConfirmation, masterSlot, progress, false);

            if (HardwareInfo.Instance.BoardId != "DTCL")
                await LedState.LedIdleSate(masterSlot);

            if (result != returnCodes.DTCL_SUCCESS)
                return result;

            for (int itr = 0; itr < slaveSlot.Length; itr++)
            {
                if (slaveSlot[itr] == 0)
                    continue;

                if (HardwareInfo.Instance.BoardId == "DTCL")
                {
                    Log.Log.Info("waiting for cart change");
                    displayUserStatus("Compare_Waiting_cart_Msg");

                    var cts = new CancellationTokenSource();
                    var changed = await HardwareInfo.Instance.WaitForCartChangeAsync(slaveSlot[itr], cts.Token);

                    if (changed)
                    {
                        Log.Log.Info("Cart changed! Performing Compare action...");
                        // Handle cart change (e.g., stop operation or refresh data)
                    }
                    else
                    {
                        Log.Log.Error("Monitoring canceled or timed out.");
                    }

                    // Cancel monitoring after done
                    cts.Cancel();

                    var shouldContinue = handleUserConfirmation("Second_CartDetected_Msg", "");

                    if (shouldContinue == CustomMessageBox.MessageBoxResult.Cancel)
                    {
                        Log.Log.Warning($"User chose to stop operation");
                        return returnCodes.DTCL_CMD_ABORT;
                    }
                }
                else
                {
                    await LedState.LedIdleSate(masterSlot);
                    await LedState.LedBusySate(slaveSlot[itr]);
                }

                displayUserStatus("Compare_Inprogess_Msg");

                result = await ReadDownloadFiles(HardwareInfo.Instance.D3Compare2FilePath, handleUserConfirmation, slaveSlot[itr], progress, false);

                if (result != returnCodes.DTCL_SUCCESS)
                {
                    await LedState.LedIdleSate(slaveSlot[itr]);

                    if (result == returnCodes.DTCL_BLANK_CARTRIDGE)
                        return returnCodes.DTCL_BLANK_CARTRIDGE2;
                }

                Log.Log.Info($"Start Comparing");

                result = FileOperations.compareDir(HardwareInfo.Instance.D3Compare2FilePath, HardwareInfo.Instance.D3Compare1FilePath);

                if (HardwareInfo.Instance.BoardId != "DTCL")
                {
                    if (result == returnCodes.DTCL_SUCCESS)
                    {
                        var info = handleUserConfirmation("Compare_Completed_Msg", " for Slot-" + slaveSlot[itr].ToString());
                    }
                    else if (result == returnCodes.DTCL_BLANK_CARTRIDGE2)
                    {
                        handleUserConfirmation("SecondCart_Blank_Msg", " for Slot-" + slaveSlot[itr].ToString());
                    }
                    else
                    {
                        var info = handleUserConfirmation("Compare_Failed_Msg", " for Slot-" + slaveSlot[itr].ToString());
                    }

                    await LedState.LedIdleSate(slaveSlot[itr]);
                }
            }

            return result;
        }

        public async Task<int> ExecuteWriteOperationAsync(string uploadPath, IMessageInfo msg, byte cartNo, IProgress<int> progress)
        {
            try
            {
                mPath = uploadPath;
                mMessageInfo = msg;
                byte[] cmdPayload = null;

                if (msg != null)
                {
                    cmdPayload = FrameInternalPayload((byte)IspCommand.TX_DATA, (byte)IspSubCommand.D3_WRITE, (int)msg.ActualFileSize, new ushort[] { (byte)msg.MsgID, (byte)cartNo });
                }
                else
                {
                    cmdPayload = FrameInternalPayload((byte)IspCommand.TX_DATA, (byte)IspSubCommand.D3_WRITE, 0, new ushort[] { (byte)msg.MsgID, (byte)cartNo });
                }

                Log.Log
                    .Info($"[EVT4002] Initiating Write for cart:{cartNo}  ActualFileSize:{msg.ActualFileSize} MsgID:{msg.MsgID} cart:{cartNo}");

                var res = await DataHandlerIsp.Instance.Execute(cmdPayload, progress);

                Log.Log.Info($"Writing Done for cart:{cartNo} MsgID:{msg.MsgID} resp:{res}");

                return res == IspSubCmdResponse.SUCESS ? returnCodes.DTCL_SUCCESS : returnCodes.DTCL_NO_RESPONSE;
            }
            catch (Exception ex)
            {
                Log.Log.Error("Error executing write operation", ex);
                return returnCodes.DTCL_BAD_BLOCK;
            }
        }

        public byte[] FrameInternalPayload(byte cmd, byte subCmd, int totalSize, ushort[] parameters)
        {
            var len1 = (byte)(totalSize >> 24);
            var len2 = (byte)(totalSize >> 16);
            var len3 = (byte)(totalSize >> 8);
            var len4 = (byte)(totalSize & 0xFF);

            return new byte[]
            {
                cmd,
                subCmd,
                len1,
                len2,
                len3,
                len4,
                (byte)parameters[0],
                (byte)parameters[1]
            };
        }

        public byte[] prepareDataToTx(byte[] data, byte subCmd)
        {
            // Prepare data to be sent

            if (mMessageInfo.FileName == "RWR.BIN")
            {
                Log.Log
                    .Info($"[EVT4003] TX data prepared: SubCmd=0x{subCmd:X2}, TotalSize={mMessageInfo.ActualFileSize}, FileName={mMessageInfo.FileName}");
            }

            var txBuff = FileOperations.ReadFileData(mPath + mMessageInfo.FileName, 0, mMessageInfo.ActualFileSize);

            Log.Log
                .Info($"[EVT4003] TX data prepared: SubCmd=0x{subCmd:X2}, TotalSize={mMessageInfo.ActualFileSize}, FileName={mMessageInfo.FileName}");

            return txBuff;
        }

        public long prepareForRx(byte[] data, byte subCmd, long len)
        {
            Log.Log.Info($"[EVT4005] prepareForRx called: SubCmd=0x{subCmd:X2}, ExpectedLength={len}");
            return 0; // Default
        }

        public uint processRxData(byte[] data, byte subCmd)
        {
            if (subCmd == (byte)IspSubCommand.D3_READ_FILES)
            {
                files = DecodeFilePacket(data);
                Log.Log.Info($"Decoded {files.Count} files:");
            }
            else
            {
                Log.Log.Info($"[EVT4006] Processing RX data: SubCmd=0x{subCmd:X2}, TotalBytes={data?.Length ?? 0}");

                FileOperations.WriteFileData(data, mPath + mMessageInfo.FileName, 0);

                Log.Log
                    .Info($"[EVT4007] RX data processed and stored in file {mPath + mMessageInfo.FileName} of size:{data.Length}");
            }

            return 0;
        }

        public async Task<PCResult> ExecutePC(bool withCart, CartType cartType, byte cartNo)
        {
            FileOperations.createDir(HardwareInfo.Instance.D3UploadFilePath);

            await LedState.DTCLAppCtrlLed();

            if (!withCart)
            {
                return await doLoopBackTest(cartNo);
            }

            FileOperations.createDir(HardwareInfo.Instance.D3UploadFilePath);

            if (!FileOperations.IsFileExist(HardwareInfo.Instance.D3UploadFilePath + @"DR.bin"))
            {
                FileOperations.Copy("D3\\DR.bin", HardwareInfo.Instance.D3UploadFilePath + @"DR.bin");
            }

            var result = new PCResult();
            result.loopBackResult = "PASS";
            result.eraseResult = "PASS";
            result.writeResult = "PASS";
            result.readResult = "PASS";

            var ret = 0;

            Log.Log.Info("Starting LoopBack Test");
            OnCommandProgress("LoopBack", Colors.DodgerBlue);

            result.loopBackTestTime = $"{DateTime.Now:HH-mm-ss}";

            if (await LedState.LoopBackTest(cartNo) == false)
                result.loopBackResult = "FAIL";

            await LedState.LedBusySate(cartNo);

            InitializeUploadMessages(HardwareInfo.Instance.D3UploadFilePath);
            Log.Log.Info("Starting PC Erase operation");

            OnCommandProgress("Erase", Colors.DodgerBlue);

            result.eraseTestTime = $"{DateTime.Now:HH-mm-ss}";

            ret = await EraseCartFiles(null, cartNo);

            if (ret != 0)
                result.eraseResult = "FAIL";

            var msg = uMessageContainerObj.FindMessageByFileName("DR.bin");

            Log.Log.Info("Starting PC Write operation");

            OnCommandProgress("Write", Colors.DodgerBlue);

            result.writeTestTime = $"{DateTime.Now:HH-mm-ss}";

            mMessageInfo = msg;
            mPath = HardwareInfo.Instance.D3UploadFilePath;

            byte[] cmdPayload = null;
            cmdPayload = FrameInternalPayload((byte)IspCommand.TX_DATA, (byte)IspSubCommand.D3_WRITE, (int)msg.ActualFileSize, new ushort[] { (byte)msg.MsgID, (byte)cartNo });

            var res2 = await DataHandlerIsp.Instance.Execute(cmdPayload, null);

            Log.Log.Info($"PC Writing Done for cart:{cartNo} MsgID:{msg.MsgID} resp:{res2}");

            ret = (res2 == IspSubCmdResponse.SUCESS) ? returnCodes.DTCL_SUCCESS : returnCodes.DTCL_NO_RESPONSE;

            if (ret != returnCodes.DTCL_SUCCESS)
                result.writeResult = "FAIL";

            Log.Log.Info("Starting PC Read operation");

            OnCommandProgress("Read", Colors.DodgerBlue);

            result.readTestTime = $"{DateTime.Now:HH-mm-ss}";

            ret = await ReadDownloadFiles(HardwareInfo.Instance.D3DownloadTempFilePath, null, cartNo, null, false);

            if (ret != returnCodes.DTCL_SUCCESS)
                result.readResult = "FAIL";

            Log.Log.Info($"Start PC Compare");

            var compare = FileOperations.CompareFiles(System.IO.Path.Combine(HardwareInfo.Instance.D3UploadFilePath, msg.FileName), System.IO.Path.Combine(HardwareInfo.Instance.D3DownloadTempFilePath, msg.FileName));

            if (!compare)
            {
                result.readResult = "FAIL";
            }

            OnCommandProgress("", Colors.DodgerBlue);

            await LedState.LedIdleSate(cartNo);

            return result;
        }

        public async Task<PCResult> doLoopBackTest(byte cartNo)
        {
            var result = new PCResult();
            result.loopBackResult = "PASS";
            result.eraseResult = "PASS";
            result.writeResult = "PASS";
            result.readResult = "PASS";

            Log.Log.Info("Starting LoopBack Test");
            OnCommandProgress("LoopBack", Colors.DodgerBlue);

            result.loopBackTestTime = $"{DateTime.Now:HH-mm-ss}";

            if (await LedState.LoopBackTest(cartNo) == false)
                result.loopBackResult = "FAIL";

            return result;
        }

        public void OnCommandProgress(string name, Color color)
        {
            System.Windows.Application.Current.Dispatcher
                .Invoke(() =>
            {
                CommandInProgress?.Invoke(this, new CommandEventArgs(name, color));
            });
        }
    }
}