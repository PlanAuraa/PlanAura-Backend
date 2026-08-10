namespace Planura.Core.Application.Common;

public static class StripeAmountConverter
{
    public static long ToSmallestUnit(decimal amount)
        => Convert.ToInt64(Math.Round(amount * 100m, MidpointRounding.AwayFromZero));

    public static decimal FromSmallestUnit(long amount) => amount / 100m;
}
