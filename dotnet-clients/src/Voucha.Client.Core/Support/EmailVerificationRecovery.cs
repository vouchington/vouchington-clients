using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Support;

public sealed record EmailVerificationRecoveryRequest(VouchaApiException Error);

public static class EmailVerificationRecovery
{
  public static bool IsRequired(Exception exception) =>
      exception is VouchaApiException { IsEmailVerificationRequired: true };
}

public sealed class EmailVerificationGatedMutation
{
  private readonly object requestLock = new();
  private EmailVerificationRecoveryRequest? pendingRequest;

  public async Task RunAsync(
      Func<Task> mutation,
      Action<VouchaApiException> onRecoveryRequired)
  {
    ArgumentNullException.ThrowIfNull(mutation);
    ArgumentNullException.ThrowIfNull(onRecoveryRequired);
    try
    {
      await mutation().ConfigureAwait(true);
    }
    catch (VouchaApiException exception) when (EmailVerificationRecovery.IsRequired(exception))
    {
      onRecoveryRequired(exception);
      StoreRecoveryRequest(exception);
    }
  }

  public async Task<TResult> RunAsync<TResult>(
      Func<Task<TResult>> mutation,
      Func<VouchaApiException, TResult> onRecoveryRequired)
  {
    ArgumentNullException.ThrowIfNull(mutation);
    ArgumentNullException.ThrowIfNull(onRecoveryRequired);
    try
    {
      return await mutation().ConfigureAwait(true);
    }
    catch (VouchaApiException exception) when (EmailVerificationRecovery.IsRequired(exception))
    {
      var fallback = onRecoveryRequired(exception);
      StoreRecoveryRequest(exception);
      return fallback;
    }
  }

  public async Task<bool> ConsumeRecoveryRequestAsync(
      Func<EmailVerificationRecoveryRequest, Task> consumer,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(consumer);
    cancellationToken.ThrowIfCancellationRequested();
    EmailVerificationRecoveryRequest? request;
    lock (requestLock)
    {
      request = pendingRequest;
      pendingRequest = null;
    }

    if (request is null) return false;
    await consumer(request).ConfigureAwait(true);
    return true;
  }

  private void StoreRecoveryRequest(VouchaApiException exception)
  {
    lock (requestLock)
    {
      pendingRequest ??= new EmailVerificationRecoveryRequest(exception);
    }
  }
}
