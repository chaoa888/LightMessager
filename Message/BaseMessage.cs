using Jil;
using System;
using System.Collections.Generic;
using System.Text;

namespace LightMessager.Message
{
    public class BaseMessage
    {
        public ulong KnuthHash { set; get; }

        [JilDirective(Ignore = true)]
        public string Source { set; get; }

        [JilDirective(Ignore = true)]
        internal ulong SeqNum { set; get; }

        internal bool NeedNAck { set; get; }

        internal int RetryCount { set; get; }

        internal DateTime PublishTime { set; get; }

        public DateTime CreatedTime { set; get; }
    }
}
