namespace Planura.Core.Application.Common;

public class BookingOptions
{
    public const string SectionName = "Booking";

    public int HoldTtlHours { get; set; } = 48;
    public int PaymentDeadlineHours { get; set; } = 72;
}
