using Microsoft.Extensions.Caching.Memory;
using Planura.Core.Application.Abstraction.BookingAgreement;

namespace Planura.Infrastructure.BookingAgreement;

/// <summary>
/// <see cref="IAgreementPreviewStore"/> backed by <see cref="IMemoryCache"/> with a sliding
/// expiration, so a Booking Agreement generated at the payment step survives as long as the client
/// is actively working through it but is dropped once they walk away. Tokens are opaque GUIDs, so
/// a client cannot forge or point one at another user's agreement.
/// </summary>
public sealed class MemoryAgreementPreviewStore : IAgreementPreviewStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    private readonly IMemoryCache _cache;

    public MemoryAgreementPreviewStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string Put(AgreementPreviewEntry entry)
    {
        var token = Guid.NewGuid().ToString("N");
        _cache.Set(CacheKey(token), entry, new MemoryCacheEntryOptions { SlidingExpiration = Ttl });
        return token;
    }

    public AgreementPreviewEntry? TryGet(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return _cache.TryGetValue(CacheKey(token), out AgreementPreviewEntry? entry) ? entry : null;
    }

    public void Remove(string token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            _cache.Remove(CacheKey(token));
        }
    }

    private static string CacheKey(string token) => $"agreement-preview:{token}";
}
