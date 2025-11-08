using DTCL.Transport;
using System.IO;
using System.Linq;
using System.Windows;

namespace DTCL.Messages
{
    internal static class HeaderInfo
    {
        // public static DTCLInfo.CartType cartType = DTCLInfo.CartType.Darin3; //default
        public static bool checkHeaderFile(string headerFilePath)
        {
            if (File.Exists(Path.Combine(headerFilePath, "DR.bin")))
            {
                return true;
            }
            else
                return false;
        }

        public static int[] ReadHeaderFileWords(string headerFilePath)
        {
            var read_bytes = new byte[64];
            var read_words = new int[33];

            if (File.Exists(Path.Combine(headerFilePath, "DR.bin")))
            {
                var fs = new FileStream(Path.Combine(headerFilePath, "DR.bin"), FileMode.Open);

                if (fs.Length != 64)
                {
                    // MessageBox.Show("Invalid Darin3 Header File");
                    fs.Close();
                    return null;
                }

                fs.Read(read_bytes, 0, (int)fs.Length);
                fs.Close();

                for (int wordNo = 1, bytePos = 0; wordNo <= 32; wordNo++)
                {
                    read_words[wordNo] = (read_bytes[bytePos] << 8) | read_bytes[bytePos + 1];
                    bytePos = bytePos + 2;
                }
            }

            return read_words;
        }

        public static int[] ReadD2HeaderFileWords(string headerFilePath)
        {
            var read_bytes = new byte[60];
            var read_words = new int[31];

            if (File.Exists(Path.Combine(headerFilePath, "DR.bin")))
            {
                var fs = new FileStream(Path.Combine(headerFilePath, "DR.bin"), FileMode.Open);

                if (fs.Length != 60)
                {
                    // MessageBox.Show("Invalid Darin2 Header File");
                    fs.Close();
                    return null;
                }

                fs.Read(read_bytes, 0, (int)fs.Length);
                fs.Close();

                for (int wordNo = 1, bytePos = 0; wordNo <= 30; wordNo++)
                {
                    read_words[wordNo] = (read_bytes[bytePos] << 8) | read_bytes[bytePos + 1];
                    bytePos = bytePos + 2;
                }
            }

            return read_words;
        }

        public static bool WriteHeaderFileWords(CartType cartType, string headerFilePath, int[] headerWords)
        {
            var headerLen = 31;
            byte[] writeBytes;

            if (cartType == CartType.Darin3)
            {
                headerLen = 33;
                writeBytes = new byte[64];
            }
            else
            {
                headerLen = 31;
                writeBytes = new byte[60];
            }

            if (headerWords.Length != headerLen)
            {
                Log.Log.Error("The headerWords array must contain exactly 33 elements (index 0 unused).");
                return false;
            }

            // Convert each word in the headerWords array to bytes (starting from index 1)
            for (int i = 1; i < headerLen; i++)
            {
                writeBytes[2 * (i - 1)] = (byte)(headerWords[i] >> 8);        // Higher byte
                writeBytes[2 * (i - 1) + 1] = (byte)(headerWords[i] & 0xFF);  // Lower byte
            }

            // Construct the file path for DR.bin
            var filePath = Path.Combine(headerFilePath, "DR.bin");

            // Write bytes to the file
            using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                fs.Write(writeBytes, 0, writeBytes.Length);

            return true;
        }

