---
title: "Durable Function で失敗したインスタンスを再実行する"
emoji: "😊"
type: "tech" # tech: 技術記事 / idea: アイデア
topics: []
published: false
---

## これは何
Durable Function で、Activity や SubOrchestrator で例外が発生した時に、うまい具合に再試行する方法が SDK でいくつか用意されています。そういった事を紹介してくださっている記事もいくつかあります。

しかし、Orchestrator インスタンスが失敗した時、同じ InstanceId を使って再試行するにはどうすればよいのかよくわからなかったので考えをまとめておきます。

※サンプルコードはインプロセスモデルを前提にしています。

## Activity や SubOrchestrator の再試行
詳しいことは様々な記事で紹介されているので軽く触れておきます。

めちゃめちゃシンプルにかけて、Activity や SubOrchestrator の呼び出し時に再試行用のメソッドを呼び出して、再試行のポリシーを決めてあげるだけです。

```cs
[FunctionName(nameof(TestOrchestrator))]
public async Task TestOrchestrator(
    [OrchestrationTrigger] IDurableOrchestrationContext orchestrationContext,
    ILogger log,
    CancellationToken cancellationToken)
{
    // サブオーケストレータの呼び出し
    await orchestrationContext.CallSubOrchestratorWithRetryAsync(nameof(TestSubOrchestrator), new RetryOptions(TimeSpan.FromSeconds(1), 5)
    {
        RetryTimeout = TimeSpan.FromMinutes(1)
    }, cancellationToken);

    // アクティビティの呼び出し
    await orchestrationContext.CallActivityWithRetryAsync(nameof(TestActivityAsync), new RetryOptions(TimeSpan.FromSeconds(5), 10)
    {
        RetryTimeout = TimeSpan.FromMinutes(5),
    }, cancellationToken);
}
```

ちょーシンプル。

参考: https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-error-handling?tabs=csharp-inproc#automatic-retry-on-failure

## Restart を使う
`IDurableOrchestrationClient` には `RestartAsync` が定義されています。これを使ってみます。

https://learn.microsoft.com/en-us/dotnet/api/microsoft.azure.webjobs.extensions.durabletask.idurableorchestrationclient.restartasync?view=azure-dotnet

失敗した Durable Instance の InstanceId を何らかの方法で取得して (Durable Orchestrator の失敗時に通知させるなど)、その Id を使って Restart させます。InstanceId を受け取り、Restart させる Http Trigger Function を作ってみます。

```cs
[FunctionName(nameof(TestInstanceRestart))]
public async Task<IActionResult> TestInstanceRestart(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "{instanceId}")]
    HttpRequest req,
    string instanceId,
    [DurableClient] IDurableOrchestrationClient durableClient,
    ILogger log,
    CancellationToken cancellationToken)
{
    var status = await durableClient.GetStatusAsync(instanceId);

    if (status == null)
    {
        throw new InvalidOperationException();
    }

    if (status.RuntimeStatus != OrchestrationRuntimeStatus.Failed)
    {
        throw new InvalidOperationException();
    }

    await durableClient.RestartAsync(instanceId, true);

    return new OkResult();
}
```

例外メッセージが手抜きなのは許してください。`RestartAsync` の第二パラメータに `true` を渡すと新しいインスタンスを作ってくれます。また、対象インスタンスを初回起動したときに渡したパラメータはそのまま使いまわしてくれます。

実装を見てもらうとわかるのですが、`bool` 値で `StartNewAsync` に渡す InstanceId を切り替えているだけでした。インプットパラメータは [DurableOrchestrationStatus](https://learn.microsoft.com/en-us/dotnet/api/microsoft.azure.webjobs.extensions.durabletask.durableorchestrationstatus?view=azure-dotnet) の Input プロパティをそのまま使っているため初回起動時と同じパラメータが渡るよって理屈なのかなと。
https://github.com/Azure/azure-functions-durable-extension/blob/4714ad1b0f4534a28d37a6871847543b8d43abc8/src/WebJobs.Extensions.DurableTask/ContextImplementations/DurableClient.cs#L1023-L1034

`false` を渡すと同じ InstanceId で起動することになるのですが、その場合 [`StartNewAsync`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.azure.webjobs.extensions.durabletask.idurableorchestrationclient.startnewasync?view=azure-dotnet) はどんな挙動になるのだろう？という疑問がわいてきます。

結論から言うと、

- 既存 Instance (Failed で終了してたもの) で再実行が行われる
- Generation の値がインクリメントされる

です。

これは Task Hub として Storage Account を使っていれば、という理解です。間違ってたら教えてください。

https://github.com/Azure/durabletask/blob/93b2dde58366da95165fe8c8512b577c84628813/src/DurableTask.AzureStorage/AzureStorageOrchestrationService.cs#L1694

Task Hub is 何や Storage Provider の話は別でまとめたい。

### Instance のステータスが Failed 以外の時に RestartAsync を呼ぶ
結論、ステータスによってはこけます。

この辺りのコードを読んでもらえればわかるのですが、

https://github.com/Azure/azure-functions-durable-extension/blob/4714ad1b0f4534a28d37a6871847543b8d43abc8/src/WebJobs.Extensions.DurableTask/ContextImplementations/DurableClient.cs#L215-L228
https://github.com/Azure/durabletask/blob/93b2dde58366da95165fe8c8512b577c84628813/src/DurableTask.AzureStorage/AzureStorageOrchestrationService.cs#L1710-L1719

`StartNewAsync` の対象インスタンスが [`OrchestrationStatus`](https://github.com/Azure/durabletask/blob/93b2dde58366da95165fe8c8512b577c84628813/src/DurableTask.Core/OrchestrationStatus.cs) のうち、`Running`、`ContinuedAsNew`、`Pending` のどれかであれば例外を投げるようになっています。

簡単に `OrchestrationStatus` の値と、それぞれの時に `StartNewAsync` を呼び出すとどうなるのかをまとめると、

| OrchestrationStatus | StartNewAsync 呼び出し時の挙動 |
| ---- | ---- |
| `Running`、`ContinuedAsNew`、`Pending` | 例外 |
| `Completed`、`Failed`、`Canceled`、`Terminated`、`Suspended` | 再開可能 |

となります。

## Rewind を使う