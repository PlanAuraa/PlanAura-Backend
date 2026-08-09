using System.Globalization;
using Microsoft.Extensions.Options;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;

namespace Planura.Core.Application.Services.Contract;

/// <summary>Everything the booking flow has already resolved, handed over in one piece.</summary>
public sealed class BookingContractInput
{
    public required Client Client { get; init; }
    public required ApplicationUser ClientUser { get; init; }
    public required Vendor Vendor { get; init; }
    public required ApplicationUser VendorUser { get; init; }
    public required EventPlan EventPlan { get; init; }
    public required VendorAvailability Slot { get; init; }
    public required VendorPackage Package { get; init; }

    /// <summary>Vendor's service category, when the vendor is classified. Null is tolerated.</summary>
    public ServiceCategory? Category { get; init; }

    public required DateOnly EventDate { get; init; }
    public int? GuestCount { get; init; }

    /// <summary>Requirements this client stated for this booking. May be empty.</summary>
    public IReadOnlyList<string> ClientRequirements { get; init; } = Array.Empty<string>();

    public string? ClientNote { get; init; }

    public required string Currency { get; init; }
    public required decimal TotalAmount { get; init; }
    public required bool IsDepositSchedule { get; init; }
    public decimal DepositAmount { get; init; }

    public long? BookingRequestId { get; init; }
    public string? BookingStatus { get; init; }
}

public interface IBookingContractContextFactory
{
    /// <summary>
    /// Assembles a fresh <see cref="ContractGenerationContext"/> for one booking. A new instance is
    /// built on every call from the entities passed in, so no state can carry between generations.
    /// </summary>
    ContractGenerationContext Create(BookingContractInput input);
}

/// <summary>
/// Turns resolved booking entities into the grounded fact sheet the AI drafts from.
/// <para>
/// This class is where the fix for repetitive contracts actually lands. Previously the booking flow
/// forwarded roughly a dozen fields - names, contacts, event type, date, city, guest count, price -
/// and discarded everything that distinguishes one booking from another: which package was bought,
/// what the vendor said it includes, how long the booked slot runs, how the money is actually split,
/// and which refund schedule the platform will really enforce. With those absent the model had
/// nothing to be specific about and fell back on generic terms. Everything recovered here is data the
/// system already held; nothing is invented, and anything genuinely absent stays absent.
/// </para>
/// </summary>
public sealed class BookingContractContextFactory : IBookingContractContextFactory
{
    private static readonly char[] ListSeparators = { '\n', '\r', ';', '•', '|', '·' };

    private readonly BookingOptions _bookingOptions;

    public BookingContractContextFactory(IOptions<BookingOptions> bookingOptions)
    {
        _bookingOptions = bookingOptions.Value;
    }

