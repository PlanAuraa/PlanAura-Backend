using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Xunit;

namespace Planura.Tests.Specifications;

public class RevenuePaymentsByVendorSpecificationTests
{
    private static Payment Payment(long vendorId, PaymentStatus status) =>
        new() { VendorId = vendorId, Status = status };

    [Theory]
    // Fully-captured = revenue: full path (Completed) and deposit path once its remainder was collected (FullyPaid).
    [InlineData(PaymentStatus.Completed, true)]
    [InlineData(PaymentStatus.FullyPaid, true)]
    // Refunded drops out, so a refunded payment stops counting toward the vendor's revenue.
    [InlineData(PaymentStatus.Refunded, false)]
    // Deposit-only / in-flight states are not revenue — the remainder isn't captured yet.
    [InlineData(PaymentStatus.DepositPaid_RemainderDue, false)]
    [InlineData(PaymentStatus.RemainderFailed, false)]
    [InlineData(PaymentStatus.RemainderCharging, false)]
    [InlineData(PaymentStatus.DepositAuthorized, false)]
    [InlineData(PaymentStatus.Authorized, false)]
    [InlineData(PaymentStatus.Pending, false)]
    public void Criteria_MatchesOnlyFullyCapturedStatuses(PaymentStatus status, bool expected)
    {
        var predicate = new RevenuePaymentsByVendorSpecification(7).Criteria!.Compile();

        Assert.Equal(expected, predicate(Payment(7, status)));
    }

    [Fact]
    public void Criteria_ExcludesOtherVendorsPayments()
    {
        var predicate = new RevenuePaymentsByVendorSpecification(7).Criteria!.Compile();

        Assert.False(predicate(Payment(8, PaymentStatus.Completed)));
        Assert.True(predicate(Payment(7, PaymentStatus.Completed)));
    }
}
