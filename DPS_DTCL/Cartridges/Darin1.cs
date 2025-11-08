using DTCL.Log;
using DTCL.Messages;
using DTCL.Transport;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DTCL.Cartridges
{
    internal class Darin1
    {
        public event EventHandler<CommandEventArgs> CommandInProgress;
        public bool InitializeMessages() => false;

        public async Task<int> WriteUploadFiles(string path, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile, IProgress<int> progress)
        {
            bool status;

            /*string uploadFileName = path + @"jmip.bin";

            DataHandler.Instance.ResetProgressValues();
            DataHandler.Instance.SetProgressValues(2048, 0);

            if (System.IO.File.Exists(uploadFileName) && (FileOperations.getFileSize(uploadFileName) == 2048))
            {
                int offset = 0;

                for (int current_page_no = 1; current_page_no <= 4; current_page_no++)
                {
                    byte[] txBuff = FileOperations.ReadFileData(uploadFileName, offset, 512);

                    byte[] cmd = { DTCLInfo.D1_COMMAND_WRITE, (byte)current_page_no, 0 };

                    Log.Log.Data($"Sending Cmd:", cmd);

                    DataHandler.Instance.SendData(cmd, 0, 3);

                    await Task.Delay(10);

                    status = await DataHandler.Instance.SendDataChunksWithOutAckAsync(txBuff, 512, 0, 0, progress);

                    byte[] res = await DataHandler.Instance.GetResponse(1, "D1 Write", null, true);

                    if ((res != null) && (res[0] != 0xFF))
                    {
                        return returnCodes.DTCL_FAILED_TO_COMMUNICATE;
                    }

                    offset = offset + 512;
                }

            }
            else
                return returnCodes.DTCL_FILE_NOT_FOUND;*/

            return returnCodes.DTCL_SUCCESS;
        }

        public async Task<int> EraseCartFiles(IProgress<int> progress, bool trueErase = false)
        {
            Log.Log.Info("start D1 Erase");
            /* DataHandler.Instance.OnProgressChanged("Erase", 0, 2, progress);

             byte[] cmd = { DTCLInfo.D1_COMMAND_ERASE, 0 };

             Log.Log.Data($"Sending Cmd:", cmd);

             DataHandler.Instance.SendData(cmd, 0, 2);

             DataHandler.Instance.OnProgressChanged("Erase", 1, 2, progress);

             await Task.Delay(2000);

             byte[] res = await DataHandler.Instance.GetResponse(1, "D1 Erase", null, true);

             DataHandler.Instance.OnProgressChanged("Erase", 2, 2, progress);

             if ((res != null) && (res[0] == 0xFF))
             {
                 int status = await ReadDownloadFiles(DTCLInfo.Instance.D1DownloadTempFilePath, null, progress);

                 if (status == returnCodes.DTCL_BLANK_CARTRIDGE)
                     return returnCodes.DTCL_SUCCESS;
             }
            */

            return returnCodes.DTCL_FAILED_TO_COMMUNICATE;
        }

        public async Task<int> EraseCartPCFiles(IProgress<int> progress, bool trueErase = false)
        {
            Log.Log.Info("start D1 pc files Erase");
            /* DataHandler.Instance.OnProgressChanged("Erase", 0, 2, progress);

             byte[] cmd = { DTCLInfo.D1_COMMAND_ERASE, 0 };

             Log.Log.Data($"Sending Cmd:", cmd);

             DataHandler.Instance.SendData(cmd, 0, 2);

             DataHandler.Instance.OnProgressChanged("Erase", 1, 2, progress);

             await Task.Delay(2000);

             byte[] res = await DataHandler.Instance.GetResponse(1, "D1 Erase", null, true);

             DataHandler.Instance.OnProgressChanged("Erase", 2, 2, progress);

             if ((res != null) && (res[0] == 0xFF))
             {
                 int status = await ReadDownloadFiles(DTCLInfo.Instance.D1DownloadTempFilePath, null, progress);

                 if (status == returnCodes.DTCL_BLANK_CARTRIDGE)
                     return returnCodes.DTCL_SUCCESS;
             }
            */

            return returnCodes.DTCL_FAILED_TO_COMMUNICATE;
        }

        public async Task<int> ReadDownloadFiles(string path, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile, IProgress<int> progress, bool checkHeaderInfo = true)
        {
            var offset = 0;
            var status = returnCodes.DTCL_SUCCESS;

            /*  FileOperations.deleteAndCreateDir(path);

              string downloadFileName = path + @"jmip.bin";

              DataHandler.Instance.OnProgressChanged("Erase", 0, 4, progress);

              for (int current_page_no = 1; current_page_no <= 4; current_page_no++)
              {
                  byte[] cmd = { DTCLInfo.D1_COMMAND_READ, (byte)current_page_no, 0 };

                  DataHandler.Instance.SendData(cmd, 0, 3);
                  await Task.Delay(10);

                  byte[] resp = await DataHandler.Instance.GetResponse(512, "D1 Read", null, false);

                  DataHandler.Instance.OnProgressChanged("Erase", current_page_no, 4, progress);

                  if ((resp != null) && (resp.Length == 512))
                  {
                      FileOperations.WriteFileData(resp, downloadFileName, offset);
                      offset += resp.Length;
                  }
                  else
                  {
                      DataHandler.Instance.OnProgressChanged("Erase", 4, 4, progress);
                      return returnCodes.DTCL_FAILED_TO_COMMUNICATE;
                  }

              }

              int status = returnCodes.DTCL_SUCCESS;
              var fs = new FileStream(downloadFileName, FileMode.Open, FileAccess.Read);
              for (int i = 0; i < fs.Length; i++)
              {
                  if (fs.ReadByte() != 0xFF)
                  {
                      fs.Close();
                      return returnCodes.DTCL_SUCCESS;
                  }
                  else
                  {
                      status = returnCodes.DTCL_BLANK_CARTRIDGE;
                  }
              }
              fs.Close();*/
            return status;
        }

        public async Task<int> CopyCartFiles(string path, Func<string, string, CustomMessageBox.MessageBoxResult> handleUserConfirmation, Func<string, string> displayUserStatus, IProgress<int> progress)
        {
            Log.Log.Info("Starting copy operation");
            var ret = -1;

            /*  displayUserStatus("Copy_Inprogess_Msg");

              DTCLInfo.Instance.isCartChanged = false;

              int ret = await ReadDownloadFiles(path, handleUserConfirmation, progress);

              if (ret != returnCodes.DTCL_SUCCESS)
                  return ret;

              displayUserStatus("Copy_Overwrite_Msg");

              bool res = false;
              Log.Log.Info("waiting for cart change");

              while (res == false)
              {
                  if (!DTCLInfo.IsDTCLConnected)
                  {
                      DTCLInfo.Instance.StartScanningPorts();
                  }
                  else
                  {
                      await DTCLInfo.Instance.EnsureScanningStopped();
                      res = await DTCLInfo.Instance.CheckCartChanged(DTCLInfo.CartType.Darin1);
                  }
                  await Task.Delay(10);
              }
              Log.Log.Info("cart change detected");


              CustomMessageBox.MessageBoxResult shouldContinue = handleUserConfirmation("Slave_CartDetected_Msg", "");
              if (shouldContinue == CustomMessageBox.MessageBoxResult.Cancel)
              {
                  Log.Log.Warning($"User chose to stop operation");
                  return returnCodes.DTCL_CMD_ABORT;
              }

              while (!DTCLInfo.IsDTCLConnected)
              {
                  DTCLInfo.Instance.StartScanningPorts();
              }
              {
                  await DTCLInfo.Instance.EnsureScanningStopped();
              }
              await Task.Delay(10);

              displayUserStatus("Copy_Inprogess_slave_Msg");

              if (ret != returnCodes.DTCL_SUCCESS)
                  return ret;

              ret = await WriteUploadFiles(path, handleUserConfirmation, progress);

              Log.Log.Info($"copy operation done with {ret}");*/

            return ret;
        }

        public async Task<int> Format(IProgress<int> progress) => -1;

        public async Task<int> CompareCartFiles(Func<string, string, CustomMessageBox.MessageBoxResult> handleUserConfirmation, Func<string, string> displayUserStatus, IProgress<int> progress)
        {
            Log.Log.Info("Starting compare operation");
            var ret = -1;

            /*  DTCLInfo.Instance.isCartChanged = false;

              displayUserStatus("Compare_Reading_Ist_cart_Msg");

              FileOperations.deleteAndCreateDir(DTCLInfo.Instance.D1Compare1FilePath);
              FileOperations.deleteAndCreateDir(DTCLInfo.Instance.D1Compare2FilePath);

              int ret = await ReadDownloadFiles(DTCLInfo.Instance.D1Compare1FilePath, handleUserConfirmation, progress);

              if (ret != returnCodes.DTCL_SUCCESS)
                  return ret;

              bool res = false;
              Log.Log.Info("waiting for cart change");
              displayUserStatus("Compare_Waiting_cart_Msg");
              while (res == false)
              {
                  if (!DTCLInfo.IsDTCLConnected)
                  {
                      DTCLInfo.Instance.StartScanningPorts();
                  }
                  else
                  {
                      await DTCLInfo.Instance.EnsureScanningStopped();
                      res = await DTCLInfo.Instance.CheckCartChanged(DTCLInfo.CartType.Darin1);
                  }
                  await Task.Delay(10);
              }
              Log.Log.Info("cart change detected");

              CustomMessageBox.MessageBoxResult shouldContinue = handleUserConfirmation("Second_CartDetected_Msg", "");
              if (shouldContinue == CustomMessageBox.MessageBoxResult.Cancel)
              {
                  Log.Log.Warning($"User chose to stop operation");
                  return returnCodes.DTCL_CMD_ABORT;
              }

              while (!DTCLInfo.IsDTCLConnected)
              {
                  DTCLInfo.Instance.StartScanningPorts();
              }
              {
                  await DTCLInfo.Instance.EnsureScanningStopped();
              }
              await Task.Delay(10);

              displayUserStatus("Compare_Inprogess_Msg");

              ret = await ReadDownloadFiles(DTCLInfo.Instance.D1Compare2FilePath, handleUserConfirmation, progress, false);

              if (ret != returnCodes.DTCL_SUCCESS)
              {
                  if (ret == returnCodes.DTCL_BLANK_CARTRIDGE)
                      return returnCodes.DTCL_BLANK_CARTRIDGE2;
              }

              Log.Log.Info($"Start Comparing");

              ret = FileOperations.compareDir(DTCLInfo.Instance.D1Compare2FilePath, DTCLInfo.Instance.D1Compare1FilePath);*/

            return ret;
        }

        public async Task<PCResult> ExecutePC(bool withCart, CartType cartType, byte cartNo) => null;
    }
}