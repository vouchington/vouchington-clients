namespace Voucha.Client.Core.Agent;

/// <summary>
/// Browser stand-in for connections created only to revoke retired grants. Browser authorization
/// no longer exists, so any attempt to open one is a programming error.
/// </summary>
public sealed class MemberMcpRevocationOnlyBrowser : IMemberMcpSystemBrowser
{
  public Task OpenAsync(Uri authorizationUri, CancellationToken cancellationToken) =>
      throw new NotSupportedException("Browser authorization is retired.");
}
