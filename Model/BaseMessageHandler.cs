using LightMessager.Common;
using LightMessager.Exceptions;
using LightMessager.Repository;
using NLog;
using System;
using System.Threading.Tasks;

namespace LightMessager.Model
{
    public class BaseMessageHandler<TMessage> : IHandleMessages<TMessage>
        where TMessage : BaseMessage
    {
        private int _maxRetry;
        private int _maxRequeue;
        private int _backoffMs;
        private IMessageRepository _repository;
        private static Logger _logger = LogManager.GetLogger("MessageHandler");

        public BaseMessageHandler(IMessageRepository repository)
        {
            _maxRetry = 2;
            _maxRequeue = 1;
            _backoffMs = 200;
            _repository = repository;
        }

        public void Handle(TMessage message)
        {
            try
            {
                // 执行DoHandle可能会发生异常，如果是我们特定的异常则进行重试操作
                // 否则直接抛出异常
                RetryHelper.Retry(() => DoHandle(message), _maxRetry, _backoffMs, p =>
                {
                    var ex = p as Exception<LightMessagerExceptionArgs>;
                    if (ex != null)
                        return true;

                    return false;
                });
            }
            catch (Exception ex)
            {
                _logger.Debug("未知异常：" + ex.Message + "；堆栈：" + ex.StackTrace);
                // _maxRetry次之后还有问题，先判该条消息requeue次数是否超过
                // 允许的最大值：如果是，不再做任何进一步尝试了，log一波；否则
                // 设置NeedRequeue为true准备重新入队列定
                message.NeedRequeue = NeedRequeue(message.MsgId);
            }
        }

        public async Task HandleAsync(TMessage message)
        {
            try
            {
                await RetryHelper.RetryAsync(() => DoHandle(message), _maxRetry, _backoffMs, p =>
                {
                    var ex = p as Exception<LightMessagerExceptionArgs>;
                    if (ex != null)
                        return true;

                    return false;
                });
            }
            catch (Exception ex)
            {
                _logger.Debug("未知异常：" + ex.Message + "；堆栈：" + ex.StackTrace);
                message.NeedRequeue = true;
            }
        }

        protected virtual void DoHandle(TMessage message)
        {
            throw new NotImplementedException();
        }

        private bool NeedRequeue(string msgId)
        {
            var model = _repository.GetModel(new SelectParam { MsgId = msgId });
            return model.Requeue < _maxRequeue;
        }
    }
}
