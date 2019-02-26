using Newtonsoft.Json;
using System;

namespace LightMessager.Message
{
    public class BaseMessage
    {
        internal long MsgHash { set; get; }

        [JsonIgnore]
        public string Source { set; get; }

        [JsonIgnore]
        internal bool NeedNAck { set; get; }

        internal int RetryCount { set; get; }

        internal DateTime LastRetryTime { set; get; }

        [JsonIgnore]
        internal string Pattern { set; get; }

        public DateTime CreatedTime { set; get; }
    }
}
