using System;
using System.Collections.Generic;
using System.Linq;

namespace DTCL.Messages
{
    public class UploadMessageInfo : IMessageInfo
    {
        public string FileName { get; set; }
        public int MsgID { get; set; }
        public int NobWordPos { get; set; }
        public int NobSize { get; set; }
        public int Nob { get; set; }
        public int MaxNob { get; set; }
        public int HeaderFileSize { get; set; }
        public int isDefinedBitPos { get; set; }
        public int ActualFileSize { get; set; }
        public int ActualFileNOB { get; set; }
        public int ActualFileLastPageSize { get; set; }
        public int ActualFilePageSize { get; set; }
        public int ActualFileNoOfPages { get; set; }
        public int ActualFileNoOfPagesLastBlock { get; set; }
        public bool isDefinedInHeader { get; set; }
        public bool isFileValid { get; set; }
        public bool isFileExists { get; set; }
        public int fsb { get; set; }
        public int PreFixedNoOfBlocks { get; set; }
        public int NoOfBlocks { get; set; }
        public bool isUploadFile { get; set; }
    }
    public class UploadMessageInfoContainer : IMessageInfoContainer
    {
        // Changed the type to List<DownloadMessageInfo> to allow proper deserialization
        public List<UploadMessageInfo> MessageInfoList { get; set; }

        // Implementing the interface property explicitly
        List<IMessageInfo> IMessageInfoContainer.MessageInfoList
        {
            get => MessageInfoList.Cast<IMessageInfo>().ToList();
            set => MessageInfoList = value.Cast<UploadMessageInfo>().ToList();
        }

        public IMessageInfo FindMessageByFileName(string fileName)
        {
            return MessageInfoList.FirstOrDefault(msg =>
                msg.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        }

        public IMessageInfo FindMessageByMsgId(int msgId)
        {
            return MessageInfoList.FirstOrDefault(msg => msg.MsgID == msgId);
        }
    }
}