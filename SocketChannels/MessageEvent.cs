using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Connection
{
    public enum MessageTypes
    {
        MESSAGERECEIVED,
        MESSAGESENT,
        SOCKETCLOSED,
        NEWCONNECTION
    };

    public class MessageEventArgs : EventArgs
    {
        public string Message { get; set; }
        public Guid RefId { get; set; }
        public MessageTypes MsgType { get; set; }
        public string Nickname { get; set; }
        public string ChannelName { get; set; }
        public System.IO.FileStream Filestream = null;
        // never send an incomplete filestream.  message should assume file is completely received and the message will no longer make reference and file is lost
        // if not kept on receiving end.
        public MessageEventArgs(Guid reference, string message, MessageTypes msgtype, string channelname, string nickname = "", System.IO.FileStream file = null)
        {
            RefId = reference;
            Message = message;
            MsgType = msgtype;
            Nickname = nickname;
            ChannelName = channelname;
            Filestream = file;
        }
    }

    public delegate void MessageEventHandler(object send, MessageEventArgs e);


}
