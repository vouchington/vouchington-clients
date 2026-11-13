namespace Voucha.Client.Core.Auth;

public interface IAccountChangeConfirmation
{
  Task<AccountChangeProof> ConfirmAsync(CancellationToken cancellationToken = default);
}

public sealed class AccountChangeProof(string token, Func<bool> isCurrent)
{
  private int consumed;

  public string Consume()
  {
    if (Interlocked.Exchange(ref consumed, 1) != 0 || !isCurrent() || string.IsNullOrWhiteSpace(token))
      throw new OperationCanceledException();
    return token;
  }
}

internal sealed class UnavailableAccountChangeConfirmation : IAccountChangeConfirmation
{
  public Task<AccountChangeProof> ConfirmAsync(CancellationToken cancellationToken = default) =>
      Task.FromException<AccountChangeProof>(new OperationCanceledException(cancellationToken));
}
