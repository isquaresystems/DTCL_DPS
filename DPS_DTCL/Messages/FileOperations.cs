using DTCL.Transport;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DTCL.Messages
{
    public class FileOperations
    {
        public static bool isFileExist { get; set; }

        public static bool IsFileExist(string _filePath)
        {
            if (File.Exists(_filePath))
            {
                isFileExist = true;
            }
            else
            { isFileExist = false; }

            return isFileExist;
        }

        public static bool CompareFiles(string filename1, string filename2)
        {
            try
            {
                // Check if both files exist
                if (!File.Exists(filename1) || !File.Exists(filename2))
                {
                    Log.Log.Error($"File don't exist for compare:{filename1} or {filename2}");
                    return false;
                }

                // Compare the files by their sizes first
                var fileInfo1 = new FileInfo(filename1);
                var fileInfo2 = new FileInfo(filename2);

                if (fileInfo1.Length != fileInfo2.Length)
                {
                    Log.Log.Error($"File length mismatch:{fileInfo1.Length} and {fileInfo2.Length}");
                    return false;
                }

                // Open the files and compare their contents
                using (FileStream fs1 = File.OpenRead(filename1))
                using (FileStream fs2 = File.OpenRead(filename2))
                {
                    int byte1, byte2;

                    do
                    {
                        byte1 = fs1.ReadByte();
                        byte2 = fs2.ReadByte();
                    }
                    while (byte1 == byte2 && byte1 != -1);

                    Log.Log.Info($"File compare pass :{filename1} and {filename2}");
                    // If we reached the end of both files without a mismatch, they are equal
                    return byte1 == -1 && byte2 == -1;
                }
            }
            catch
            {
                // Handle exceptions (e.g., file not found, access denied) and return false
                return false;
            }
        }

        public static int compareDir(string dir1, string dir2)
        {
            try
            {
                // Get the file names and sizes in both directories
                var dirInfo1 = new DirectoryInfo(dir1);
                var dirInfo2 = new DirectoryInfo(dir2);

                var files1 = dirInfo1.GetFiles("*", SearchOption.AllDirectories);
                var files2 = dirInfo2.GetFiles("*", SearchOption.AllDirectories);

                // Compare the number of files
                if (files1.Length != files2.Length)
                {
                    Log.Log.Error($"Compare failed as number of files are different");
                    return returnCodes.DTCL_CARTRIDGE_NOT_EQUAL;
                }

                // Compare file names and sizes
                foreach (FileInfo file1 in files1)
                {
                    // Get the relative path for comparison
                    var relativePath = file1.FullName.Substring(dir1.Length);
                    var file2 = new FileInfo(Path.Combine(dir2, relativePath));

                    if (!file2.Exists || file1.Length != file2.Length)
                    {
                        Log.Log.Error($"Compare failed for files : {file2.Name} and {file1.Name}");
                        return returnCodes.DTCL_CARTRIDGE_NOT_EQUAL;
                    }
                }

                // If all checks pass, directories are identical
                Log.Log.Info($"Compare passed for all files");
                return returnCodes.DTCL_SUCCESS;
            }
            catch (Exception ex)
            {
                // Handle errors (e.g., directory not found, access denied)
                Log.Log.Error($"Error comparing directories: {ex.Message}");
                return returnCodes.DTCL_CARTRIDGE_NOT_EQUAL;
            }
        }

        public static int compareD2Dir(string dir1, string dir2, IMessageInfoContainer message)
        {
            try
            {
                // Get the file names and sizes in both directories
                var dirInfo1 = new DirectoryInfo(dir1);
                var dirInfo2 = new DirectoryInfo(dir2);

                var files1 = dirInfo1.GetFiles("*", SearchOption.AllDirectories);
                var files2 = dirInfo2.GetFiles("*", SearchOption.AllDirectories);

                foreach (var msg in message.MessageInfoList)
                {
                    if (FileOperations.IsFileExist(dir1 + msg.FileName))
                    {
                        if (msg.FileName.ToLower().Contains("fpl") && !msg.FileName.Any(char.IsDigit))
                            continue;

                        if (msg.isUploadFile)
                        {
                            var res = CompareFiles(dir1 + msg.FileName, dir2 + msg.FileName);

                            if (!res)
                            {
                                Log.Log
                                    .Error($"Compare failed for files : {dir1 + msg.FileName} Size: {getFileSize(dir1 + msg.FileName)}  and {dir2 + msg.FileName} Size: {getFileSize(dir2 + msg.FileName)}");

                                return returnCodes.DTCL_CARTRIDGE_NOT_EQUAL;
                            }
                        }
                    }
                    else
                    {
                        Log.Log.Error($"Compare skipping for file : {dir1 + msg.FileName}");
                    }
                }

                // If all checks pass, directories are identical
                Log.Log.Info($"Compare passed for all files");
                return returnCodes.DTCL_SUCCESS;
            }
            catch (Exception ex)
            {
                // Handle errors (e.g., directory not found, access denied)
                Log.Log.Error($"Error comparing directories: {ex.Message}");
                return returnCodes.DTCL_CARTRIDGE_NOT_EQUAL;
            }
        }

        public static int compareD2Dir_2(string dir1, string dir2, IMessageInfoContainer message)
        {
            try
            {
                // Get the file names and sizes in both directories
                var dirInfo1 = new DirectoryInfo(dir1);
                var dirInfo2 = new DirectoryInfo(dir2);

                var files1 = dirInfo1.GetFiles("*", SearchOption.AllDirectories);
                var files2 = dirInfo2.GetFiles("*", SearchOption.AllDirectories);

                /*int totalUploadFiles_dir1 = 0;
                int totalUploadFiles_dir2 = 0;

                foreach (var msg in message.MessageInfoList)
                {
                    if ((msg.isUploadFile && msg.isDefinedInHeader) && ())
                    {
                        if (FileOperations.IsFileExist(dir1 + msg.FileName))
                        {
                            Log.Log.Debug($"Upload FileName in upload Dir: {msg.FileName} ");
                            ++totalUploadFiles_dir1;
                        }
                        if (FileOperations.IsFileExist(dir2 + msg.FileName))
                        {
                            Log.Log.Debug($"Upload FileName in download Dir: {msg.FileName} ");
                            if(msg.ActualFileSize!=0)
                            ++totalUploadFiles_dir2;
                        }
                    }
                }

                if(totalUploadFiles_dir1 != totalUploadFiles_dir2)
                {
                    Log.Log.Error($"Directories are not equal {totalUploadFiles_dir1} and {totalUploadFiles_dir2}");
                    return returnCodes.DTCL_CARTRIDGE_NOT_EQUAL;
                }*/

                foreach (var msg in message.MessageInfoList)
                {
                    if ((msg.isUploadFile) && (msg.isDefinedInHeader))
                    {
                        if (msg.FileName.ToLower().Contains("fpl") && !msg.FileName.Any(char.IsDigit))
                            continue;

                        if (!(FileOperations.IsFileExist(dir1 + msg.FileName)))
                        {
                            Log.Log
                                .Error($"Compare failed for files : {dir1 + msg.FileName} Size: {getFileSize(dir1 + msg.FileName)}  and {dir2 + msg.FileName} Size: {getFileSize(dir2 + msg.FileName)}");

                            return returnCodes.DTCL_CARTRIDGE_NOT_EQUAL;
                        }

                        if (msg.isUploadFile)
                        {
                            var res = CompareFiles(dir1 + msg.FileName, dir2 + msg.FileName);

                            if (!res)
                            {
                                Log.Log
                                    .Error($"Compare failed for files : {dir1 + msg.FileName} Size: {getFileSize(dir1 + msg.FileName)}  and {dir2 + msg.FileName} Size: {getFileSize(dir2 + msg.FileName)}");

                                return returnCodes.DTCL_CARTRIDGE_NOT_EQUAL;
                            }
                        }
                    }
                    else
                    {
                        Log.Log.Error($"Compare skipping for file : {dir1 + msg.FileName}");
                    }
                }

                // If all checks pass, directories are identical
                Log.Log.Info($"Compare passed for all files");
                return returnCodes.DTCL_SUCCESS;
            }
            catch (Exception ex)
            {
                // Handle errors (e.g., directory not found, access denied)
                Log.Log.Error($"Error comparing directories: {ex.Message}");
                return returnCodes.DTCL_CARTRIDGE_NOT_EQUAL;
            }
        }

        public static void Copy(string dir1, string dir2)
        {
            createDir(Path.GetDirectoryName(dir2));

            if (File.Exists(dir1))
            {
                if (dir1.Equals(dir2))
                {
                    Log.Log.Info($"Source {dir1} and destination {dir2} are same, skipping.");
                }
                else
                    File.Copy(dir1, dir2, true);

                Log.Log.Info($"Copied {dir1} to {dir2}.");
            }
            else
            {
                Log.Log.Error($"File {dir1} does not exist.");
            }
        }

        public static int getFileSize(string name)
        {
            var size = 0;

            if (File.Exists(name))
            {
                var fs = new FileStream(name, FileMode.Open);
                size = (int)fs.Length;
                fs.Close();
            }

            return size;
        }

        public static void deleteAndCreateDir(string dir)
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);

            Directory.CreateDirectory(dir);
        }

        public static bool isDirExists(string dir)
        {
            if (Directory.Exists(dir))
                return true;
            else
                return false;
        }

        public static void deleteDir(string dir)
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }

        public static void createDir(string dir)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                Log.Log.Info($"Directory Created:{dir}");
            }
            else
            {
                Log.Log.Info($"Directory already Exists:{dir}");
            }
        }

        public static void deleteFile(string file)
        {
            if (File.Exists(file))
                File.Delete(file);
        }

        public static List<string> getBinFileNames(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException("Directory path cannot be null or whitespace.", nameof(directoryPath));
            }

            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"The directory '{directoryPath}' does not exist.");
            }

            var binFileNames = new List<string>();
            var binFiles = Directory.GetFiles(directoryPath, "*.bin", SearchOption.TopDirectoryOnly);

            foreach (var file in binFiles)
                binFileNames.Add(Path.GetFileName(file));

            return binFileNames;
        }

        public static int getDirectorySize(string dir)
        {
            var di = new System.IO.DirectoryInfo(dir);
            var directory = di.GetFiles("*.bin");
            var totalSize = 0;

            for (short i = 0; i < directory.Length; i++)
            {
                var fs = new FileStream(dir + directory[i].Name, FileMode.Open);
                totalSize = totalSize + (int)fs.Length;
                fs.Close();
            }

            return totalSize;
        }

        public static byte[] ReadFileData(string fileName, int offset, int count)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                Log.Log.Error("File name cannot be null or empty " + nameof(fileName));
                return null;
            }

            if (!File.Exists(fileName))
            {
                Log.Log.Error("The specified file does not exist " + fileName);
                return null;
            }

            try
            {
                var buffer = new byte[count];

                using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                {
                    fs.Seek(offset, SeekOrigin.Begin);
                    var bytesRead = fs.Read(buffer, 0, count);

                    if (bytesRead < count)
                    {
                        Array.Resize(ref buffer, bytesRead);
                    }

                    fs.Close();
                }

                return buffer;
            }
            catch (IOException ex)
            {
                Log.Log.Error("Error reading file data " + ex);
                return null;
            }
        }

        public static void WriteFileData(byte[] data, string fileName, int offset)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException("File name cannot be null or empty", nameof(fileName));
            }

            try
            {
                using (FileStream fs = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    fs.Seek(offset, SeekOrigin.Begin);
                    fs.Write(data, 0, data.Length);
                    fs.Close();
                }
            }
            catch (IOException ex)
            {
                throw new Exception("Error writing file data", ex);
            }
        }
    }
}