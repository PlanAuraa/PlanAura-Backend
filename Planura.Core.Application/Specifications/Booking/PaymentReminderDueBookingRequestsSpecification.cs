using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class PaymentReminderDueBookingRequestsSpecification : BaseSpecification<BookingRequest>
{
    public PaymentReminderDueBookingRequestsSpecification(DateTimeOffset reminderCutoff, DateTimeOffset deadlineCutoff)
        : base(booking =>
            booking.Status == BookingStatus.Accepted &&
            booking.PaymentStatus == BookingPaymentStatus.Unpaid &&
            booking.PaymentReminderSentAt == null &&
            booking.RespondedAt != null &&
            booking.RespondedAt <= reminderCutoff &&
            booking.RespondedAt > deadlineCutoff)
    {
    }
}
