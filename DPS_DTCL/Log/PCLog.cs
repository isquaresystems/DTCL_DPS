using DTCL.Transport;
using System;
using System.Collections.Generic;
using System.IO;

namespace DTCL.Log
{
    public class PCResult
    {
        public string loopBackResult { get; set; }
        public string eraseResult { get; set; }
        public string writeResult { get; set; }
        public string readResult { get; set; }

        public string loopBackTestName { get; private set; }
        public string eraseTestName { get; private set; }
        public string writeTestName { get; private set; }
        public string readTestName { get; private set; }

        public string loopBackTestTime { get; set; }
        public string eraseTestTime { get; set; }
        public string writeTestTime { get; set; }
        public string readTestTime { get; set; }

        public PCResult()
        {
            loopBackTestName = "LoopBack";
            eraseTestName = "Erase";
            writeTestName = "Write";
            readTestName = "Read";

            // Initialize test times to current time
            var now = DateTime.Now;
            loopBackTestTime = now.ToString();
            eraseTestTime = now.ToString();
            writeTestTime = now.ToString();
            readTestTime = now.ToString();
        }
    }

    public class PCLog
    {
        static PCLog _instance;
        static readonly object _lockObject = new object();
        public Dictionary<int, string> LogFileNameList = new Dictionary<int, string>();
        // Singleton instance
        public static PCLog Instance
        {
            get
            {
                // Ensure thread safety when creating the instance
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        if (_instance == null)
                        {
                            _instance = new PCLog();
                        }
                    }
                }

