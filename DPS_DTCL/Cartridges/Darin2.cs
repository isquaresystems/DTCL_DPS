using DTCL.JsonParser;
using DTCL.Log;
using DTCL.Messages;
using DTCL.Transport;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using IspProtocol;

namespace DTCL.Cartridges
{
    public class Darin2 : ICart
    {
        public UploadMessageInfoContainer uMessageContainerObj;
        public DownloadMessageInfoContainer dMessageContainerObj;
        public JsonParser<UploadMessageInfoContainer> uMessageParserObj;
        public JsonParser<DownloadMessageInfoContainer> dMessageParserObj;

        public event EventHandler<CommandEventArgs> CommandInProgress;

        public const int FORMAT_HEADER_BLOCK_NO = 0;
        public const int NO_OF_PAGES_IN_FORMAT_HEADER = 5;
        public const int HEADER_PAGE_NO = 2;
        public const int NO_OF_BYTES_IN_BLOCK = 512 * 32;

        public const uint NAV1_NOB = 200;
        public const uint NAV2_NOB = 100;
        public const uint NAV3_NOB = 100;
        public const uint UPDATE_NOB = 1;
        public const uint MISSION1_NOB = 10;
        public const uint MISSION2_NOB = 10;
        public const uint LRU_NOB = 50;
        public const uint USAGE_NOB = 1;
        public const uint SPJDL_NOB = 100;
        public const uint RWRDL_NOB = 100;
        public const uint SPJUL_NOB = 10;
        public const uint RWRUL_NOB = 10;
        public const uint FPLUL_NOB = 9;
        public const uint WP_NOB = 1;
        public const uint STR_NOB = 1;
        public const uint HEADER_NOB = 1;
        public const uint THT_NOB = 1;

        public bool uploadSpjFlag;
        public bool uploadRwrFlag;

        public uint m_NoOfPages = 0;
        public uint m_LastPageSize = 0;
        public Darin2()
        {
        }

        public bool InitializeUploadMessages(string msgPath)
        {
            uMessageContainerObj = new UploadMessageInfoContainer();

            uMessageParserObj = new JsonParser<UploadMessageInfoContainer>();

            var jsonFile = "D2\\D2UploadMessageDetails.json";

            // Check if the JSON file exists
            if (!File.Exists(@jsonFile))
            {
                Log.Log.Error($"JSON file not found at path: {jsonFile}");
                MessageBox.Show("D2\\D2UploadMessageDetails.json file is missing");
                return false;
            }

            // Check if the JSON file is empty
            if (new FileInfo(@jsonFile).Length == 0)
            {
                Log.Log.Error($"JSON file is empty at path: {jsonFile}");
                MessageBox.Show("D2\\D2UploadMessageDetails.json file length is zero");
                return false;
            }

            uMessageContainerObj = uMessageParserObj.Deserialize("D2\\D2UploadMessageDetails.json");

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
                msg.NoOfBlocks = 0;
            }

            Log.Log.Info("Upload Messages Initialized");

            var serializedJson = uMessageParserObj.Serialize((UploadMessageInfoContainer)uMessageContainerObj);

            File.WriteAllText("D2\\D2UploadMessageDetails.json", serializedJson);

            return true;
        }

        public bool InitializeUploadMessagesFrom_DR(string msgPath)
        {
            Log.Log.Info("Upload Messages Initialized From DR");

            var res = HeaderInfo.UpdateMessageInfoWithHeaderData(CartType.Darin2, msgPath, uMessageContainerObj);

            if (!res)
                return res;

            var totalSize = 0;

            foreach (var msg in uMessageContainerObj.MessageInfoList)
                totalSize = totalSize + msg.ActualFileSize;

            DataHandlerIsp.Instance.totalDataSize = totalSize;
            DataHandlerIsp.Instance.totalDataProcessed = 0;

            var serializedJson = uMessageParserObj.Serialize((UploadMessageInfoContainer)uMessageContainerObj);

            File.WriteAllText("D2\\D2UploadMessageDetails.json", serializedJson);
            return res;
        }

        public bool InitializeDownloadMessages()
        {
            FileOperations.deleteAndCreateDir(HardwareInfo.Instance.D2DownloadTempFilePath);

            dMessageContainerObj = new DownloadMessageInfoContainer();

            dMessageParserObj = new JsonParser<DownloadMessageInfoContainer>();

            var jsonFile = "D2\\D2DownloadMessageDetails.json";
            // Check if the JSON file exists
            if (!File.Exists(@jsonFile))
            {
                Log.Log.Error($"JSON file not found at path: {jsonFile}");
                MessageBox.Show("D2\\D2DownloadMessageDetails.json file is missing");
                return false;
            }

            // Check if the JSON file is empty
            if (new FileInfo(@jsonFile).Length == 0)
            {
                Log.Log.Error($"JSON file is empty at path: {jsonFile}");
                MessageBox.Show("D2\\D2DownloadMessageDetails.json file length is zero");
                return false;
            }

            dMessageContainerObj = dMessageParserObj.Deserialize("D2\\D2DownloadMessageDetails.json");

            Log.Log.Info("Download Messages Initialized");
            return true;
        }

        public async Task<int> ReadHeaderSpaceDetails(byte cartNo)
        {
            var ret = await ReadD2BlockData(FORMAT_HEADER_BLOCK_NO, 5, 512, cartNo, null);

            if (ret != returnCodes.DTCL_SUCCESS)
            {
                Log.Log.Error("Failed to Read Header Area Data.");
                return ret;
            }

            var fs = new FileStream(@"D2\D2_Header_Space.bin", FileMode.Create);

            for (int itr = 1; itr <= 5; itr++)
            {
                var pageData2 = DataBlock.GetPageData(itr, 512);

                fs.Write(pageData2, 0, 512);
            }

            fs.Close();

            var pageData = DataBlock.GetPageData(1, 512);

            // Logic to check if the cartridge is blank
            if (!(pageData[57] == 255 && pageData[58] == 255))
            {
                Log.Log.Info("Header Space is Not Blank.");
            }
            else
            {
                Log.Log.Error("Header Space is Blank.");
                return returnCodes.DTCL_BLANK_CARTRIDGE;
            }

            CartHeader.LoadHeaderData();

            return returnCodes.DTCL_SUCCESS;
        }

        public int InitDwnMsgWithHeaderSpaceDetails()
        {
            // Process download messages
            for (int msg = 2; msg <= 17; msg++)
            {
                if ((msg == 5) || (msg == 6))
                    continue;

                var row_no = 10 + msg;
                int no_of_words = CartHeader.cartridgeHeader[row_no, 3];

                var lsb = CartHeader.cartridgeHeader[row_no, 5];
                var msb = CartHeader.cartridgeHeader[row_no, 4];
                var no_of_blocks = (msb << 8) | lsb;

                var messageInfo = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(msg);

                messageInfo.HeaderFileSize = (int)(no_of_words * 2 * no_of_blocks);
                messageInfo.Nob = no_of_blocks;

                messageInfo.NoOfBlocks = messageInfo.HeaderFileSize / NO_OF_BYTES_IN_BLOCK;

                if ((messageInfo.HeaderFileSize % NO_OF_BYTES_IN_BLOCK) != 0)
                    messageInfo.NoOfBlocks = messageInfo.NoOfBlocks + 1;

                messageInfo.ActualFileLastPageSize = 512;

                if ((messageInfo.HeaderFileSize % 512) != 0)
                    messageInfo.ActualFileLastPageSize = messageInfo.HeaderFileSize % 512;

                messageInfo.ActualFileNoOfPages = messageInfo.HeaderFileSize / 512;

                if ((messageInfo.HeaderFileSize % 512) != 0)
                    messageInfo.ActualFileNoOfPages = messageInfo.ActualFileNoOfPages + 1;

                messageInfo.ActualFileNoOfPagesLastBlock = 32;

                if ((messageInfo.ActualFileNoOfPages % 32) != 0)
                    messageInfo.ActualFileNoOfPagesLastBlock = (int)(messageInfo.ActualFileNoOfPages % 32);

                lsb = CartHeader.cartridgeHeader[row_no, 7];
                msb = CartHeader.cartridgeHeader[row_no, 6];

                messageInfo.fsb = (msb << 8) | lsb;
            }

            var serializedJson = dMessageParserObj.Serialize(dMessageContainerObj);

            File.WriteAllText("D2\\D2DownloadMessageDetails.json", serializedJson);

            var totalSize = 0;

            foreach (var msg in uMessageContainerObj.MessageInfoList)
                totalSize = totalSize + msg.HeaderFileSize;

            DataHandlerIsp.Instance.SetProgressValues(totalSize + 15000, 0);

            return returnCodes.DTCL_SUCCESS;
        }

        public int InitUpdMsgWithHeaderSpaceDetails(int CartStatus, string path = "")
        {
            DataHandlerIsp.Instance.totalDataSize = 0;

            if (path == "")
                path = HardwareInfo.Instance.D2UploadFilePath;

            for (int msg = 3; msg <= 9; msg++)
            {
                var messageInfo = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(msg);

                Log.Log.Info($"InitUpdMsgWithHeaderSpaceDetails msgName: {messageInfo.FileName}");

                if (CartStatus != returnCodes.DTCL_BLANK_CARTRIDGE)
                {
                    var row_no = msg;
                    int no_of_words = CartHeader.cartridgeHeader[row_no, 3];

                    var lsb = CartHeader.cartridgeHeader[row_no, 5];
                    var msb = CartHeader.cartridgeHeader[row_no, 4];
                    var no_of_blocks = (msb << 8) | lsb;

                    messageInfo.HeaderFileSize = (int)(no_of_words * 2 * no_of_blocks);
                    messageInfo.Nob = no_of_blocks;

                    messageInfo.ActualFileNOB = messageInfo.HeaderFileSize / NO_OF_BYTES_IN_BLOCK;

                    if ((messageInfo.HeaderFileSize % NO_OF_BYTES_IN_BLOCK) != 0)
                        messageInfo.ActualFileNOB = messageInfo.ActualFileNOB + 1;

                    messageInfo.ActualFileLastPageSize = 512;

                    if ((messageInfo.HeaderFileSize % 512) != 0)
                        messageInfo.ActualFileLastPageSize = messageInfo.HeaderFileSize % 512;

                    messageInfo.ActualFileNoOfPages = messageInfo.HeaderFileSize / 512;

                    if ((messageInfo.HeaderFileSize % 512) != 0)
                        messageInfo.ActualFileNoOfPages = messageInfo.ActualFileNoOfPages + 1;

                    messageInfo.ActualFileNoOfPagesLastBlock = 32;

                    if ((messageInfo.ActualFileNoOfPages % 32) != 0)
                        messageInfo.ActualFileNoOfPagesLastBlock = (int)(messageInfo.ActualFileNoOfPages % 32);

                    lsb = CartHeader.cartridgeHeader[row_no, 7];
                    msb = CartHeader.cartridgeHeader[row_no, 6];

                    messageInfo.fsb = (msb << 8) | lsb;
                }

                else
                {
                    if (FileOperations.IsFileExist(path + messageInfo.FileName))
                    {
                        messageInfo.isFileExists = true;
                        messageInfo.ActualFileSize = FileOperations.getFileSize(path + messageInfo.FileName);
                    }

                    messageInfo.NoOfBlocks = messageInfo.ActualFileSize / NO_OF_BYTES_IN_BLOCK;

                    if ((messageInfo.ActualFileSize % NO_OF_BYTES_IN_BLOCK) != 0)
                        messageInfo.NoOfBlocks = messageInfo.NoOfBlocks + 1;

                    messageInfo.ActualFileLastPageSize = 512;

                    if ((messageInfo.ActualFileSize % 512) != 0)
                        messageInfo.ActualFileLastPageSize = messageInfo.ActualFileSize % 512;

                    messageInfo.ActualFileNoOfPages = messageInfo.ActualFileSize / 512;

                    if ((messageInfo.ActualFileSize % 512) != 0)
                        messageInfo.ActualFileNoOfPages = messageInfo.ActualFileNoOfPages + 1;

                    messageInfo.ActualFileNoOfPagesLastBlock = 32;

                    if ((messageInfo.ActualFileNoOfPages % 32) != 0)
                        messageInfo.ActualFileNoOfPagesLastBlock = (int)(messageInfo.ActualFileNoOfPages % 32);

                    messageInfo.ActualFileNOB = messageInfo.ActualFileSize / (messageInfo.NobSize);
                }
            }

            // Serialize the updated object to JSON
            var serializedJson = string.Empty;

            try
            {
                serializedJson = uMessageParserObj.Serialize(uMessageContainerObj);
            }
            catch (Exception ex)
            {
                Log.Log.Error($"Failed to serialize JSON: {ex.Message}");
                MessageBox.Show($"Failed to serialize JSON: {ex.Message}");
                return returnCodes.DTCL_WRONG_PARAMETERS;
            }

            if (string.IsNullOrWhiteSpace(serializedJson))
            {
                Log.Log.Error("Serialized JSON content is empty.");
                MessageBox.Show("Serialized JSON content is empty.");
                return returnCodes.DTCL_WRONG_PARAMETERS;
            }

            var jsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "D2\\D2UploadMessageDetails.json");

