using Jil;
using System;

namespace LightMessager.Message
{
    public class BaseMessage
    {
        public ulong KnuthHash { set; get; }

        [JilDirective(Ignore = true)]
        public string Source { set; get; }

        [JilDirective(Ignore = true)]
        internal ulong DeliveryTag { set; get; }

        internal bool NeedNAck { set; get; }

        internal int RetryCount { set; get; }

        internal DateTime LastRetryTime { set; get; }

        public DateTime CreatedTime { set; get; }
    }
}
