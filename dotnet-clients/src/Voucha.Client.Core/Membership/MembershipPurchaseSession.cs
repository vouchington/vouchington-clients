using System.Text.Json;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.NativeBilling;

public sealed class MembershipPurchaseSession
{
  private readonly IMembershipPurchaseTransport transport;
  private readonly IMembershipStoreAdapter store;
  private readonly IMembershipEvidenceOutbox outbox;
  private readonly IMembershipIdempotencyKeys keys;
  private readonly IMembershipPurchaseClock clock;
  private readonly MembershipPurchaseSignals signals;

  public MembershipPurchaseSession(
      IMembershipPurchaseTransport transport,
      IMembershipStoreAdapter store,
      IMembershipEvidenceOutbox outbox,
      IMembershipIdempotencyKeys keys,
      IMembershipPurchaseClock clock,
      MembershipPurchaseSignals? signals = null)
  {
    ArgumentNullException.ThrowIfNull(transport);
    ArgumentNullException.ThrowIfNull(store);
    ArgumentNullException.ThrowIfNull(outbox);
    ArgumentNullException.ThrowIfNull(keys);
    ArgumentNullException.ThrowIfNull(clock);
    this.transport = transport;
    this.store = store;
    this.outbox = outbox;
    this.keys = keys;
    this.clock = clock;
    this.signals = signals ?? new MembershipPurchaseSignals();
  }

  public async Task<MembershipPurchaseOutcome> PurchaseAsync(
      string productId,
      string provider,
      NativeMembershipStore surface,
      IReadOnlyList<MembershipCatalogProduct> products,
      NativeMembershipOverview overview,
      CancellationToken cancellationToken)
  {
    var key = keys.NextKey();
    var intent = await transport.CreateIntentAsync(
        new NativeMembershipIntentBody(key, productId, provider),
        cancellationToken).ConfigureAwait(false);
    if (intent.Kind == MembershipIntentKind.Conflict)
    {
      var conflict = intent.Conflict ?? throw new InvalidOperationException("Conflict result is missing.");
      return Outcome(key, MembershipPurchaseRefusal.Conflict, conflict.EligibleAt, conflict.Management.Destination);
    }

    var created = intent.Created ?? throw new InvalidOperationException("Created intent is missing.");
    return await FinishAsync(key, created, surface, products, overview, cancellationToken).ConfigureAwait(false);
  }

  private async Task<MembershipPurchaseOutcome> FinishAsync(
      string key,
      NativeMembershipPurchaseIntentResponse response,
      NativeMembershipStore surface,
      IReadOnlyList<MembershipCatalogProduct> products,
      NativeMembershipOverview overview,
      CancellationToken cancellationToken)
  {
    var intent = response.PurchaseIntent;
    var refusal = NativeMembershipCatalog.Refusal(intent, products, surface, overview);
    if (refusal is not null)
    {
      return Outcome(key, refusal);
    }

    // The launch, including app_account_token, is forwarded unchanged.
    var purchased = await store.PurchaseAsync(intent.Launch, cancellationToken).ConfigureAwait(false);
    var body = new NativeMembershipVerificationSubmission(
        purchased.Evidence,
        key,
        intent.Provider,
        intent.Id);
    await outbox.SaveAsync(key, purchased.Evidence, cancellationToken).ConfigureAwait(false);
    var submitted = await SubmitAsync(body, cancellationToken).ConfigureAwait(false);
    await outbox.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
    var verification = await ResolveAsync(submitted, cancellationToken).ConfigureAwait(false);
    return Outcome(
        key,
        refusal: null,
        sheetRequested: true,
        status: verification.Status,
        reason: verification.ReasonCode,
        replayed: submitted.Replayed);
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
      return initial;
    }

    return await PollAsync(initial.Id, cancellationToken).ConfigureAwait(false);
  }

  private async Task<NativeMembershipVerification> PollAsync(string id, CancellationToken cancellationToken)
  {
    for (var attempt = 0; attempt < 8; attempt += 1)
    {
      var status = (await transport.VerificationAsync(id, cancellationToken).ConfigureAwait(false)).Verification;
      var current = await transport.MembershipAsync(cancellationToken).ConfigureAwait(false);
      if (MembershipVerificationPolling.IsTerminal(status.Status, status.ReasonCode))
      {
        return status;
      }

      var next = current.Pending.Verifications.FirstOrDefault(item => item.Id == id)?.NextProcessingAt;
      var delay = signals.ConsumeRefresh()
          ? TimeSpan.Zero
          : MembershipVerificationPolling.Delay(attempt, next, clock.Now);
      if (delay > TimeSpan.Zero)
      {
        await clock.SleepAsync(delay, cancellationToken).ConfigureAwait(false);
      }
    }

    throw new InvalidOperationException(MembershipPurchaseError.VerificationDidNotFinish.ToString());
  }

  private static MembershipPurchaseOutcome Outcome(
      string key,
      MembershipPurchaseRefusal? refusal = null,
      string? eligibleAt = null,
      string? destination = null,
      bool sheetRequested = false,
      string? status = null,
      string? reason = null,
      bool replayed = false) =>
      new(sheetRequested, key, refusal, eligibleAt, destination, status, reason, replayed);
}