            try
            {
                // Ensure the directory exists
                var directoryPath = Path.GetDirectoryName(jsonFilePath);

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // Write JSON to file with proper stream handling
                using (FileStream fs = new FileStream(jsonFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(fs))
                {
                    writer.Write(serializedJson);
                    writer.Flush();
                }

                Log.Log.Info($"JSON file successfully written to: {jsonFilePath}");
            }
            catch (Exception ex)
            {
                Log.Log.Error($"Failed to write JSON file: {ex.Message}");
                MessageBox.Show($"Failed to write JSON file: {ex.Message}");
                return returnCodes.DTCL_WRONG_PARAMETERS;
            }

            return returnCodes.DTCL_SUCCESS;
        }

        public int InitDwnMsgForWritingToCart(string path)
        {
            DataHandlerIsp.Instance.totalDataSize = 0;

            for (int msg = 2; msg <= 17; msg++)
            {
                if ((msg == 5) || (msg == 6) || (msg == 14))
                    continue;

                var messageInfo = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(msg);

                if (FileOperations.IsFileExist(path + messageInfo.FileName))
                {
                    // messageInfo.isFileExists = true;
                    messageInfo.ActualFileSize = FileOperations.getFileSize(path + messageInfo.FileName);

                    messageInfo.NoOfBlocks = messageInfo.ActualFileSize / NO_OF_BYTES_IN_BLOCK;

                    if ((messageInfo.ActualFileSize % NO_OF_BYTES_IN_BLOCK) != 0)
                        messageInfo.NoOfBlocks = messageInfo.NoOfBlocks + 1;

                    messageInfo.ActualFileLastPageSize = 512;

                    if ((messageInfo.ActualFileSize % 512) != 0)
                        messageInfo.ActualFileLastPageSize = messageInfo.ActualFileSize % 512;

                    messageInfo.ActualFileNoOfPages = messageInfo.ActualFileSize / 512;

                    if ((messageInfo.ActualFileSize % 512) != 0)
                        messageInfo.ActualFileNoOfPages = messageInfo.ActualFileNoOfPages + 1;

                    messageInfo.ActualFileNoOfPagesLastBlock = 32;

                    if ((messageInfo.ActualFileNoOfPages % 32) != 0)
                        messageInfo.ActualFileNoOfPagesLastBlock = (int)(messageInfo.ActualFileNoOfPages % 32);

                    messageInfo.ActualFileNOB = (messageInfo.ActualFileSize / (messageInfo.NobSize));
                }
            }

            var serializedJson = dMessageParserObj.Serialize(dMessageContainerObj);

            File.WriteAllText("D2\\D2DownloadMessageDetails.json", serializedJson);

            return returnCodes.DTCL_SUCCESS;
        }

        public async Task WriteHeaderSpaceDetails(byte cartNo)
        {
            var temp_format_ds = new byte[32, 16];

            var filename1 = @"D2\sformat.bin";

            var fs1 = new FileStream(filename1, FileMode.OpenOrCreate);

            for (int i = 0; i <= 31; i++)
                for (int j = 0; j <= 15; j++)
                    temp_format_ds[i, j] = 255;

            var cartridge_header = new byte[32, 64];

            UploadMessageInfo messageInfo;
            DownloadMessageInfo dmessageInfo;

            // '''Number of words
            for (int msg = 3; msg <= 9; msg++)
            {
                messageInfo = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(msg);

                cartridge_header[msg, 3] = (byte)(messageInfo.NobSize / 2);
                cartridge_header[msg, 2] = 0;
            }

            // Msg start block number
            for (int msg = 3; msg <= 9; msg++)
            {
                messageInfo = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(msg);

                cartridge_header[msg, 7] = (byte)(((messageInfo.fsb) & 0xFF));      //'LSB
                cartridge_header[msg, 6] = (byte)(((messageInfo.fsb >> 8) & 0xFF)); //'MSB
                temp_format_ds[msg, 9] = (byte)(((messageInfo.fsb) & 0xFF));        //'LSB
                temp_format_ds[msg, 8] = (byte)(((messageInfo.fsb >> 8) & 0xFF));   //'MSB
            }

            // Number of blocks
            for (int msg = 3; msg <= 9; msg++)
            {
                messageInfo = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(msg);

                cartridge_header[msg, 5] = (byte)(((messageInfo.ActualFileNOB) & 0xFF));      //'LSB
                cartridge_header[msg, 4] = (byte)(((messageInfo.ActualFileNOB >> 8) & 0xFF)); //'MSB
            }

            // assign no of blocks
            for (int msg = 3; msg <= 9; msg++)
            {
                cartridge_header[msg, 11] = 0;  //'LSB
                cartridge_header[msg, 10] = 0;  //'MSB
                temp_format_ds[msg, 7] = 0;     //'LSB
                temp_format_ds[msg, 6] = 0;     //'MSB
            }

            for (int msg = 1; msg <= 17; msg++)
            {
                dmessageInfo = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(msg);

                if (dmessageInfo == null)
                    continue;

                cartridge_header[10 + msg, 3] = (byte)(dmessageInfo.NobSize / 2);
                cartridge_header[10 + msg, 2] = 0;
            }

            // Msg start block number
            for (int msg = 1; msg <= 17; msg++)
            {
                dmessageInfo = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(msg);

                if (dmessageInfo == null)
                    continue;

                cartridge_header[10 + msg, 7] = (byte)(((dmessageInfo.fsb) & 0xFF));        //'LSB
                cartridge_header[10 + msg, 6] = (byte)(((dmessageInfo.fsb >> 8) & 0xFF));   //'MSB
                temp_format_ds[10 + msg, 9] = (byte)(((dmessageInfo.fsb) & 0xFF));          //'LSB
                temp_format_ds[10 + msg, 8] = (byte)(((dmessageInfo.fsb >> 8) & 0xFF));     //'MSB
            }

            // Number of blocks
            for (int msg = 1; msg <= 17; msg++)
            {
                dmessageInfo = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(msg);

                if (dmessageInfo == null)
                    continue;

                cartridge_header[10 + msg, 5] = (byte)(((dmessageInfo.Nob) & 0xFF));      //'LSB
                cartridge_header[10 + msg, 4] = (byte)(((dmessageInfo.Nob >> 8) & 0xFF)); //'MSB
            }

            for (int i = 0; i <= 31; i++)
            {
                for (int j = 0; j <= 15; j++)

                    fs1.WriteByte(temp_format_ds[i, j]);
                // Put #2, , newcart_header(i, j);

                // fs1.Close();
            }

            for (int i = 0; i <= 31; i++)
            {
                for (int j = 0; j <= 63; j++)

                    fs1.WriteByte(cartridge_header[i, j]);
            }

            fs1.Close();

            fs1 = new FileStream(filename1, FileMode.Open);

            var last_pages_size = 512;

            for (int page = 1; page <= 5; page++)
            {
                var bytesToRead = (page == 5 && last_pages_size != 512) ? last_pages_size : 512;
                var pageData = new byte[bytesToRead];

                // Read the appropriate number of bytes from the stream into the buffer
                var bytesRead = fs1.Read(pageData, 0, bytesToRead);

                // Update DataBlock's page data
                DataBlock.UpdatePageData(page, pageData);
            }

            fs1.Close();

            await WriteD2BlockData(0, 5, 512, cartNo, null);
        }

        public void allocate_space()
        {
            var present_block_no = 1;

            var uMessageInfo3 = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(3);
            var uMessageInfo4 = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(4);
            var uMessageInfo5 = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(5);
            var uMessageInfo6 = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(6);
            var uMessageInfo7 = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(7);
            var uMessageInfo8 = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(8);
            var uMessageInfo9 = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(9);

            var dMessageInfo2 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(2);
            var dMessageInfo3 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(3);
            var dMessageInfo4 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(4);
            var dMessageInfo7 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(7);
            var dMessageInfo8 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(8);
            var dMessageInfo9 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(9);
            var dMessageInfo10 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(10);
            var dMessageInfo11 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(11);
            var dMessageInfo12 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(12);
            var dMessageInfo13 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(13);
            var dMessageInfo14 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(14);
            var dMessageInfo15 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(15);
            var dMessageInfo16 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(16);
            var dMessageInfo17 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(17);

            for (int msg = 2; msg <= 17; msg++)
            {
                if ((msg == 5) || (msg == 6))
                    continue;

                var dMessageInfo = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(msg);

                if (dMessageInfo != null)
                    dMessageInfo.NoOfBlocks = dMessageInfo.PreFixedNoOfBlocks;
            }

            for (int msg = 3; msg <= 9; msg++)
            {
                var uMessageInfo = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(msg);

                if (uMessageInfo != null)
                    uMessageInfo.NoOfBlocks = uMessageInfo.PreFixedNoOfBlocks;
            }

            dMessageInfo2.fsb = present_block_no;
            present_block_no = present_block_no + dMessageInfo2.NoOfBlocks;
            dMessageInfo3.fsb = present_block_no;
            present_block_no = present_block_no + dMessageInfo3.NoOfBlocks;
            dMessageInfo4.fsb = present_block_no;
            present_block_no = present_block_no + dMessageInfo4.NoOfBlocks;
            dMessageInfo16.fsb = present_block_no;
            present_block_no = present_block_no + dMessageInfo16.NoOfBlocks;
            dMessageInfo17.fsb = present_block_no;
            present_block_no = present_block_no + dMessageInfo17.NoOfBlocks;
            dMessageInfo10.fsb = present_block_no;
            present_block_no = present_block_no + dMessageInfo10.NoOfBlocks;
            dMessageInfo8.fsb = present_block_no;
            present_block_no = present_block_no + dMessageInfo8.NoOfBlocks;
            dMessageInfo9.fsb = present_block_no;
            present_block_no = present_block_no + dMessageInfo9.NoOfBlocks;
            dMessageInfo7.fsb = present_block_no;
            present_block_no = present_block_no + dMessageInfo7.NoOfBlocks;
            dMessageInfo11.fsb = present_block_no;
            present_block_no = present_block_no + dMessageInfo11.NoOfBlocks;

            uMessageInfo8.fsb = present_block_no;
            present_block_no = present_block_no + uMessageInfo8.NoOfBlocks;
            uMessageInfo9.fsb = present_block_no;
            present_block_no = present_block_no + uMessageInfo9.NoOfBlocks;
            uMessageInfo6.fsb = present_block_no;
            dMessageInfo14.fsb = present_block_no;
            present_block_no = present_block_no + uMessageInfo6.NoOfBlocks;
            uMessageInfo3.fsb = present_block_no;

            present_block_no = present_block_no + uMessageInfo3.NoOfBlocks;
            uMessageInfo4.fsb = present_block_no;
            dMessageInfo12.fsb = present_block_no;
            present_block_no = present_block_no + uMessageInfo4.NoOfBlocks;
            uMessageInfo5.fsb = present_block_no;
            dMessageInfo13.fsb = present_block_no;
            present_block_no = present_block_no + uMessageInfo5.NoOfBlocks;
            uMessageInfo7.fsb = present_block_no;
            dMessageInfo15.fsb = present_block_no;
            present_block_no = present_block_no + uMessageInfo7.NoOfBlocks;

            var serializedJson = uMessageParserObj.Serialize(uMessageContainerObj);

            File.WriteAllText("D2\\D2UploadMessageDetails.json", serializedJson);

            serializedJson = dMessageParserObj.Serialize(dMessageContainerObj);

            File.WriteAllText("D2\\D2DownloadMessageDetails.json", serializedJson);
        }

