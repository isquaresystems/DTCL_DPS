using System;

public class ProgressEventArgs : EventArgs
{
    public string Operation { get; }
    public int BytesProcessed { get; }
    public int TotalBytes { get; }

    public ProgressEventArgs(string operation, int bytesProcessed, int totalBytes)
    {
        Operation = operation;
        BytesProcessed = bytesProcessed;
        TotalBytes = totalBytes;
    }
}