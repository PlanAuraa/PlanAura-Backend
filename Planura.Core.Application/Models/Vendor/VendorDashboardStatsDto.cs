namespace Planura.Core.Application.Models.Vendor;

public class VendorDashboardStatsDto
{
    // Booking requests breakdown
    public int TotalBookingRequests { get; set; }
    public int PendingRequests { get; set; }
    public int AcceptedRequests { get; set; }
    public int RejectedRequests { get; set; }
    public int CancelledRequests { get; set; }
    public int CompletedRequests { get; set; }
    public int ExpiredRequests { get; set; }

    // Accepted bookings whose event date is still in the future
    public int UpcomingBookings { get; set; }

    // Sum of captured (completed) payments for this vendor
    public decimal TotalRevenue { get; set; }

    // Reputation (denormalized on the vendor profile)
    public decimal AvgRating { get; set; }
    public int TotalReviews { get; set; }
    public int TotalCompletedBookings { get; set; }

    // Catalog
    public int ActivePackages { get; set; }
}
