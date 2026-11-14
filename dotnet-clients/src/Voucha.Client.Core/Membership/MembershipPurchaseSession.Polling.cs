using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.NativeBilling;

public sealed partial class MembershipPurchaseSession
{
  [System.Diagnostics.CodeAnalysis.SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "One intent retry covers any non-cancellation transport failure.")]
  private async Task<MembershipIntentSubmission> CreateIntentAsync(
      NativeMembershipIntentBody body,
      CancellationToken cancellationToken)
  {
    try
    {
      return await transport.CreateIntentAsync(body, cancellationToken).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception)
    {
      return await transport.CreateIntentAsync(body, cancellationToken).ConfigureAwait(false);
    }
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "One verification retry covers any non-cancellation transport failure.")]
  private async Task<MembershipVerificationSubmissionResult> SubmitAsync(
      NativeMembershipVerificationSubmission body,
      CancellationToken cancellationToken)
  {
    try
    {
      return await transport.SubmitVerificationAsync(body, cancellationToken).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception)
    {
      return await transport.SubmitVerificationAsync(body, cancellationToken).ConfigureAwait(false);
    }
  }

  private async Task<NativeMembershipVerification> ResolveAsync(
      MembershipVerificationSubmissionResult submitted,
      CancellationToken cancellationToken)
  {
    var initial = submitted.Verification.Verification;
    if (MembershipVerificationPolling.IsTerminal(initial.Status, initial.ReasonCode))
    {
      await RefreshMembershipAsync(cancellationToken).ConfigureAwait(false);
      return initial;
    }

    return await PollAsync(initial.Id, cancellationToken).ConfigureAwait(false);
  }

  private async Task<NativeMembershipVerification> PollAsync(string id, CancellationToken cancellationToken)
  {
    for (var attempt = 0; attempt < 8; attempt += 1)
    {
      cancellationToken.ThrowIfCancellationRequested();
      var status = (await transport.VerificationAsync(id, cancellationToken).ConfigureAwait(false)).Verification;
      if (MembershipVerificationPolling.IsTerminal(status.Status, status.ReasonCode))
      {
        await RefreshMembershipAsync(cancellationToken).ConfigureAwait(false);
        return status;
      }

      var hint = await MembershipHintAsync(cancellationToken).ConfigureAwait(false);
      if (attempt == 7)
      {
        break;
      }

      var pending = hint?.Pending.Verifications.FirstOrDefault(item => item.Id == id);
      var delay = signals.ConsumeRefresh()
          ? TimeSpan.Zero
          : MembershipVerificationPolling.Delay(attempt, pending?.NextProcessingAt, clock.Now);
      if (delay > TimeSpan.Zero)
      {
        await clock.SleepAsync(delay, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
      }
    }

    throw new InvalidOperationException(MembershipPurchaseError.VerificationDidNotFinish.ToString());
  }

  private async Task RefreshMembershipAsync(CancellationToken cancellationToken)
  {
    _ = await MembershipHintAsync(cancellationToken).ConfigureAwait(false);
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "A failed membership read keeps a terminal verification and continues pending polling.")]
  private async Task<NativeMembershipOverview?> MembershipHintAsync(CancellationToken cancellationToken)
  {
    try
    {
      return await transport.MembershipAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception)
    {
      return null;
    }
  }
}
