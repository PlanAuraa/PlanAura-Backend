namespace Planura.Core.Application.Models;

public class AcceptBookingRequestDto
{
    /// <summary>Must be true — the vendor ticked "I have read and agree to the Booking Agreement" before accepting.</summary>
    public bool AgreementAccepted { get; set; }
}
