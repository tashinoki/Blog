using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace KinoToyBoxFunctions;

public class DurableRetryTest
{

    private record DurableRetryTestParameter
    {
        public string Id { get; set; }
    }

    [FunctionName(nameof(TestOrchestratorStartAsync))]
    public async Task TestOrchestratorStartAsync(
        [ServiceBusTrigger("durable-retry-test")] string myQueueItem,
        [DurableClient] IDurableOrchestrationClient orchestrationClient,
        ILogger log,
        CancellationToken cancellationToken)
    {
        await orchestrationClient.StartNewAsync(nameof(TestOrchestrator), new DurableRetryTestParameter { Id = myQueueItem});
    }

    [FunctionName(nameof(TestOrchestrator))]
    public async Task TestOrchestrator(
        [OrchestrationTrigger] IDurableOrchestrationContext orchestrationContext,
        ILogger log,
        CancellationToken cancellationToken)
    {
        var input = orchestrationContext.GetInput<DurableRetryTestParameter>();

        throw new Exception("Application Exception");
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

    [FunctionName(nameof(TestSubOrchestrator))]
    public Task TestSubOrchestrator(
        [OrchestrationTrigger] IDurableOrchestrationContext oeDurableOrchestrationContext,
        ILogger log,
        CancellationToken cancellationToken)
    {
        // 例外が起きるかもしれない処理
        // ...

        return Task.CompletedTask;
    }

    [FunctionName(nameof(TestActivityAsync))]
    public Task TestActivityAsync(
        [ActivityTrigger] IDurableActivityContext activityContext,
        ILogger log,
        CancellationToken cancellationToken)
    {
        // 例外が起きるかもしれない処理
        // ....

        return Task.CompletedTask;
    }

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

        return new OkResult();
    }
}

