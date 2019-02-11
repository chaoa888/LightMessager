using LightMessager.DAL;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;

namespace LightMessager.Pool
{
    internal class PooledChannel : IPooledWapper
    {
        private IModel _internalChannel;
        private ObjectPool<IPooledWapper> _pool;
        public DateTime LastGetTime { set; get; }
        public IModel Channel { get { return this._internalChannel; } }

        public PooledChannel(IModel channel, ObjectPool<IPooledWapper> pool)
        {
            _pool = pool;
            _internalChannel = channel;
            _internalChannel.ConfirmSelect();
            // 此处不考虑BasicReturn的情况，因为消息发送并没有指定mandatory属性
            _internalChannel.BasicAcks += Channel_BasicAcks;
            _internalChannel.BasicNacks += Channel_BasicNacks;
            _internalChannel.ModelShutdown += Channel_ModelShutdown;
        }

        private void Channel_BasicNacks(object sender, BasicNackEventArgs e)
        {
            // 日志记录
        }

        private void Channel_BasicAcks(object sender, BasicAckEventArgs e)
        {
            // 数据更新该条消息的状态信息
            var ret = MessageQueueHelper.Update(new MessageQueue
            {
                //KnuthHash = e.DeliveryTag,
                CanBeRemoved = true,
                LastExecuteTime = DateTime.Now,
            });
            if (!ret)
            {
                // 日志记录
            }
        }

        private void Channel_ModelShutdown(object sender, ShutdownEventArgs e)
        {
            _internalChannel.BasicAcks -= Channel_BasicAcks;
            _internalChannel.BasicNacks -= Channel_BasicNacks;
            _internalChannel.ModelShutdown -= Channel_ModelShutdown;
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
                    _internalChannel.Dispose();
                }
                else
                {
                    _pool.Put(this);
                }
            }

            // 清理非托管资源
        }
    }
}
