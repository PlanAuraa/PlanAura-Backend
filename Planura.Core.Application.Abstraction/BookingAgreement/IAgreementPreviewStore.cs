namespace Planura.Core.Application.Abstraction.BookingAgreement;

/// <summary>
/// The already-generated Booking Agreement for an in-progress booking flow: the AI contract's id
/// and the relative path of its stored PDF. Held only between the client reaching the payment step
/// (preview) and clicking "Confirm &amp; Book" (create) — never persisted.
/// </summary>
public sealed record AgreementPreviewEntry(
    long ClientId, string ContractId, string RelativeUrl, DateTimeOffset GeneratedAt);

/// <summary>
/// Short-lived, in-memory bridge that carries a generated Booking Agreement from the payment-step
/// preview to booking creation. Planura keeps no persistent booking drafts: if the client abandons
/// or refreshes the flow, the entry simply expires and the next visit regenerates. Deliberately not
/// a database entity — there is nothing to persist once the booking either captures the contract or
/// is never created.
/// </summary>
public interface IAgreementPreviewStore
{
    /// <summary>Stores an entry under a fresh opaque token and returns that token.</summary>
    string Put(AgreementPreviewEntry entry);

    /// <summary>Reads the entry for <paramref name="token"/> without removing it, or null if unknown/expired.</summary>
    AgreementPreviewEntry? TryGet(string token);

    /// <summary>
    /// Removes the entry. Called only once a booking has successfully committed, so that a failed
    /// attempt (e.g. a declined card the client then retries) leaves the token usable.
    /// </summary>
    void Remove(string token);
}
