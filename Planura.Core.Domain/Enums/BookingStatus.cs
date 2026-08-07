namespace Planura.Core.Domain.Enums;

public enum BookingStatus
{
    Pending = 1,
    Accepted = 2,
    Rejected = 3,
    Cancelled = 4,
    Completed = 5,
    Expired = 6,

    /// <summary>
    /// The event's slot has ended and the client has been asked to confirm the service was
    /// delivered. Transitions to Completed (client confirms, or the auto-confirm grace window
    /// elapses with no open dispute) — never set directly by EndAt passing alone.
    /// </summary>
    AwaitingConfirmation = 7,

    /// <summary>
    /// The client has requested to cancel an Accepted booking and an admin has not yet decided.
    /// The vendor's slot stays Booked and no refund exists until the admin approves (-> Cancelled)
    /// or rejects (-> back to Accepted).
    /// </summary>
    CancellationRequested = 8
}
