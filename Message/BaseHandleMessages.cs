using LightMessager.Exceptions;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace LightMessager.Message
{
    public class BaseHandleMessages<TMessage> : IHandleMessages<TMessage> 
        where TMessage : BaseMessage
    {
        private static Logger _logger = LogManager.GetLogger("MessageHandler");
        private static readonly int retry_wait = 1000 * 2; // 2秒
        private static readonly ConcurrentDictionary<string, int> retry_list = new ConcurrentDictionary<string, int>();

        public async Task Handle(TMessage message)
        {
            var sleep = 0;
            try
            {
                await DoHandle(message);
            }
            catch (Exception<LightMessagerExceptionArgs> ex)
            {
                // 如果异常设定了不能被吞掉，则进行延迟重试
                if (!ex.Args.CanBeSwallowed)
                {
                    var retry_count = 0;
                    if (retry_list.TryGetValue(message.ID, out retry_count))
                    {
                        var new_value = retry_count * 2;
                        retry_list.TryUpdate(message.ID, new_value, retry_count);
                        if (new_value > 4) // 1, 2, 4 最大允许重试3次
                        {
                            //ErrorStore.LogExceptionWithoutContext(ex);
                            _logger.Debug("重试超过最大次数(4)，异常：" + ex.Message + "；堆栈：" + ex.StackTrace);
                        }
                        else
                        {
                            sleep = retry_wait * new_value;
                        }
                    }
                    else
                    {
                        retry_list.GetOrAdd(message.ID, p => 1);
                        sleep = retry_wait * 1;
                    }
                }
                else
                {
                    //ErrorStore.LogExceptionWithoutContext(ex);
                    _logger.Debug("CanBeSwallowed=true，异常：" + ex.Message + "；堆栈：" + ex.StackTrace);
                }
            }
            catch (Exception ex)
            {
                //ErrorStore.LogExceptionWithoutContext(ex); 
                _logger.Debug("未知异常：" + ex.Message + "；堆栈：" + ex.StackTrace);
            }

            // 简单优化，将catch中的耗时逻辑移出来
            if (sleep > 0)
            {
                Thread.Sleep(sleep);
                message.NeedNAck = true;
            }
        }

        protected virtual Task DoHandle(TMessage message)
        {
            throw new NotImplementedException();
        }
    }
}
