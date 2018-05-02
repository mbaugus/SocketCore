using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Connection
{
    public class ChannelGroupSettings
    {
        public List<Channel> Channels { get; set; }
        
        public ChannelGroupSettings()
        {
            Channels = new List<Channel>();
        }

        public void MakeChannel(Channel newchannel)
        {
            newchannel.ChannelNumber = Channels.Count + 1;
            Channels.Add(newchannel);
        }

        public string ToJson()
        {
            List<Object> channelinfo = new List<Object>();
            foreach (Channel c in Channels)
            {
                channelinfo.Add(new { c.ChannelNumber, c.Chunksizes, c.MessageType, c.Priority, c.Name, });
            }
            return JsonConvert.SerializeObject(channelinfo);
        }
    }
}
