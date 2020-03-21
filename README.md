# LightMessager
一个针对RabbitMQ的简单封装类

### 基本概念
#### exchange
一条消息的转发是由*exchange*的类型和具体的转发规则（*bindings*）共同决定的
+ **类型**
    + ***direct***：msg的routekey跟queue绑定的routekey一致，则直接转发
    + ***fanout***：忽略routekey，挂多少个queue就转发多少个msg，类似于广播
    + ***topic***：可以看成是direct的高级版本，因为这里routekey不光是一致就转发，还可以是满足某种pattern就可以转发
    + ***headers***：忽略routekey，更高级的一种形式

+ **重要的属性**
    + *name*
    + *durability*（一个durable的exchange可以从broker的重启中存活下来）
    + *auto-delete* 
    + *arguments*（optional; used by plugins and broker-specific features such as message TTL, queue length limit, etc）

#### queue
+ **重要的属性**
    + *name*
    + *durability*（一个durable的queue可以从broker的重启中存活下来）
    + *auto-delete*
    + *arguments*

#### message
+ **可设置的属性**
    + *Content type*
    + *Content encoding* 
    + *Routing key* 
    + *Delivery mode*（persistent or not）
    + *Message priority* 
    + *Message publishing timestamp*
    + *Expiration period*
    + *Publisher application id*等
  
+ 将一条消息发送至一个durable的queue并不能使该消息persistent，唯一能决定一条消息是否持久化的是其delivery mode属性值
  
+ 同时注意mandatory和persistent的区别，前者会在消息无法送达的情况下触发basic.return事件，后者则是会让消息持久化至磁盘

### 线程模型
在线程模型上，主要考虑两个类型`Connection`和`Channle`。
> Connections and Channels are meant to be long-lived. 

在当前版本的c#客户端中，一个connection对应着一个线程，抽象的是一个tcp连接，而多个channel通过多路复用共用一个connection。

一种常见的策略是应用层面一个线程分配一个独立的channel通过共用一个connection与服务端进行交互。
> 注意channel是非线程安全的，这意味着如果真的需要多个线程共用一个channel的话需要自己做加锁保护

在channel层面更容易出现各种问题导致channel关闭（connection相对来说要稳定一些），因此考虑对channel池化进而达到复用的目的比较可取。

最后一个与线程模型息息相关的概念是消息顺序。虽然rabbitmq可以在单个channel上保证消息的投递顺序（与发送顺序一致），但是消息到达时触发回调执行的顺序是乱序的。因为在默认情况下回调是由TaskScheduler来处理的，不过好在rabbitmq留了一个口子，我们也可以插入自己的scheduler实现来保证更严格的执行顺序：
```csharp
public class CustomTaskScheduler : TaskScheduler
{ }

var cf = new ConnectionFactory();
cf.TaskScheduler = new CustomTaskScheduler();
```
**2020-03-18 补充**：
客户端版本5.x往上走（甚至可能再靠前），我查看了5.1.0的源代码确认TaskScheduler这个属性已经没有在使用了。由此修正下前面的说法：这样一来消息触发回调执行的默认顺序就是消息的投递顺序！

