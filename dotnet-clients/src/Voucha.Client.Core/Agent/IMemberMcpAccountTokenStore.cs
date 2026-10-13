namespace Voucha.Client.Core.Agent;

/// <summary>Allows account cleanup without OAuth discovery, including after a cold start.</summary>
public interface IMemberMcpAccountTokenStore : IMemberMcpOAuthTokenStore
{
  Task SavePendingAsync(string accountId, string state, string verifier, Uri redirectUri);
  Task ClearPendingAsync(string accountId);
  Task<bool> HasAccountGrantAsync(string accountId);
  Task<IReadOnlyList<MemberMcpStoredGrant>> TakeAccountAsync(string accountId);
  Task ClearAccountAsync(string accountId);
  Task ClearAllAccountsAsync();
}

public sealed record MemberMcpStoredGrant(MemberMcpOAuthTokenScope Scope, MemberMcpOAuthTokens Tokens);

/// <summary>Small encrypted key-value boundary implemented by the native app.</summary>
public interface IMemberMcpProtectedValues
{
  Task<string?> ReadAsync(string key);
  Task WriteAsync(string key, string value);
  Task RemoveAsync(string key);
}
