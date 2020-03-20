using LightMessager.Model;
using LightMessager.Pool;
using System;

namespace LightMessager
{
    /// <summary>
    /// 使用该类型来获取一个到底层rabbitmq的口子，通过
    /// 这个口子发送消息。通常用于一个线程绑定一个connector
    /// 执行一个while(true){ ... }
    /// </summary>
    public sealed class HubConnector<TMessage> : IDisposable
        where TMessage : BaseMessage
    {
        private bool _disposed;
        private string _pattern;
        private RabbitMqHub _hub;
        private PooledChannel _wapper;
        private QueueInfo _send_queue_info;
        private QueueInfo _publish_queue_info;

        public bool IsDisposed { get { return _disposed; } }

        internal HubConnector(RabbitMqHub hub, IPooledWapper pooled, string publishPattern)
        {
            _hub = hub;
            _wapper = pooled as PooledChannel;
            _pattern = publishPattern;
            EnsureQueue();
        }

        private void EnsureQueue()
        {
            _hub.EnsureSendQueue(_wapper.Channel, typeof(TMessage), out QueueInfo _send_queue_info);
            if (!string.IsNullOrEmpty(_pattern))
                _hub.EnsurePublishQueue(_wapper.Channel, typeof(TMessage), _pattern, 0, out QueueInfo _publish_queue_info);
        }

        public bool Send(TMessage message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HubConnector<TMessage>));

            if (string.IsNullOrWhiteSpace(message.MsgId))
                throw new ArgumentNullException("message.MsgId");

            if (!_hub.PrePersistMessage(message))
                return false;

            try
            {
                _wapper.Publish(message, _send_queue_info.Exchange, _send_queue_info.RouteKey);
                return true;
            }
            catch
            {
                _wapper.Dispose();
            }

            return false;
        }

        public bool Publish(TMessage message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HubConnector<TMessage>));

            try
            {
                _wapper.Publish(message, _publish_queue_info.Exchange, _publish_queue_info.RouteKey);
                return true;
            }
            catch
            {
                _wapper.Dispose();
            }

            return false;
        }

        public void Dispose()
        {
            // 必须为true
            Dispose(true);
            // 通知垃圾回收机制不再调用终结器（析构器）
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // 清理托管资源
                _wapper.Dispose();
            }

            // 清理非托管资源

            // 让类型知道自己已经被释放
            _disposed = true;
        }
    }
}
