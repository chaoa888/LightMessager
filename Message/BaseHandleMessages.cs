using LightMessager.Exceptions;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace LightMessager.Message
{
    public class BaseHandleMessages<TMessage> : IHandleMessages<TMessage> 
        where TMessage : BaseMessage
    {
        private static Logger _logger = LogManager.GetLogger("MessageHandler");
        private static readonly int retry_wait = 1000 * 2; // 2秒
        private static readonly ConcurrentDictionary<ulong, int> retry_list = new ConcurrentDictionary<ulong, int>();

        public void Handle(TMessage message)
        {
            var sleep = 0;
            try
            {
                DoHandle(message);
            }
            catch (Exception<LightMessagerExceptionArgs> ex)
            {
                // 如果异常设定了不能被吞掉，则进行延迟重试
                if (!ex.Args.CanBeSwallow)
                {
                    var retry_count = 0;
                    if (retry_list.TryGetValue(message.KnuthHash, out retry_count))
                    {
                        var new_value = retry_count * 2;
                        retry_list.TryUpdate(message.KnuthHash, new_value, retry_count);
                        if (new_value > 4) // 1, 2, 4 最大允许重试3次
                        {
                            _logger.Debug("重试超过最大次数(4)，异常：" + ex.Message + "；堆栈：" + ex.StackTrace);
                        }
                        else
                        {
                            sleep = retry_wait * new_value;
                        }
                    }
                    else
                    {
                        retry_list.GetOrAdd(message.KnuthHash, p => 1);
                        sleep = retry_wait * 1;
                    }
                }
                else
                {
                    _logger.Debug("CanBeSwallowed=true，异常：" + ex.Message + "；堆栈：" + ex.StackTrace);
                }
            }
            catch (Exception ex)
            {
                _logger.Debug("未知异常：" + ex.Message + "；堆栈：" + ex.StackTrace);
            }

            // 简单优化，将catch中的耗时逻辑移出来
            if (sleep > 0)
            {
                Thread.Sleep(sleep);
                message.NeedNAck = true;
            }
        }

        protected virtual void DoHandle(TMessage message)
        {
            throw new NotImplementedException();
        }
    }
}
