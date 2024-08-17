---
title: "Service Bus の Prefetch"
emoji: "📧"
type: "tech" # tech: 技術記事 / idea: アイデア
topics: ["servicebus", "csharp"]
published: true
---

## Azure Service Bus の Prefetch

Azure Service Bus には Prefetch 機能があります。その名の通り、あらかじめ message を取得しておき、Local のキャッシュに保持しておく機能です。この機能を使えば、Service Bus へ message を取得する頻度が減り、スループットが向上するかもしれません。

Service Bus Receiver は Local のキャッシュに message がなくなった時に Service Bus から message を取得します。キャッシュにある場合は、キャッシュから message を取り出して呼び出し元に返却します。この時、Service Bus とのやり取りが発生しません。

PrefetchCount のデフォルトの値は 0 です、これにはいくつかの理由があります。ReceiveMode 毎に考えてみます。

|Mode||
|---|---|
|PeekLock|message のロックは Fetch したタイミングで行われます。つまり、キャッシュで保持されている message にもロックはかけられています。<br>アプリケーションの処理時間が長いと、キャッシュ内の message が処理対象になった時にはすでにロックが破棄されている可能性があります。|
|ReceiveAndDelete|fetch をした段階でその message は他の Receiver から受信できなくなります。<br>もしアプリケーションが途中で落ちた場合、キャッシュに保持された未処理の message はロストすることになります。|

という点があるのでデフォルトでは Prefetch しないようになっているようです。アプリケーションに高スループットが求められ、Prefetch を試す場合には慎重になった方がいいかもしれません。アプリケーションの処理時間に対して Prefetch の数が大きすぎる and ロックの期間が短すぎると、`LockLostException` が発生し、逆にスループットが落ちるかもしれません。

## 試してみる

[ServiceBusReceiver](https://learn.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebusreceiver?view=azure-dotnet) を使って message を Prefetch してみます。Prefetch する件数は Receiver のインスタンスを作る際に [ServiceBusReceiverOptions](https://learn.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebusreceiveroptions.prefetchcount?view=azure-dotnet) を使う事で指定できます。

Prefetch した件数以上を Receive しても意味がないので、Prefetch の件数を 100、Receive する件数を 10 として、100 Receive するまで処理を続けるプログラムで試してみます。その時の実行時間を Prefetch の件数が 0 の時と比較してみます。サンプルコードは以下です。

```csharp
var queue = "test";
var c = new ServiceBusClient(locals);
await using var sender = c.CreateSender(queue);

var messageBatch = await sender.CreateMessageBatchAsync();

foreach(var _ in Enumerable.Range(0, 100))
{
    var message = new ServiceBusMessage(BinaryData.FromString("hoge"));
    messageBatch.TryAddMessage(message);
}

await sender.SendMessagesAsync(messageBatch);


await using var r = c.CreateReceiver(queue, new ServiceBusReceiverOptions
{
    // PrefetchCount = 0
    PrefetchCount = 100
});

var sw = new System.Diagnostics.Stopwatch();

sw.Start();

var receivedCount = 0;
do 
{
    var m = await r.ReceiveMessagesAsync(10, TimeSpan.FromSeconds(30));

    receivedCount += m.Count;
}
while(receivedCount < 100);

sw.Stop();

sw.ElapsedMilliseconds.Dump();
```

PrefetchCount をす指定した時、L48 で `ReceiveMessagesAsync` を呼び出したタイミングで最大 100 件の message を Client の Buffer に持ちます。do-while 文で `ReceiveMessagesAsync` が呼び出される度に Buffer から message を取ってくることになります。つまり Service Bus に接続しなくなります。

軽く計測してみた感じ、PrefetchCount を 0 にした場合と比較して、半分ほどに実行時間が短縮されました。

今回の場合は Prefetch 後の処理が非常に単純で、Buffer 上でロックの有効期限を迎えるという事はありませんでした。本番環境で試す場合には、この例よりもはるかに複雑な処理を行うと思います。その処理のスループット、ロックの有効期間と PrefetchCount のバランスを取ることが大切だと思います。慎重に検討したいですね。

## 参考文献
- https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-prefetch?tabs=dotnet#why-is-prefetch-not-the-default-option
- https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-performance-improvements?tabs=net-standard-sdk-2#prefetching