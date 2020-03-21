using LightMessager.Model;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;

namespace LightMessager
{
    public sealed partial class RabbitMqHub
    {
        public void RegisterHandler<TMessage, THandler>(bool asyncConsume = false)
            where THandler : BaseMessageHandler<TMessage>
            where TMessage : BaseMessage
        {
            var handler = Activator.CreateInstance(typeof(THandler), _repository) as THandler;
            IModel channel = null;
            IBasicConsumer consumer = null;
            if (!asyncConsume)
            {
                channel = _connection.CreateModel();
                consumer = SetupConsumer<TMessage, THandler>(channel, handler);
            }
            else
            {
                channel = _asynConnection.CreateModel();
                consumer = SetupAsyncConsumer<TMessage, THandler>(channel, handler);
            }
            /*
              @param prefetchSize maximum amount of content (measured in octets) that the server will deliver, 0 if unlimited
              @param prefetchCount maximum number of messages that the server will deliver, 0 if unlimited
              @param global true if the settings should be applied to the entire channel rather than each consumer
            */
            channel.BasicQos(0, _prefetch_count, false);

            EnsureSendQueue(channel, typeof(TMessage), out QueueInfo info);
            channel.BasicConsume(info.Queue, false, consumer);
        }

        public void RegisterHandler<TMessage, THandler>(string subscriber, string[] subscribeKeys, bool asyncConsume = false)
            where THandler : BaseMessageHandler<TMessage>
            where TMessage : BaseMessage
        {
            var handler = Activator.CreateInstance(typeof(THandler), _repository) as THandler;
            IModel channel = null;
            IBasicConsumer consumer = null;
            if (!asyncConsume)
            {
                channel = _connection.CreateModel();
                consumer = SetupConsumer<TMessage, THandler>(channel, handler);
            }
            else
            {
                channel = _asynConnection.CreateModel();
                consumer = SetupAsyncConsumer<TMessage, THandler>(channel, handler);
            }
            /*
              @param prefetchSize maximum amount of content (measured in octets) that the server will deliver, 0 if unlimited
              @param prefetchCount maximum number of messages that the server will deliver, 0 if unlimited
              @param global true if the settings should be applied to the entire channel rather than each consumer
            */
            channel.BasicQos(0, _prefetch_count, false);

            EnsureRouteQueue(channel, typeof(TMessage), subscriber, subscribeKeys, out QueueInfo info);
            channel.BasicConsume(info.Queue, false, consumer);
        }

        public void RegisterHandler<TMessage, THandler>(string subscriber, bool asyncConsume = false)
            where THandler : BaseMessageHandler<TMessage>
            where TMessage : BaseMessage
        {
            var handler = Activator.CreateInstance(typeof(THandler), _repository) as THandler;
            IModel channel = null;
            IBasicConsumer consumer = null;
            if (!asyncConsume)
            {
                channel = _connection.CreateModel();
                consumer = SetupConsumer<TMessage, THandler>(channel, handler);
            }
            else
            {
                channel = _asynConnection.CreateModel();
                consumer = SetupAsyncConsumer<TMessage, THandler>(channel, handler);
            }
            /*
              @param prefetchSize maximum amount of content (measured in octets) that the server will deliver, 0 if unlimited
              @param prefetchCount maximum number of messages that the server will deliver, 0 if unlimited
              @param global true if the settings should be applied to the entire channel rather than each consumer
            */
            channel.BasicQos(0, _prefetch_count, false);

            EnsurePublishQueue(channel, typeof(TMessage), subscriber, out QueueInfo info);
            channel.BasicConsume(info.Queue, false, consumer);
        }

        private EventingBasicConsumer SetupConsumer<TMessage, THandler>(IModel channel, THandler handler)
            where THandler : BaseMessageHandler<TMessage>
            where TMessage : BaseMessage
        {
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var json = Encoding.UTF8.GetString(ea.Body);
                var msg = JsonConvert.DeserializeObject<TMessage>(json);
                handler.Handle(msg);
                // 当消息需要requeue的时候，意味着处理流从这里需要断一下，
                // 这条消息之前的消息（一条或者多条）需要马上ack掉；
                // 接着才能继续进行之前的正常批处理流
                if (msg.NeedRequeue)
                {
                    var last_unack = new BaseMessage();
                    channel.BasicAck(last_unack.DeliveryTag, true);
                    channel.BasicNack(ea.DeliveryTag, false, true);
                }
                else
                {
                    if (_repository.GetCount() >= _prefetch_count)
                    {
                        channel.BasicAck(ea.DeliveryTag, false);
                        _repository.Clear();
                    }
                    else
                    {
                        _repository.Add(msg);
                    }
                }
            };

            return consumer;
        }

        private AsyncEventingBasicConsumer SetupAsyncConsumer<TMessage, THandler>(IModel channel, THandler handler)
            where THandler : BaseMessageHandler<TMessage>
            where TMessage : BaseMessage
        {
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += async (model, ea) =>
            {
                var json = Encoding.UTF8.GetString(ea.Body);
                var msg = JsonConvert.DeserializeObject<TMessage>(json);
                await handler.HandleAsync(msg);
                // 当消息需要requeue的时候，意味着处理流从这里需要断一下，
                // 这条消息之前的消息（一条或者多条）需要马上ack掉；
                // 接着才能继续进行之前的正常批处理流
                if (msg.NeedRequeue)
                {
                    var last_unack = new BaseMessage();
                    channel.BasicAck(last_unack.DeliveryTag, true);
                    channel.BasicNack(ea.DeliveryTag, false, true);
                }
                else
                {
                    if (_repository.GetCount() >= _prefetch_count)
                    {
                        channel.BasicAck(ea.DeliveryTag, false);
                        _repository.Clear();
                    }
                    else
                    {
                        _repository.Add(msg);
                    }
                }
            };

            return consumer;
        }
    }
}
