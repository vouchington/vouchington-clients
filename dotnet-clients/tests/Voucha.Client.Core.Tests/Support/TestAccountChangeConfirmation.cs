global using TestAccountChangeConfirmation = Voucha.Client.Core.Tests.Support.TestAccountChangeConfirmation;

using Voucha.Client.Core.Auth;

namespace Voucha.Client.Core.Tests.Support;

internal sealed class TestAccountChangeConfirmation : IAccountChangeConfirmation
{
  public const string Token = "test-fresh-account-proof";
  public Task<AccountChangeProof> ConfirmAsync(CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    return Task.FromResult(new AccountChangeProof(Token, () => true));
  }
}
