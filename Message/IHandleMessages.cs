using Jil;
using System;
using System.Threading.Tasks;

namespace LightMessager.Message
{
    public class BaseMessage
    {
        public string ID { set; get; }

        [JilDirective(Ignore = true)]
        public string Source { set; get; }

        [JilDirective(Ignore = true)]
        internal ulong SeqNum { set; get; }

        internal bool NeedNAck { set; get; }

        public DateTime PublishTime { set; get; }

        public DateTime CreatedTime { set; get; }
    }

    public interface IHandleMessages
    {
    }

    //
    // 摘要:
    //     Message handler interface. Implement this in order to get to handle messages
    //     of a specific type
    public interface IHandleMessages<in TMessage> : IHandleMessages where TMessage : BaseMessage
    {
        //
        // 摘要:
        //     This method will be invoked with a message of type TMessage
        void Handle(TMessage message);
    }
}
