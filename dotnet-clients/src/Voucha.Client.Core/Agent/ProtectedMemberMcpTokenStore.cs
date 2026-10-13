using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Voucha.Client.Core.Agent;

/// <summary>One encrypted document per exact account; the encrypted index is written first.</summary>
public sealed class ProtectedMemberMcpTokenStore(IMemberMcpProtectedValues values) : IMemberMcpAccountTokenStore, IDisposable
{
  private const string IndexKey = "voucha.mcp.oauth.accounts.v1";
  private readonly SemaphoreSlim gate = new(1, 1);

  public async Task<MemberMcpOAuthTokens?> LoadAsync(MemberMcpOAuthTokenScope scope)
  {
    ArgumentNullException.ThrowIfNull(scope);
    await gate.WaitAsync().ConfigureAwait(false);
    try
    {
      return (await ReadAccountAsync(scope.AccountId).ConfigureAwait(false)).Tokens
          .FirstOrDefault(entry => SameScope(entry.Scope, scope))?.Tokens;
    }
    finally { gate.Release(); }
  }

  public async Task SaveAsync(MemberMcpOAuthTokenScope scope, MemberMcpOAuthTokens tokens)
  {
    ArgumentNullException.ThrowIfNull(scope);
    ArgumentNullException.ThrowIfNull(tokens);
    await gate.WaitAsync().ConfigureAwait(false);
    try
    {
      await EnsureIndexedAsync(scope.AccountId).ConfigureAwait(false);
      var document = await ReadAccountAsync(scope.AccountId).ConfigureAwait(false);
      document.Tokens.RemoveAll(entry => SameScope(entry.Scope, scope));
      document.Tokens.Add(new(scope, tokens));
      await WriteAccountAsync(scope.AccountId, document).ConfigureAwait(false);
    }
    finally { gate.Release(); }
  }

  public async Task ClearAsync(MemberMcpOAuthTokenScope scope)
  {
    ArgumentNullException.ThrowIfNull(scope);
    await gate.WaitAsync().ConfigureAwait(false);
    try
    {
      var document = await ReadAccountAsync(scope.AccountId).ConfigureAwait(false);
      if (document.Tokens.RemoveAll(entry => SameScope(entry.Scope, scope)) == 0) return;
      if (document.Tokens.Count > 0 || document.Pending is not null)
      {
        await WriteAccountAsync(scope.AccountId, document).ConfigureAwait(false);
        return;
      }
      await RemoveAccountAsync(scope.AccountId).ConfigureAwait(false);
    }
    finally { gate.Release(); }
  }

  public async Task SavePendingAsync(string accountId, string state, string verifier, Uri redirectUri)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
    ArgumentException.ThrowIfNullOrWhiteSpace(state);
    ArgumentException.ThrowIfNullOrWhiteSpace(verifier);
    ArgumentNullException.ThrowIfNull(redirectUri);
    await gate.WaitAsync().ConfigureAwait(false);
    try
    {
      await EnsureIndexedAsync(accountId).ConfigureAwait(false);
      var document = await ReadAccountAsync(accountId).ConfigureAwait(false);
      await WriteAccountAsync(accountId, document with
      {
        Pending = new PendingAuthorization(state, verifier, redirectUri)
      }).ConfigureAwait(false);
    }
    finally { gate.Release(); }
  }

  public async Task ClearPendingAsync(string accountId)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
    await gate.WaitAsync().ConfigureAwait(false);
    try
    {
      var document = await ReadAccountAsync(accountId).ConfigureAwait(false);
      if (document.Pending is null) return;
      if (document.Tokens.Count == 0) await RemoveAccountAsync(accountId).ConfigureAwait(false);
      else await WriteAccountAsync(accountId, document with { Pending = null }).ConfigureAwait(false);
    }
    finally { gate.Release(); }
  }

  public async Task ClearAccountAsync(string accountId)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
    await gate.WaitAsync().ConfigureAwait(false);
    try { await RemoveAccountAsync(accountId).ConfigureAwait(false); }
    finally { gate.Release(); }
  }

  public async Task<bool> HasAccountGrantAsync(string accountId)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
    await gate.WaitAsync().ConfigureAwait(false);
    try { return (await ReadAccountAsync(accountId).ConfigureAwait(false)).Tokens.Count > 0; }
    finally { gate.Release(); }
  }

  public async Task ClearAllAccountsAsync()
  {
    await gate.WaitAsync().ConfigureAwait(false);
    try
    {
      foreach (var accountId in await ReadIndexAsync().ConfigureAwait(false))
        await values.RemoveAsync(AccountKey(accountId)).ConfigureAwait(false);
      await values.RemoveAsync(IndexKey).ConfigureAwait(false);
    }
    finally { gate.Release(); }
  }

  private async Task RemoveAccountAsync(string accountId)
  {
    await values.RemoveAsync(AccountKey(accountId)).ConfigureAwait(false);
    var accounts = await ReadIndexAsync().ConfigureAwait(false);
    if (accounts.RemoveAll(value => string.Equals(value, accountId, StringComparison.Ordinal)) > 0)
      await values.WriteAsync(IndexKey, JsonSerializer.Serialize(accounts)).ConfigureAwait(false);
  }

  private async Task EnsureIndexedAsync(string accountId)
  {
    var accounts = await ReadIndexAsync().ConfigureAwait(false);
    if (accounts.Contains(accountId, StringComparer.Ordinal)) return;
    accounts.Add(accountId);
    await values.WriteAsync(IndexKey, JsonSerializer.Serialize(accounts)).ConfigureAwait(false);
  }

  private Task WriteAccountAsync(string accountId, AccountDocument document) =>
      values.WriteAsync(AccountKey(accountId), JsonSerializer.Serialize(document));

  private async Task<List<string>> ReadIndexAsync() =>
      Deserialize<List<string>>(await values.ReadAsync(IndexKey).ConfigureAwait(false)) ?? [];

  private async Task<AccountDocument> ReadAccountAsync(string accountId) =>
      Deserialize<AccountDocument>(await values.ReadAsync(AccountKey(accountId)).ConfigureAwait(false)) ?? new([], null);

  private static T? Deserialize<T>(string? json) =>
      string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json);

  private static string AccountKey(string accountId) => "voucha.mcp.oauth.account.v1." +
      Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(accountId)));

  private static bool SameScope(MemberMcpOAuthTokenScope left, MemberMcpOAuthTokenScope right) =>
      string.Equals(left.AccountId, right.AccountId, StringComparison.Ordinal) &&
      string.Equals(left.IssuerIdentifier, right.IssuerIdentifier, StringComparison.Ordinal) &&
      left.Resource == right.Resource && left.ClientId == right.ClientId;

  public void Dispose() => gate.Dispose();

  private sealed record ScopedToken(MemberMcpOAuthTokenScope Scope, MemberMcpOAuthTokens Tokens);
  private sealed record PendingAuthorization(string State, string Verifier, Uri RedirectUri);
  private sealed record AccountDocument(List<ScopedToken> Tokens, PendingAuthorization? Pending);
}