        public static int getMessageFileSizeFromHeader(int[] headerWords, IMessageInfo message)
        {
            switch (message.MsgID)
            {
                // STR
                case 4:
                    message.Nob = (headerWords[message.NobWordPos] & 0xF000) >> 12;
                    return ((headerWords[message.NobWordPos] & 0xF000) >> 12) * message.NobSize;
                case 22:
                    message.Nob = (headerWords[message.NobWordPos] & 0xF000) >> 12;
                    return ((headerWords[message.NobWordPos] & 0xF000) >> 12) * message.NobSize;

                // WP
                case 5:
                    message.Nob = (headerWords[message.NobWordPos] & 0xFF00) >> 8;
                    return ((headerWords[message.NobWordPos] & 0xFF00) >> 8) * message.NobSize;
                case 23:
                    message.Nob = (headerWords[message.NobWordPos] & 0xFF00) >> 8;
                    return ((headerWords[message.NobWordPos] & 0xFF00) >> 8) * message.NobSize;

                // THT
                case 7:
                    message.Nob = (headerWords[message.NobWordPos] & 0xFF00) >> 9;
                    return ((headerWords[message.NobWordPos] & 0xFF00) >> 9) * message.NobSize;
                case 25:
                    message.Nob = (headerWords[message.NobWordPos] & 0xFF00) >> 9;
                    return ((headerWords[message.NobWordPos] & 0xFF00) >> 9) * message.NobSize;


                case 8:
                    message.Nob = headerWords[message.NobWordPos];
                    return (headerWords[message.NobWordPos]) * message.NobSize;
                case 9:
                    message.Nob = headerWords[message.NobWordPos];
                    return (headerWords[message.NobWordPos]) * message.NobSize;

                case 10:
                    message.Nob = (headerWords[message.NobWordPos] & 0xFF00) >> 8;
                    return ((headerWords[message.NobWordPos] & 0xFF00) >> 8) * message.NobSize;
                case 11:
                    message.Nob = headerWords[message.NobWordPos] & 0xFF;
                    return ((headerWords[message.NobWordPos] & 0xFF)) * message.NobSize;
                case 12:
                    message.Nob = (headerWords[message.NobWordPos] & 0xFF00) >> 8;
                    return ((headerWords[message.NobWordPos] & 0xFF00) >> 8) * message.NobSize;
                case 13:
                    message.Nob = headerWords[message.NobWordPos] & 0xFF;
                    return ((headerWords[message.NobWordPos] & 0xFF)) * message.NobSize;

                case 14:
                    message.Nob = (headerWords[message.NobWordPos] & 0xFF00) >> 8;
                    return ((headerWords[message.NobWordPos] & 0xFF00) >> 8) * message.NobSize;
                case 15:
                    message.Nob = headerWords[message.NobWordPos] & 0xFF;
                    return ((headerWords[message.NobWordPos] & 0xFF)) * message.NobSize;
                case 16:
                    message.Nob = (headerWords[message.NobWordPos] & 0xFF00) >> 8;
                    return ((headerWords[message.NobWordPos] & 0xFF00) >> 8) * message.NobSize;
                case 18:
                    message.Nob = headerWords[message.NobWordPos] & 0xFF;
                    return ((headerWords[message.NobWordPos] & 0xFF)) * message.NobSize;

                default: return -1;
            }
        }

        public static void setMessageNOBToHeader(ref int[] headerWords, IMessageInfo message)
        {
            switch (message.MsgID)
            {
                case 4:
                    // Clear the relevant bits (upper 4 bits) and set the NOB value
                    headerWords[message.NobWordPos] = (headerWords[message.NobWordPos] & 0x0FFF) | ((message.Nob << 12) & 0xF000);
                    break;

                case 5:
                case 10:
                case 12:
                case 14:
                case 16:
                    // Clear the relevant bits (bits 8-15) and set the NOB value
                    headerWords[message.NobWordPos] = (headerWords[message.NobWordPos] & 0x00FF) | ((message.Nob << 8) & 0xFF00);
                    break;

                case 7:
                    // Clear the relevant bits (bits 9-15) and set the NOB value
                    headerWords[message.NobWordPos] = (headerWords[message.NobWordPos] & 0x007F) | ((message.Nob << 9) & 0xFF00);
                    break;

                case 8:
                case 9:
                    // Directly set the NOB value without changing other bits
                    headerWords[message.NobWordPos] = message.Nob;
                    break;

                case 11:
                case 13:
                case 15:
                case 18:
                    // Clear the relevant bits (lower 8 bits) and set the NOB value
                    headerWords[message.NobWordPos] = (headerWords[message.NobWordPos] & 0xFF00) | (message.Nob & 0x00FF);
                    break;

                default:
                    // Handle the default case if needed
                    break;
            }
        }