        public async Task<int> ReadDecodeHeader(byte cartNo, IProgress<int> progress)
        {
            Log.Log.Info("Reading and saving Header File");
            int ret;

            var dMessageInfo2 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(2);
            var dMessageInfo3 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(3);
            var dMessageInfo4 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(4);
            var dMessageInfo7 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(7);
            var dMessageInfo8 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(8);
            var dMessageInfo9 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(9);
            var dMessageInfo10 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(10);
            var dMessageInfo11 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(11);
            var dMessageInfo12 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(12);
            var dMessageInfo13 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(13);
            var dMessageInfo14 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(14);
            var dMessageInfo15 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(15);
            var dMessageInfo16 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(16);
            var dMessageInfo17 = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(17);

            ret = await ReadD2BlockData(uMessageContainerObj.FindMessageByMsgId(3).fsb, 1, 60, cartNo, progress);

            var pageData = DataBlock.GetPageData(1, 60);

            var hasNonFFByte = pageData.Any(b => b != 0xFF);

            if (!hasNonFFByte)
            {
                using (FileStream fs = new FileStream(HardwareInfo.Instance.D2DownloadTempFilePath + "DR.bin", FileMode.OpenOrCreate, FileAccess.Write))
                    fs.Close();

                return returnCodes.DTCL_MISSING_HEADER;
            }
            else
            {
                FileOperations.WriteFileData(pageData, HardwareInfo.Instance.D2DownloadTempFilePath + "DR.bin", 0);
                HeaderInfo.UpdateMessageInfoWithHeaderData(CartType.Darin2, HardwareInfo.Instance.D2DownloadTempFilePath, dMessageContainerObj);
                HeaderInfo.UpdateMessageInfoWithHeaderData(CartType.Darin2, HardwareInfo.Instance.D2DownloadTempFilePath, uMessageContainerObj);

                var serializedJson = uMessageParserObj.Serialize(uMessageContainerObj);

                File.WriteAllText("D2\\D2UploadMessageDetails.json", serializedJson);

                serializedJson = dMessageParserObj.Serialize(dMessageContainerObj);

                File.WriteAllText("D2\\D2DownloadMessageDetails.json", serializedJson);

                for (int fplNo = 1; fplNo <= 9; fplNo++)
                {
                    var filename = "fpl" + fplNo.ToString() + ".bin";
                    var uMessageInfo = (UploadMessageInfo)uMessageContainerObj.FindMessageByFileName(filename);

                    if (uMessageInfo.isFileValid)
                    {
                        var no_of_pages = ((uMessageInfo.Nob * 21 * 2) / 512);
                        int last_page_size = (short)((uMessageInfo.Nob * 21 * 2) % 512);

                        DataHandlerIsp.Instance.totalDataSize = DataHandlerIsp.Instance.totalDataSize + ((no_of_pages * 512) + last_page_size);
                        DataHandlerIsp.Instance.totalDataProcessed = 0;
                    }
                }

                return returnCodes.DTCL_SUCCESS;
            }
        }

        public async Task<int> WriteUploadFiles(string path, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile, byte cartNo, IProgress<int> progress)
        {
            var ret = await EraseCartFiles(progress, cartNo);

            if (ret != returnCodes.DTCL_SUCCESS)
                return ret;

            DataHandlerIsp.Instance.totalDataProcessed = 0;
            DataHandlerIsp.Instance.totalDataSize = 0;

            var res = InitializeUploadMessages(path);

            if (res == false)
                return returnCodes.DTCL_FILE_NOT_FOUND;

            res = InitializeUploadMessagesFrom_DR(path);

            if (res == false)
                return returnCodes.DTCL_MISSING_HEADER;

            res = InitializeDownloadMessages();

            if (res == false)
                return returnCodes.DTCL_FILE_NOT_FOUND;

            allocate_space(); // assign fsb

            // Read header and initialize messages

            ret = await ReadHeaderSpaceDetails(cartNo);

            if ((ret == returnCodes.DTCL_SUCCESS) || (ret == returnCodes.DTCL_BLANK_CARTRIDGE))
            {
                ret = InitUpdMsgWithHeaderSpaceDetails(ret);
            }

            if (ret != returnCodes.DTCL_SUCCESS)
            {
                return ret;
            }

            SplitFPLToIndividualFiles(path);

            ret = CopyValidFilesToTempFolder(path, handleInvalidFile);

            if (ret != returnCodes.DTCL_SUCCESS)
            {
                return ret;
            }

            for (int msg_number = 3; msg_number <= 9; msg_number++)
            {
                if (msg_number == 6)
                    continue;

                var uMessageInfo = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(msg_number);

                if (!FileOperations.IsFileExist(HardwareInfo.Instance.D2UploadTempFilePath + uMessageInfo.FileName))
                {
                    Log.Log.Info($"{uMessageInfo.FileName} does not Exist. Skipping.");
                    continue;
                }

                if (uMessageInfo == null || uMessageInfo.NoOfBlocks <= 0)
                {
                    Log.Log.Error($"No blocks found for MessageID-{msg_number}. Skipping.");
                    continue;
                }

                var present_block_no = uMessageInfo.fsb;

                Log.Log
                    .Info($"Writing MessageID-{msg_number} MessageName-{uMessageInfo.FileName}, Block Address: {present_block_no}, Actual size: {uMessageInfo.ActualFileSize} Total Blocks: {uMessageInfo.NoOfBlocks}");

                var blocks_written = 0;

                while (blocks_written < uMessageInfo.NoOfBlocks)
                {
                    var no_of_pages = (blocks_written + 1 == uMessageInfo.NoOfBlocks) ?
                                        uMessageInfo.ActualFileNoOfPagesLastBlock : 32;

                    var last_page_size = (blocks_written + 1 == uMessageInfo.NoOfBlocks) ?
                                          uMessageInfo.ActualFileLastPageSize : 512;

                    ret = ReadBlockDataFromFile(HardwareInfo.Instance.D2UploadTempFilePath, uMessageInfo, (blocks_written + 1), no_of_pages, last_page_size);

                    if (ret != returnCodes.DTCL_SUCCESS)
                    {
                        Log.Log.Error($"Error reading block data for MessageID-{msg_number}, Block {blocks_written}. Aborting.");
                        return ret;
                    }

                    ret = await WriteD2BlockData(present_block_no, no_of_pages, last_page_size, cartNo, progress);

                    if (ret != returnCodes.DTCL_SUCCESS)
                    {
                        Log.Log.Error($"Error writing block data for MessageID-{msg_number}, Block {blocks_written}. Aborting.");
                        return ret;
                    }

                    present_block_no++;
                    blocks_written++;
                }
            }

            await HandleFPLData(HardwareInfo.Instance.D2UploadTempFilePath, cartNo, progress);

            await WriteHeaderSpaceDetails(cartNo);

            ret = await ReadDownloadFiles(HardwareInfo.Instance.D2DownloadTempFilePath, handleInvalidFile, cartNo, progress, false);

            if (ret != returnCodes.DTCL_SUCCESS)
                return ret;

            Log.Log.Info($"Start Comparing");

            ret = FileOperations.compareD2Dir_2(HardwareInfo.Instance.D2UploadTempFilePath, HardwareInfo.Instance.D2DownloadTempFilePath, uMessageContainerObj);

            return ret;
        }

        public bool checkDownloadSPJRWRFileSize(string path, int msgID)
        {
            var dMessageInfo = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(msgID);

            if (!FileOperations.IsFileExist(path + dMessageInfo.FileName))
            {
                Log.Log.Error($"{dMessageInfo.FileName} does not Exist.");
                return false;
            }

            var fs1 = new FileStream(path + dMessageInfo.FileName, FileMode.Open, FileAccess.Read);
            var length = fs1.Length;
            fs1.Close();

            if (length != 0)
            {
                Log.Log.Info($"{dMessageInfo.FileName} size is {length}, hence skipping reading upload file");
                return true;
            }
            else
            {
                Log.Log.Info($"{dMessageInfo.FileName} size is {length}, hence reading upload file");
                return false;
            }
        }

        public async Task<int> WriteUploadFilesForCopy(string path, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile, byte cartNo, IProgress<int> progress)
        {
            // await EraseBlockNo(0, cartNo);
            var ret = await EraseCartFiles(progress, cartNo);

            if (ret != returnCodes.DTCL_SUCCESS)
                return ret;

            DataHandlerIsp.Instance.totalDataProcessed = 0;
            DataHandlerIsp.Instance.totalDataSize = 0;
            var checkHeader = false;

            var res = InitializeUploadMessages(path);

            if (res == false)
                return returnCodes.DTCL_FILE_NOT_FOUND;

            res = InitializeUploadMessagesFrom_DR(path);

            //if (res == false)
            //    return returnCodes.DTCL_MISSING_HEADER;

            res = InitializeDownloadMessages();

            if (res == false)
                return returnCodes.DTCL_FILE_NOT_FOUND;

            allocate_space(); // assign fsb

            // Read header and initialize messages

            ret = await ReadHeaderSpaceDetails(cartNo);

            if ((ret == returnCodes.DTCL_SUCCESS) || (ret == returnCodes.DTCL_BLANK_CARTRIDGE))
            {
                ret = InitUpdMsgWithHeaderSpaceDetails(ret, path);
            }

            //InitializeUploadMessagesFrom_DR(path);

            if (ret != returnCodes.DTCL_SUCCESS)
            {
                return ret;
            }

            SplitFPLToIndividualFiles(path);

            var msg = uMessageContainerObj.FindMessageByMsgId(8);
            msg.isFileValid = false;
            msg = uMessageContainerObj.FindMessageByMsgId(9);
            msg.isFileValid = false;

            ret = CopyValidFilesToTempFolder_ToBeDeleted(path, handleInvalidFile, checkHeader);

            if (ret != returnCodes.DTCL_SUCCESS)
            {
                return ret;
            }

            for (int msg_number = 3; msg_number <= 9; msg_number++)
            {
                if (msg_number == 6)
                    continue;

                var uMessageInfo = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(msg_number);

                if (!FileOperations.IsFileExist(HardwareInfo.Instance.D2UploadTempFilePath + uMessageInfo.FileName))
                {
                    Log.Log.Info($"{uMessageInfo.FileName} does not Exist. Skipping.");
                    continue;
                }

                if (uMessageInfo == null || uMessageInfo.NoOfBlocks <= 0)
                {
                    Log.Log.Error($"No blocks found for MessageID-{msg_number}. Skipping.");
                    continue;
                }

                var present_block_no = uMessageInfo.fsb;

                Log.Log
                    .Info($"Writing MessageID-{msg_number} MessageName-{uMessageInfo.FileName}, Block Address: {present_block_no}, Actual size: {uMessageInfo.ActualFileSize} Total Blocks: {uMessageInfo.NoOfBlocks}");

                var blocks_written = 0;

                while (blocks_written < uMessageInfo.NoOfBlocks)
                {
                    var no_of_pages = (blocks_written + 1 == uMessageInfo.NoOfBlocks) ?
                                        uMessageInfo.ActualFileNoOfPagesLastBlock : 32;

                    var last_page_size = (blocks_written + 1 == uMessageInfo.NoOfBlocks) ?
                                          uMessageInfo.ActualFileLastPageSize : 512;

                    ret = ReadBlockDataFromFile(HardwareInfo.Instance.D2UploadTempFilePath, uMessageInfo, (blocks_written + 1), no_of_pages, last_page_size);

                    if (ret != returnCodes.DTCL_SUCCESS)
                    {
                        Log.Log.Error($"Error reading block data for MessageID-{msg_number}, Block {blocks_written}. Aborting.");
                        return ret;
                    }

                    ret = await WriteD2BlockData(present_block_no, no_of_pages, last_page_size, cartNo, progress);

                    if (ret != returnCodes.DTCL_SUCCESS)
                    {
                        Log.Log.Error($"Error writing block data for MessageID-{msg_number}, Block {blocks_written}. Aborting.");
                        return ret;
                    }

                    present_block_no++;
                    blocks_written++;
                }
            }

            await HandleFPLData(HardwareInfo.Instance.D2UploadTempFilePath, cartNo, progress);

            await WriteHeaderSpaceDetails(cartNo);

            ret = await WriteDownloadFilesForCopy(path, cartNo, progress);

            ret = await ReadDownloadFiles(HardwareInfo.Instance.D2DownloadTempFilePath, handleInvalidFile, cartNo, progress, false);

            if (ret != returnCodes.DTCL_SUCCESS)
                return ret;

            Log.Log.Info($"Start Comparing");

            ret = FileOperations.compareD2Dir(HardwareInfo.Instance.D2UploadTempFilePath, HardwareInfo.Instance.D2DownloadTempFilePath, dMessageContainerObj);

            return ret;
        }