    public ContractGenerationContext Create(BookingContractInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var currency = string.IsNullOrWhiteSpace(input.Currency) ? "EGP" : input.Currency.ToUpperInvariant();
        var remainder = input.IsDepositSchedule
            ? Math.Round(input.TotalAmount - input.DepositAmount, 2, MidpointRounding.AwayFromZero)
            : 0m;

        return new ContractGenerationContext
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Currency = currency,

            Client = new ContractPartyContext
            {
                LegalName = Clean(input.ClientUser.FullName),
                RepresentativeName = Clean(input.ClientUser.FullName),
                Email = Clean(input.ClientUser.Email),
                Phone = Clean(input.ClientUser.PhoneNumber),
                City = Clean(input.Client.City),
                PartyType = "Individual client booking through the Planura platform"
            },

            Vendor = new ContractPartyContext
            {
                LegalName = Clean(input.Vendor.BusinessName),
                RepresentativeName = Clean(input.VendorUser.FullName),
                Email = Clean(input.VendorUser.Email),
                Phone = Clean(input.VendorUser.PhoneNumber),
                Address = Clean(input.Vendor.Address),
                City = Clean(input.Vendor.City),
                PartyType = input.Vendor.VendorType == VendorType.Business
                    ? "Registered business operating as an independent contractor"
                    : "Individual practitioner operating as an independent contractor",
                BusinessDescription = Clean(input.Vendor.BusinessDescription),
                TrackRecord = BuildTrackRecord(input.Vendor)
            },

            Service = new ContractServiceContext
            {
                Category = Clean(input.Category?.NameEn),
                PackageTitle = Clean(input.Package.Title),
                PackageDescription = Clean(input.Package.Description),
                Inclusions = Clean(input.Package.Includes),
                InclusionItems = SplitList(input.Package.Includes),
                MaxGuests = input.Package.MaxGuests
            },

            Booking = new ContractBookingContext
            {
                EventType = Clean(input.EventPlan.EventType),
                EventTitle = Clean(input.EventPlan.Title),
                EventDate = input.EventDate,
                // The booked slot is the real, agreed service window. Collapsing it to a date - as the
                // previous implementation did - is what erased duration from every contract.
                StartAt = input.Slot.StartAt,
                EndAt = input.Slot.EndAt,
                City = Clean(input.EventPlan.City) ?? Clean(input.Vendor.City),
                GuestCount = input.GuestCount ?? input.EventPlan.GuestCount,
                StyleNotes = Clean(input.EventPlan.StyleNotes),
                Status = Clean(input.BookingStatus)
            },

            Financials = new ContractFinancialContext
            {
                PackageBasePrice = input.Package.BasePrice,
                TotalAmount = input.TotalAmount,
                IsDepositSchedule = input.IsDepositSchedule,
                DepositAmount = input.IsDepositSchedule ? input.DepositAmount : null,
                DepositPercentage = input.IsDepositSchedule ? _bookingOptions.DepositPercentage : null,
                RemainderAmount = input.IsDepositSchedule ? remainder : null,
                RemainderDueDate = input.IsDepositSchedule
                    ? input.EventDate.AddDays(-_bookingOptions.RemainderChargeLeadDays)
                    : null,
                PaymentMechanism =
                    "Paid through the Planura platform by card. The amount payable at booking is authorized " +
                    "when the booking request is submitted and captured when the vendor accepts it.",
                SettlementTerms =
                    "Funds are collected by Planura on the vendor's behalf and settled to the vendor under " +
                    "the vendor's own agreement with Planura.",
                ClientBudget = input.EventPlan.BudgetTotal
            },

            Policies = new ContractPolicyContext
            {
                // Straight from configuration, so the contract's refund terms are the ones the platform
                // will actually apply rather than plausible-sounding industry defaults.
                CancellationTiers = _bookingOptions.CancellationTiers
                    .Select(t => new ContractCancellationTier
                    {
                        MinDaysBefore = t.MinDaysBefore,
                        RefundPercent = t.RefundPercent
                    })
                    .ToList(),
                AutoConfirmAfterDays = _bookingOptions.AutoConfirmAfterDays > 0
                    ? _bookingOptions.AutoConfirmAfterDays
                    : null,
                VendorResponseWindowHours = _bookingOptions.HoldTtlHours,
                RemainderChargeLeadDays = input.IsDepositSchedule ? _bookingOptions.RemainderChargeLeadDays : null,
                GracePeriodDays = input.IsDepositSchedule ? _bookingOptions.GracePeriodDays : null,
                CancellationRequiresPlatformReview = true
            },

            ClientRequirements = input.ClientRequirements
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .ToList(),

            ClientNote = Clean(input.ClientNote),

            Trace = new ContractTraceIds
            {
                ClientId = input.Client.Id,
                VendorId = input.Vendor.Id,
                VendorPackageId = input.Package.Id,
                BookingRequestId = input.BookingRequestId,
                EventPlanId = input.EventPlan.Id,
                AvailabilityId = input.Slot.Id
            }
        };
    }

    private static string? BuildTrackRecord(Vendor vendor)
    {
        if (vendor.TotalCompletedBookings <= 0)
        {
            return null;
        }

        var record = $"{vendor.TotalCompletedBookings.ToString(CultureInfo.InvariantCulture)} bookings completed on the platform";

        if (vendor.TotalReviews > 0)
        {
            record += $", average client rating {vendor.AvgRating.ToString("0.#", CultureInfo.InvariantCulture)} from " +
                      $"{vendor.TotalReviews.ToString(CultureInfo.InvariantCulture)} reviews";
        }

        return record;
    }

    /// <summary>
    /// Splits the vendor's free-text inclusions into discrete commitments. Vendors write this field
    /// however they like, so line breaks and bullet characters are tried first; a single line of
    /// comma-separated items is a common enough style to be worth splitting too, but only when the
    /// result looks like a genuine list rather than one sentence containing a comma.
    /// </summary>
    private static List<string> SplitList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        var parts = value
            .Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.TrimStart('-', '*', '•', ' ').Trim())
            .Where(p => p.Length > 0)
            .ToList();

        if (parts.Count > 1)
        {
            return parts;
        }

        var single = parts.Count == 1 ? parts[0] : value.Trim();
        var commaParts = single
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length > 0)
            .ToList();

        // Two commas or more, and no part long enough to be prose: treat it as a list.
        return commaParts.Count >= 3 && commaParts.All(p => p.Length <= 60)
            ? commaParts
            : parts;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