                return _instance;
            }
        }

        private PCLog()
        {
        }
        public string LogType { get; set; }

        public SlotInfo[] _slotInfo { get; set; }

        string cartName;

        public void SetCartType(CartType cartType)
        {
            switch (cartType)
            {
                case CartType.Darin1:
                    cartName = "Darin-I";
                    break;
                case CartType.Darin2:
                    cartName = "Darin-II";
                    break;
                case CartType.Darin3:
                    cartName = "Darin-III";
                    break;
            }
        }

        public void CreateNewLog(string testNumber, string inspectorName, string dtcSiNo, string unitSiNo, bool withCart, SlotInfo slotInfo, int ChNo = 0)
        {
            var currentPath = Directory.GetCurrentDirectory();
            string logDirectory;

            if (HardwareInfo.Instance.BoardId != "DTCL")
            {
                logDirectory = Path.Combine(currentPath, "PC_Log");

                if (slotInfo.SlotNumber != 0)
                    logDirectory = Path.Combine(logDirectory, "Slot-" + slotInfo.SlotNumber.ToString());
            }
            else
            {
                logDirectory = Path.Combine(currentPath, "PC_Log");
            }

            var OldLogDirectory = Path.Combine(logDirectory, "TestLog");

            if (ChNo != 0)
            {
                logDirectory = Path.Combine(currentPath, "Mux");
                logDirectory = Path.Combine(logDirectory, "ChannelNo-" + ChNo.ToString());
                OldLogDirectory = Path.Combine(logDirectory, "OldLog");
            }

            LogType = "New";

            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            if (!Directory.Exists(OldLogDirectory))
            {
                Directory.CreateDirectory(OldLogDirectory);
            }

            MoveAndRenameLogs(logDirectory, OldLogDirectory);

            slotInfo.SlotPCLogName = Path.Combine(logDirectory, $"{testNumber}_log.txt");

            using (FileStream fs = new FileStream(slotInfo.SlotPCLogName, FileMode.Create, FileAccess.Write))
            using (StreamWriter writer = new StreamWriter(fs))
            {
                writer.WriteLine("                                               TEST REPORT");
                WriteLogHeader(writer, testNumber, inspectorName, dtcSiNo, unitSiNo, withCart, slotInfo);
                writer.Close();
            }

            Log.Info($"New Log Created with Name {slotInfo.SlotPCLogName}");

            if (ChNo != 0)
                LogFileNameList.Add(ChNo, slotInfo.SlotPCLogName);
        }

        public void EditLogHeaderDateTime(SlotInfo slotInfo)
        {
            var logLines = System.IO.File.ReadAllLines(slotInfo.SlotPCLogName);

            // Find the lines with "Date" and "Time" and modify them
            for (int i = 0; i < logLines.Length; i++)
            {
                if (logLines[i].StartsWith("Date"))
                {
                    logLines[i] = $"Date            : {DateTime.Now.ToString("dd-MM-yyyy")}";
                }
                else if (logLines[i].StartsWith("Time"))
                {
                    logLines[i] = $"Time            : {DateTime.Now.ToString("HH-mm-ss")}";
                }
            }

            System.IO.File.WriteAllLines(slotInfo.SlotPCLogName, logLines);
        }

        public void EditIterationDurationType(int iteration, int duration, SlotInfo slotInfo)
        {
            var logLines = System.IO.File.ReadAllLines(slotInfo.SlotPCLogName);
            // Find the lines with "Date" and "Time" and modify them
            for (int i = 0; i < logLines.Length; i++)
            {
                if (logLines[i].StartsWith("Iteration Number"))
                {
                    logLines[i] = $"Iteration Number: {iteration}   Duration        : {duration}";
                }
            }

            System.IO.File.WriteAllLines(slotInfo.SlotPCLogName, logLines);
        }

        public void AddIterationDuration(int iteration, int duration, SlotInfo slotInfo)
        {
            // Check if the log file exists
            if (!LogFileExists(slotInfo.SlotPCLogName))
            {
                Log.Error("Log file not found.");
                return;
            }

            try
            {
                using (FileStream fs = new FileStream(slotInfo.SlotPCLogName, FileMode.Append, FileAccess.Write))
                using (StreamWriter writer = new StreamWriter(fs))
                {
                    writer.WriteLine($"Iteration Number: {iteration}   Duration        : {duration}");
                    writer.WriteLine("\n");
                    writer.Close();
                    fs.Close();
                }

                return;
            }
            catch (Exception ex)
            {
                Log.Error($"Error adding performance response to log: {ex.Message}");
                return;
            }
        }

        public void AppendToOldLog(string testNumber, string inspectorName, string dtcSiNo, string unitSiNo, bool withCart, SlotInfo slotInfo)
        {
            LogType = "Old";

            var currentPath = Directory.GetCurrentDirectory();
            var logDirectory = Path.Combine(currentPath, "PC_Log");

            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            slotInfo.SlotPCLogName = Path.Combine(logDirectory, "OldLog.txt");

            if (!File.Exists(slotInfo.SlotPCLogName))
            {
                using (FileStream fs = new FileStream(slotInfo.SlotPCLogName, FileMode.Create, FileAccess.Write))
                    fs.Close();
            }

            using (FileStream fs = new FileStream(slotInfo.SlotPCLogName, FileMode.Append, FileAccess.Write))
            using (StreamWriter writer = new StreamWriter(fs))
            {
                WriteLogHeader(writer, testNumber, inspectorName, dtcSiNo, unitSiNo, withCart, slotInfo);
                writer.Close();
            }
        }

        void WriteLogHeader(StreamWriter writer, string testNumber, string inspectorName, string dtcSiNo, string unitSiNo, bool withCart, SlotInfo slotInfo)
        {
            writer.WriteLine("----------------------------------------------------------------------------------------------------");
            writer.WriteLine($"Date            : {DateTime.Now:dd-MM-yyyy}");
            writer.WriteLine($"Time            : {DateTime.Now:HH-mm-ss}");
            writer.WriteLine($"Unit SI No      : {unitSiNo}");

            if (withCart)
            {
                writer.WriteLine($"DTC SI No       : {dtcSiNo}");
            }

            writer.WriteLine($"Test No         : {testNumber}");
            writer.WriteLine($"Inspector's Name: {inspectorName}");

            if (withCart)
            {
                writer.WriteLine($"DTC             : {slotInfo.DetectedCartTypeAtSlot}");
            }

            writer.WriteLine($"Iteration Number: 0   Duration        : 0");
            writer.WriteLine("");
            writer.WriteLine("Test             Itrn No         PASS/FAIL          Date          Time");
        }

        public void MoveAndRenameLogs(string sourceFolder, string destinationFolder)
        {
            // Find all files matching the pattern *_log.txt
            var logPattern = "*_log.txt";
            var logFiles = Directory.GetFiles(sourceFolder, logPattern);
            string date, time;

            foreach (var logFile in logFiles)
            {
                try
                {
                    // Extract the test number from the file name
                    var testNumber = Path.GetFileNameWithoutExtension(logFile).Split('_')[0];

                    // Extract date and time from log file content
                    (date, time) = ExtractDateTimeFromLog(logFile);

                    // Create the new file name with the extracted test number and date/time
                    var newFileName = $"{testNumber}_log_" + date + "_" + time + ".txt";
                    var destinationPath = Path.Combine(destinationFolder, newFileName);

                    // Move and rename the file
                    File.Move(logFile, destinationPath);
                    Log.Info($"Moved and renamed {logFile} to {destinationPath}");
                }
                catch (Exception ex)
                {
                    Log.Error($"Error processing file {logFile}: {ex.Message}");
                }
            }
        }

        (string, string) ExtractDateTimeFromLog(string logFilePath)
        {
            try
            {
                var logLines = File.ReadAllLines(logFilePath);

                // Extract date and time lines
                var dateLine = Array.Find(logLines, line => line.StartsWith("Date"));
                var timeLine = Array.Find(logLines, line => line.StartsWith("Time"));

                // Extract date and time values
                var dateString = dateLine.Split(':')[1].Trim();
                var timeString = timeLine.Split(':')[1].Trim();

                // Combine date and time into DateTime
                return (dateString, timeString);
            }
            catch (Exception ex)
            {
                Log.Error($"Error extracting date/time from log: {ex.Message}");
            }

            return (string.Empty, string.Empty);
        }

        public bool AddPerformanceResponse(bool withCart, PCResult result, int iterationCompleted, SlotInfo slotInfo)
        {
            // Check if the log file exists
            if (!LogFileExists(slotInfo.SlotPCLogName))
            {
                Log.Error("Log file not found.");
                return false;
            }

            try
            {
                using (FileStream fs = new FileStream(slotInfo.SlotPCLogName, FileMode.Append, FileAccess.Write))
                using (StreamWriter writer = new StreamWriter(fs))
                {
                    var entry = CreatePerformanceLogEntry(withCart, result.loopBackResult, iterationCompleted, result.loopBackTestName, result.loopBackTestTime);

                    if (!withCart)
                    {
                        writer.WriteLine(entry);
                        writer.Close();
                        fs.Close();
                    }
                    else
                    {
                        var entry1 = CreatePerformanceLogEntry(withCart, result.eraseResult, iterationCompleted, result.eraseTestName, result.eraseTestTime);
                        var entry2 = CreatePerformanceLogEntry(withCart, result.writeResult, iterationCompleted, result.writeTestName, result.writeTestTime);
                        var entry3 = CreatePerformanceLogEntry(withCart, result.readResult, iterationCompleted, result.readTestName, result.readTestTime);
                        writer.WriteLine(entry);
                        writer.WriteLine(entry1);
                        writer.WriteLine(entry2);
                        writer.WriteLine(entry3);
                        writer.Close();
                        fs.Close();
                    }
                }

                Log.Info("Performance response added to the log.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Error adding performance response to log: {ex.Message}");
                return false;
            }
        }

        public void AddEntry(string Data, SlotInfo slotInfo)
        {
            // Check if the log file exists
            if (!LogFileExists(slotInfo.SlotPCLogName))
            {
                Log.Error("Log file not found.");
                return;
            }

            try
            {
                using (FileStream fs = new FileStream(slotInfo.SlotPCLogName, FileMode.Append, FileAccess.Write))
                using (StreamWriter writer = new StreamWriter(fs))
                {
                    writer.WriteLine("\n");
                    writer.WriteLine(Data);
                    writer.WriteLine("----------------------------------------------------------------------------------------------------\n");
                    writer.Close();
                    fs.Close();
                }

                Log.Info($"Data Added to Log:{Data}");
                return;
            }
            catch (Exception ex)
            {
                Log.Error($"Error adding performance response to log: {ex.Message}");
                return;
            }
        }

        string CreatePerformanceLogEntry(bool withCart, string logResult, int iterationCompleted, string testName, string testTime)
        {
            var entry = string.Empty;

            if (testName.Equals("LoopBack", StringComparison.OrdinalIgnoreCase))
            {
                entry = testName + "         ";
            }
            else if (testName.Equals("Read", StringComparison.OrdinalIgnoreCase))
            {
                entry = testName + "             ";
            }
            else
            {
                entry = testName + "            ";
            }

            // entry = withCart == true ? $"{testName}    " : $"{testName}            ";

            entry += iterationCompleted;
            entry += "   "; // Space between columns

            if (logResult.Equals("PASS", StringComparison.OrdinalIgnoreCase))
            {
                entry += "              PASS";
            }
            else if (logResult.Equals("FAIL", StringComparison.OrdinalIgnoreCase))
            {
                entry += "              FAIL";
            }

            entry += "   ";
            entry += $"          {DateTime.Now:dd-MM-yyyy}";
            entry += $"    {testTime}";

            return entry;
        }

        public bool LogFileExists(string filePath) => File.Exists(filePath);
    }
}