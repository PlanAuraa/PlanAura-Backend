namespace Planura.Core.Application.Common;

/// <summary>
/// Shared literals for BookingStatusHistory.Notes entries that are parsed back out elsewhere.
/// The dispute reason has no column of its own - it is written into the note and later extracted
/// for the admin dispute view - so the writer (BookingService.FlagDisputeAsync) and the reader
/// (MappingProfile's AdminDisputeDetailsDto map) must agree on this exact prefix.
/// </summary>
public static class BookingStatusHistoryNotes
{
    public const string DisputeRaisedPrefix = "Dispute raised: ";
    public const string DisputeResolvedPrefix = "Dispute resolved: ";
}
