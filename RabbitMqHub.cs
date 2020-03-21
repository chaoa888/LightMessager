using LightMessager.Model;
using LightMessager.Pool;
using LightMessager.Repository;
using Microsoft.Extensions.Configuration;
using NLog;
using RabbitMQ.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LightMessager
{
    /* 
     * links: 
     * https://www.rabbitmq.com/dotnet-api-guide.html
     * https://www.rabbitmq.com/queues.html
     * https://www.rabbitmq.com/confirms.html
     * https://stackoverflow.com/questions/4444208/delayed-message-in-rabbitmq
    */
    public sealed partial class RabbitMqHub
    {
        private IConnection _connection;
        private IConnection _asynConnection;
        private int _max_requeue;
        private int _max_republish;
        private int _min_delaysend;
        private ushort _prefetch_count;
        private IMessageRepository _repository;
        private static ObjectPool<IPooledWapper> _channel_pools;
        private ConcurrentDictionary<string, QueueInfo> _send_queue;
        private ConcurrentDictionary<string, QueueInfo> _route_queue;
        private ConcurrentDictionary<string, QueueInfo> _publish_queue;
        private ConcurrentDictionary<string, QueueInfo> _send_dlx;
        private ConcurrentDictionary<string, QueueInfo> _route_dlx;
        private ConcurrentDictionary<string, QueueInfo> _publish_dlx;
        private object _lockobj = new object();
        private Logger _logger = LogManager.GetLogger("RabbitMqHub");

        public IMessageRepository Repository { get { return this._repository; } }

        public RabbitMqHub()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json");

            var configuration = builder.Build();
            InitConnection(configuration);
            InitAsyncConnection(configuration);
            InitRepository(configuration);
            InitChannelPool(configuration);
        }

        public RabbitMqHub(IConfigurationRoot configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            InitConnection(configuration);
            InitAsyncConnection(configuration);
            InitRepository(configuration);
            InitChannelPool(configuration);
        }

        private void InitConnection(IConfigurationRoot configuration)
        {
            var factory = GetConnectionFactory(configuration);
            _connection = factory.CreateConnection();
        }

        private void InitAsyncConnection(IConfigurationRoot configuration)
        {
            var factory = GetConnectionFactory(configuration);
            factory.DispatchConsumersAsync = true;
            _asynConnection = factory.CreateConnection();
        }

        private ConnectionFactory GetConnectionFactory(IConfigurationRoot configuration)
        {
            var factory = new ConnectionFactory();
            factory.UserName = configuration.GetSection("LightMessager:UserName").Value;
            factory.Password = configuration.GetSection("LightMessager:Password").Value;
            factory.VirtualHost = configuration.GetSection("LightMessager:VirtualHost").Value;
            factory.HostName = configuration.GetSection("LightMessager:HostName").Value;
            factory.Port = int.Parse(configuration.GetSection("LightMessager:Port").Value);
            factory.AutomaticRecoveryEnabled = true;
            factory.NetworkRecoveryInterval = TimeSpan.FromSeconds(10);

            return factory;
        }

        private void InitRepository(IConfigurationRoot configuration)
        {
            _repository = new InMemoryRepository();
        }

        private void InitChannelPool(IConfigurationRoot configuration)
        {
            // 注意这里使用的是_connection，后面可以考虑producer的channel走
            // 自己的connection（甚至connection也可以池化掉）
            var cpu = Environment.ProcessorCount;
            _channel_pools = new ObjectPool<IPooledWapper>(
                p => new PooledChannel(_connection.CreateModel(), p, _repository, _connection),
                cpu, cpu * 2);
        }

        private void InitOther(IConfigurationRoot configuration)
        {
            _max_republish = 2;
            _max_requeue = 2;
            _min_delaysend = 5;
            _prefetch_count = 200;
            _send_queue = new ConcurrentDictionary<string, QueueInfo>();
            _route_queue = new ConcurrentDictionary<string, QueueInfo>();
            _publish_queue = new ConcurrentDictionary<string, QueueInfo>();
            _send_dlx = new ConcurrentDictionary<string, QueueInfo>();
            _route_dlx = new ConcurrentDictionary<string, QueueInfo>();
            _publish_dlx = new ConcurrentDictionary<string, QueueInfo>();
        }

        internal bool PrePersistMessage(BaseMessage message)
        {
            var model = _repository.GetModel(new SelectParam { MsgId = message.MsgId });
            if (model != null)
            {
                if (model.State == MessageState.Created && model.Republish < _max_republish)
                    return true;
            }
            else
            {
                // 出于安全考虑这里还是将BaseMessage复制一份
                var copy = new BaseMessage
                {
                    MsgId = message.MsgId,
                    Content = message.Content,
                    Pattern = message.Pattern,
                    State = MessageState.Created,
                    CreatedTime = message.CreatedTime
                };
                _repository.Add(copy);
                return true;
            }

            return false;
        }

        // send方式的生产端
        internal void EnsureSendQueue(IModel channel, Type messageType, int delaySend, out QueueInfo info)
        {
            var key = GetTypeName(messageType);
            if (!_send_queue.TryGetValue(key, out info))
            {
                info = GetSendQueueInfo(key);
                channel.QueueDeclare(info.Queue, durable: true, exclusive: false, autoDelete: false);
            }

            if (delaySend > 0)
            {
                // links:
                // https://www.rabbitmq.com/ttl.html
                // https://www.rabbitmq.com/dlx.html
                delaySend = Math.Max(delaySend, _min_delaysend); // 至少保证有个5秒的延时，不然意义不大
                var dlx_key = $"{key}.delay_{delaySend}";
                if (!_send_dlx.TryGetValue(dlx_key, out QueueInfo dlx))
                {
                    dlx = new QueueInfo
                    {
                        Exchange = string.Empty,
                        Queue = dlx_key
                    };
                    info.Delay_Exchange = dlx.Exchange;
                    info.Delay_Queue = dlx.Queue;

                    var args = new Dictionary<string, object>();
                    args.Add("x-message-ttl", delaySend * 1000);
                    args.Add("x-dead-letter-exchange", info.Delay_Exchange);
                    args.Add("x-dead-letter-routing-key", info.Queue);
                    channel.QueueDeclare(
                        info.Delay_Queue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: args);

                    _send_dlx.TryAdd(dlx_key, dlx);
                }
            }
        }

        // send方式的消费端
        internal void EnsureSendQueue(IModel channel, Type messageType, out QueueInfo info)
        {
            var key = GetTypeName(messageType);
            if (!_send_queue.TryGetValue(key, out info))
            {
                info = GetSendQueueInfo(key);
                channel.QueueDeclare(info.Queue, durable: true, exclusive: false, autoDelete: false);
            }
        }

        // send with route方式的生产端
        internal void EnsureRouteQueue(IModel channel, Type messageType, int delaySend, out QueueInfo info)
        {
            var key = GetTypeName(messageType);
            if (!_route_queue.TryGetValue(key, out info))
            {
                info = GetRouteQueueInfo(key);
                channel.ExchangeDeclare(info.Exchange, ExchangeType.Direct, durable: true);
            }

            if (delaySend > 0)
            {
                delaySend = Math.Max(delaySend, _min_delaysend);
                var dlx_key = $"{key}.delay_{delaySend}";
                if (!_route_dlx.TryGetValue(dlx_key, out QueueInfo dlx))
                {
                    dlx = new QueueInfo
                    {
                        Exchange = info.Exchange,
                        Queue = dlx_key
                    };
                    info.Delay_Exchange = $"{dlx.Exchange}.delay";
                    info.Delay_Queue = dlx.Queue;

                    channel.ExchangeDeclare(info.Delay_Exchange, ExchangeType.Direct, durable: true);
                    var args = new Dictionary<string, object>();
                    args.Add("x-message-ttl", delaySend * 1000);
                    args.Add("x-dead-letter-exchange", dlx.Exchange);
                    channel.QueueDeclare(
                        info.Delay_Queue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: args);
                    channel.QueueBind(info.Delay_Queue, info.Delay_Exchange, string.Empty);

                    _route_dlx.TryAdd(key, dlx);
                }
            }
        }

        // send with route方式的消费端
        internal void EnsureRouteQueue(IModel channel, Type messageType, string subscriber, string[] subscribeKeys, out QueueInfo info)
        {
            var type_name = GetTypeName(messageType);
            var key = $"{type_name}.sub.{subscriber}";
            if (!_route_queue.TryGetValue(key, out info))
            {
                info = GetRouteQueueInfo(key, type_name);
                info.Queue = key;
                channel.ExchangeDeclare(info.Exchange, ExchangeType.Direct, durable: true);
                channel.QueueDeclare(info.Queue, durable: true, exclusive: false, autoDelete: false);
                for (var i = 0; i < subscribeKeys.Length; i++)
                    channel.QueueBind(info.Queue, info.Exchange, routingKey: subscribeKeys[i]);
            }
        }

        // publish（fanout）方式的生产端
        internal void EnsurePublishQueue(IModel channel, Type messageType, int delaySend, out QueueInfo info)
        {
            var key = GetTypeName(messageType);
            if (!_publish_queue.TryGetValue(key, out info))
            {
                info = GetPublishQueueInfo(key);
                channel.ExchangeDeclare(info.Exchange, ExchangeType.Fanout, durable: true);
            }

            if (delaySend > 0)
            {
                delaySend = Math.Max(delaySend, _min_delaysend);
                var dlx_key = $"{key}.delay_{delaySend}";
                if (!_publish_dlx.TryGetValue(dlx_key, out QueueInfo dlx))
                {
                    dlx = new QueueInfo
                    {
                        Exchange = info.Exchange,
                        Queue = dlx_key
                    };
                    info.Delay_Exchange = dlx.Exchange;
                    info.Delay_Queue = dlx.Queue;

                    var args = new Dictionary<string, object>();
                    args.Add("x-message-ttl", delaySend * 1000);
                    args.Add("x-dead-letter-exchange", info.Delay_Exchange);
                    args.Add("x-dead-letter-routing-key", info.Queue);
                    channel.QueueDeclare(
                        info.Delay_Queue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: args);

                    _publish_dlx.TryAdd(key, dlx);
                }
            }
        }

        // publish（fanout）方式的消费端
        internal void EnsurePublishQueue(IModel channel, Type messageType, string subscriber, out QueueInfo info)
        {
            var type_name = GetTypeName(messageType);
            var key = $"{type_name}.sub.{subscriber}";
            if (!_publish_queue.TryGetValue(key, out info))
            {
                info = GetPublishQueueInfo(key, type_name);
                info.Queue = key;
                channel.ExchangeDeclare(info.Exchange, ExchangeType.Fanout, durable: true);
                channel.QueueDeclare(info.Queue, durable: true, exclusive: false, autoDelete: false);
                channel.QueueBind(info.Queue, info.Exchange, string.Empty);
            }
        }

        internal QueueInfo GetSendQueueInfo(string key)
        {
            var info = _send_queue.GetOrAdd(key, t => new QueueInfo
            {
                Exchange = string.Empty,
                Queue = key
            });

            return info;
        }

        internal QueueInfo GetRouteQueueInfo(string key, string typeName = null)
        {
            var info = _route_queue.GetOrAdd(key, t => new QueueInfo
            {
                Exchange = (typeName ?? key) + ".exchange",
                Queue = string.Empty
            });

            return info;
        }

        internal QueueInfo GetPublishQueueInfo(string key, string typeName = null)
        {
            var info = _publish_queue.GetOrAdd(key, t => new QueueInfo
            {
                Exchange = (typeName ?? key) + ".exchange",
                Queue = string.Empty
            });

            return info;
        }

        private string GetTypeName(Type messageType)
        {
            return messageType.IsGenericType ? messageType.GenericTypeArguments[0].Name : messageType.Name;
        }
    }
}
