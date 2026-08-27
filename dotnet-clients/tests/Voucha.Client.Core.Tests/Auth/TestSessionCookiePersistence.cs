using Voucha.Client.Core.Auth;

namespace Voucha.Client.Core.Tests.Auth;

internal sealed class TestSessionCookiePersistence : ISessionCookiePersistence
{
  public string? Value { get; private set; }
  public int ReadCount { get; private set; }
  public int WriteCount { get; private set; }
  public int ClearCount { get; private set; }

  public Task<string?> ReadAsync(CancellationToken cancellationToken = default)
  {
    ReadCount++;
    return Task.FromResult(Value);
  }

  public Task WriteAsync(string serializedCookies, CancellationToken cancellationToken = default)
  {
    WriteCount++;
    Value = serializedCookies;
    return Task.CompletedTask;
  }

  public Task ClearAsync(CancellationToken cancellationToken = default)
  {
    Value = null;
    ClearCount++;
    return Task.CompletedTask;
  }
}
