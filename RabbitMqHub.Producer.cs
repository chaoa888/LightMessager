using LightMessager.Model;
using LightMessager.Pool;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LightMessager
{
    public sealed partial class RabbitMqHub
    {
        public bool Send<TMessage>(TMessage message, string routeKey = "", int delaySend = 0)
            where TMessage : BaseMessage
        {
            if (string.IsNullOrWhiteSpace(message.MsgId))
                throw new ArgumentNullException("message.MsgId");

            if (!PrePersistMessage(message))
                return false;

            using (var pooled = _channel_pools.Get() as PooledChannel)
            {
                if (string.IsNullOrEmpty(routeKey))
                {
                    EnsureSendQueue(pooled.Channel, typeof(TMessage), delaySend, out QueueInfo info);
                    pooled.Publish(message, string.Empty, delaySend == 0 ? info.Queue : info.Delay_Queue);
                }
                else
                {
                    EnsureRouteQueue(pooled.Channel, typeof(TMessage), delaySend, out QueueInfo info);
                    pooled.Publish(message, delaySend == 0 ? info.Exchange : info.Delay_Exchange, routeKey);
                }
            }

            return true;
        }

        public IEnumerable<bool> Send<TMessage>(IEnumerable<TMessage> messages, string routeKey = "", int delaySend = 0)
            where TMessage : BaseMessage
        {
            if (!messages.Any())
                yield return false;

            using (var pooled = _channel_pools.Get() as PooledChannel)
            {
                QueueInfo info = null;
                if (string.IsNullOrEmpty(routeKey))
                    EnsureSendQueue(pooled.Channel, typeof(TMessage), delaySend, out info);
                else
                    EnsureRouteQueue(pooled.Channel, typeof(TMessage), delaySend, out info);

                foreach (var message in messages)
                {
                    if (string.IsNullOrWhiteSpace(message.MsgId))
                        throw new ArgumentNullException("message.MsgId");

                    if (!PrePersistMessage(message))
                        yield return false;

                    if (string.IsNullOrEmpty(routeKey))
                        pooled.Publish(message, string.Empty, delaySend == 0 ? info.Queue : info.Delay_Queue);
                    else
                        pooled.Publish(message, delaySend == 0 ? info.Exchange : info.Delay_Exchange, routeKey);

                    yield return true;
                }
            }
        }

        public IEnumerable<bool> Send<TMessage>(IEnumerable<TMessage> messages, Func<TMessage, string> routeKeySelector, int delaySend = 0)

            where TMessage : BaseMessage
        {
            if (routeKeySelector == null)
                throw new ArgumentNullException("routeKeySelector");

            if (!messages.Any())
                yield return false;

            using (var pooled = _channel_pools.Get() as PooledChannel)
            {
                QueueInfo info = null;
                EnsureRouteQueue(pooled.Channel, typeof(TMessage), delaySend, out info);

                foreach (var message in messages)
                {
                    if (string.IsNullOrWhiteSpace(message.MsgId))
                        throw new ArgumentNullException("message.MsgId");

                    if (!PrePersistMessage(message))
                        yield return false;

                    var routekey = routeKeySelector(message);
                    pooled.Publish(message, delaySend == 0 ? info.Exchange : info.Delay_Exchange, routekey);

                    yield return true;
                }
            }
        }

        public bool Publish<TMessage>(TMessage message, int delaySend = 0)
            where TMessage : BaseMessage
        {
            if (string.IsNullOrWhiteSpace(message.MsgId))
                throw new ArgumentNullException("message.MsgId");

            if (!PrePersistMessage(message))
                return false;

            using (var pooled = _channel_pools.Get() as PooledChannel)
            {
                EnsurePublishQueue(pooled.Channel, typeof(TMessage), delaySend, out QueueInfo info);
                if (delaySend == 0)
                    pooled.Publish(message, info.Exchange, string.Empty);
                else
                    pooled.Publish(message, string.Empty, info.Delay_Queue);
            }

            return true;
        }

        public IEnumerable<bool> Publish<TMessage>(IEnumerable<TMessage> messages, int delaySend = 0)
            where TMessage : BaseMessage
        {
            if (!messages.Any())
                yield return false;

            using (var pooled = _channel_pools.Get() as PooledChannel)
            {
                EnsurePublishQueue(pooled.Channel, typeof(TMessage), delaySend, out QueueInfo info);
                foreach (var message in messages)
                {
                    if (string.IsNullOrWhiteSpace(message.MsgId))
                        throw new ArgumentNullException("message.MsgId");

                    if (!PrePersistMessage(message))
                        yield return false;

                    if (delaySend == 0)
                        pooled.Publish(message, info.Exchange, string.Empty);
                    else
                        pooled.Publish(message, string.Empty, info.Delay_Queue);
                    yield return true;
                }
            }
        }

        public HubConnector<TMessage> GetConnector<TMessage>(int delaySend = 0)
            where TMessage : BaseMessage
        {
            return new HubConnector<TMessage>(this, _channel_pools.Get(), delaySend);
        }
    }
}