另外从代码中还发现了一个叫做`AsyncEventingBasicConsumer`的[类型](https://gigi.nullneuron.net/gigilabs/asynchronous-rabbitmq-consumers-in-net/)，这货可以让我们非常轻松的写出真正的异步回调逻辑（同时仍然保持执行的顺序性）：
```csharp
// 打开异步消费开关
var cf = new ConnectionFactory();
cf.DispatchConsumersAsync = true;

// 显式使用AsyncEventHandler
var consumer = new AsyncEventingBasicConsumer(channel);
consumer.Received += Consumer_Received;
```

### 自动恢复
rabbitmq自带有`automatic recovery`特性，能在网络发生异常时进行自我恢复。这包括连接的恢复和网络拓扑（topology）（queues、exchanges、bindings and consumers）的恢复。

通常来说可能导致automatic recovery的事件有：
+ An I/O exception is thrown in connection's I/O loop
+ A socket read operation times out
+ Missed server heartbeats are detected
+ Any other unexpected exception is thrown in connection's I/O loop

注意，任何channel-level的异常并不会导致recovery动作，这是因为它被当作是应用层面的异常来对待了。

rabbitmq对于connection的恢复有一定的缺陷：
+ 首先，
    > when a connection is down or lost, it takes time to detect.

    一个失效的连接需要一定的时间才能被发现，因此在这段时间中发送的消息就需要额外的手段来保证其不被丢失（*publisher confirms*）

+ 其次，
    > recovery begins after a configurable delay
    
    rabbitmq默认情况下每隔5秒重试一次恢复连接，重试的时候如果试图发送一条消息，这将会触发一个exception，应用层可能需要处理该异常以保证发送的消息不会丢失掉（也建议配合*publisher confirms*来做处理）

### 可靠的消息送达
#### publisher -> broker
设置durability for exchanges, queues and persistent for messages；还可以设定消息的`mandatory`属性，这样当消息无法送达的时候broker会直接basic.return返回给publisher。
> 还有一种情况是broker拒绝了该条消息（basic.nack），这表明broker因为某种原因当前不能处理该条消息，并拒绝为该条消息的发送负责。此时需要publisher自己来负责该条消息的后续处理，可能重发也可能就此作废等。
  
开启rabbitmq自带的 ***publisher confirms*** 机制。一般的处理策略可以简单表述为：
- Enable publisher confirms on a channel
- For every published message, add a map entry that maps current sequence number to the message
- When a positive ack arrives, remove the entry
- When a negative ack arrives, remove the entry and schedule its message for republishing (or something else that's suitable)

设置rabbitmq的`AutomaticRecoveryEnabled`属性为true（似乎默认就是true）。

#### broker -> consumer
这个方向上可以使用*consumer ack*机制，有三个方法可供使用：
- `basic.ack`
- `basic.nack`（与reject的区别仅仅在于nack支持批量操作）
- `basic.reject`
> consumer还可以选择自动ack，但通常不应该启用这个选项。

consumer这边有一个叫做`QoS`的概念，不复杂，其实质就是用来控制客户端的消息处理窗口大小的。
> 找到一个合适的值并不容易，通常需要结合业务场景做一定的压测来找到这个值。官方的建议给到100-300，可以以这个值作为起点一步步试出来。

QoS决定了信道上`in flight`的消息的条数。配合批量的ack/nack，整个rabbitmq的处理效率可以获得很大的提高。当然，这些参数的配合还需要考虑到客户端的处理能力，本身使用队列的一大目的就是防止过大的流量压垮后端。

一个极端的例子就是自动ack模式，在这个模式下QoS是没有限制的导致整个投递效率非常高，但是你客户端受得了吗？我能想到的唯一场景只有一个日志记录，这种因为逻辑简单并且允许部分丢失所以也还好。

另外QoS还牵扯到一个特殊情况：当网络出现异常时如果批量ack且QoS大于1，那么很有可能还有几条in flight的消息最终到不了消费端。针对这种情况其实需要稍微改变下思路，我们只需要做到永远在处理完毕一批消息之后才进行批量ack即可。

最后是当网络异常时的一些处理策略：

+ 网络异常的情况中连接执行recovery，此时delivery tags都会过期失效，rabbitmq客户端并不会发送带有过期tag的ack消息。这又会进一步导致rabbitmq broker重发所有没有收到ack确认的消息，因此consumer一定要能够处理重复达到的消息才行：
    > Acknowledgements with stale delivery tags will not be sent. Applications that use manual acknowledgements and automatic recovery must be capable of handling redeliveries.

+ rabbitmq会针对重传的消息设置`redelivered`标志值，这意味着consumer可能在之前有处理过该条消息；为什么是可能，原因在于该条消息可能刚发送出去，还在传递过程中，整个连接就断开了！所以很不幸，该属性为`true`的时候并不能说明客户端已经处理过该条消息；不过可以确信的是如果值为`false`，那么这条消息一定没有被处理过！

+ 采用nack告知rabbitmq node时要注意`requeue/redelivery loop`这种情况的发生，常见的解决办法是跟踪当前消息的`redelivery`次数，要么做延迟requeueing，要么标记状态直接丢弃。



### 参考链接

https://www.rabbitmq.com/dotnet-api-guide.html
https://www.rabbitmq.com/confirms.html
https://www.rabbitmq.com/consumers.html
https://www.rabbitmq.com/publishers.html


