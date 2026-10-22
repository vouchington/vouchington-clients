using System.Text.Json;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.NativeBilling;

public sealed partial class MembershipPurchaseSession
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
    var admitted = NativeMembershipCatalog.Admission(provider, productId, surface, products, overview);
    if (admitted is not null)
    {
      return Outcome("", admitted);
    }

    var key = keys.NextKey();
    var intent = await CreateIntentAsync(
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
