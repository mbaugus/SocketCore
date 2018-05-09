using System;
using System.Collections.Generic;
using System.Threading;
using Connection;

//using Newtonsoft.Json;

using static System.Console;

namespace Server
{
    class JSONMessage
    {
        public string Name;
        public string Message;
    }

    class MUD
    {
        Queue<string> outgoing = new Queue<string>();

        public void Start()
        {
            // pass port number
            
            ChannelGroupSettings settings = new ChannelGroupSettings();
            settings.MakeChannel(new Channel(1024, "textchannel", ChannelPriority.HIGH, ChannelMessageTypes.TEXT));
            settings.MakeChannel(new Channel(1024, "fileschannel", ChannelPriority.HIGH, ChannelMessageTypes.BINARY));
            settings.MakeChannel(new Channel(1024, "jsonchannel", ChannelPriority.HIGH, ChannelMessageTypes.JSON));

            Connection.WebSocketServer wss = new Connection.WebSocketServer(settings);

            wss.LostConnection += new MessageEventHandler(this.OnClosedConnection);
            wss.NewConnection += new MessageEventHandler(this.OnNewConnection);
            wss.ClientMessageReceived += new MessageEventHandler(this.OnNewMessage);
            
            //wss.Start("http://localhost:8080/MUD/");
            wss.Start("http://159.89.227.216:8080/MUD/");
            /*
            JSONMessage message = new JSONMessage();
            message.Message = "Hey";
            message.Name = "Server";
            //Console.WriteLine(msg);
            */

            //BasicInfo binfo = JsonConvert.DeserializeObject<BasicInfo>(msg);
            //string json = JsonConvert.SerializeObject(message);

            while (true)
            {   
                Thread.Sleep(1);
            }
        }

        private void OnNewConnection(object send, MessageEventArgs e)
        {
            WriteLine("NewConnection: " + e.Message);
        }

        private void OnNewMessage(object send, MessageEventArgs e)
        {
            //WriteLine("New Message: " + e.Message);
            string message = "Channel: " + e.ChannelName + " from " + e.RefId + ", " + e.Message;
            WriteLine(message);
        }
        private void OnClosedConnection(object send, MessageEventArgs e)
        {
            WriteLine("Closed Connection: " + e.Message);
        }
    }
}
