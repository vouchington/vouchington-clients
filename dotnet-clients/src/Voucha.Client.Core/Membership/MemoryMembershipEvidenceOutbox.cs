using System.Text.Json;

namespace Voucha.Client.Core.NativeBilling;

/// <summary>
/// Process-memory outbox for unsent verification evidence.
/// Platform secure storage can replace this type later. Evidence is not written to preferences.
/// </summary>
public sealed class MemoryMembershipEvidenceOutbox : IMembershipEvidenceOutbox
{
  private readonly Dictionary<string, JsonElement> stored = new(StringComparer.Ordinal);
  private readonly Lock gate = new();

  public Task SaveAsync(string key, JsonElement evidence, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    lock (gate)
    {
      stored[key] = evidence;
    }

    return Task.CompletedTask;
  }

  public Task RemoveAsync(string key, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    lock (gate)
    {
      stored.Remove(key);
    }

    return Task.CompletedTask;
  }

  public Task<bool> ContainsAsync(string key, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    lock (gate)
    {
      return Task.FromResult(stored.ContainsKey(key));
    }
  }
}
