using System;

namespace Planura.Core.Application.Models.AdminClient
{
    /// <summary>Row shape for the "Client Management" admin list (AdminDashboardPlan.md 2.5).</summary>
    public class AdminClientListItemDto
    {
        public long ClientId { get; set; }
        public long UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? City { get; set; }
        public bool IsAccountActive { get; set; }
        public int BookingCount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class AdminClientFilterDto
    {
        public string? Search { get; set; }
        public string? City { get; set; }
        public bool? IsAccountActive { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class AdminClientDetailsDto
    {
        public long ClientId { get; set; }
        public long UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? City { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public bool IsAccountActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public int EventPlanCount { get; set; }
        public int BookingCount { get; set; }
        public decimal TotalSpend { get; set; }

        public List<AdminClientEventPlanDto> EventPlans { get; set; } = new();
        public List<AdminClientBookingDto> Bookings { get; set; } = new();
    }

    public class AdminClientEventPlanDto
    {
        public long Id { get; set; }
        public string? Title { get; set; }
        public string EventType { get; set; } = null!;
        public DateOnly? EventDate { get; set; }
        public string Status { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class AdminClientBookingDto
    {
        public long Id { get; set; }
        public string VendorName { get; set; } = null!;
        public DateOnly EventDate { get; set; }
        public decimal? AgreedPrice { get; set; }
        public string Status { get; set; } = null!;
        public string PaymentStatus { get; set; } = null!;
    }
}
