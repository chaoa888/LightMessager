using LightMessager.Model;
using LightMessager.Pool;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LightMessager
{
    public sealed partial class RabbitMqHub
    {
        public bool Send<TMessage>(TMessage message, int delaySend = 0)
            where TMessage : BaseMessage
        {
            if (string.IsNullOrWhiteSpace(message.MsgId))
                throw new ArgumentNullException("message.MsgId");

            if (!PrePersistMessage(message))
                return false;

            using (var pooled = _channel_pools.Get() as PooledChannel)
            {
                if (delaySend > 0)
                    delaySend = Math.Max(delaySend, _min_delaysend); // 至少保证有个5秒的延时，不然意义不大

                EnsureSendQueue(pooled.Channel, typeof(TMessage), delaySend, out QueueInfo info);
                pooled.Publish(message, info.Exchange, info.RouteKey);
            }

            return true;
        }

        public IEnumerable<bool> Send<TMessage>(IEnumerable<TMessage> messages, int delaySend = 0)
            where TMessage : BaseMessage
        {
            if (!messages.Any())
                yield return false;

            using (var pooled = _channel_pools.Get() as PooledChannel)
            {
                if (delaySend > 0)
                    delaySend = Math.Max(delaySend, _min_delaysend);

                EnsureSendQueue(pooled.Channel, typeof(TMessage), delaySend, out QueueInfo info);
                foreach (var message in messages)
                {
                    if (string.IsNullOrWhiteSpace(message.MsgId))
                        throw new ArgumentNullException("message.MsgId");

                    if (!PrePersistMessage(message))
                        yield return false;

                    pooled.Publish(message, info.Exchange, info.RouteKey);
                    yield return true;
                }
            }
        }

        public bool Publish<TMessage>(TMessage message, string publishPattern = null, int delaySend = 0)
            where TMessage : BaseMessage
        {
            if (string.IsNullOrWhiteSpace(message.MsgId))
                throw new ArgumentNullException("message.MsgId");

            if (!PrePersistMessage(message))
                return false;

            using (var pooled = _channel_pools.Get() as PooledChannel)
            {
                if (delaySend > 0)
                    delaySend = Math.Max(delaySend, _min_delaysend);

                EnsurePublishQueue(pooled.Channel, typeof(TMessage), publishPattern, delaySend, out QueueInfo info);
                pooled.Publish(message, info.Exchange, info.RouteKey);
            }

            return true;
        }

        public IEnumerable<bool> Publish<TMessage>(IEnumerable<TMessage> messages, string publishPattern = null, int delaySend = 0)
            where TMessage : BaseMessage
        {
            if (!messages.Any())
                yield return false;

            using (var pooled = _channel_pools.Get() as PooledChannel)
            {
                if (delaySend > 0)
                    delaySend = Math.Max(delaySend, _min_delaysend);

                EnsurePublishQueue(pooled.Channel, typeof(TMessage), publishPattern, delaySend, out QueueInfo info);
                foreach (var message in messages)
                {
                    if (string.IsNullOrWhiteSpace(message.MsgId))
                        throw new ArgumentNullException("message.MsgId");

                    if (!PrePersistMessage(message))
                        yield return false;

                    pooled.Publish(message, info.Exchange, info.RouteKey);
                    yield return true;
                }
            }
        }

        public HubConnector<TMessage> GetConnector<TMessage>(string publishPattern = null)
            where TMessage : BaseMessage
        {
            return new HubConnector<TMessage>(this, _channel_pools.Get(), publishPattern);
        }
    }
}
