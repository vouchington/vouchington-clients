using Voucha.Client.Core.Agent;

namespace Voucha.Client.App;

/// <summary>MAUI SecureStorage uses Windows DataProtectionProvider for app-local encrypted values.</summary>
public sealed class MauiMemberMcpProtectedValues : IMemberMcpProtectedValues
{
  public Task<string?> ReadAsync(string key) => SecureStorage.Default.GetAsync(key);

  public Task WriteAsync(string key, string value) => SecureStorage.Default.SetAsync(key, value);

  public Task RemoveAsync(string key)
  {
    SecureStorage.Default.Remove(key);
    return Task.CompletedTask;
  }
}
