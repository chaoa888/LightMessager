using LightMessager.Model;
using LightMessager.Pool;
using LightMessager.Repository;
using Microsoft.Extensions.Configuration;
using NLog;
using RabbitMQ.Client;
using System;
using System.Collections.Concurrent;

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
        private ConcurrentDictionary<Type, QueueInfo> _dict_queue;
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
            _min_delaysend = 5;
        }

        internal bool PrePersistMessage(BaseMessage message)
        {
            var model = _repository.GetModel(new SelectParam { MsgId = message.MsgId });
            if (model != null)
            {
                if (model.Republish < _max_republish)
                    return true;
            }
            else
            {
                // 处于安全考虑这里还是将BaseMessage复制一份
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

        internal void EnsureSendQueue(IModel channel, Type messageType, int delaySend, out QueueInfo info)
        {
            if (!_dict_queue.TryGetValue(messageType, out info))
            {
                info = GetQueueInfo(messageType);

                channel.ExchangeDeclare(info.Exchange, ExchangeType.Direct, durable: true);
                channel.QueueDeclare(info.Queue, durable: true, exclusive: false, autoDelete: false);
                channel.QueueBind(info.Queue, info.Exchange, info.RouteKey);

                if (delaySend > 0)
                {
                    //var args = new Dictionary<string, object>();
                    //args.Add("x-message-ttl", delaySend);
                    //args.Add("x-dead-letter-exchange", exchange);
                    //args.Add("x-dead-letter-routing-key", queue);
                    //channel.QueueDeclare(queue + ".delay", durable: false, exclusive: false, autoDelete: false, arguments: args);
                    //exchange = string.Empty;
                    //routeKey = info.Queue + ".delay";
                    //queue = info.Queue + ".delay";
                }
            }
        }

        internal void EnsureSendQueue(IModel channel, Type messageType, out QueueInfo info)
        {
            if (!_dict_queue.TryGetValue(messageType, out info))
            {
                info = GetQueueInfo(messageType);

                channel.ExchangeDeclare(info.Exchange, ExchangeType.Direct, durable: true);
                channel.QueueDeclare(info.Queue, durable: true, exclusive: false, autoDelete: false);
                channel.QueueBind(info.Queue, info.Exchange, info.RouteKey);
            }
        }

        internal void EnsurePublishQueue(IModel channel, Type messageType, string publishPattern, int delaySend, out QueueInfo info)
        {
            var type = messageType;
            if (!_dict_queue.TryGetValue(messageType, out info))
            {
                info = GetQueueInfo(type);
                if (string.IsNullOrEmpty(publishPattern))
                    channel.ExchangeDeclare(info.Exchange, ExchangeType.Fanout, durable: true);
                else
                    channel.ExchangeDeclare(info.Exchange, ExchangeType.Direct, durable: true);

                if (delaySend > 0)
                {
                }
            }
        }

        internal void EnsurePublishQueue(IModel channel, Type messageType, string[] subscribePatterns, out QueueInfo info)
        {
            if (!_dict_queue.TryGetValue(messageType, out info))
            {
                info = GetQueueInfo(messageType);
                channel.QueueDeclare(info.Queue, durable: true, exclusive: false, autoDelete: false);
                if (subscribePatterns != null && subscribePatterns.Length > 0)
                {
                    channel.ExchangeDeclare(info.Exchange, ExchangeType.Direct, durable: true);
                    for (var i = 0; i < subscribePatterns.Length; i++)
                        channel.QueueBind(info.Queue, info.Exchange, routingKey: subscribePatterns[i]);
                }
                else
                {
                    channel.ExchangeDeclare(info.Exchange, ExchangeType.Fanout, durable: true);
                }
            }
        }

        internal QueueInfo GetQueueInfo(Type messageType)
        {
            var type_name = messageType.IsGenericType ? messageType.GenericTypeArguments[0].Name : messageType.Name;
            var info = _dict_queue.GetOrAdd(messageType, t => new QueueInfo
            {
                Exchange = type_name + ".exchange",
                RouteKey = type_name + ".input",
                Queue = type_name + ".input"
            });

            return info;
        }
    }
}
