using PdfToolStack.Domain.ValueObjects;

namespace PdfToolStack.Domain.Interfaces;

public interface ICloudAuthStateStore
{
    Task SaveAsync(CloudAuthState authState, CancellationToken ct = default);
    Task<CloudAuthState?> GetAsync(string provider, string state, CancellationToken ct = default);
    Task RemoveAsync(string provider, string state, CancellationToken ct = default);
}