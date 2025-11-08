using System;
using System.IO;

namespace DTCL.Log
{
    public static class Log
    {
        static readonly object LockObject = new object();

        public static LogLevel MinimumLogLevel { get; set; } = LogLevel.Info;
        public static string LogFilePath { get; set; } = "DebugLog.txt";

        public static void SetLogLevel(LogLevel level) => MinimumLogLevel = level;

        public static void Info(string message) => LogMessage(LogLevel.Info, message);

        public static void Debug(string message) => LogMessage(LogLevel.Debug, message);

        public static void Warning(string message) => LogMessage(LogLevel.Warning, message);

        public static void Error(string message, Exception ex = null) => LogMessage(LogLevel.Error, FormatErrorMessage(message, ex));

        public static void Data(string description, byte[] data) => LogMessage(LogLevel.Data, FormatDataMessage(description, data));

        static void LogMessage(LogLevel level, string message)
        {
            if (level >= MinimumLogLevel)
            {
                var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
                WriteToFile(logEntry);
                OnMessageLogged(new LogMessageEventArgs(logEntry));
            }
        }

        static void WriteToFile(string logEntry)
        {
            try
            {
                lock (LockObject)
                {
                    using (StreamWriter writer = new StreamWriter(LogFilePath, true))
                        writer.WriteLine(logEntry);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }

        static string FormatErrorMessage(string message, Exception ex)
        {
            return ex != null
                ? $"{message}: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
                : message;
        }

        static string FormatDataMessage(string description, byte[] data)
        {
            return data != null
                ? $"{description}: {BitConverter.ToString(data)}"
                : description;
        }

        public static event EventHandler<LogMessageEventArgs> MessageLogged;

        static void OnMessageLogged(LogMessageEventArgs e) => MessageLogged?.Invoke(null, e);
    }

    public enum LogLevel
    {
        Debug=0,
        Info,
        Warning,
        Error,
        Data
    }

    public class LogMessageEventArgs : EventArgs
    {
        public string Message { get; }
        public LogMessageEventArgs(string message) => Message = message;
    }
}