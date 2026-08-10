using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Planura.Core.Application.Abstraction.Contract;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services.Contract;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Xunit;

namespace Planura.Tests.Services;

/// <summary>
/// Guards the property that motivated the contract-generation redesign: the document must be a
/// function of THIS transaction, not a generic template with names swapped in. The booking contract
/// is composed deterministically (see ContractService.BuildTemplateDraft) rather than by an AI call,
/// so these tests assert directly on the fact sheet that feeds it and on the rendered
/// sections/summary the template produces from real booking data - no Gemini mock needed.
/// </summary>
public class ContractGenerationTests
{
    private readonly Mock<IGeminiService> _geminiMock = new();
    private readonly Mock<IPdfService> _pdfMock = new();

    public ContractGenerationTests()
    {
        _pdfMock.Setup(p => p.GenerateContractPdf(It.IsAny<ContractPdfModel>())).Returns(new byte[] { 1, 2, 3 });
    }

    private ContractService CreateService() =>
        new(_geminiMock.Object, _pdfMock.Object, NullLogger<ContractService>.Instance);

    // ---------------------------------------------------------------- Scenario fixtures

    private static BookingContractContextFactory Factory(BookingOptions? options = null) =>
        new(Options.Create(options ?? new BookingOptions()));

    /// <summary>Scenario 1 - wedding photography, deposit path, creative deliverables.</summary>
    private static BookingContractInput PhotographyBooking()
    {
        var eventDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(60));
        var start = new DateTimeOffset(eventDate.ToDateTime(new TimeOnly(14, 0)), TimeSpan.Zero);

