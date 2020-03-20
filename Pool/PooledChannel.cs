using LightMessager.Model;
using LightMessager.Repository;
using Newtonsoft.Json;
using NLog;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace LightMessager.Pool
{
    internal class PooledChannel : IPooledWapper
    {
        private bool _disposed;
        private IModel _innerChannel;
        private IConnection _connection;
        private IMessageRepository _repository;
        private ObjectPool<IPooledWapper> _pool;
        private Dictionary<ulong, string> _unconfirm; // <DeliveryTag, MsgId>
        private object _lockobj;
        private static Logger _logger = LogManager.GetLogger("PooledChannel");

        public IModel Channel
        {
            get
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(PooledChannel));

                return this._innerChannel;
            }
        }

        public PooledChannel(IModel channel, ObjectPool<IPooledWapper> pool,
            IMessageRepository repository, IConnection connection)
        {
            _lockobj = new object();
            _pool = pool;
            _innerChannel = channel;
            _repository = repository;
            _connection = connection;
            InitChannel();
        }

        private void InitChannel()
        {
            _unconfirm = new Dictionary<ulong, string>();
            _innerChannel.ConfirmSelect();
            _innerChannel.BasicAcks += Channel_BasicAcks;
            _innerChannel.BasicNacks += Channel_BasicNacks;
            _innerChannel.BasicReturn += Channel_BasicReturn;
            _innerChannel.ModelShutdown += Channel_ModelShutdown;
        }

        // broker正常接受到消息，会触发该ack事件
        private void Channel_BasicAcks(object sender, BasicAckEventArgs e)
        {
            /*
             * the broker may also set the multiple field in basic.ack to indicate 
             * that all messages up to and including the one with the sequence number 
             * have been handled.
             */
            if (e.Multiple)
            {
                List<string> list = null;
                Dictionary<ulong, string> dict = new Dictionary<ulong, string>();
                foreach (var key in _unconfirm.Keys)
                {
                    if (key <= e.DeliveryTag)
                    {
                        if (list == null)
                            list = new List<string>();

                        list.Add(_unconfirm[key]);
                    }
                    else
                    {
                        dict.Add(key, _unconfirm[key]);
                    }
                }

                if (list != null)
                {
                    var ok = _repository.Update(new UpdateParam
                    {
                        MsgIds = list,
                        OldState = MessageState.Created,
                        NewState = MessageState.Persistent,
                        Remark = "消息成功到达Broker",
                        ModifyTime = DateTime.Now
                    });

                    if (ok)
                        _unconfirm = dict;
                    else
                        throw new Exception("Repository Update[created -> persistent]出现异常!");
                }
            }
            else
            {
                var msgId = string.Empty;
                if (_unconfirm.TryGetValue(e.DeliveryTag, out msgId))
                {
                    var ok = _repository.Update(new UpdateParam
                    {
                        MsgId = msgId,
                        OldState = MessageState.Created,
                        NewState = MessageState.Persistent,
                        Remark = "消息成功到达Broker",
                        ModifyTime = DateTime.Now
                    });

                    if (ok)
                        _unconfirm.Remove(e.DeliveryTag);
                    else
                        throw new Exception("Repository Update[created -> persistent]出现异常!");
                }
            }
        }

        // nack的时候通常broker那里可能出了什么状况，log一波并且准备重试
        private void Channel_BasicNacks(object sender, BasicNackEventArgs e)
        {

        }

        // return类似于nack，不同在于return通常代表着unroutable，
        // 所以log一下但并不会重试，消息的状态也直接置为终结态Error_Unroutable：5
        private void Channel_BasicReturn(object sender, BasicReturnEventArgs e)
        {
            _repository.Update(new UpdateParam
            {
                MsgId = e.BasicProperties.MessageId,
                OldState = MessageState.Created,
                NewState = MessageState.Error_Unroutable,
                Remark = $"Broker Return，ReplyCode：{e.ReplyCode}，ReplyText：{e.ReplyText}",
                ModifyTime = DateTime.Now
            });
        }

        private void Channel_ModelShutdown(object sender, ShutdownEventArgs e)
        {
            _logger.Warn($"Channel Shutdown，ReplyCode：{e.ReplyCode}，ReplyText：{e.ReplyText}");

            _unconfirm.Clear();
            _repository = null;
            _innerChannel.BasicAcks -= Channel_BasicAcks;
            _innerChannel.BasicNacks -= Channel_BasicNacks;
            _innerChannel.BasicReturn -= Channel_BasicReturn;
            _innerChannel.ModelShutdown -= Channel_ModelShutdown;
        }

        internal void Publish(BaseMessage message, string exchange, string routeKey)
        {
            PreRecord(message);

            var json = JsonConvert.SerializeObject(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            var props = _innerChannel.CreateBasicProperties();
            props.MessageId = message.MsgId;
            props.ContentType = "text/plain";
            props.DeliveryMode = 2;
            _innerChannel.BasicPublish(exchange, routeKey, props, bytes);
        }

        private void PreRecord(BaseMessage message)
        {
            _unconfirm.Add(_innerChannel.NextPublishSeqNo, message.MsgId);
        }

        public void Dispose()
        {
            // 必须为true
            Dispose(true);
            // 通知垃圾回收机制不再调用终结器（析构器）
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 清理托管资源
                if (_pool.IsDisposed)
                {
                    _unconfirm = null;
                    _innerChannel.Dispose();
                }
                else
                {
                    if (_innerChannel.IsClosed)
                    {
                        _innerChannel = _connection.CreateModel();
                        InitChannel();
                        _pool.Put(this);
                    }
                    else
                    {
                        _unconfirm.Clear();
                        _pool.Put(this);
                    }
                }
            }

            // 清理非托管资源
        }
    }
}
