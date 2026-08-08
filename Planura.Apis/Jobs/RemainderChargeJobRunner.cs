using Hangfire;
using Planura.Core.Application.Services.RemainderChargeJob;

namespace Planura.Apis.Jobs;

/// <summary>
/// Thin Hangfire entry point for the deposit remainder-charge job. Lives in the API composition root so the
/// Application layer stays free of any Hangfire dependency. <see cref="DisableConcurrentExecutionAttribute"/>
/// stops a slow run from overlapping itself on a single node — layer 3 of the no-double-charge design (the
/// Stripe idempotency key remains the hard guarantee across nodes/crashes).
/// </summary>
public class RemainderChargeJobRunner
{
    private readonly IRemainderChargeJob _job;

    public RemainderChargeJobRunner(IRemainderChargeJob job)
    {
        _job = job;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public Task RunAsync() => _job.RunAsync();
}