        public async Task<int> WriteDownloadFilesForCopy(string path, byte cartNo, IProgress<int> progress)
        {
            var ret = InitDwnMsgForWritingToCart(path);

            Log.Log.Info("start writing downloading files to slave cart");

            var size = FileOperations.getDirectorySize(path);

            DataHandlerIsp.Instance.ResetProgressValues();
            DataHandlerIsp.Instance.SetProgressValues(size, 0);

            for (int msg_number = 2; msg_number <= 17; msg_number++)
            {
                if ((msg_number == 5) || (msg_number == 6) || (msg_number == 14))
                    continue;

                var dMessageInfo = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(msg_number);

                if (dMessageInfo == null || dMessageInfo.NoOfBlocks <= 0)
                {
                    Log.Log.Error($"No blocks found for MessageID-{msg_number}. Skipping.");
                    continue;
                }

                var present_block_no = dMessageInfo.fsb;

                Log.Log
                    .Info($"Writing MessageID-{msg_number} MessageName-{dMessageInfo.FileName}, Block Address: {present_block_no}, Actual size: {dMessageInfo.ActualFileSize} Total Blocks: {dMessageInfo.NoOfBlocks}");

                var blocks_written = 0;

                while (blocks_written < dMessageInfo.NoOfBlocks)
                {
                    var no_of_pages = (blocks_written + 1 == dMessageInfo.NoOfBlocks) ?
                                        dMessageInfo.ActualFileNoOfPagesLastBlock : 32;

                    var last_page_size = (blocks_written + 1 == dMessageInfo.NoOfBlocks) ?
                                          dMessageInfo.ActualFileLastPageSize : 512;

                    ret = ReadBlockDataFromFile(path, dMessageInfo, (blocks_written + 1), no_of_pages, last_page_size);

                    if (ret == returnCodes.DTCL_FILE_NOT_FOUND)
                    {
                        Log.Log.Error($"Error reading block data for MessageID-{msg_number}, file missing.");
                        break;
                    }

                    else if (ret != returnCodes.DTCL_SUCCESS)
                    {
                        Log.Log.Error($"Error reading block data for MessageID-{msg_number}, Block {blocks_written}. Aborting.");
                        // MessageBox.Show($"Error reading block data for MessageID-{msg_number}, Block {blocks_written}. Aborting.");
                        return ret;
                    }

                    ret = await WriteD2BlockData(present_block_no, no_of_pages, last_page_size, cartNo, progress);

                    if (ret != returnCodes.DTCL_SUCCESS)
                    {
                        Log.Log.Error($"Error writing block data for MessageID-{msg_number}, Block {blocks_written}. Aborting.");
                        // MessageBox.Show($"Error writing block data for MessageID-{msg_number}, Block {blocks_written}. Aborting.");
                        return ret;
                    }

                    present_block_no++;
                    blocks_written++;
                }
            }

            return returnCodes.DTCL_SUCCESS;
        }

        public async Task HandleFPLData(string path, byte cartNo, IProgress<int> progress)
        {
            var FplMessageInfo = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(6);

            var startBlock = FplMessageInfo.fsb;

            for (int msg = 61; msg <= 69; msg++)
            {
                var messageInfo = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(msg);

                if (FileOperations.IsFileExist(path + messageInfo.FileName))
                {
                    messageInfo.isFileExists = true;
                    messageInfo.ActualFileSize = FileOperations.getFileSize(path + messageInfo.FileName);
                }

                messageInfo.NoOfBlocks = messageInfo.ActualFileSize / NO_OF_BYTES_IN_BLOCK;

                if ((messageInfo.ActualFileSize % NO_OF_BYTES_IN_BLOCK) != 0)
                    messageInfo.NoOfBlocks = messageInfo.NoOfBlocks + 1;

                messageInfo.ActualFileLastPageSize = 512;

                if ((messageInfo.ActualFileSize % 512) != 0)
                    messageInfo.ActualFileLastPageSize = messageInfo.ActualFileSize % 512;

                messageInfo.ActualFileNoOfPages = messageInfo.ActualFileSize / 512;

                if ((messageInfo.ActualFileSize % 512) != 0)
                    messageInfo.ActualFileNoOfPages = messageInfo.ActualFileNoOfPages + 1;

                messageInfo.ActualFileNoOfPagesLastBlock = 32;

                if ((messageInfo.ActualFileNoOfPages % 32) != 0)
                    messageInfo.ActualFileNoOfPagesLastBlock = (int)(messageInfo.ActualFileNoOfPages % 32);

                messageInfo.ActualFileNOB = messageInfo.ActualFileSize / (messageInfo.NobSize);

                var blocks_written = 0;

                while (blocks_written < messageInfo.NoOfBlocks)
                {
                    var no_of_pages = (blocks_written + 1 == messageInfo.NoOfBlocks) ?
                                        messageInfo.ActualFileNoOfPagesLastBlock : 32;

                    var last_page_size = (blocks_written + 1 == messageInfo.NoOfBlocks) ?
                                          messageInfo.ActualFileLastPageSize : 512;

                    var ret = ReadBlockDataFromFile(path, messageInfo, (blocks_written + 1), no_of_pages, last_page_size);

                    if (ret != returnCodes.DTCL_SUCCESS)
                    {
                        Log.Log.Error($"Error reading block data for MessageID-{msg}, Block {blocks_written}. Aborting.");
                        return;
                    }

                    ret = await WriteD2BlockData(startBlock, no_of_pages, last_page_size, cartNo, progress);

                    if (ret != returnCodes.DTCL_SUCCESS)
                    {
                        Log.Log.Error($"Error writing block data for MessageID-{msg}, Block {blocks_written}. Aborting.");
                        return;
                    }

                    startBlock++;
                    blocks_written++;
                }
            }
        }

        public void SplitFPLToIndividualFiles(string path)
        {
            var FPLMessageInfo = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(6);

            var FplName = Path.Combine(path, FPLMessageInfo.FileName);

            if (!System.IO.File.Exists(FplName))
            {
                Log.Log.Error($"{FplName} does not exist");
                return;
            }

            var fs1 = new FileStream(FplName, FileMode.Open, FileAccess.Read);

            for (int msg = 61; msg <= 69; msg++)
            {
                var messageInfo = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(msg);

                if (FileOperations.IsFileExist(FplName))
                {
                    Log.Log.Info($"Start Creating fpl{msg - 60} its Header Size: {messageInfo.HeaderFileSize}");

                    if (messageInfo.isDefinedInHeader)
                    {
                        DataHandlerIsp.Instance.totalDataSize = DataHandlerIsp.Instance.totalDataSize + messageInfo.HeaderFileSize;

                        var fs2 = new FileStream(Path.Combine(path, messageInfo.FileName), FileMode.OpenOrCreate, FileAccess.Write);

                        for (int byteNo = 0; byteNo < (messageInfo.HeaderFileSize); byteNo++)
                            fs2.WriteByte((byte)fs1.ReadByte());

                        fs2.Close();
                    }
                    else
                    {
                        Log.Log.Error($"Start Creating fpl{msg - 60} failed not defined in header");
                    }
                }
            }

            fs1.Close();
        }

        int CopyValidFilesToTempFolder(string msgPath, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile)
        {
            Log.Log.Info("Starting Copy of valid files to temp folder");
            FileOperations.deleteAndCreateDir(HardwareInfo.Instance.D2UploadTempFilePath);

            foreach (var msg in uMessageContainerObj.MessageInfoList)
            {
                if (msg.isDefinedInHeader)
                {
                    var sourceFilePath = Path.Combine(msgPath, msg.FileName);
                    var destinationFilePath = Path.Combine(HardwareInfo.Instance.D2UploadTempFilePath, msg.FileName);

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
                        if (msg.FileName.ToLower().Contains("fpl") && !msg.FileName.Any(char.IsDigit))
                        {
                            FileOperations.Copy(sourceFilePath, destinationFilePath);
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
                            fs.Close();
                        }
                    }
                }
            }

            Log.Log.Info("Copy of valid files to temp folder done");

            Log.Log.Info("Calculation size of temp directory");

            var size = FileOperations.getDirectorySize(HardwareInfo.Instance.D2UploadTempFilePath);
            size = size - FileOperations.getFileSize(HardwareInfo.Instance.D2UploadTempFilePath + "FPL.bin");
            size = size + 1024;

            DataHandlerIsp.Instance.ResetProgressValues();
            DataHandlerIsp.Instance.SetProgressValues(size, 0);

