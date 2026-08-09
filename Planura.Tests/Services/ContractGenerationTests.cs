using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Planura.Core.Application.Abstraction.Contract;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services.Contract;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Shared.Errors.Models;
using Xunit;

namespace Planura.Tests.Services;

/// <summary>
/// Guards the property that motivated the contract-generation redesign: the document must be a
/// function of THIS transaction. Because the AI itself is mocked here, these tests assert on what
/// actually reaches it - the fact sheet built from the booking - plus the validation that runs on what
/// comes back. That is precisely where the old implementation failed: it sent roughly a dozen fields,
/// none of which described the service being bought, so every contract came back generic.
/// </summary>
public class ContractGenerationTests
{
    private readonly Mock<IGeminiService> _geminiMock = new();
    private readonly Mock<IPdfService> _pdfMock = new();
    private readonly List<GeminiTextGenerationRequest> _requests = new();

    public ContractGenerationTests()
    {
        _pdfMock.Setup(p => p.GenerateContractPdf(It.IsAny<ContractPdfModel>())).Returns(new byte[] { 1, 2, 3 });
    }

    private ContractService CreateService() =>
        new(_geminiMock.Object, _pdfMock.Object, NullLogger<ContractService>.Instance);

    /// <summary>Records every prompt sent, and answers analysis/draft calls with schema-valid stubs.</summary>
    private void SetupGemini(Func<int, string>? draftFactory = null)
    {
        var draftCall = 0;

        _geminiMock
            .Setup(g => g.GenerateTextAsync(It.IsAny<GeminiTextGenerationRequest>(), It.IsAny<CancellationToken>()))
            .Returns((GeminiTextGenerationRequest request, CancellationToken _) =>
            {
                _requests.Add(request);

                // The analysis stage is the one whose system instruction talks about extracting the deal.
                var isAnalysis = request.SystemInstruction.Contains("contracts analyst", StringComparison.OrdinalIgnoreCase);
                if (isAnalysis)
                {
                    return Task.FromResult(EmptyAnalysisJson);
                }

                var response = draftFactory is null
                    ? CompleteDraftJson(_requests[^1].Prompt)
                    : draftFactory(draftCall);
                draftCall++;
                return Task.FromResult(response);
            });
    }

    private static string EmptyAnalysisJson => JsonSerializer.Serialize(new ContractDealAnalysis
    {
        ServiceSummary = "Summary",
        ScopeItems = new List<string> { "Scope" },
        Deliverables = new List<string> { "Deliverable" },
        VendorCommitments = new List<string> { "Commitment" },
        ClientObligations = new List<string> { "Obligation" },
        FinancialTerms = new List<string> { "Term" },
        RequiredClauses = new List<ContractClausePlan> { new() { Title = "Scope", Reason = "Because" } }
    });

    /// <summary>
    /// A draft that echoes the whole prompt back into a clause. Artificial, but it means every source
    /// fact is present, so validation passes and the test isolates what it means to isolate.
    /// </summary>
    private static string CompleteDraftJson(string prompt) => JsonSerializer.Serialize(new ContractDraft
    {
        Title = "Event Booking Contract",
        Preamble = "Preamble.",
        Sections = new List<ContractDraftSection>
        {
            new() { Title = "Scope of Services", Paragraphs = new List<string> { prompt } }
        }
    });

    /// <summary>A draft that mentions nobody and no amount - the failure mode validation must catch.</summary>
    private static string HollowDraftJson => JsonSerializer.Serialize(new ContractDraft
    {
        Title = "Event Booking Contract",
        Preamble = "The parties agree as follows.",
        Sections = new List<ContractDraftSection>
        {
            new()
            {
                Title = "Scope of Services",
                Paragraphs = new List<string> { "The vendor shall provide services to the client as agreed." }
            }
        }
    });

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

    // ---------------------------------------------------------------- Acceptance criteria