        public static int getFPLMessageFileSizeFromHeader(int[] headerWords, IMessageInfo message)
        {
            var temp = 0;

            // if(message.isDefinedInHeader == true)
            // {
            temp = temp + ((headerWords[message.NobWordPos] & 0x00FF) * message.NobSize);
            // }

            return temp;
        }

        public static int getFPLMessageFileNobFromHeader(int[] headerWords, IMessageInfo message)
        {
            var temp = 0;

            temp = temp + ((headerWords[message.NobWordPos] & 0x00FF));

            return temp;
        }

        public static bool UpdateMessageInfoWithHeaderData(CartType cartType, string headerFilePath, IMessageInfoContainer message)
        {
            if (!File.Exists(Path.Combine(headerFilePath, "DR.bin")))
            {
                Log.Log.Error($"Header File Does Not Exist in path:{headerFilePath}");
                return false;
            }

            int[] read_words;
            var headerSize = 64;

            if (cartType == CartType.Darin3)
            {
                read_words = ReadHeaderFileWords(headerFilePath);
                headerSize = 64;
            }
            else if (cartType == CartType.Darin2)
            {
                read_words = ReadD2HeaderFileWords(headerFilePath);

                if (read_words != null)
                {
                    var hasNonFFByte = read_words.Skip(1).Any(b => b != 0xFFFF); // Skip the first element

                    if (!hasNonFFByte)
                    {
                        return false;
                    }

                    headerSize = 60;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                MessageBox.Show("Unknown Cart try again");
                return false;
            }

            var fplCount = 0;

            if (read_words == null)
                return false;

            int fplHeaderSize = 0, fplNob = 0;

            foreach (var msg in message.MessageInfoList)
            {
                var FileName = Path.Combine(headerFilePath + msg.FileName);

                if (msg.isUploadFile == false)
                {
                    handleDownloadMsg(headerFilePath, msg);
                }
                else if (FileName.ToLower().Contains("dr"))
                {
                    msg.isFileExists = true;
                    msg.isFileValid = true;
                    msg.ActualFileNOB = 1;
                    msg.ActualFileSize = headerSize;
                    msg.ActualFileLastPageSize = 0;
                    msg.HeaderFileSize = headerSize;
                    msg.isDefinedInHeader = true;
                    PerformHeaderFileValidation(msg);
                }
                else if (FileName.Contains("fpl") && (FileName.Any(char.IsDigit)))
                {
                    GetIsFPLDefinedInHeader(ref read_words, msg);
                    msg.HeaderFileSize = getFPLMessageFileSizeFromHeader(read_words, msg);
                    msg.Nob = (read_words[msg.NobWordPos] & 0x00FF);
                    msg.isFileValid = false;

                    if (FileOperations.IsFileExist(FileName))
                    {
                        var fs = new FileStream(FileName, FileMode.Open);
                        msg.ActualFileSize = (int)fs.Length;
                        fs.Close();
                        msg.ActualFileNOB = msg.ActualFileSize / 64;
                        msg.ActualFileLastPageSize = msg.ActualFileSize - (msg.ActualFileNOB * 64);
                        msg.isFileExists = true;
                        PerformHeaderFileValidation(msg);
                    }
                    else
                    {
                        msg.isFileExists = false;
                        msg.ActualFileSize = 0;
                    }

                    if (msg.isDefinedInHeader)
                    {
                        fplHeaderSize = fplHeaderSize + msg.HeaderFileSize;
                        fplNob = fplNob + msg.Nob;
                        ++fplCount;
                    }

                    if (cartType == CartType.Darin3)
                    {
                        msg.ActualFileSize = 0;

                        if ((msg.isDefinedInHeader))
                        {
                            if ((msg.Nob <= 92))
                                msg.isFileValid = true;
                            else
                                msg.isFileValid = false;
                        }
                    }
                }
                else if (FileName.ToLower().Contains("fpl"))
                {
                    msg.isFileValid = false;

                    if (File.Exists(FileName))
                    {
                        msg.Nob = fplNob;
                        msg.isFileExists = true;
                        var fs = new FileStream(FileName, FileMode.Open);
                        msg.ActualFileSize = (int)fs.Length;
                        fs.Close();
                        msg.ActualFileNOB = msg.ActualFileSize / 64;
                        msg.ActualFileLastPageSize = msg.ActualFileSize - (msg.ActualFileNOB * 64);
                    }
                    else
                    {
                        msg.ActualFileSize = 0;
                        msg.isFileExists = false;
                    }
                }
                else if ((!FileName.ToLower().Contains("fpl")) && (!FileName.ToLower().Contains("dr")))
                {
                    msg.HeaderFileSize = getMessageFileSizeFromHeader(read_words, msg);
                    msg.isDefinedInHeader = ((read_words[6] & (1 << msg.isDefinedBitPos)) != 0);

                    if (msg.isDefinedBitPos == -1)
                    {
                        msg.isDefinedInHeader = false;
                    }

                    // if (msg.isDefinedInHeader == false)
                    // {
                    if (File.Exists(FileName))
                    {
                        msg.isFileExists = true;
                        var fs = new FileStream(FileName, FileMode.Open);
                        msg.ActualFileSize = (int)fs.Length;
                        fs.Close();
                    }
                    else
                    {
                        msg.isFileExists = false;
                        msg.ActualFileSize = 0;
                    }
                    // }

                    if (msg.isDefinedInHeader == true)
                    {
                        if (File.Exists(FileName))
                        {
                            msg.isFileExists = true;
                            var fs = new FileStream(FileName, FileMode.Open);
                            msg.ActualFileSize = (int)fs.Length;
                            fs.Close();
                            msg.ActualFileNOB = msg.ActualFileSize / 64;
                            msg.ActualFileLastPageSize = msg.ActualFileSize - (msg.ActualFileNOB * 64);

                            if (FileName.Contains("IFF"))
                            {
                                msg.HeaderFileSize = msg.HeaderFileSize - 128;
                            }

                            if (FileName.Contains("CMDS"))
                            {
                                msg.HeaderFileSize = msg.HeaderFileSize - 64;
                            }

                            if (FileName.Contains("KEY"))
                            {
                                msg.HeaderFileSize = msg.HeaderFileSize - (msg.Nob * 2 * 4);
                            }

                            if (FileName.Contains("CRY"))
                            {
                                msg.HeaderFileSize = msg.HeaderFileSize - (msg.Nob * 2 * 4);
                            }

                            if (FileName.Contains("MNE"))
                            {
                                msg.HeaderFileSize = msg.HeaderFileSize - (msg.Nob * 2 * 4);
                            }

                            PerformHeaderFileValidation(msg);
                        }
                        else
                        {
                            msg.isFileValid = false;
                            msg.isFileExists = false;
                        }
                    }
                }
            }

            foreach (var msg in message.MessageInfoList)
            {
                if (msg.FileName.ToLower().Contains("fpl") && !(msg.FileName.Any(char.IsDigit)))
                {
                    var FileName = Path.Combine(headerFilePath + msg.FileName);

                    if (fplHeaderSize > 0)
                    {
                        msg.isDefinedInHeader = true;
                    }
                    else
                    {
                        msg.isDefinedInHeader = false;
                    }

                    if (File.Exists(FileName))
                    {
                        msg.HeaderFileSize = fplHeaderSize;
                        msg.isFileExists = true;
                        var fs = new FileStream(FileName, FileMode.Open);
                        msg.ActualFileSize = (int)fs.Length;
                        msg.ActualFileNOB = msg.ActualFileSize / 64;
                        msg.ActualFileLastPageSize = msg.ActualFileSize - (msg.ActualFileNOB * 64);

                        fs.Close();
                    }
                    else
                    {
                        msg.ActualFileSize = 0;
                        msg.isFileExists = false;
                    }

                    /*if (msg.ActualFileSize >= msg.HeaderFileSize)
                    {
                        msg.isFileValid = true;
                    }
                    else
                    {
                        msg.isFileValid = false;
                        Log.Log.Error($"File Size not matching- FileName: {msg.FileName} ActualSize: {msg.ActualFileSize} HeaderFileSize: {msg.HeaderFileSize}");
                    }*/
                    PerformHeaderFileValidation(msg);
                    break;
                }
            }

            return true;
        }

        public static void SetIsDefinedInHeader(ref int[] read_words, IMessageInfo msg)
        {
            if (msg.isDefinedInHeader)
            {
                // Set the specific bit at msg.isDefinedBitPos in read_words[6] without modifying other bits
                read_words[6] |= (1 << msg.isDefinedBitPos);
            }
            else
            {
                // Clear the specific bit at msg.isDefinedBitPos in read_words[6] without modifying other bits
                read_words[6] &= ~(1 << msg.isDefinedBitPos);
            }
        }

        public static void SetFPLNob(ref int[] read_words, IMessageInfo msg)
        {
            read_words[msg.NobWordPos] = read_words[msg.NobWordPos] & 0xFF00;
            read_words[msg.NobWordPos] = read_words[msg.NobWordPos] | msg.Nob;
        }

        public static void GetIsFPLDefinedInHeader(ref int[] read_words, IMessageInfo msg)
        {
            if (msg.FileName.Any(char.IsDigit))
            {
                msg.isDefinedInHeader = ((read_words[msg.isDefinedBitPos] & 0x0800) != 0);
            }
        }

        public static void SetIsFPLDefinedInHeader(ref int[] read_words, IMessageInfo msg)
        {
            if (msg.FileName.Any(char.IsDigit))
            {
                var fplNumber = msg.FileName.Replace("fpl", "").Replace(".bin", "");
                int.TryParse(fplNumber, out int result);

                if (msg.isDefinedInHeader)
                {
                    read_words[msg.isDefinedBitPos] |= 0x0800;
                }
                else
                {
                    // Clear the specific bit at msg.isDefinedBitPos in read_words[6] without modifying other bits
                    read_words[msg.isDefinedBitPos] &= ~0x0800;
                }
            }
        }

        public static void handleDownloadMsg(string headerFilePath, IMessageInfo msg)
        {
            if (File.Exists(headerFilePath + msg.FileName))
            {
                msg.isFileExists = true;
                var fs = new FileStream(headerFilePath + msg.FileName, FileMode.Open);
                msg.ActualFileSize = (int)fs.Length;
                fs.Close();
                msg.ActualFileNOB = msg.ActualFileSize / 64;
            }
            else
            {
                msg.isFileExists = false;
                msg.ActualFileSize = 0;
                msg.ActualFileNOB = 0;
            }
        }

        public static void PerformHeaderFileValidation(IMessageInfo message)
        {
            // foreach (var message in MessageContainerObj.MessageInfoList)
            // {
            if (message.ActualFileSize == message.HeaderFileSize)
            {
                message.isFileValid = true;
            }
            else
            {
                // var Diag = Logger.ShowPopUpMessageById("Header_Missing_Msg2");

                // if (Diag == DialogResult.No)
                // {
                message.isFileValid = false;

                Log.Log
                    .Error($"File Size not matching- FileName: {message.FileName} ActualSize: {message.ActualFileSize} HeaderFileSize: {message.HeaderFileSize}");
                //     return returnCode.DPS_CMD_ABORT;
                // }
                // else
                // {
                //   message.isFileValid = true;
                // }
            }
            // }
        }
    }
}