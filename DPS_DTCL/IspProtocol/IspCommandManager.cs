using System;
using System.Collections.Generic;

namespace IspProtocol
{
    public class IspCommandManager
    {
        IspBoardId mBoardId { get; set; } = IspBoardId.UNKNOWN_BOARD_ID;

        readonly List<IIspCommandHandler> handlers = new List<IIspCommandHandler>();

        public void AddHandler(IIspCommandHandler handler) => handlers.Add(handler);

        public void HandleData(byte[] payload)
        {
            foreach (var handler in handlers)
            {
                if (handler.Match(payload[0]))
                {
                    handler.Execute(payload);
                    break;
                }
            }
        }

        public void setBoardID(IspBoardId id) => mBoardId = id;

        public IspBoardId getBoardID() => mBoardId;

        public IspBoardId GetMatchedBoardId(byte input)
        {
            foreach (IspBoardId id in Enum.GetValues(typeof(IspBoardId)))
            {
                if ((byte)id == input)
                {
                    return id;
                }
            }

            return IspBoardId.UNKNOWN_BOARD_ID;
        }

        /*public void HandleReceivedData(byte[] payload)
        {
            byte command = payload[0];

            // Switch command internally
            if (command == (byte)IspCommand.TX_DATA)
            {
                command = (byte)IspCommand.RX_DATA;
            }
            else if (command == (byte)IspCommand.RX_DATA)
            {
                command = (byte)IspCommand.TX_DATA;
            }

            foreach (var handler in handlers)
            {
                if (handler.Match(command))
                {
                    handler.Execute(payload);
                    break;
                }
            }
        }*/
    }
}