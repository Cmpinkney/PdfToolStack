using Microsoft.Extensions.Caching.Memory;
using PdfToolStack.Domain.Interfaces;
using PdfToolStack.Domain.ValueObjects;

namespace PdfToolStack.Infrastructure.Auth;

public sealed class InMemoryCloudAuthStateStore : ICloudAuthStateStore
{
    private readonly IMemoryCache _cache;

    public InMemoryCloudAuthStateStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task SaveAsync(CloudAuthState authState, CancellationToken ct = default)
    {
        var key = BuildKey(authState.Provider, authState.State);

        _cache.Set(key, authState, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        });

        return Task.CompletedTask;
    }

    public Task<CloudAuthState?> GetAsync(string provider, string state, CancellationToken ct = default)
    {
        _cache.TryGetValue(BuildKey(provider, state), out CloudAuthState? authState);
        return Task.FromResult(authState);
    }

    public Task RemoveAsync(string provider, string state, CancellationToken ct = default)
    {
        _cache.Remove(BuildKey(provider, state));
        return Task.CompletedTask;
    }

    private static string BuildKey(string provider, string state)
        => $"cloud-auth:{provider}:{state}";
}