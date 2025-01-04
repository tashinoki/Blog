---
title: ""
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

## Rewind を使う