# LightMessager
RabbitMQ简单封装

基本概念
exchange
类型
direct：msg的routekey跟queue绑定的routekey一致，则直接转发
fanout：忽略routekey，挂多少个queue就转发多少个msg，类似于广播
topic：可以看成是direct的高级版本，因为这里routekey不光是一致就转发，还可以是满足某种pattern就可以转发
headers：忽略routekey，更高级的一种形式
重要的属性
name, durability（一个durable的exchange可以从broker的重启中存活下来）, auto-delete, arguments（optional; used by plugins and broker-specific features such as message TTL, queue length limit, etc）

queue
重要的属性
name, durability（一个durable的queue可以从broker的重启中存活下来）, auto-delete, arguments

message
可设置的属性
Content type, Content encoding, Routing key, Delivery mode（persistent or not）, Message priority, Message publishing timestamp, Expiration period, Publisher application id等
将一条消息发送至一个durable的queue并不能使该消息persistent，唯一能决定一条消息是否persistent的是其delivery mode属性值
同时注意mandatory和persistent的区别，前者会在消息无法送达的情况下触发basic.return，后者则是会让消息持久化至磁盘



在线程模型上，主要考虑两个类型connection和channle
在当前版本的c#客户端中，一个connection对应着一个线程，抽象的是一个tcp连接，而多个channel通过多路复用共用一个connection
一种常见的策略是应用层面一个线程分配一个独立的channel通过共用一个connection与服务端进行交互
Connections and Channels are meant to be long-lived. 在channel层面更容易出现各种问题导致channel关闭（connection相对来说要稳定一些），因此考虑对channel池化进而达到复用的目的比较可取


















