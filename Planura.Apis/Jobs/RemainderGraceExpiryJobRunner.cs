using Hangfire;
using Planura.Core.Application.Services.RemainderGraceExpiryJob;

namespace Planura.Apis.Jobs;

/// <summary>
/// Thin Hangfire entry point for the deposit remainder grace-expiry job. Lives in the API composition root
/// so the Application layer stays free of any Hangfire dependency. <see cref="DisableConcurrentExecutionAttribute"/>
/// stops a slow run from overlapping itself on a single node.
/// </summary>
public class RemainderGraceExpiryJobRunner
{
    private readonly IRemainderGraceExpiryJob _job;

    public RemainderGraceExpiryJobRunner(IRemainderGraceExpiryJob job)
    {
        _job = job;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public Task RunAsync() => _job.RunAsync();
}
