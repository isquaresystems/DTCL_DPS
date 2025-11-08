using System.Collections.Generic;

namespace DTCL.Log
{
    public class PopUpMessages
    {
        public string MessageId { get; set; }
        public string MessageText { get; set; }
        public string MessageBoxIcon { get; set; }
        public string MessageBoxButtons { get; set; }
        public int FontSize { get; set; }
    }
    public class PopUpMessagesContainer
    {
        public List<PopUpMessages> PopUpMessagesList { get; set; }

        public PopUpMessages FindMessageById(string messageId)
        {
            return PopUpMessagesList.Find(msg => msg.MessageId == messageId);
        }

        public string FindStatusMsgById(string messageId)
        {
            var temp = PopUpMessagesList.Find(msg => msg.MessageId == messageId);
            return temp.MessageText;
        }
    }
}