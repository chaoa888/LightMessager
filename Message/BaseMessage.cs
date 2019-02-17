using Jil;
using System;

namespace LightMessager.Message
{
    public class BaseMessage
    {
        internal long MsgHash { set; get; }

        [JilDirective(Ignore = true)]
        public string Source { set; get; }

        [JilDirective(Ignore = true)]
        internal bool NeedNAck { set; get; }

        internal int RetryCount { set; get; }

        internal DateTime LastRetryTime { set; get; }

        [JilDirective(Ignore = true)]
        internal string Pattern { set; get; }

        public DateTime CreatedTime { set; get; }
    }
}