    [Fact]
    public void FactSheet_CarriesTheServiceBeingBought_NotJustNamesAndPrice()
    {
        var sheet = Factory().Create(PhotographyBooking()).BuildFactSheet();

        // Everything below existed in the database before this change and none of it reached the AI.
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

        // Not merely different names and numbers: each carries scope the other has no concept of.
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

        // No labelled blanks the model could feel invited to fill in.
        Assert.DoesNotContain("N/A", sheet);
        Assert.DoesNotContain("Style and preference notes", sheet);
        Assert.DoesNotContain("Guest count for this booking", sheet);

        // And the gaps are surfaced explicitly as things the parties must still agree.
        var unknowns = context.BuildUnknownFactSheet();
        Assert.Contains(unknowns, u => u.Contains("guests", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(unknowns, u => u.Contains("includes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ThreeDifferentBookings_SendThreeMateriallyDifferentPromptsToTheAi()
    {
        SetupGemini();
        var service = CreateService();
        var factory = Factory();

        await service.GenerateBookingContractAsync(factory.Create(PhotographyBooking()));
        await service.GenerateBookingContractAsync(factory.Create(CateringBooking()));
        await service.GenerateBookingContractAsync(factory.Create(PlanningBooking()));

        // Two Gemini calls per contract (analysis + draft), none of them repaired.
        Assert.Equal(6, _requests.Count);

        var drafts = _requests.Where(r => r.SystemInstruction.Contains("contracts lawyer")).Select(r => r.Prompt).ToList();
        Assert.Equal(3, drafts.Count);
        Assert.Equal(3, drafts.Distinct().Count());

        Assert.Contains("Photography", drafts[0]);
        Assert.Contains("Catering", drafts[1]);
        Assert.Contains("Event Planning", drafts[2]);

        // Scope from one booking must never appear in another's prompt.
        Assert.DoesNotContain("500 edited photographs", drafts[1]);
        Assert.DoesNotContain("gluten-free", drafts[2]);
        Assert.DoesNotContain("Table Nine Catering", drafts[0]);
    }

    [Fact]
    public async Task SameClientAndVendor_DifferentBookings_ProduceDifferentPrompts()
    {
        SetupGemini();
        var service = CreateService();
        var factory = Factory();

        var first = PhotographyBooking();

        var secondDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(120));
        var secondStart = new DateTimeOffset(secondDate.ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero);

        // Same two parties, a different package and slot — the case where the old implementation
        // produced near-identical documents.
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

        await service.GenerateBookingContractAsync(factory.Create(first));
        await service.GenerateBookingContractAsync(factory.Create(second));

        var drafts = _requests.Where(r => r.SystemInstruction.Contains("contracts lawyer")).Select(r => r.Prompt).ToList();

        Assert.Contains("500 edited photographs", drafts[0]);
        Assert.Contains("120 edited photographs", drafts[1]);
        Assert.DoesNotContain("500 edited photographs", drafts[1]);
    }

    [Fact]
    public async Task Draft_MissingMaterialFacts_IsRegeneratedWithThoseFactsNamed()
    {
        // First draft omits everything; second echoes the prompt back and therefore contains it all.
        SetupGemini(call => call == 0 ? HollowDraftJson : CompleteDraftJson(_requests[^1].Prompt));

        var service = CreateService();
        await service.GenerateBookingContractAsync(Factory().Create(PhotographyBooking()));

        var drafts = _requests.Where(r => r.SystemInstruction.Contains("contracts lawyer")).ToList();
        Assert.Equal(2, drafts.Count);

        var repair = drafts[1].Prompt;
        Assert.Contains("MANDATORY CORRECTION", repair);
        Assert.Contains("Mariam Hassan", repair);
        Assert.Contains("25,000.00", repair);
    }

    [Fact]
    public async Task Draft_StillMissingTheDealItself_IsRefusedRatherThanIssued()
    {
        SetupGemini(_ => HollowDraftJson);
        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(
            () => service.GenerateBookingContractAsync(Factory().Create(PhotographyBooking())));

        _pdfMock.Verify(p => p.GenerateContractPdf(It.IsAny<ContractPdfModel>()), Times.Never);
    }

    [Fact]
    public async Task GeneratedContract_IsRenderedFromStructuredClauses_NotParsedProse()
    {
        SetupGemini();
        ContractPdfModel? captured = null;
        _pdfMock.Setup(p => p.GenerateContractPdf(It.IsAny<ContractPdfModel>()))
            .Callback<ContractPdfModel>(m => captured = m)
            .Returns(new byte[] { 1 });

        var service = CreateService();
        await service.GenerateBookingContractAsync(Factory().Create(PhotographyBooking()));

        Assert.NotNull(captured);
        Assert.NotEmpty(captured!.Sections);
        Assert.Equal("Scope of Services", captured.Sections[0].Title);

        // Cover facts are built server-side from the context, so they cannot drift from the booking.
        Assert.Contains(captured.SummaryItems, i => i.Label == "Package" && i.Value == "Signature Wedding Day");
        Assert.Contains(captured.SummaryItems, i => i.Label == "Deposit Now");
        Assert.Contains(captured.SummaryItems, i => i.Label == "Duration" && i.Value == "8 hours");
    }

    [Fact]
    public async Task EveryGeneration_RequestsSchemaConstrainedJson()
    {
        SetupGemini();
        var service = CreateService();
        await service.GenerateBookingContractAsync(Factory().Create(CateringBooking()));

        Assert.All(_requests, r =>
        {
            Assert.Equal("application/json", r.ResponseMimeType);
            Assert.NotNull(r.ResponseSchema);
        });
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
}
