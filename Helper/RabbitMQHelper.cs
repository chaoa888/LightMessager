using LightMessager.Common;
using LightMessager.DAL;
using LightMessager.Message;
using LightMessager.Pool;
using Microsoft.Extensions.Configuration;
using NLog;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace LightMessager.Helper
{
    /* 
     * links: 
     * https://www.rabbitmq.com/dotnet-api-guide.html
     * https://www.rabbitmq.com/queues.html
     * https://www.rabbitmq.com/confirms.html
    */
    public sealed class RabbitMQHelper
    {
        static ConnectionFactory factory;
        static IConnection connection;
        static volatile int prepersist_count;
        static readonly int default_retry_wait;
        static List<ulong> prepersist;
        static ConcurrentQueue<BaseMessage> retry_queue;
        static ConcurrentDictionary<Type, QueueInfo> dict_info;
        static ConcurrentDictionary<Type, object> dict_func;
        static ConcurrentDictionary<Type, ObjectPool<IPooledWapper>> pools;
        static readonly ushort prefetch_count;
        static object lockobj = new object();
        static Logger _logger = LogManager.GetLogger("RabbitMQHelper");

        private RabbitMQHelper()
        { }

        static RabbitMQHelper()
        {
            #region 读取配置
            // 添加json配置文件路径
#if LOCAL
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.Local.json");
#elif DEBUG
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.Development.json");
#else
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json");
#endif
            // 创建配置根对象
            var configurationRoot = builder.Build();
            #endregion

            prefetch_count = 100;
            prepersist_count = 0;
            default_retry_wait = 1000; // 1秒
            prepersist = new List<ulong>();
            retry_queue = new ConcurrentQueue<BaseMessage>();
            dict_info = new ConcurrentDictionary<Type, QueueInfo>();
            dict_func = new ConcurrentDictionary<Type, object>();
            pools = new ConcurrentDictionary<Type, ObjectPool<IPooledWapper>>();
            factory = new ConnectionFactory();
            factory.UserName = configurationRoot.GetSection("LightMessager:UserName").Value; // "admin";
            factory.Password = configurationRoot.GetSection("LightMessager:Password").Value; // "123456";
            factory.VirtualHost = configurationRoot.GetSection("LightMessager:VirtualHost").Value; // "/";
            factory.HostName = configurationRoot.GetSection("LightMessager:HostName").Value; // "127.0.0.1";
            factory.Port = int.Parse(configurationRoot.GetSection("LightMessager:Port").Value); // 5672;
            factory.AutomaticRecoveryEnabled = true;
            factory.NetworkRecoveryInterval = TimeSpan.FromSeconds(15);
            connection = factory.CreateConnection();

            // 开启轮询检测，扫描重试队列，重发消息
            new Thread(() =>
            {
                // 先实现为spin的方式，后面考虑换成blockingqueue的方式
                while (true)
                {
                    BaseMessage item;
                    while (retry_queue.TryDequeue(out item))
                    {
                        Send(item);
                    }
                    Thread.Sleep(1000 * 5);
                }
            }).Start();
        }

        /// <summary>
        /// 注册消息处理器
        /// </summary>
        /// <typeparam name="TMessage">消息类型</typeparam>
        /// <typeparam name="THandler">消息处理器类型</typeparam>
        public static void RegisterHandler<TMessage, THandler>()
            where THandler : BaseHandleMessages<TMessage>
            where TMessage : BaseMessage
        {
            try
            {
                var type = typeof(TMessage);
                if (!dict_func.ContainsKey(type))
                {
                    var handler = dict_func.GetOrAdd(type, t => Activator.CreateInstance<THandler>()) as THandler;
                    var channel = connection.CreateModel();
                    var consumer = new EventingBasicConsumer(channel);
                    /*
                      @param prefetchSize maximum amount of content (measured in octets) that the server will deliver, 0 if unlimited
                      @param prefetchCount maximum number of messages that the server will deliver, 0 if unlimited
                      @param global true if the settings should be applied to the entire channel rather than each consumer
                    */
                    channel.BasicQos(0, prefetch_count, false);
                    consumer.Received += async (model, ea) =>
                    {
                        var json = Encoding.UTF8.GetString(ea.Body);
                        var msg = Jil.JSON.Deserialize<TMessage>(json);
                        await handler.Handle(msg);
                        if (msg.NeedNAck)
                        {
                            channel.BasicNack(ea.DeliveryTag, false, true);
                        }
                        else
                        {
                            channel.BasicAck(ea.DeliveryTag, false);
                        }
                    };

                    var exchange = string.Empty;
                    var route_key = string.Empty;
                    var queue = string.Empty;
                    EnsureQueue<TMessage>(channel, out exchange, out route_key, out queue);
                    channel.BasicConsume(queue, false, consumer);
                }
            }
            catch (Exception ex)
            {
                _logger.Debug("RegisterHandler()出错，异常：" + ex.Message + "；堆栈：" + ex.StackTrace);
            }
        }

        /// <summary>
        /// 注册消息处理器，根据模式匹配接收消息
        /// </summary>
        /// <typeparam name="TMessage">消息类型</typeparam>
        /// <typeparam name="THandler">消息处理器类型</typeparam>
        /// <param name="subscriberName">订阅器的名称</param>
        /// <param name="subscribePatterns">订阅器支持的消息模式</param>
        public static void RegisterHandlerAs<TMessage, THandler>(string subscriberName, params string[] subscribePatterns)
            where THandler : BaseHandleMessages<TMessage>
            where TMessage : BaseMessage
        {
            if (string.IsNullOrWhiteSpace(subscriberName))
            {
                throw new ArgumentException("subscriberName不允许为空");
            }

            if (subscribePatterns == null || subscribePatterns.Length == 0)
            {
                throw new ArgumentException("subscribePatterns不允许为空");
            }

            try
            {
                var type = typeof(TMessage);
                if (!dict_func.ContainsKey(type))
                {
                    var handler = dict_func.GetOrAdd(type, t => Activator.CreateInstance<THandler>()) as THandler;
                    var channel = connection.CreateModel();
                    var consumer = new EventingBasicConsumer(channel);
                    /*
                      @param prefetchSize maximum amount of content (measured in octets) that the server will deliver, 0 if unlimited
                      @param prefetchCount maximum number of messages that the server will deliver, 0 if unlimited
                      @param global true if the settings should be applied to the entire channel rather than each consumer
                    */
                    channel.BasicQos(0, prefetch_count, false);
                    consumer.Received += async (model, ea) =>
                    {
                        var json = Encoding.UTF8.GetString(ea.Body);
                        var msg = Jil.JSON.Deserialize<TMessage>(json);
                        await handler.Handle(msg);
                        if (msg.NeedNAck)
                        {
                            channel.BasicNack(ea.DeliveryTag, false, true);
                        }
                        else
                        {
                            channel.BasicAck(ea.DeliveryTag, false);
                        }
                    };

                    var exchange = string.Empty;
                    var queue = string.Empty;
                    EnsureQueue<TMessage>(channel, subscriberName, out exchange, out queue, subscribePatterns);
                    channel.BasicConsume(queue, false, consumer);
                }
            }
            catch (Exception ex)
            {
                _logger.Debug("RegisterHandler(string subscriberName)出错，异常：" + ex.Message + "；堆栈：" + ex.StackTrace);
            }
        }

        /// <summary>
        /// 发送一条消息
        /// </summary>
        /// <typeparam name="TMessage">消息类型</typeparam>
        /// <param name="message">消息</param>
        /// <param name="delaySend">延迟多少毫秒发送消息</param>
        /// <returns>发送成功返回true，否则返回false</returns>
        public static bool Send<TMessage>(TMessage message, int delaySend = 0)
            where TMessage : BaseMessage
        {
            if (string.IsNullOrWhiteSpace(message.Source))
            {
                throw new ArgumentNullException("message.Source");
            }

            if (!PrePersistMessage(message))
            {
                return false;
            }

            delaySend = Math.Max(delaySend, 1000); // 至少保证1秒的延迟，否则意义不大
            using (var pooled = InnerCreateChannel<TMessage>())
            {
                IModel channel = pooled.Channel;
                message.DeliveryTag = channel.NextPublishSeqNo;
                var exchange = string.Empty;
                var route_key = string.Empty;
                var queue = string.Empty;
                if (delaySend > 0)
                {
                    EnsureQueue<TMessage>(channel, delaySend, out route_key);
                }
                else
                {
                    EnsureQueue<TMessage>(channel, out exchange, out route_key, out queue);
                }

                var json = Jil.JSON.SerializeDynamic(message, Jil.Options.IncludeInherited);
                var bytes = Encoding.UTF8.GetBytes(json);
                var props = channel.CreateBasicProperties();
                props.ContentType = "text/plain";
                props.DeliveryMode = 2;
                channel.BasicPublish(exchange, route_key, props, bytes);
                var time_out = Math.Max(default_retry_wait, message.RetryCount * 1000);
                var ret = channel.WaitForConfirms(TimeSpan.FromMilliseconds(time_out));
                if (!ret)
                {
                    message.DeliveryTag = 0; // 重置为0
                    message.RetryCount = Math.Max(1, message.RetryCount);
                    message.RetryCount *= 2;
                    message.LastRetryTime = DateTime.Now;
                    retry_queue.Enqueue(message);
                }
            }

            return true;
        }

        /// <summary>
        /// 发布一条消息
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <param name="message">消息</param>
        /// <param name="pattern">消息满足的模式（也就是routeKey）</param>
        /// <param name="delaySend">延迟多少毫秒发布消息</param>
        /// <returns>发布成功返回true，否则返回false</returns>
        public static bool Publish<TMessage>(TMessage message, string pattern, int delaySend = 0)
            where TMessage : BaseMessage
        {
            if (string.IsNullOrWhiteSpace(message.Source))
            {
                throw new ArgumentException("message.Source不允许为空");
            }

            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new ArgumentException("pattern不允许为空");
            }

            if (!PrePersistMessage(message))
            {
                return false;
            }

            delaySend = Math.Max(delaySend, 1000); // 至少保证1秒的延迟，否则意义不大
            using (var pooled = InnerCreateChannel<TMessage>())
            {
                IModel channel = pooled.Channel;
                message.DeliveryTag = channel.NextPublishSeqNo;
                var exchange = string.Empty;
                var route_key = string.Empty;
                var queue = string.Empty;
                if (delaySend > 0)
                {
                    EnsureQueue<TMessage>(channel, delaySend, pattern, out exchange);
                }
                else
                {
                    EnsureQueue<TMessage>(channel, out exchange);
                }

                var json = Jil.JSON.SerializeDynamic(message, Jil.Options.IncludeInherited);
                var bytes = Encoding.UTF8.GetBytes(json);
                var props = channel.CreateBasicProperties();
                props.ContentType = "text/plain";
                props.DeliveryMode = 2;
                if (delaySend > 0)
                {
                    channel.BasicPublish(exchange, pattern, props, bytes);
                    channel.WaitForConfirms();
                }
                else
                {
                    channel.BasicPublish(exchange, pattern, props, bytes);
                    channel.WaitForConfirms();
                }
            }

            return true;
        }

        private static PooledChannel InnerCreateChannel<TMessage>()
            where TMessage : BaseMessage
        {
            var pool = pools.GetOrAdd(
                typeof(TMessage),
                t => new ObjectPool<IPooledWapper>(p => new PooledChannel(connection.CreateModel(), p), 10));
            return pool.Get() as PooledChannel;
        }

        private static bool PrePersistMessage<TMessage>(TMessage message)
            where TMessage : BaseMessage
        {
            if (message.RetryCount == 0)
            {
                var knuthHash = MessageIdHelper.GenerateMessageIdFrom(Encoding.UTF8.GetBytes(message.Source));
                if (prepersist.Contains(knuthHash))
                {
                    return false;
                }
                else
                {
                    message.KnuthHash = knuthHash;
                    if (Interlocked.Increment(ref prepersist_count) != 1000)
                    {
                        prepersist.Add(knuthHash);
                    }
                    else
                    {
                        prepersist.RemoveRange(0, 950);
                    }

                    var model = MessageQueueHelper.GetModelBy(knuthHash);
                    if (model != null)
                    {
                        return false;
                    }
                    else
                    {
                        var new_model = new MessageQueue
                        {
                            KnuthHash = knuthHash,
                            MsgContent = message.Source,
                            RetryCount = 0,
                            CanBeRemoved = false,
                            CreatedTime = DateTime.Now,
                        };
                        MessageQueueHelper.Insert(new_model);
                        return true;
                    }
                }
            }
            else // RetryCount > 0
            {
                // 直接返回true，以便后续可以进行重发
                return true;
            }
        }

        private static void EnsureQueue<TMessage>(IModel channel, out string exchange, out string routeKey, out string queue)
            where TMessage : BaseMessage
        {
            var type = typeof(TMessage);
            if (!dict_info.ContainsKey(type))
            {
                var info = GetQueueInfo(type);
                exchange = info.Exchange;
                routeKey = info.DefaultRouteKey;
                queue = info.Queue;

                channel.ExchangeDeclare(exchange, ExchangeType.Direct, durable: true);
                channel.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false);
                channel.QueueBind(queue, exchange, routeKey);
            }
            else
            {
                var info = GetQueueInfo(type);
                exchange = info.Exchange;
                queue = info.Queue;
                routeKey = info.DefaultRouteKey;
            }
        }

        private static void EnsureQueue<TMessage>(IModel channel, string subscriberName, out string exchange, out string queue, params string[] subscribePatterns)
            where TMessage : BaseMessage
        {
            var type = typeof(TMessage);
            if (!dict_info.ContainsKey(type))
            {
                var info = GetQueueInfo(type);
                exchange = "topic." + info.Exchange;
                queue = info.Queue + "." + subscriberName;
                channel.ExchangeDeclare(exchange, ExchangeType.Topic, durable: true);
                channel.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false);
                foreach (var pattern in subscribePatterns)
                {
                    channel.QueueBind(queue, exchange, routingKey: pattern);
                }
            }
            else
            {
                var info = GetQueueInfo(type);
                exchange = "topic." + info.Exchange;
                queue = info.Queue + "." + subscriberName;
            }
        }

        private static void EnsureQueue<TMessage>(IModel channel, int delaySend, out string routeKey)
            where TMessage : BaseMessage
        {
            var type = typeof(DelayTypeWapper<TMessage>);
            if (!dict_info.ContainsKey(type))
            {
                var info = GetQueueInfo(type);
                channel.QueueDeclare(info.Queue, durable: true, exclusive: false, autoDelete: false);

                routeKey = info.Queue + ".delay";
                var args = new Dictionary<string, object>();
                args.Add("x-message-ttl", delaySend);
                args.Add("x-dead-letter-exchange", string.Empty);
                args.Add("x-dead-letter-routing-key", info.Queue);
                channel.QueueDeclare(routeKey, durable: false, exclusive: false, autoDelete: false, arguments: args);
            }
            else
            {
                var info = GetQueueInfo(type);
                routeKey = info.Queue + ".delay";
            }
        }

        private static void EnsureQueue<TMessage>(IModel channel, out string exchange)
            where TMessage : BaseMessage
        {
            var type = typeof(TMessage);
            if (!dict_info.ContainsKey(type))
            {
                var info = GetQueueInfo(type);
                exchange = "topic." + info.Exchange;
                channel.ExchangeDeclare(exchange, ExchangeType.Topic, durable: true);
            }
            else
            {
                var info = GetQueueInfo(type);
                exchange = "topic." + info.Exchange;
            }
        }

        private static void EnsureQueue<TMessage>(IModel channel, int delaySend, string pattern, out string exchange)
            where TMessage : BaseMessage
        {
            var type = typeof(DelayTypeWapper<TMessage>);
            if (!dict_info.ContainsKey(type))
            {
                var info = GetQueueInfo(type);
                exchange = info.Exchange + ".delay";

                var args = new Dictionary<string, object>();
                args.Add("x-message-ttl", delaySend);
                args.Add("x-dead-letter-exchange", exchange);
                args.Add("x-dead-letter-routing-key", pattern);
                channel.QueueDeclare(info.Queue + ".delay", durable: true, exclusive: false, autoDelete: false, arguments: args);
            }
            else
            {
                var info = GetQueueInfo(type);
                exchange = info.Exchange + ".delay";
            }
        }

        private static QueueInfo GetQueueInfo(Type messageType)
        {
            var type_name = messageType.IsGenericType ? messageType.GenericTypeArguments[0].Name : messageType.Name;
            var info = dict_info.GetOrAdd(messageType, t => new QueueInfo
            {
                Exchange = type_name + ".exchange",
                DefaultRouteKey = type_name + ".input",
                Queue = type_name + ".input"
            });

            return info;
        }

        private class DelayTypeWapper<TMessage>
            where TMessage : BaseMessage
        {
        }

        private class QueueInfo
        {
            public string Exchange;
            public string DefaultRouteKey;
            public string Queue;
        }
    }
}
