using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Connection
{
    public enum ChannelPriority
    {
        LOW, NORMAL, HIGH
    }
    public enum ChannelMessageTypes
    {
        BINARY, TEXT, JSON
    }
    public class Channel
    {

        public Channel(Channel copyfrom = null)
        {
            if (copyfrom == null)
            {
                Chunksizes = 1024;
                Priority = ChannelPriority.NORMAL;
                MessageType = ChannelMessageTypes.TEXT;
                Name = "";
                ChannelNumber = -1;
            }
            else
            {
                Chunksizes = copyfrom.Chunksizes;
                Name = copyfrom.Name;
                Priority = copyfrom.Priority;
                MessageType = copyfrom.MessageType;
                ChannelNumber = copyfrom.ChannelNumber;
            }

            Filestreamer = null;
            outgoing = new SocketMessage(Chunksizes);
        }

        public Channel(int chunksize, string channelname, ChannelPriority priority, ChannelMessageTypes messagetype)
        {
            Chunksizes = chunksize;
            Name = channelname;
            Priority = priority;
            MessageType = messagetype;
            outgoing = new SocketMessage(chunksize);
        }

        public bool Queue(string message)
        {
            if( OutgoingQueue.Count == 0)
            {
                SetupOutGoing(message);
            }
            else
            {
                OutgoingQueue.Enqueue(message);
            }
            return true;
        }
        
        public bool Queue(FileStream filestream)
        {
            
            return true;
        }

        private void SetupOutGoing(string message)
        {
            //todo the encoding is Unicode, which requires 4 bytes per characters
            CurrentOutGoingQueuedMessage = Encoding.Unicode.GetBytes(message);
            int len = CurrentOutGoingQueuedMessage.Length;
            if(len <= Chunksizes )
            {
                TotalOutgoingMessages = 1;
            }
            else
            {
                TotalOutgoingMessages = (int)Math.Floor( (decimal)(len / Chunksizes) );
                CurrentPosition = 0;
                if (len % Chunksizes > 0)
                {
                    TotalOutgoingMessages++;
                }
            }
            RemainingBytes = len;
            TotalBytes = len;
            CurrentOutgoingMessageNumber = 1;
        }

        public bool HasMoreToSend()
        {
            if (CurrentOutgoingMessageNumber > 0 && TotalOutgoingMessages > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public ArraySegment<byte> GetNextChunk()
        {
            int chunky = 0;
            if(RemainingBytes >= Chunksizes)
            {
                chunky = Chunksizes;
            }
            else
            {
                chunky = RemainingBytes;
            }

            outgoing.Encode(CurrentOutGoingQueuedMessage, chunky, CurrentOutgoingMessageNumber, TotalOutgoingMessages, ChannelNumber);

            CurrentOutgoingMessageNumber++;
            if (CurrentOutgoingMessageNumber > TotalOutgoingMessages)
            {
                CurrentOutgoingMessageNumber = 0;
                TotalOutgoingMessages = 0;

                if (OutgoingQueue.Count > 0)
                {
                    SetupOutGoing(OutgoingQueue.Dequeue());
                }
            }
            return outgoing.GetEncodedMessage();
           // return new ArraySegment<byte>(outgoing.buffer,0,outgoing.buffer.Length);
        }
        public void GiveChunk(SocketMessage socketmessage)
        {
            IncomingString.Append(Encoding.Unicode.GetString(socketmessage.GetMessage().ToArray())); // not sure how efficient this is
            
            if (socketmessage.TotalMessages == socketmessage.MessageNumber) // finished
            {
                MessageCompleteEvent(new MessageEventArgs
                    (Guid.Empty,
                    this.IncomingString.ToString(),
                    MessageTypes.MESSAGERECEIVED, this.Name));
                this.IncomingString.Clear();
            }
          //  socketmessage.GetMessage();
          //  socketmessage.MessageLength
        }

        public int ChannelNumber { get; set; }
        public FileStream Filestreamer { get; set; }
        public int Chunksizes { get; set; }
        public string Name { get; set; }
        public ChannelPriority Priority { get; set; }
        public ChannelMessageTypes MessageType { get; set; }

        // currently bytes left to send;
        private int RemainingBytes = 0;
        // position in the byte array we are sending from
        private int CurrentPosition = 0;
        // total bytes in array
        private int TotalBytes = 0;
        // current outgoing message number
        private int CurrentOutgoingMessageNumber = 0;
        // total outgoing messages
        private int TotalOutgoingMessages = 0;
        // used to encode headers into outgoing messages
        public SocketMessage outgoing;
        // each time you pop from queue, it converts it into an array and copies into this. then next chunk will pull from this.
        byte[] CurrentOutGoingQueuedMessage;
        bool MessageInProgress { get; set; }

        Queue<string> OutgoingQueue = new Queue<string>();
        StringBuilder IncomingString = new StringBuilder();
        // emit when channel completes a message.

        public event MessageEventHandler MessageComplete;
        protected virtual void MessageCompleteEvent(MessageEventArgs e)
        {
            MessageComplete?.Invoke(this, e);
        }
    }
}