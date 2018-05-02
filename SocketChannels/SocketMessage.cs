using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Connection
{
    public class SocketMessage
    {
        // message 1 of 20
        // 
        // ushort msg1, of up to 65,000. 1 byte channel number,

        // header
        // [2 bytes = message # of , 2 bytes = totalmessages, 1 byte = channel number] 5 bytes total.
        // This class is built for use with a web socket protocol, and because WS guarantees a 
        // complete message on receive you can simply check the message size to get total bytes.
         // it is redunant to send this information.
         //
        public byte[] buffer { get; set; }
        public static int offset = sizeof(ushort) * 3;
        int buffersize;
        public int ChannelNumber { get; set; }
        public ushort TotalMessages { get; set; }
        public ushort MessageNumber { get; set; }
        public ushort MessageLength { get; set; }

        public bool ValidHeader { get; private set; }

        public SocketMessage(int byteChunkSize)
        {
            buffersize = byteChunkSize;
            buffer = new byte[buffersize + offset];
            ValidHeader = false;
            MessageNumber = 0;
            ChannelNumber = 0;
            TotalMessages = 0;
            MessageLength = 0;
        }

        public ArraySegment<byte> GetMessage()
        {
            if(MessageLength == 0)
            {
                Console.WriteLine("Attempting to get zero length message");
                return new ArraySegment<byte>(buffer, offset, 0);
            }
            return new ArraySegment<byte>(buffer, offset, MessageLength - offset);
        }
        public ArraySegment<byte> GetEncodedMessage()
        {
            return new ArraySegment<byte>(buffer, 0, MessageLength + offset);
        }

        public ArraySegment<byte> GetBuffer()
        {
            return new ArraySegment<byte>(buffer, 0, buffersize+offset);
        }
       
        public void Encode(byte[] message, int messagelength, int messagenumber, int totalmessages, int channelnumber)
        {
                byte[] messagebytes = BitConverter.GetBytes((ushort)messagenumber);
                byte[] totalmessagebytes = BitConverter.GetBytes((ushort)totalmessages);
                byte[] channelnumberbytes = BitConverter.GetBytes((ushort)channelnumber);

                ushort one = BitConverter.ToUInt16(messagebytes,0);// Convert.ToInt16(messagebytes);
                ushort two = BitConverter.ToUInt16(totalmessagebytes, 0);
                ushort three = BitConverter.ToUInt16(channelnumberbytes, 0);
                int shortsize = sizeof(ushort);
                Buffer.BlockCopy(messagebytes, 0, buffer, shortsize * 0, shortsize);
                Buffer.BlockCopy(totalmessagebytes, 0, buffer, shortsize * 1, shortsize);
                Buffer.BlockCopy(channelnumberbytes, 0, buffer, shortsize * 2, shortsize);
                Buffer.BlockCopy(message, 0, buffer, shortsize * 3, messagelength);
                MessageLength = (ushort)messagelength;
                //Console.WriteLine(message);
        }

        // this method assumes you've already set the internal buffer and MessageLenth, this will fail if you haven't
        public void Encode(int messagenumber, int totalmessages, int channelnumber)
        {
            byte[] messagebytes = BitConverter.GetBytes((ushort)messagenumber);
            byte[] totalmessagebytes = BitConverter.GetBytes((ushort)totalmessages);
            byte[] channelnumberbytes = BitConverter.GetBytes((ushort)channelnumber);

            ushort one = BitConverter.ToUInt16(messagebytes, 0);// Convert.ToInt16(messagebytes);
            ushort two = BitConverter.ToUInt16(totalmessagebytes, 0);
            ushort three = BitConverter.ToUInt16(channelnumberbytes, 0);
            int shortsize = sizeof(ushort);
            Buffer.BlockCopy(messagebytes, 0, buffer, shortsize * 0, shortsize);
            Buffer.BlockCopy(totalmessagebytes, 0, buffer, shortsize * 1, shortsize);
            Buffer.BlockCopy(channelnumberbytes, 0, buffer, shortsize * 2, shortsize);
            //Console.WriteLine(message);
        }


        public bool Decode()
        {
            MessageNumber = BitConverter.ToUInt16(buffer, 0);
            TotalMessages = BitConverter.ToUInt16(buffer, sizeof(ushort));
            ChannelNumber = BitConverter.ToUInt16(buffer, sizeof(ushort) * 2);

            Console.WriteLine($"Decode: MsgNumber {MessageNumber} TotalMessages {TotalMessages} ChanneNumber {ChannelNumber}");

            if (MessageNumber >= 1 && MessageNumber <= 65000 && TotalMessages > 0 && TotalMessages <= 65000
                && MessageNumber <= TotalMessages && ChannelNumber > 0 && ChannelNumber <= 16 && MessageLength > offset)
            {
                ValidHeader = true;
                return true;
            }
            else
            {
                ValidHeader = false;
                return false;
            }
        }

        public void Reset()
        {
            Array.Clear(buffer, 0, buffersize);
            MessageNumber = 0;
            ChannelNumber = 0;
            TotalMessages = 0;
            MessageLength = 0;
            ValidHeader = false;
        }
    }
}