            return returnCodes.DTCL_SUCCESS;
        }

        int CopyValidFilesToTempFolder_ToBeDeleted(string msgPath, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile, bool checkHeader = true)
        {
            Log.Log.Info("Starting Copy of valid files to temp folder");
            FileOperations.deleteAndCreateDir(HardwareInfo.Instance.D2UploadTempFilePath);

            foreach (var msg in uMessageContainerObj.MessageInfoList)
            {
                if ((msg.MsgID == 8) || (msg.MsgID == 9))
                    continue;

                if (msg.isDefinedInHeader)
                {
                    var sourceFilePath = Path.Combine(msgPath, msg.FileName);
                    var destinationFilePath = Path.Combine(HardwareInfo.Instance.D2UploadTempFilePath, msg.FileName);

                    if (checkHeader == true)
                    {
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
                            if (msg.FileName.ToLower().Contains("fpl") && !msg.FileName.Any(char.IsDigit))
                            {
                                FileOperations.Copy(sourceFilePath, destinationFilePath);
                                continue;
                            }

                            CustomMessageBox.MessageBoxResult shouldContinue;

                            if (msg.isFileValid == false)
                            {
                                handleInvalidFile("Invalid_FileSize_Msg", msg.FileName);
                            }
                            else if (msg.isFileExists == false)
                            {
                                handleInvalidFile("Missing_File", msg.FileName);
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
                        }
                    }
                    else
                    {
                        if ((msg.isFileExists))
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
                            if (msg.FileName.ToLower().Contains("fpl") && !msg.FileName.Any(char.IsDigit))
                            {
                                FileOperations.Copy(sourceFilePath, destinationFilePath);
                                continue;
                            }

                            CustomMessageBox.MessageBoxResult shouldContinue;

                            if (msg.isFileValid == false)
                            {
                                handleInvalidFile("Invalid_FileSize_Msg", msg.FileName);
                            }
                            else if (msg.isFileExists == false)
                            {
                                handleInvalidFile("Missing_File", msg.FileName);
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
                        }
                    }
                }
            }

            Log.Log.Info("Copy of valid files to temp folder done");

            Log.Log.Info("Calculation size of temp directory");

            var size = FileOperations.getDirectorySize(HardwareInfo.Instance.D2UploadTempFilePath);
            size = size - FileOperations.getFileSize(HardwareInfo.Instance.D2UploadTempFilePath + "FPL.bin");
            size = size + 1024;

            DataHandlerIsp.Instance.ResetProgressValues();
            DataHandlerIsp.Instance.SetProgressValues(size, 0);

            return returnCodes.DTCL_SUCCESS;
        }

        public int ReadBlockDataFromFile(string path, IMessageInfo msg, int blockNo, int NoOfPages, int LastPageSize)  // size of the *last* page in *this* block
        {
            const int PageSize = 512;
            const int PagesPerBlock = 32;      // <-- full block size

            var filePath = Path.Combine(path, msg.FileName);

            for (int pageNo = 1; pageNo <= NoOfPages; pageNo++)
            {
                // 1) How many pages did *all* the previous blocks have?
                var pagesBeforeThisBlock = (blockNo - 1) * PagesPerBlock;

                // 2) How many pages into *this* block are we?
                var pagesIntoBlock = pageNo - 1;

                // 3) Global page index
                var globalPageIndex = pagesBeforeThisBlock + pagesIntoBlock;

                var offset = globalPageIndex * PageSize;

                // last page in the block might be smaller
                var length = (pageNo == NoOfPages && LastPageSize != PageSize)
                           ? LastPageSize
                           : PageSize;

                var dataPacket = FileOperations.ReadFileData(filePath, offset, length);

                if (dataPacket == null)
                    return returnCodes.DTCL_FILE_NOT_FOUND;

                DataBlock.UpdatePageData(pageNo, dataPacket);
            }

            return returnCodes.DTCL_SUCCESS;
        }

        public async Task<int> Format(IProgress<int> progress, byte cartNo) => -1;

        public async Task<int> EraseCartFiles(IProgress<int> progress, byte cartNo, bool trueErase = false)
        {
            Log.Log.Info("Start Erasing D2 Crat");

            if (trueErase)
            {
                DataHandlerIsp.Instance.OnProgressChanged("Erase", 0, 1024, progress);
            }

            Log.Log.Info("Start Erasing D2 full");

            var cmdPayload = FrameInternalPayload((byte)IspCommand.TX_DATA, (byte)IspSubCommand.D2_ERASE, 0,
               new ushort[] { (ushort)0, 1, (ushort)1, 0, cartNo });

            DataHandlerIsp.Instance.Execute(cmdPayload, null);

            var i = 0;

            while (DataHandlerIsp.Instance._tx.SubCmdResponse == IspSubCmdResponse.IN_PROGRESS)
            {
                await Task.Delay(100);
                DataHandlerIsp.Instance.OnProgressChanged("Erase", i, 1024, progress);
                i += 10;
            }

            Log.Log.Info("Erasing D2 full Done");

            DataHandlerIsp.Instance.OnProgressChanged("Erase", 1024, 1024, progress);

            return DataHandlerIsp.Instance._tx.SubCmdResponse == IspSubCmdResponse.SUCESS ? returnCodes.DTCL_SUCCESS : returnCodes.DTCL_NO_RESPONSE;
        }

        public async Task<int> EraseCartPCFiles(IProgress<int> progress, byte cartNo, bool trueErase = false)
        {
            Log.Log.Info("Start Erasing D2 PC files");

            var uMessageContainerObj = new UploadMessageInfoContainer();
            var uMessageParserObj = new JsonParser<UploadMessageInfoContainer>();
            uMessageContainerObj = uMessageParserObj.Deserialize("D2\\D2UploadMessageDetails.json");
            var uMessageInfo = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(3);

            var ret = await EraseBlockNo(uMessageInfo.fsb, cartNo);

            return ret;
        }

        public async Task<int> EraseBlockNo(int blockNo, byte cartNo)
        {
            Log.Log.Info("Start Erasing D2 Block");

            var cmdPayload = FrameInternalPayload((byte)IspCommand.TX_DATA, (byte)IspSubCommand.D2_ERASE_BLOCK, 0,
               new ushort[] { (ushort)blockNo, 1, (ushort)1, 0, cartNo });

            Log.Log.Info($"[EVT4002] Initiating Erase: Block={blockNo}");

            var res = await DataHandlerIsp.Instance.Execute(cmdPayload, null);

            Log.Log.Info("Erasing D2 Block Done");

            return res == IspSubCmdResponse.SUCESS ? returnCodes.DTCL_SUCCESS : returnCodes.DTCL_NO_RESPONSE;
        }

        public async Task<int> ReadDownloadFiles(string path, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile, byte cartNo, IProgress<int> progress, bool checkHeaderInfo = true)
        {
            DataHandlerIsp.Instance.ResetProgressValues();

            uploadRwrFlag = false;
            uploadSpjFlag = false;

            FileOperations.deleteAndCreateDir(path);

            InitializeUploadMessages(path);
            InitializeDownloadMessages();

            var ret = await ReadHeaderSpaceDetails(cartNo);

            if ((ret == returnCodes.DTCL_BLANK_CARTRIDGE))
            {
                return ret;
            }

            ret = InitUpdMsgWithHeaderSpaceDetails(ret);

            if (ret == returnCodes.DTCL_SUCCESS)
            {
                ret = InitDwnMsgWithHeaderSpaceDetails();
            }

            ret = await ReadDecodeHeader(cartNo, progress);

            if ((ret != returnCodes.DTCL_SUCCESS) && (checkHeaderInfo == true))
            {
                checkHeaderInfo = false;
                var shouldContinue = handleInvalidFile("Header_Missing_Msg2", "");

                if (shouldContinue == CustomMessageBox.MessageBoxResult.No)
                {
                    Log.Log.Warning($"User chose to stop operation due to missing header file");
                    return returnCodes.DTCL_CMD_ABORT;
                }
            }

            await FindEnd(HardwareInfo.Instance.D2DownloadTempFilePath, cartNo, progress);

            if (ret == returnCodes.DTCL_SUCCESS)
            {
                Log.Log.Debug("Reading and Creating FPL data, since header is present ");
                await ReadCreateFPLData(cartNo, progress);
            }
            else
            {
                Log.Log.Debug("Skip Reading and Creating FPL data, since header is absent, creating 0kb FPL ");
                var fs = new FileStream(HardwareInfo.Instance.D2DownloadTempFilePath + "FPL.bin", FileMode.OpenOrCreate, FileAccess.Write);
                fs.Close();
            }

            var spjRwrFlag = checkDownloadSPJRWRFileSize(HardwareInfo.Instance.D2DownloadTempFilePath, 16);

            if (!spjRwrFlag)
            {
                ret = await ReadUploadSpjRwr(HardwareInfo.Instance.D2DownloadTempFilePath, cartNo, progress, 8);

                if (ret != returnCodes.DTCL_SUCCESS)
                {
                    Log.Log.Error($"Reading of upload SPJ failed");
                    return ret;
                }

                uploadSpjFlag = true;
            }

            spjRwrFlag = checkDownloadSPJRWRFileSize(HardwareInfo.Instance.D2DownloadTempFilePath, 17);

            if (!spjRwrFlag)
            {
                ret = await ReadUploadSpjRwr(HardwareInfo.Instance.D2DownloadTempFilePath, cartNo, progress, 9);

                if (ret != returnCodes.DTCL_SUCCESS)
                {
                    Log.Log.Error($"Reading of upload RWR failed");
                    return ret;
                }

                uploadRwrFlag = true;
            }

            if (checkHeaderInfo == true)
            {
                if (CopyValidUploadMessages(path, handleInvalidFile) == false)
                    return returnCodes.DTCL_CMD_ABORT;

                CopyValidDownloadMessages(path);
            }
            else
            {
                CopyAllMessages(path);
            }

            return 0;
        }

        public async Task<int> ReadDownloadFiles2(string path, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile, byte cartNo, IProgress<int> progress, bool checkHeaderInfo = true)
        {
            DataHandlerIsp.Instance.ResetProgressValues();

            uploadRwrFlag = false;
            uploadSpjFlag = false;

            FileOperations.deleteAndCreateDir(path);

            var ret = await ReadHeaderSpaceDetails(cartNo);

            if ((ret == returnCodes.DTCL_BLANK_CARTRIDGE))
            {
                return ret;
            }

            ret = InitUpdMsgWithHeaderSpaceDetails(ret);

            if (ret == returnCodes.DTCL_SUCCESS)
            {
                ret = InitDwnMsgWithHeaderSpaceDetails();
            }

            ret = await ReadDecodeHeader(cartNo, progress);

            if ((ret != returnCodes.DTCL_SUCCESS) && (checkHeaderInfo == true))
            {
                checkHeaderInfo = false;
                var shouldContinue = handleInvalidFile("Header_Missing_Msg2", "");

                if (shouldContinue == CustomMessageBox.MessageBoxResult.No)
                {
                    Log.Log.Warning($"User chose to stop operation due to missing header file");
                    return returnCodes.DTCL_CMD_ABORT;
                }
            }

            await FindEnd(HardwareInfo.Instance.D2DownloadTempFilePath, cartNo, progress);

            if (ret == returnCodes.DTCL_SUCCESS)
            {
                Log.Log.Debug("Reading and Creating FPL data, since header is present ");
                await ReadCreateFPLData(cartNo, progress);
            }
            else
            {
                Log.Log.Debug("Skip Reading and Creating FPL data, since header is absent, creating 0kb FPL ");
                var fs = new FileStream(HardwareInfo.Instance.D2DownloadTempFilePath + "FPL.bin", FileMode.OpenOrCreate, FileAccess.Write);
                fs.Close();
            }

            var spjRwrFlag = checkDownloadSPJRWRFileSize(HardwareInfo.Instance.D2DownloadTempFilePath, 16);

            if (!spjRwrFlag)
            {
                ret = await ReadUploadSpjRwr(HardwareInfo.Instance.D2DownloadTempFilePath, cartNo, progress, 8);

                if (ret != returnCodes.DTCL_SUCCESS)
                {
                    Log.Log.Error($"Reading of upload SPJ failed");
                    return ret;
                }

                uploadSpjFlag = true;
            }

            spjRwrFlag = checkDownloadSPJRWRFileSize(HardwareInfo.Instance.D2DownloadTempFilePath, 17);

            if (!spjRwrFlag)
            {
                ret = await ReadUploadSpjRwr(HardwareInfo.Instance.D2DownloadTempFilePath, cartNo, progress, 9);

                if (ret != returnCodes.DTCL_SUCCESS)
                {
                    Log.Log.Error($"Reading of upload RWR failed");
                    return ret;
                }

                uploadRwrFlag = true;
            }

            if (checkHeaderInfo == true)
            {
                if (CopyValidUploadMessages(path, handleInvalidFile) == false)
                    return returnCodes.DTCL_CMD_ABORT;

                CopyValidDownloadMessages(path);
            }
            else
            {
                CopyAllMessages(path);
            }

            return 0;
        }

        public async Task<int> ReadCreateFPLData(byte cartNo, IProgress<int> progress)
        {
            var uMessageInfo = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(6);
            var present_block_no = uMessageInfo.fsb;

            for (int fplNo = 1; fplNo <= 9; fplNo++)
            {
                var filename = "fpl" + fplNo.ToString() + ".bin";
                uMessageInfo = (UploadMessageInfo)uMessageContainerObj.FindMessageByFileName(filename);

                if (uMessageInfo.isDefinedInHeader)
                {
                    var no_of_pages = ((uMessageInfo.Nob * 21 * 2) / 512);
                    int last_page_size = (short)((uMessageInfo.Nob * 21 * 2) % 512);
                    var ret = await ReadD2BlockData(present_block_no, no_of_pages + 1, last_page_size, cartNo, progress);
                    var FileName = HardwareInfo.Instance.D2DownloadTempFilePath + uMessageInfo.FileName;

                    // Create empty file initially
                    File.Create(FileName).Dispose();

                    var offset = 0;

                    for (int pageNo = 1; pageNo <= (no_of_pages + 1); pageNo++)
                    {
                        var pageSize = (pageNo == (no_of_pages + 1)) ? last_page_size : 512;
                        var data = DataBlock.GetPageData(pageNo, pageSize);

                        // Only write if data is not all 0xFF
                        if (!IsAllFF(data))
                        {
                            FileOperations.WriteFileData(data, FileName, offset);
                        }

                        offset += data.Length;
                    }

                    present_block_no = (present_block_no + 1);
                }
            }

            CombineFpl();
            return 0;
        }

        bool IsAllFF(byte[] data)
        {
            foreach (byte b in data)
            {
                if (b != 0xFF)
                {
                    return false;
                }
            }

            return true;
        }

        public void CombineFpl()
        {
            var uMessageInfo2 = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(6);
            var FileName2 = HardwareInfo.Instance.D2DownloadTempFilePath + uMessageInfo2.FileName;

            if (File.Exists(FileName2))
                File.Delete(FileName2);

            for (int i = 1; i <= 9; i++)
            {
                var filename = "fpl" + i.ToString() + ".bin";
                var uMessageInfo = (UploadMessageInfo)uMessageContainerObj.FindMessageByFileName(filename);

                if (uMessageInfo.isDefinedInHeader)
                {
                    var FileName = HardwareInfo.Instance.D2DownloadTempFilePath + uMessageInfo.FileName;

                    if (FileOperations.IsFileExist(FileName))
                    {
                        var fs1 = new FileStream(FileName, FileMode.Open, FileAccess.Read);
                        var fs2 = new FileStream(FileName2, FileMode.Append, FileAccess.Write);

                        if (fs1.Length != 0)
                        {
                            var len = uMessageInfo.Nob * 21 * 2;

                            for (int k = 1; k <= (len); k++)
                                fs2.WriteByte((byte)fs1.ReadByte());
                        }

                        fs1.Close();
                        fs2.Close();
                    }
                }
            }
            // ''DELETE THE 9 FPL FILES
            /*for (i = 0; i <= 9; i++)
            {
                destination = dir_download + f[i];
                if (System.IO.File.Exists(destination))
                    System.IO.File.Delete(destination);
            }*/
        }

        public async Task<int> FindEnd(string Path, byte cartNo, IProgress<int> progress)
        {
            int ret;
            int presentBlockNo;
            int endBlock = 0, endPage = 0;

            for (int msg = 2; msg <= 17; msg++)
            {
                var exitFlag = false;
                // Skip message IDs 5 and 6
                if (msg == 5 || msg == 6 || msg == 14)
                    continue;

                var dMessageInfo = (DownloadMessageInfo)dMessageContainerObj.FindMessageByMsgId(msg);

                presentBlockNo = dMessageInfo.fsb;

                var blockCounter = 0;

                var filename = Path + dMessageInfo.FileName;

                if (File.Exists(filename))
                    File.Delete(filename);

                var fs = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write);
                fs.Close();

                for (int k = 1; k <= dMessageInfo.PreFixedNoOfBlocks; k++)
                {
                    Log.Log
                       .Info($"Reading data of MsgId-{msg} MsgName-{dMessageInfo.FileName} from block address-{presentBlockNo}");

                    ret = await ReadD2BlockData(presentBlockNo, 32, 512, cartNo, progress);

                    if (ret != returnCodes.DTCL_SUCCESS)
                        return ret;

                    blockCounter++;

                    for (int presentPage = 1; presentPage <= 32; presentPage++)
                    {
                        fs = new FileStream(filename, FileMode.Append, FileAccess.Write);

                        var pageData = DataBlock.GetPageData(presentPage, 512);

                        if ((pageData[509] == 255) && (pageData[508] == 255) && (pageData[507] == 255) && (pageData[506] == 255))
                        {
                            var partData = new byte[512];
                            var isEndPage = IsEndPage(ref partData, pageData);

                            if (isEndPage > 1)
                            {
                                endBlock = blockCounter;
                                endPage = presentPage;
                                fs.Write(partData, 0, isEndPage);
                            }

                            fs.Close();
                            exitFlag = true;
                            Log.Log.Info($"End Page Reached at PageNo-{presentPage}");
                            break;
                        }

                        fs.Write(pageData, 0, pageData.Length);
                        fs.Close();
                    }

                    if (exitFlag)
                        break;

                    presentBlockNo++;
                }
            }

            return returnCodes.DTCL_SUCCESS;
        }

        public async Task<int> ReadUploadSpjRwr(string Path, byte cartNo, IProgress<int> progress, int msg)
        {
            int ret;
            int presentBlockNo;
            int endBlock = 0, endPage = 0;

            // for (int msg = 8; msg <= 9; msg++)
            // {
            var exitFlag = false;

            var dMessageInfo = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(msg);

            presentBlockNo = dMessageInfo.fsb;

            var blockCounter = 0;

            var filename = Path + dMessageInfo.FileName;

            if (File.Exists(filename))
                File.Delete(filename);

            var fs = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write);
            fs.Close();

            for (int k = 1; k <= dMessageInfo.PreFixedNoOfBlocks; k++)
            {
                Log.Log
                    .Info($"Reading data of MsgId-{msg} MsgName-{dMessageInfo.FileName} from block address-{presentBlockNo}");

                ret = await ReadD2BlockData(presentBlockNo, 32, 512, cartNo, progress);

                if (ret != returnCodes.DTCL_SUCCESS)
                    return ret;

                blockCounter++;

                for (int presentPage = 1; presentPage <= 32; presentPage++)
                {
                    fs = new FileStream(filename, FileMode.Append, FileAccess.Write);

                    var pageData = DataBlock.GetPageData(presentPage, 512);

                    if ((pageData[509] == 255) && (pageData[508] == 255) && (pageData[507] == 255) && (pageData[506] == 255))
                    {
                        var partData = new byte[512];
                        var isEndPage = IsEndPage(ref partData, pageData);

                        if (isEndPage > 1)
                        {
                            endBlock = blockCounter;
                            endPage = presentPage;
                            fs.Write(partData, 0, isEndPage);
                        }

                        fs.Close();
                        exitFlag = true;
                        Log.Log.Info($"End Page Reached at PageNo-{presentPage}");
                        break;
                    }

                    fs.Write(pageData, 0, pageData.Length);
                    fs.Close();
                }

                if (exitFlag)
                    break;

                presentBlockNo++;
            }
            // }

            return returnCodes.DTCL_SUCCESS;
        }

        public async Task<int> FindEndForMSg(string Path, byte cartNo, IMessageInfo msg)
        {
            int ret;
            int presentBlockNo;
            int endBlock = 0, endPage = 0;

            var exitFlag = false;

            presentBlockNo = msg.fsb;

            var blockCounter = 0;

            var filename = Path + msg.FileName;

            if (File.Exists(filename))
                File.Delete(filename);

            var fs = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write);
            fs.Close();

            for (int k = 1; k <= msg.PreFixedNoOfBlocks; k++)
            {
                Log.Log.Info($"Reading data of MsgId-{msg} MsgName-{msg.FileName} from block address-{presentBlockNo}");

                ret = await ReadD2BlockData(presentBlockNo, 32, 512, cartNo, null);

                if (ret != returnCodes.DTCL_SUCCESS)
                    return ret;

                blockCounter++;

                for (int presentPage = 1; presentPage <= 32; presentPage++)
                {
                    fs = new FileStream(filename, FileMode.Append, FileAccess.Write);

                    var pageData = DataBlock.GetPageData(presentPage, 512);

                    if ((pageData[509] == 255) && (pageData[508] == 255) && (pageData[507] == 255) && (pageData[506] == 255))
                    {
                        var partData = new byte[512];
                        var isEndPage = IsEndPage(ref partData, pageData);

                        if (isEndPage > 1)
                        {
                            endBlock = blockCounter;
                            endPage = presentPage;
                            fs.Write(partData, 0, isEndPage);
                        }

                        fs.Close();
                        exitFlag = true;
                        Log.Log.Info($"End Page Reached at PageNo-{presentPage}");
                        break;
                    }

                    fs.Write(pageData, 0, pageData.Length);
                    fs.Close();
                }

                if (exitFlag)
                    break;

                presentBlockNo++;
            }

            return returnCodes.DTCL_SUCCESS;
        }

        public bool CopyValidUploadMessages(string destPath, Func<string, string, CustomMessageBox.MessageBoxResult> handleInvalidFile)
        {
            var res = HeaderInfo.UpdateMessageInfoWithHeaderData(CartType.Darin2, HardwareInfo.Instance.D2DownloadTempFilePath, uMessageContainerObj);

            if (!res)
                return res;

            foreach (var msg in uMessageContainerObj.MessageInfoList)
            {
                if ((msg.MsgID == 8))
                {
                    if (!uploadSpjFlag)
                        continue;
                }
                else if ((msg.MsgID == 9))
                {
                    if (!uploadRwrFlag)
                        continue;
                }

                if (msg.isUploadFile)
                {
                    if (msg.isDefinedInHeader)
                    {
                        if (msg.isFileValid)
                        {
                            FileOperations.Copy(HardwareInfo.Instance.D2DownloadTempFilePath + msg.FileName, destPath + msg.FileName);
                        }
                        else
                        {
                            if (msg.FileName.ToLower().Contains("fpl") && !msg.FileName.Any(char.IsDigit))
                            {
                                FileOperations.Copy(HardwareInfo.Instance.D2DownloadTempFilePath + msg.FileName, destPath + msg.FileName);
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
                                FileOperations.Copy(HardwareInfo.Instance.D2DownloadTempFilePath + msg.FileName, destPath + msg.FileName);
                            }
                        }
                    }
                    else
                    {
                        var fs = new FileStream(destPath + msg.FileName, FileMode.OpenOrCreate, FileAccess.Write);
                        fs.Close();
                    }
                }
            }

            var serializedJson = uMessageParserObj.Serialize((UploadMessageInfoContainer)uMessageContainerObj);

            File.WriteAllText("D2\\D2UploadMessageDetails.json", serializedJson);

            return res;
        }

        public bool CopyValidDownloadMessages(string destPath)
        {
            var res = HeaderInfo.UpdateMessageInfoWithHeaderData(CartType.Darin2, HardwareInfo.Instance.D2DownloadTempFilePath, dMessageContainerObj);

            if (!res)
                return res;

            foreach (var msg in dMessageContainerObj.MessageInfoList)
            {
                if (!msg.isUploadFile)
                {
                    FileOperations.Copy(HardwareInfo.Instance.D2DownloadTempFilePath + msg.FileName, destPath + msg.FileName);
                }
            }

            var serializedJson = dMessageParserObj.Serialize((DownloadMessageInfoContainer)dMessageContainerObj);

            File.WriteAllText("D2\\D2DownloadMessageDetails.json", serializedJson);

            return res;
        }

        public bool CopyAllMessages(string destPath)
        {

            foreach (var msg in dMessageContainerObj.MessageInfoList)
            {
                if (!msg.isUploadFile)
                {
                    FileOperations.Copy(HardwareInfo.Instance.D2DownloadTempFilePath + msg.FileName, destPath + msg.FileName);
                }
            }

            foreach (var msg in uMessageContainerObj.MessageInfoList)
            {
                if (msg.isUploadFile)
                {
                    FileOperations.Copy(HardwareInfo.Instance.D2DownloadTempFilePath + msg.FileName, destPath + msg.FileName);
                }
            }

            var serializedJson = dMessageParserObj.Serialize((DownloadMessageInfoContainer)dMessageContainerObj);

            File.WriteAllText("D2\\D2DownloadMessageDetails.json", serializedJson);

            return true;
        }

        int IsEndPage(ref byte[] partData, byte[] pageData)
        {
            for (int i = 0; i < 512; i++)
            {
                switch (i)
                {
                    case 0:
                        // Check if the first two bytes are 255
                        if (pageData[0] == 255 && pageData[1] == 255)
                        {
                            return 1;
                        }
                        else
                            partData[i] = pageData[i];
                        break;

                    case 511:
                    case 510:
                        // Check if the last bytes are not 255
                        if (pageData[511] != 255)
                        {
                            return 0; // Not an end page
                        }

                        break;

                    default:
                        // For other bytes, check if two consecutive bytes are 255
                        if ((i > 0 && i <= 509) && (pageData[i] == 255 && pageData[i + 1] == 255))
                        {
                            return i; // End page condition met
                        }
                        else
                        {
                            partData[i] = pageData[i];
                        }

                        break;
                }
            }

            // If none of the conditions match, it is not an end page
            return 0;
        }

        void CopyPageData(int blockCounter, int presentPage)
        {
            // Assuming DataBlock is your object storing data blocks and pages
            var pageData = DataBlock.GetPageData(presentPage, 512);

            for (int byteNo = 0; byteNo < 512; byteNo++)
                DataBlock.SetBlockData(blockCounter, presentPage, byteNo, pageData[byteNo]);
        }

        void WritePageDataToFile(string filename, byte[] data, int length)
        {
            using (var fs = new FileStream(filename, FileMode.Append, FileAccess.Write))
                fs.Write(data, 0, data.Length);
        }

        void WriteDataToFile(string filename, int endBlock, int endPage)
        {
            if (File.Exists(filename))
                File.Delete(filename);

            using (var fs = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write))
            {
                for (int i = 1; i <= endBlock; i++)
                {
                    // Get all 32 pages of the current block
                    var blockData = DataBlock.GetBlockData(i);

                    for (int j = 1; j <= (i == endBlock ? endPage : 32); j++)
                    {
                        var pageData = new byte[512];

                        // Copy the current page data from the 2D array
                        for (int k = 0; k < 512; k++)
                            pageData[k] = blockData[j - 1, k];
                        // j-1 since pages are 0-indexed

                        if (i == endBlock && j == endPage)
                        {
                            WritePartialPage(fs, pageData); // Handle partial page for the last block and page
                        }
                        else
                        {
                            fs.Write(pageData, 0, 512); // Write the full page (512 bytes)
                        }
                    }
                }
            }
        }

        void WritePartialPage(FileStream fs, byte[] pageData)
        {
            for (int l = 0; l <= 511; l++)
            {
                if (l == 0 && pageData[0] == 255 && pageData[1] == 255)
                {
                    return; // Stop writing if end marker is detected
                }

                if (l == 511 || l == 510)
                {
                    if (pageData[511] != 255)
                    {
                        fs.WriteByte(pageData[l]);
                    }
                }
                else if (pageData[l] == 255 && pageData[l + 1] == 255)
                {
                    return; // Stop writing if end marker is detected in middle of the page
                }
                else
                {
                    fs.WriteByte(pageData[l]);
                }
            }
        }

        public async Task<int> CopyCartFiles(string path, Func<string, string, CustomMessageBox.MessageBoxResult> handleUserConfirmation, Func<string, string> displayUserStatus, byte masterCartNo, byte[] slaveCartNo, IProgress<int> progress)
        {
            Log.Log.Info($"Starting copy operation form master cart: {masterCartNo} to slave cart:{slaveCartNo}");

            displayUserStatus("Copy_Inprogess_Msg");

            // int result = -1;

            // HardwareInfo.Instance.isCartChanged = false;

            var result = await ReadDownloadFiles(path, handleUserConfirmation, masterCartNo, progress, true);
            // result = FindEndForMSg(path,);

            if (result != returnCodes.DTCL_SUCCESS)
                return result;

            if (HardwareInfo.Instance.BoardId != "DTCL")
                await LedState.LedIdleSate(masterCartNo);

            for (int itr = 0; itr < slaveCartNo.Length; itr++)
            {
                if (slaveCartNo[itr] == 0)
                    continue;

                displayUserStatus("Copy_Overwrite_Msg3");

                if (HardwareInfo.Instance.BoardId == "DTCL")
                {
                    Log.Log.Info("waiting for cart change");
                    displayUserStatus("Copy_Overwrite_Msg");

                    var cts = new CancellationTokenSource();
                    var changed = await HardwareInfo.Instance.WaitForCartChangeAsync(slaveCartNo[itr], cts.Token);

                    if (changed)
                    {
                        Log.Log.Info("Cart changed! Performing Copy action...");
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
                    await LedState.LedIdleSate(masterCartNo);
                    var shouldContinue = handleUserConfirmation("Copy_Overwrite_Msg2", "");

                    if (shouldContinue == CustomMessageBox.MessageBoxResult.No)
                    {
                        Log.Log.Warning($"User chose to stop operation");
                        return returnCodes.DTCL_CMD_ABORT;
                    }

                    await LedState.LedBusySate(slaveCartNo[itr]);
                }

                displayUserStatus("Copy_Inprogess_slave_Msg");

                result = await WriteUploadFilesForCopy(path, handleUserConfirmation, slaveCartNo[itr], progress);

                Log.Log.Info($"copy operation done with {result}");

                if (HardwareInfo.Instance.BoardId != "DTCL")
                {
                    if (result == returnCodes.DTCL_SUCCESS)
                    {
                        var info = handleUserConfirmation("Copy_completed_Msg", " for Slot-" + slaveCartNo[itr].ToString());
                    }
                    else
                    {
                        var info = handleUserConfirmation("Copy_Failed_Msg", " for Slot-" + slaveCartNo[itr].ToString());
                    }

                    await LedState.LedIdleSate(slaveCartNo[itr]);
                }
            }

            return result;
        }

        public async Task<int> CompareCartFiles(Func<string, string, CustomMessageBox.MessageBoxResult> handleUserConfirmation, Func<string, string> displayUserStatus, byte masterCartNo, byte[] slaveCartNo, IProgress<int> progress)
        {
            Log.Log.Info("Starting compare operation");
            var result = -1;

            displayUserStatus("Compare_Reading_Ist_cart_Msg");

            FileOperations.deleteAndCreateDir(HardwareInfo.Instance.D2Compare1FilePath);
            FileOperations.deleteAndCreateDir(HardwareInfo.Instance.D2Compare2FilePath);

            result = await ReadDownloadFiles(HardwareInfo.Instance.D2Compare1FilePath, handleUserConfirmation, masterCartNo, progress, false);

            if (HardwareInfo.Instance.BoardId != "DTCL")
                await LedState.LedIdleSate(masterCartNo);

            if (result != returnCodes.DTCL_SUCCESS)
                return result;

            for (int itr = 0; itr < slaveCartNo.Length; itr++)
            {
                if (slaveCartNo[itr] == 0)
                    continue;

                if (HardwareInfo.Instance.BoardId == "DTCL")
                {
                    Log.Log.Info("waiting for cart change");
                    displayUserStatus("Compare_Waiting_cart_Msg");

                    var cts = new CancellationTokenSource();
                    var changed = await HardwareInfo.Instance.WaitForCartChangeAsync(slaveCartNo[itr], cts.Token);

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

                    var shouldContinue = handleUserConfirmation("Second_CartDetected_Msg", "");

                    if (shouldContinue == CustomMessageBox.MessageBoxResult.Cancel)
                    {
                        Log.Log.Warning($"User chose to stop operation");
                        return returnCodes.DTCL_CMD_ABORT;
                    }
                }
                else
                {
                    await LedState.LedIdleSate(masterCartNo);
                    await LedState.LedBusySate(slaveCartNo[itr]);
                }

                displayUserStatus("Compare_Inprogess_Msg");

                result = await ReadDownloadFiles(HardwareInfo.Instance.D2Compare2FilePath, handleUserConfirmation, slaveCartNo[itr], progress, false);

                if (result != returnCodes.DTCL_SUCCESS)
                {
                    await LedState.LedIdleSate(slaveCartNo[itr]);

                    if (result == returnCodes.DTCL_BLANK_CARTRIDGE)
                        return returnCodes.DTCL_BLANK_CARTRIDGE2;
                }

                Log.Log.Info($"Start Comparing");

                result = FileOperations.compareDir(HardwareInfo.Instance.D2Compare2FilePath, HardwareInfo.Instance.D2Compare1FilePath);

                if (HardwareInfo.Instance.BoardId != "DTCL")
                {
                    if (result == returnCodes.DTCL_SUCCESS)
                    {
                        var info = handleUserConfirmation("Compare_Completed_Msg", " for Slot-" + slaveCartNo[itr].ToString());
                    }
                    else if (result == returnCodes.DTCL_BLANK_CARTRIDGE2)
                    {
                        handleUserConfirmation("SecondCart_Blank_Msg", " for Slot-" + slaveCartNo[itr].ToString());
                    }
                    else
                    {
                        var info = handleUserConfirmation("Compare_Failed_Msg", " for Slot-" + slaveCartNo[itr].ToString());
                    }

                    await LedState.LedIdleSate(slaveCartNo[itr]);
                }
            }

            return result;
        }

        public async Task<int> ReadD2BlockData(int blockNo, int noOfPages, int lastPageSize, byte cartNo, IProgress<int> progress = null)
        {
            m_NoOfPages = (uint)noOfPages;
            m_LastPageSize = (uint)lastPageSize;

            var totalSize = (int)(((m_NoOfPages - 1) * 512) + m_LastPageSize);

            var cmdPayload = FrameInternalPayload((byte)IspCommand.RX_DATA, (byte)IspSubCommand.D2_READ, totalSize,
                new ushort[] { (ushort)blockNo, 1, (ushort)noOfPages, (ushort)lastPageSize, cartNo });

            Log.Log
                .Info($"[EVT4001] Initiating Read: Block={blockNo}, Pages={m_NoOfPages}, LastPageSize={m_LastPageSize}, TotalSize={totalSize}");

            var res = await DataHandlerIsp.Instance.Execute(cmdPayload, progress);

            return res == IspSubCmdResponse.SUCESS ? returnCodes.DTCL_SUCCESS : returnCodes.DTCL_NO_RESPONSE;
        }

        public async Task<int> WriteD2BlockData(int blockNo, int noOfPages, int lastPageSize, byte cartNo, IProgress<int> progress)
        {
            m_NoOfPages = (uint)noOfPages;
            m_LastPageSize = (uint)lastPageSize;

            var totalSize = (int)(((m_NoOfPages - 1) * 512) + m_LastPageSize);

            var cmdPayload = FrameInternalPayload((byte)IspCommand.TX_DATA, (byte)IspSubCommand.D2_WRITE, totalSize,
                new ushort[] { (ushort)blockNo, 1, (ushort)noOfPages, (ushort)lastPageSize, cartNo });

            Log.Log
                .Info($"[EVT4002] Initiating Write: Block={blockNo}, Pages={m_NoOfPages}, LastPageSize={m_LastPageSize}, TotalSize={totalSize}, cartNo={cartNo}");

            var res = await DataHandlerIsp.Instance.Execute(cmdPayload, progress);

            return res == IspSubCmdResponse.SUCESS ? returnCodes.DTCL_SUCCESS : returnCodes.DTCL_NO_RESPONSE;
        }

        public byte[] FrameInternalPayload(byte cmd, byte subCmd, int totalSize, ushort[] parameters)
        {
            var len1 = (byte)(totalSize >> 24);
            var len2 = (byte)(totalSize >> 16);
            var len3 = (byte)(totalSize >> 8);
            var len4 = (byte)(totalSize & 0xFF);

            var blockNo = parameters[0];
            var pageNo = parameters[1];
            var noOfPages = parameters[2];
            var lastPage = parameters[3];
            var cartNo = (parameters.Length > 4) ? parameters[4] : (ushort)0;

            var blockAddress = ((blockNo << 5) | (pageNo - 1));

            return new byte[]
            {
                cmd,
                subCmd,
                len1,
                len2,
                len3,
                len4,
                (byte)(blockAddress & 0xFF),
                (byte)((blockAddress >> 8) & 0xFF),
                (byte)(noOfPages - 1),
                (byte)(lastPage & 0xFF),
                (byte)((lastPage >> 8) & 0xFF),
                (byte)(cartNo)
            };
        }

        public byte[] prepareDataToTx(byte[] data, byte subCmd)
        {
            if (subCmd == (byte)IspSubCommand.D2_WRITE)
            {
                var totalSize = ((m_NoOfPages - 1) * 512) + m_LastPageSize;
                var buffer = new byte[totalSize];
                var offset = 0;

                for (int page = 1; page <= m_NoOfPages; page++)
                {
                    var size = (int)((page == m_NoOfPages) ? m_LastPageSize : 512);
                    Buffer.BlockCopy(DataBlock.GetPageData(page, size), 0, buffer, offset, size);
                    offset += size;
                }

                Log.Log
                    .Info($"[EVT4003] TX data prepared: SubCmd=0x{subCmd:X2}, TotalSize={totalSize}, Pages={m_NoOfPages}, LastPageSize={m_LastPageSize}");

                return buffer;
            }

            Log.Log.Warning($"[EVT4004] Unsupported subCmd=0x{subCmd:X2} for TX data.");
            return null;
        }

        public long prepareForRx(byte[] data, byte subCmd, long len)
        {
            Log.Log.Info($"[EVT4005] prepareForRx called: SubCmd=0x{subCmd:X2}, ExpectedLength={len}");
            return 0; // Default
        }

        public uint processRxData(byte[] data, byte subCmd)
        {
            Log.Log.Info($"[EVT4006] Processing RX data: SubCmd=0x{subCmd:X2}, TotalBytes={data?.Length ?? 0}");

            var offset = 0;

            for (int pageNo = 1; pageNo <= m_NoOfPages; pageNo++)
            {
                var size = (int)((pageNo == m_NoOfPages) ? m_LastPageSize : 512);
                var tempData = new byte[512]; // always 512 for UpdatePageData
                Buffer.BlockCopy(data, offset, tempData, 0, size);
                DataBlock.UpdatePageData(pageNo, tempData);
                offset += size;
            }

            Log.Log.Info($"[EVT4007] RX data processed and stored for {m_NoOfPages} pages.");
            return 0;
        }

        public async Task<PCResult> ExecutePC(bool withCart, CartType cartType, byte cartNo)
        {
            await LedState.DTCLAppCtrlLed();

            if (!withCart)
            {
                return await doLoopBackTest(cartNo);
            }

            FileOperations.createDir(HardwareInfo.Instance.D2UploadFilePath);

            if (!FileOperations.IsFileExist(HardwareInfo.Instance.D2UploadFilePath + @"DR.bin"))
            {
                FileOperations.Copy("D2\\DR.bin", HardwareInfo.Instance.D2UploadFilePath + @"DR.bin");
            }

            var result = new PCResult();
            result.loopBackResult = "PASS";
            result.eraseResult = "PASS";
            result.writeResult = "PASS";
            result.readResult = "PASS";
            result.loopBackTestTime = $"{DateTime.Now:HH-mm-ss}";

            var uMessageContainerObj = new UploadMessageInfoContainer();
            var uMessageParserObj = new JsonParser<UploadMessageInfoContainer>();
            uMessageContainerObj = uMessageParserObj.Deserialize("D2\\D2UploadMessageDetails.json");

            var ret = 0;

            Log.Log.Info("Starting LoopBack Test");
            OnCommandProgress("LoopBack", Colors.DodgerBlue);

            if (await LedState.LoopBackTest(cartNo) == false)
                result.loopBackResult = "FAIL";

            await LedState.LedBusySate(cartNo);

            InitializeUploadMessages(HardwareInfo.Instance.D2UploadFilePath);
            InitializeDownloadMessages();

            allocate_space();

            DataHandlerIsp.Instance.totalDataProcessed = 0;

            ret = await ReadHeaderSpaceDetails(cartNo);

            if ((ret == returnCodes.DTCL_SUCCESS) || (ret == returnCodes.DTCL_BLANK_CARTRIDGE))
            {
                ret = InitUpdMsgWithHeaderSpaceDetails(ret);
            }

            Log.Log.Info("Starting PC Erase operation");

            OnCommandProgress("Erase", Colors.DodgerBlue);

            result.eraseTestTime = $"{DateTime.Now:HH-mm-ss}";

            var uMessageInfo = (UploadMessageInfo)uMessageContainerObj.FindMessageByMsgId(3);

            // ret = await EraseCartFiles(null, cartNo);
            ret = await EraseBlockNo(0, cartNo);

            if (ret != 0)
                result.eraseResult = "FAIL";

            await Task.Delay(500);

            Log.Log.Info("Starting PC Write operation");

            OnCommandProgress("Write", Colors.DodgerBlue);

            result.writeTestTime = $"{DateTime.Now:HH-mm-ss}";

            ret = ReadBlockDataFromFile(HardwareInfo.Instance.D2UploadFilePath, uMessageInfo, (1), 1, 60);

            if (ret != returnCodes.DTCL_SUCCESS)
            {
                Log.Log.Error($"Error reading block data for MessageID-{3}, Block {1}. Aborting.");
            }

            ret = await WriteD2BlockData(uMessageInfo.fsb, 1, 60, cartNo, null);

            if (ret != returnCodes.DTCL_SUCCESS)
            {
                Log.Log.Error($"Error writing block data for MessageID-{3}, Block {1}. Aborting.");
            }

            if (ret != returnCodes.DTCL_SUCCESS)
                result.writeResult = "FAIL";

            await Task.Delay(500);

            Log.Log.Info("Starting PC Read operation");

            OnCommandProgress("Read", Colors.DodgerBlue);

            result.readTestTime = $"{DateTime.Now:HH-mm-ss}";

            ret = await ReadD2BlockData(uMessageInfo.fsb, 1, 60, cartNo, null);
            var pageData = DataBlock.GetPageData(1, 60);

            FileOperations.WriteFileData(pageData, HardwareInfo.Instance.D2DownloadTempFilePath + "DR.bin", 0);

            if (ret != returnCodes.DTCL_SUCCESS)
                result.readResult = "FAIL";

            Log.Log.Info($"Start PC Compare");

            var compare = FileOperations.CompareFiles(System.IO.Path.Combine(HardwareInfo.Instance.D2UploadFilePath, uMessageInfo.FileName), System.IO.Path.Combine(HardwareInfo.Instance.D2DownloadTempFilePath, uMessageInfo.FileName));

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

            if (cartNo == 0)
            {
                if (await LedState.LoopBackTestAll() == false)
                    result.loopBackResult = "FAIL";
            }
            else if (await LedState.LoopBackTest(cartNo) == false)
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
public static class CartHeader
{
    public static byte[,] cartridgeHeader = new byte[32, 64];
    public static void LoadHeaderData()
    {
        var page = 2;       // Start with the first page
        var byte_no = 0;    // Byte index within the page

        // Open the first file stream for writing
        var fs = new FileStream(@"D2\D2_Header_" + page.ToString() + ".bin", FileMode.Create);

        var writer4 = new StreamWriter(fs);

        var writer5 = new StreamWriter(@"D2\D2_Header_Matrix.txt", false);

        // Iterate through 32 rows of the cartridge_header
        for (int i = 0; i < 32; i++)
        {
            writer5.WriteLine(i.ToString() + " row");

            // Iterate through 64 columns of the cartridge_header
            for (int j = 0; j < 64; j++)
            {
                // Read byte from DataBlock.pages and store it in cartridge_header
                cartridgeHeader[i, j] = DataBlock.pages[page, byte_no];

                writer5.Write(cartridgeHeader[i, j]);
                writer5.Write(" ");
                // Write to log file and file stream
                writer4.Write(cartridgeHeader[i, j]);
                writer4.Write(" ");
                fs.WriteByte(cartridgeHeader[i, j]);

                // Move to the next byte
                byte_no++;

                // If we reach the end of the current page (512 bytes)
                if (byte_no == 512)
                {
                    // Close the current file stream
                    writer4.Flush();
                    fs.Close();

                    // Reset byte number for the next page
                    byte_no = 0;

                    // Move to the next page
                    page++;

                    // Open a new file for the next page
                    fs = new FileStream(@"D2\D2_Header_" + page.ToString() + ".bin", FileMode.Create);
                    writer4 = new StreamWriter(fs);
                }
            }

            writer5.WriteLine("");
        }

        // Close the final file stream and writer
        writer4.Flush();
        writer4.Close();
        writer5.Close();
        fs.Close();
    }
}

public static class DataBlock
{
    // A block consists of 32 pages, each page is 512 bytes
    public static byte[,] pages = new byte[33, 513];
    public static byte[,,] Blockpages = new byte[400, 33, 513]; // 3D array: 400 blocks, 33 pages per block, 513 bytes per page

    public static void SetBlockData(int blockNumber, int pageNumber, int byteIndex, byte data)
    {
        // Validate blockNumber, pageNumber, and byteIndex
        if (blockNumber < 0 || blockNumber >= 400) // Assuming 400 blocks
            throw new ArgumentOutOfRangeException(nameof(blockNumber), "Block number must be between 0 and 399.");

        if (pageNumber < 0 || pageNumber >= 33) // 33 pages per block
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be between 0 and 32.");

        if (byteIndex < 0 || byteIndex >= 513) // 513 bytes per page
            throw new ArgumentOutOfRangeException(nameof(byteIndex), "Byte index must be between 0 and 512.");

        // Set the data at the specified block, page, and byte position
        Blockpages[blockNumber, pageNumber, byteIndex] = data;
    }

    public static byte[,] GetBlockData(int blockNumber)
    {
        // Validate blockNumber
        if (blockNumber < 0 || blockNumber >= 400)
            throw new ArgumentOutOfRangeException(nameof(blockNumber), "Block number must be between 0 and 399.");

        // Create a 2D array to store 32 pages (each page has 512 bytes)
        var blockData = new byte[32, 512];

        // Loop through each page and copy the data for the block
        for (int pageNumber = 0; pageNumber < 32; pageNumber++)
        {
            for (int byteIndex = 0; byteIndex < 512; byteIndex++)
            {
                blockData[pageNumber, byteIndex] = Blockpages[blockNumber, pageNumber, byteIndex];
            }
        }

        return blockData; // Return the entire 2D array containing 32 pages of data
    }

    // Method to update data for a specific page
    public static void UpdatePageData(int pageNumber, byte[] pageData)
    {
        // Validate pageNumber and pageData length
        // if (pageNumber < 0 || pageNumber >= 32)
        //    throw new ArgumentOutOfRangeException("Page number must be between 0 and 31.");

        // Update the specific page with the given data
        for (int i = 0; i < pageData.Length; i++)
            pages[pageNumber, i] = pageData[i];
    }

    // Method to retrieve data from a specific page
    public static byte[] GetPageData(int pageNumber, int pageLength)
    {
        // Validate pageNumber
        // if (pageNumber < 0 || pageNumber >= 32)
        //   throw new ArgumentOutOfRangeException("Page number must be between 0 and 31.");

        // Retrieve the data from the specific page
        var pageData = new byte[pageLength];

        for (int i = 0; i < pageLength; i++)
            pageData[i] = pages[pageNumber, i];

        return pageData;
    }
}