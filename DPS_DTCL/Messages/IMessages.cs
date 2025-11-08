using System.Collections.Generic;

namespace DTCL.Messages
{
    public interface IMessageInfo
    {
        string FileName { get; set; }
        int MsgID { get; set; }
        int NobWordPos { get; set; }
        int NobSize { get; set; }
        int Nob { get; set; }
        int HeaderFileSize { get; set; }
        int isDefinedBitPos { get; set; }
        int ActualFileSize { get; set; }
        int ActualFileNoOfPages { get; set; }
        int ActualFileNoOfPagesLastBlock { get; set; }
        int ActualFileNOB { get; set; }
        int ActualFileLastPageSize { get; set; }
        int ActualFilePageSize { get; set; }
        bool isDefinedInHeader { get; set; }
        bool isFileValid { get; set; }
        bool isFileExists { get; set; }
        int fsb { get; set; }
        int PreFixedNoOfBlocks { get; set; }
        int NoOfBlocks { get; set; }
        bool isUploadFile { get; set; }

    }
    public interface IMessageInfoContainer
    {
        List<IMessageInfo> MessageInfoList { get; set; }
        IMessageInfo FindMessageByFileName(string fileName);
        IMessageInfo FindMessageByMsgId(int msgId);
    }
}