        return new BookingContractInput
        {
            Client = new Client { Id = 1, UserId = 101, City = "Cairo" },
            ClientUser = new ApplicationUser { Id = 101, FullName = "Mariam Hassan", Email = "mariam@example.com" },
            Vendor = new Vendor
            {
                Id = 2,
                UserId = 202,
                BusinessName = "Lumen Studio",
                BusinessDescription = "Documentary-style wedding photography.",
                City = "Cairo",
                VendorType = VendorType.Business,
                CategoryId = 7,
                TotalCompletedBookings = 42,
                TotalReviews = 30,
                AvgRating = 4.8m
            },
            VendorUser = new ApplicationUser { Id = 202, FullName = "Omar Fathy", Email = "omar@lumen.example" },
            Category = new ServiceCategory { Id = 7, NameEn = "Photography", Slug = "photography" },
            EventPlan = new EventPlan
            {
                Id = 3,
                ClientId = 1,
                EventType = "Wedding",
                Title = "Mariam & Youssef",
                City = "Cairo",
                GuestCount = 200,
                BudgetTotal = 30000m,
                StyleNotes = "Warm natural light, candid moments."
            },
            Slot = new VendorAvailability { Id = 4, VendorId = 2, StartAt = start, EndAt = start.AddHours(8) },
            Package = new VendorPackage
            {
                Id = 5,
                VendorId = 2,
                Title = "Signature Wedding Day",
                Description = "Full-day documentary coverage.",
                Includes = "8 hours coverage; 2 photographers; 500 edited photographs; online gallery",
                BasePrice = 25000m,
                MaxGuests = 300
            },
            EventDate = eventDate,
            GuestCount = 200,
            ClientRequirements = new[]
            {
                "Deliverables requested by the client: 500 edited photos delivered within 14 days",
                "Special requests: drone shots during the outdoor ceremony"
            },
            Currency = "EGP",
            TotalAmount = 25000m,
            IsDepositSchedule = true,
            DepositAmount = 5000m
        };
    }

    /// <summary>Scenario 2 - catering, full-payment path, consumable deliverables and dietary rules.</summary>
    private static BookingContractInput CateringBooking()
    {
        var eventDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(4));
        var start = new DateTimeOffset(eventDate.ToDateTime(new TimeOnly(18, 30)), TimeSpan.Zero);

        return new BookingContractInput
        {
            Client = new Client { Id = 11, UserId = 111, City = "Alexandria" },
            ClientUser = new ApplicationUser { Id = 111, FullName = "Karim Nabil", Email = "karim@example.com" },
            Vendor = new Vendor
            {
                Id = 12,
                UserId = 212,
                BusinessName = "Table Nine Catering",
                City = "Alexandria",
                VendorType = VendorType.Business,
                CategoryId = 9
            },
            VendorUser = new ApplicationUser { Id = 212, FullName = "Nour Adel", Email = "nour@tablenine.example" },
            Category = new ServiceCategory { Id = 9, NameEn = "Catering", Slug = "catering" },
            EventPlan = new EventPlan
            {
                Id = 13,
                ClientId = 11,
                EventType = "Corporate Dinner",
                City = "Alexandria",
                GuestCount = 120
            },
            Slot = new VendorAvailability { Id = 14, VendorId = 12, StartAt = start, EndAt = start.AddHours(4) },
            Package = new VendorPackage
            {
                Id = 15,
                VendorId = 12,
                Title = "Seated Three-Course Dinner",
                Includes = "Three-course plated menu; waiting staff; crockery and linen; setup and cleanup",
                BasePrice = 84000m,
                MaxGuests = 150
            },
            EventDate = eventDate,
            GuestCount = 120,
            ClientRequirements = new[]
            {
                "Special requests: twenty vegetarian covers and four gluten-free covers",
                "Timing requirements: service must begin promptly at 19:00"
            },
            Currency = "EGP",
            TotalAmount = 84000m,
            IsDepositSchedule = false
        };
    }

    /// <summary>Scenario 3 - event planning, sparse data, to prove absent facts stay absent.</summary>
    private static BookingContractInput PlanningBooking()
    {
        var eventDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(90));
        var start = new DateTimeOffset(eventDate.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);

        return new BookingContractInput
        {
            Client = new Client { Id = 21, UserId = 121 },
            ClientUser = new ApplicationUser { Id = 121, FullName = "Salma Ibrahim" },
            Vendor = new Vendor
            {
                Id = 22,
                UserId = 222,
                BusinessName = "Atlas Event Planning",
                VendorType = VendorType.Individual,
                CategoryId = 3
            },
            VendorUser = new ApplicationUser { Id = 222, FullName = "Hana Saeed" },
            Category = new ServiceCategory { Id = 3, NameEn = "Event Planning", Slug = "event-planning" },
            EventPlan = new EventPlan { Id = 23, ClientId = 21, EventType = "Product Launch" },
            Slot = new VendorAvailability { Id = 24, VendorId = 22, StartAt = start, EndAt = start.AddHours(6) },
            Package = new VendorPackage
            {
                Id = 25,
                VendorId = 22,
                Title = "Launch Day Coordination",
                BasePrice = 12000m
            },
            EventDate = eventDate,
            Currency = "EGP",
            TotalAmount = 12000m,
            IsDepositSchedule = true,
            DepositAmount = 2400m
        };
    }

    // ---------------------------------------------------------------- Fact sheet (unaffected by the
    // template-vs-AI drafting choice - this is the shared input both approaches read from)

    [Fact]
    public void FactSheet_CarriesTheServiceBeingBought_NotJustNamesAndPrice()
    {
        var sheet = Factory().Create(PhotographyBooking()).BuildFactSheet();

        Assert.Contains("Photography", sheet);
        Assert.Contains("Signature Wedding Day", sheet);
        Assert.Contains("500 edited photographs", sheet);
        Assert.Contains("2 photographers", sheet);
        Assert.Contains("8 hours", sheet);
        Assert.Contains("drone shots", sheet);
        Assert.Contains("Warm natural light", sheet);
    }

    [Fact]
    public void FactSheet_DiffersMateriallyBetweenDifferentServices()
    {
        var photography = Factory().Create(PhotographyBooking()).BuildFactSheet();
        var catering = Factory().Create(CateringBooking()).BuildFactSheet();

        Assert.Contains("500 edited photographs", photography);
        Assert.DoesNotContain("500 edited photographs", catering);

        Assert.Contains("gluten-free", catering);
        Assert.DoesNotContain("gluten-free", photography);

        Assert.Contains("Three-course plated menu", catering);
        Assert.DoesNotContain("Three-course plated menu", photography);
    }

    [Fact]
    public void FactSheet_ReflectsTheActualPaymentStructureForThisBooking()
    {
        var deposit = Factory().Create(PhotographyBooking()).BuildFactSheet();
        var full = Factory().Create(CateringBooking()).BuildFactSheet();

        Assert.Contains("Deposit now, remaining balance later", deposit);
        Assert.Contains("20,000.00", deposit);   // 25,000 total less the 5,000 deposit
        Assert.Contains("Full contract amount payable at booking", full);
        Assert.DoesNotContain("Deposit payable at booking", full);
    }

    [Fact]
    public void FactSheet_StatesTheRefundScheduleThePlatformActuallyEnforces()
    {
        var sheet = Factory(new BookingOptions
        {
            CancellationTiers =
            [
                new() { MinDaysBefore = 30, RefundPercent = 100 },
                new() { MinDaysBefore = 14, RefundPercent = 50 },
                new() { MinDaysBefore = 0, RefundPercent = 0 }
            ]
        }).Create(PhotographyBooking()).BuildFactSheet();

        Assert.Contains("30 or more days before the event date: 100%", sheet);
        Assert.Contains("14 or more days before the event date: 50%", sheet);
    }

    [Fact]
    public void FactSheet_OmitsAbsentFactsRatherThanInventingOrBlankingThem()
    {
        var context = Factory().Create(PlanningBooking());
        var sheet = context.BuildFactSheet();

        Assert.DoesNotContain("N/A", sheet);
        Assert.DoesNotContain("Style and preference notes", sheet);
        Assert.DoesNotContain("Guest count for this booking", sheet);

        var unknowns = context.BuildUnknownFactSheet();
        Assert.Contains(unknowns, u => u.Contains("guests", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(unknowns, u => u.Contains("includes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DiagnosticSignature_DistinguishesBookings_WithoutLeakingPersonalData()
    {
        var factory = Factory();
        var photography = factory.Create(PhotographyBooking()).BuildDiagnosticSignature();
        var catering = factory.Create(CateringBooking()).BuildDiagnosticSignature();

        Assert.NotEqual(photography, catering);
        Assert.Contains("category=Photography", photography);
        Assert.DoesNotContain("Mariam Hassan", photography);
        Assert.DoesNotContain("mariam@example.com", photography);
    }

    // ---------------------------------------------------------------- Template generation

    [Fact]
    public async Task GenerateBookingContractAsync_NeverCallsGemini()
    {
        var service = CreateService();
        await service.GenerateBookingContractAsync(Factory().Create(PhotographyBooking()));

        _geminiMock.Verify(
            g => g.GenerateTextAsync(It.IsAny<GeminiTextGenerationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ThreeDifferentBookings_ProduceThreeMateriallyDifferentContracts()
    {
        var service = CreateService();
        var factory = Factory();

        var captured = new List<ContractPdfModel>();
        _pdfMock.Setup(p => p.GenerateContractPdf(It.IsAny<ContractPdfModel>()))
            .Callback<ContractPdfModel>(captured.Add)
            .Returns(new byte[] { 1 });

        await service.GenerateBookingContractAsync(factory.Create(PhotographyBooking()));
        await service.GenerateBookingContractAsync(factory.Create(CateringBooking()));
        await service.GenerateBookingContractAsync(factory.Create(PlanningBooking()));

        Assert.Equal(3, captured.Count);

        string AllText(ContractPdfModel m) =>
            string.Join(" ", m.Sections.SelectMany(s => s.Paragraphs.Concat(s.Items)));

        var photography = AllText(captured[0]);
        var catering = AllText(captured[1]);
        var planning = AllText(captured[2]);

        // Scope from one booking must never appear in another's contract.
        Assert.Contains("500 edited photographs", photography);
        Assert.DoesNotContain("500 edited photographs", catering);
        Assert.DoesNotContain("500 edited photographs", planning);

        Assert.Contains("gluten-free", catering);
        Assert.DoesNotContain("gluten-free", photography);

        Assert.Contains("Photography", photography);
        Assert.Contains("Catering", catering);
        Assert.Contains("Event Planning", planning);
    }

    [Fact]
    public async Task SameClientAndVendor_DifferentBookings_ProduceDifferentContracts()
    {
        var service = CreateService();
        var factory = Factory();

        var first = PhotographyBooking();

        var secondDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(120));
        var secondStart = new DateTimeOffset(secondDate.ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero);

        // Same two parties, a different package and slot — the case where a generic template would
        // have produced near-identical documents.
        var second = new BookingContractInput
        {
            Client = first.Client,
            ClientUser = first.ClientUser,
            Vendor = first.Vendor,
            VendorUser = first.VendorUser,
            Category = first.Category,
            EventPlan = new EventPlan { Id = 33, ClientId = 1, EventType = "Engagement Party", City = "Giza", GuestCount = 40 },
            Slot = new VendorAvailability { Id = 34, VendorId = 2, StartAt = secondStart, EndAt = secondStart.AddHours(3) },
            Package = new VendorPackage
            {
                Id = 35,
                VendorId = 2,
                Title = "Half-Day Session",
                Includes = "3 hours coverage; 1 photographer; 120 edited photographs",
                BasePrice = 9000m
            },
            EventDate = secondDate,
            GuestCount = 40,
            Currency = "EGP",
            TotalAmount = 9000m,
            IsDepositSchedule = true,
            DepositAmount = 1800m
        };

        var captured = new List<ContractPdfModel>();
        _pdfMock.Setup(p => p.GenerateContractPdf(It.IsAny<ContractPdfModel>()))
            .Callback<ContractPdfModel>(captured.Add)
            .Returns(new byte[] { 1 });

        await service.GenerateBookingContractAsync(factory.Create(first));
        await service.GenerateBookingContractAsync(factory.Create(second));

        string AllText(ContractPdfModel m) =>
            string.Join(" ", m.Sections.SelectMany(s => s.Paragraphs.Concat(s.Items)));

        Assert.Contains("500 edited photographs", AllText(captured[0]));
        Assert.Contains("120 edited photographs", AllText(captured[1]));
        Assert.DoesNotContain("500 edited photographs", AllText(captured[1]));
    }

    [Fact]
    public async Task GeneratedContract_IsRenderedFromStructuredSections()
    {
        ContractPdfModel? captured = null;
        _pdfMock.Setup(p => p.GenerateContractPdf(It.IsAny<ContractPdfModel>()))
            .Callback<ContractPdfModel>(m => captured = m)
            .Returns(new byte[] { 1 });

        var service = CreateService();
        await service.GenerateBookingContractAsync(Factory().Create(PhotographyBooking()));

        Assert.NotNull(captured);
        Assert.NotEmpty(captured!.Sections);
        Assert.Equal("Scope of Services", captured.Sections[0].Title);

        // Fixed boilerplate clauses are always present, regardless of booking specifics.
        Assert.Contains(captured.Sections, s => s.Title == "Role of the Platform");
        Assert.Contains(captured.Sections, s => s.Title == "Governing Law");

        // Cover facts are built server-side from the context, so they cannot drift from the booking.
        Assert.Contains(captured.SummaryItems, i => i.Label == "Package" && i.Value == "Signature Wedding Day");
        Assert.Contains(captured.SummaryItems, i => i.Label == "Deposit Now");
        Assert.Contains(captured.SummaryItems, i => i.Label == "Duration" && i.Value == "8 hours");
    }

    [Fact]
    public async Task SparseBooking_OmitsSectionsWithNothingToSay_RatherThanInventingContent()
    {
        ContractPdfModel? captured = null;
        _pdfMock.Setup(p => p.GenerateContractPdf(It.IsAny<ContractPdfModel>()))
            .Callback<ContractPdfModel>(m => captured = m)
            .Returns(new byte[] { 1 });

        // No cancellation tiers configured, so that section is exercised as absent too — the default
        // BookingOptions() ships four tiers, which would otherwise mask this behavior.
        var service = CreateService();
        await service.GenerateBookingContractAsync(
            Factory(new BookingOptions { CancellationTiers = [] }).Create(PlanningBooking()));

        Assert.NotNull(captured);

        // No client requirements/note were given, so that section must not appear at all.
        Assert.DoesNotContain(captured!.Sections, s => s.Title == "Client Requirements");
        Assert.DoesNotContain(captured.Sections, s => s.Title == "Cancellation and Refund Policy");

        // But the gaps the AI-repair path used to chase down are still surfaced explicitly.
        Assert.Contains(captured.Sections, s => s.Title == "Open Points Requiring Agreement");
    }
}
