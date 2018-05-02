using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using Newtonsoft.Json;

namespace Connection
{
    public class BasicInfo
    {
        public string Name = "";
        public string Message = "";
    }

    public class Connection
    {
        // Size of receive buffer.  
        public const int BufferSize = 1024;
        // Receive buffer.  
        public byte[] receiveBuffer = new byte[BufferSize];
        //socket is initiated by the Webserver
        public WebSocket socket = null;
        // reference with guid, is faster lookup
        public Guid guid { get; set; }
        // reference with a nickname, can be slower to find
        public string Nickname { get; set; }
        // holds reference to channels by key
        Dictionary<string, Channel> Channels;
        Dictionary<int, Channel> ChannelByNumber;

        // used to cancel outgoing messages
        CancellationToken AbortSendToken = new CancellationToken();
        CancellationToken AbortReceiveToken = new CancellationToken();
        // a flag to check if SendAsync on websocket has returned yet.
        private bool SendInProgress = false;
        // string build used for incoming messsages that may be fragmented
        StringBuilder sb = new StringBuilder();
        // list used to build incoming binary messages that also may be fragmented.
        List<byte> bb = new List<byte>();

        SocketMessage incomingMessage = new SocketMessage(BufferSize);
        /// <summary>
        /// 
        /// </summary>
        public event MessageEventHandler MessageReceived;

        protected virtual void OnMessage(MessageEventArgs e)
        {
            e.Nickname = Nickname;
            e.RefId = guid;
            MessageReceived?.Invoke(this, e);
        }

        private void OnChannelMessage(object send, MessageEventArgs e)
        {
            OnMessage(e);
        }

        // OnMessage(new MessageEventArgs(guid, msg, MessageType.MESSAGERECEIVED, channel));

        public async Task<bool> SendMessage(string channel, string message)
        {
            Channel c = Channels[channel];
            if (c.MessageType == ChannelMessageTypes.BINARY)
            {
                return false;
            }
            c.Queue(message);
            if (!SendInProgress) // dont send if the last async send hasnt returned, just queue.
            {
                await ProcessChannelOutput(c);
            }
            return true;
        }

        // only use this function for the first message sent
        public async Task<bool> SendMessageNoChannel(string message)
        {
            SendInProgress = true;
            await socket.SendAsync(new ArraySegment<byte>(Encoding.Unicode.GetBytes(message)), WebSocketMessageType.Binary, true, AbortSendToken);
            SendInProgress = false;
            return true;
        }

        public async Task<bool> SendMessage(string channel, System.IO.FileStream filestream)
        {
            Channel c = Channels[channel];
            if(c.MessageType != ChannelMessageTypes.BINARY)
            {
                return false;
            }
            c.Queue(filestream);
            if (!SendInProgress) // dont send if the last async send hasnt returned, just queue.
            {
                await ProcessChannelOutput(c);
            }
            return true;
        }

        private async Task ProcessChannelOutput(Channel channel)
        {
            WebSocketMessageType type = WebSocketMessageType.Binary;

            while (channel.HasMoreToSend())
            {
                SendInProgress = true;
                await socket.SendAsync(channel.GetNextChunk(), type, true, AbortSendToken);
                SendInProgress = false;
            }
        }

        public Connection(ChannelGroupSettings info)
        {
            Channels = new Dictionary<string, Channel>();
            ChannelByNumber = new Dictionary<int, Channel>();

            if (info.Channels.Count < 1)
            {
                throw new Exception("You cannot have less than 1 channel in a Connection");
            }
            if ( info.Channels.Count > 16)
            {
                throw new Exception("You cannot have more than 16 channels in a Connection");
            }

            ClearBuffer();

            foreach (var c in info.Channels)
            {
                Channel ch = new Channel(c);
                ch.MessageComplete += new MessageEventHandler(this.OnChannelMessage); // attach each channel to the event handler
                Channels[c.Name] = ch;
                ChannelByNumber[c.ChannelNumber] = ch;
            }
        }

        public async Task ReceiveAsync()
        {
            try
            {
                if (socket.State != WebSocketState.Open)
                {
                    socket.Abort();
                    KillSocket("Socket state != WebSocketState.Open");
                    return;
                }

                WebSocketReceiveResult receiveResult = null;
                int received = 0;
                int offset = 6;
                do
                {
                    // incomingMessage.buffer
                    Console.WriteLine("Awaiting message");
                    receiveResult = await socket.ReceiveAsync(
                        incomingMessage.GetBuffer(), AbortReceiveToken);
                    Console.WriteLine("Received Message: " + receiveResult.Count);
                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        socket.Abort();
                        CloseSocketRequest("Request to close socket");
                        KillSocket("Request to close socket from client");
                        return;
                    }
                    if (receiveResult.MessageType == WebSocketMessageType.Text)
                    {
                        socket.Abort();
                        CloseSocketRequest("Bad data type sent from client");
                        KillSocket("Bad data type sent from client");
                        return;
                    }

                    received += receiveResult.Count;
                    if (received > BufferSize + offset)
                    {
                        Console.WriteLine("Received more than a buffer can hold");
                        return;
                    }

                } while (!receiveResult.EndOfMessage);

                Console.WriteLine("Received result: " + receiveResult.MessageType.ToString() + ": Bytes: " + receiveResult.Count  + " " + received);

                incomingMessage.MessageLength = (ushort)received;
                if (!incomingMessage.Decode())
                {
                    // bad
                    Console.WriteLine("Bad decode");
                    incomingMessage.Reset();
                    // await ReceiveAsync();
                    throw new Exception("Bad decode on socket");
                }

                // new message chunk here, figure out what to do with it.
                int channelNumber = incomingMessage.ChannelNumber;
                Channel channel = GetChannel(channelNumber);
                if(channel == null)
                {
                    // bad
                    Console.WriteLine("Trying to access a nonexistant channel number i think.");
                }
                // let channel figure out what to do with it, but dont make a reference, as we reset it right after.
                channel.GiveChunk(incomingMessage);
                incomingMessage.Reset();
                await ReceiveAsync();
            }
            catch (Exception e)
            {
                // Just log any exceptions to the console. Pretty much any exception that occurs when calling `SendAsync`/`ReceiveAsync`/`CloseAsync` is unrecoverable in that it will abort the connection and leave the `WebSocket` instance in an unusable state.
                Console.WriteLine("Exception: {0}", e);
            }
            finally
            {
                // Clean up by disposing the WebSocket once it is closed/aborted.
                KillSocket("Exception thrown");
            }
        }

        public void KillSocket(string SpecialMessage)
        {
            if (socket != null)
            {
                CloseSocketRequest(SpecialMessage);
                socket.Dispose();
            }

            OnMessage(new MessageEventArgs(guid, SpecialMessage, MessageTypes.SOCKETCLOSED, ""));
        }

        private void ClearBuffer()
        {
            Array.Clear(receiveBuffer, 0, BufferSize);
            sb.Clear();
        }

        private async void CloseSocketRequest(string msg)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, msg, CancellationToken.None);
        }

        private Channel GetChannel(int channelNumber)
        {
            Channel c = null;
            ChannelByNumber.TryGetValue(channelNumber, out c);
            return c;
        }
        private Channel GetChannel(string ChannelName)
        {
            Channel c = null;
            Channels.TryGetValue(ChannelName, out c);
            return c;
        }
    }
}

