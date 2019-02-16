using Jil;
using System;

namespace LightMessager.Message
{
    public class BaseMessage
    {
        internal long MsgHash { set; get; }

        [JilDirective(Ignore = true)]
        public string Source { set; get; }

        internal bool NeedNAck { set; get; }

        internal int RetryCount { set; get; }

        internal DateTime LastRetryTime { set; get; }

        public DateTime CreatedTime { set; get; }
    }
}
