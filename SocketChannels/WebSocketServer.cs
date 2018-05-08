using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.WebSockets;

namespace Connection
{
    public class WebSocketServer
    {

        public event MessageEventHandler NewConnection;
        public event MessageEventHandler LostConnection;
        public event MessageEventHandler ClientMessageReceived;
  
        protected virtual void NewConnectionEvent(MessageEventArgs e)
        {
            NewConnection?.Invoke(this, e);
        }
        protected virtual void LostConnectionEvent(MessageEventArgs e)
        {
            LostConnection?.Invoke(this, e);
        }
        protected virtual void MessageReceivedEvent(MessageEventArgs e)
        {
            ClientMessageReceived?.Invoke(this, e);
        }


        private int count = 0;
        private static int MaxConnections = 500;

        ChannelGroupSettings GroupSettings;
        Dictionary<Guid, Connection> ConnectionReferences = new Dictionary<Guid, Connection>();
        List<Connection> Connections = new List<Connection>();

        public WebSocketServer(ChannelGroupSettings channelsettings = null)
        {
            if(channelsettings == null)
            {
                GroupSettings = new ChannelGroupSettings();
            }
            else
            {
                GroupSettings = channelsettings;
            }
        }

        private int GetFreeSlot()
        {
            return 0;
        }

        private void SortConnections()
        {

        }

        public async void SendAll(string channel, string message)
        {
            try
            {
                foreach (Connection c in Connections)
                {
                    await c.SendMessage(channel, message);
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }
        }
        public async void SendAll(string channel, ICollection<string> collection)
        {
            foreach (string msg in collection)
            {
                foreach( Connection c in Connections)
                {
                    await c.SendMessage(channel, msg);
                }
            }
        }

        public async void Send(Guid guid, string channelname, string message, string nickname = null)
        {
            if (nickname != null)
            {

            }
            else
            {
                await ConnectionReferences[guid].SendMessage(channelname, message);

            }
        }

        private int LookupRefId(int id)
        {
            return 0;//ConnectionLocations[id];
        }
       
        public void ReturnConnectionToPool(Guid referenceID, string message)
        {
            Connection c = ConnectionReferences[referenceID];
            Connections.Remove(c);
            ConnectionReferences.Remove(referenceID);
            // for now just delete it, but we will use a pool soon

            //Connections[referenceID].InUse = false;
            //Console.WriteLine($"Socket closed: {referenceID} Reason: {message}");
        }

        public void ReceiveMessageFromSocket(object send, MessageEventArgs e)
        {
            if(e.MsgType == MessageTypes.SOCKETCLOSED)
            {
                LostConnectionEvent(e); // send event out
                ReturnConnectionToPool(e.RefId, e.Message);
            }
            else if(e.MsgType == MessageTypes.MESSAGERECEIVED)
            {
                MessageReceivedEvent(e);
            }
            else if(e.MsgType == MessageTypes.NEWCONNECTION)
            {
                NewConnectionEvent(e);
            }
        }

        public async void Start(string listenerPrefix)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(listenerPrefix);
            listener.Start();
            Console.WriteLine("Listening...");

            while (true)
            {
                // await makes sure the context is returned to main program.
                HttpListenerContext listenerContext = await listener.GetContextAsync();
                if (listenerContext.Request.IsWebSocketRequest)
                {
                    ProcessRequest(listenerContext);
                }
                else
                {
                    listenerContext.Response.StatusCode = 400;
                    listenerContext.Response.Close();
                }
            }
        }

        private async void ProcessRequest(HttpListenerContext listenerContext)
        {
            WebSocketContext wsctx = null;
            try
            {
                wsctx = await listenerContext.AcceptWebSocketAsync(subProtocol: null);
            }
            catch (Exception e)
            {
                listenerContext.Response.StatusCode = 500;
                listenerContext.Response.Close();
                return;
            }
            Console.WriteLine($"Secure:{wsctx.IsSecureConnection}, Authenticated: {wsctx.IsAuthenticated}, IsLocal: {wsctx.IsLocal}, " +
                $"Origin: {wsctx.Origin}, WS Version: {wsctx.SecWebSocketVersion}, User: {wsctx.User}");

            /*
            for (int i = 0; i < MaxConnections; i++)
            {
                Connections[i] = new Connection(i);
                Connections[i].refId = -1;
                Connections[i].socket = null;
                Connections[i].MessageReceived += new MessageEventHandler(this.ReceiveMessageFromSocket);
            }
            */

            Guid guid = Guid.NewGuid();
            Connection connection = new Connection(GroupSettings);
            connection.socket = wsctx.WebSocket;
            connection.guid = guid;

            Connections.Add(connection);
            ConnectionReferences[guid] = connection;

            connection.MessageReceived += new MessageEventHandler(this.ReceiveMessageFromSocket);
            NewConnectionEvent(new MessageEventArgs(guid, "", MessageTypes.NEWCONNECTION, ""));
            // await connection.SendMessage("DEFAULT", this.GroupSettings.ToJson());

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Console.WriteLine(this.GroupSettings.ToJson());
            connection.SendMessageNoChannel(this.GroupSettings.ToJson());
            connection.ReceiveAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }
    }
}
