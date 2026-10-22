using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.NativeBilling;

namespace Voucha.Client.Core.Tests.NativeBilling;

internal sealed class Harness
{
  internal ScriptedTransport Transport { get; }

  internal RecordingStore Store { get; } = new();

  internal MemoryMembershipEvidenceOutbox Outbox { get; } = new();

  internal QueueKeys Keys { get; } = new(["idem-1", "idem-2"]);

  internal ManualClock Clock { get; } = new(DateTimeOffset.UnixEpoch);

  internal MembershipPurchaseSignals Signals { get; } = new();

  internal NativeMembershipOverview Empty { get; }

  private readonly IReadOnlyList<MembershipCatalogProduct> products;

  private Harness(MembershipIntentSubmission intent)
  {
    Transport = new ScriptedTransport(intent);
    Empty = MembershipFixture.Decode<NativeMembershipOverview>("native.memberships.me.default");
    products = MembershipFixture.Decode<MembershipPlansResponse>("native.memberships.plans.default").Products;
  }

  internal static Harness For(MembershipIntentSubmission intent) => new(intent);

  internal static Harness Created(string fixture) =>
      For(new MembershipIntentSubmission(
          MembershipIntentKind.Created,
          MembershipFixture.Decode<NativeMembershipPurchaseIntentResponse>(fixture),
          null));

  internal Task<MembershipPurchaseOutcome> PurchaseAsync(
      NativeMembershipOverview overview,
      CancellationToken cancellationToken) =>
      PurchaseAsync(overview, NativeMembershipStore.AppleIos, cancellationToken);

  internal Task<MembershipPurchaseOutcome> PurchaseAsync(
      NativeMembershipOverview overview,
      NativeMembershipStore surface,
      CancellationToken cancellationToken)
  {
    var session = new MembershipPurchaseSession(Transport, Store, Outbox, Keys, Clock, Signals);
    return session.PurchaseAsync(
        Transport.IntentProductId,
        Transport.IntentProvider,
        surface,
        products,
        overview,
        cancellationToken);
  }
}

internal sealed class ScriptedTransport : IMembershipPurchaseTransport
{
  internal MembershipIntentSubmission Intent { get; }

  internal Queue<MembershipVerificationSubmissionResult> Submissions { get; } = new();

  internal Queue<NativeMembershipVerificationResponse> Statuses { get; } = new();

  internal Queue<NativeMembershipOverview> Overviews { get; } = new();

  internal List<NativeMembershipVerificationSubmission> VerificationBodies { get; } = [];

  internal Exception? SubmitError { get; set; }

  internal Action? OnVerification { get; set; }

  private int submitCount;

  internal ScriptedTransport(MembershipIntentSubmission intent)
  {
    Intent = intent;
  }

  internal string IntentProductId =>
      Intent.Created?.PurchaseIntent.ProductId ?? "00000000-0000-7000-8000-000000000701";

  internal string IntentProvider => Intent.Created?.PurchaseIntent.Provider ?? "apple_app_store";

  public Task<MembershipIntentSubmission> CreateIntentAsync(
      NativeMembershipIntentBody body,
      CancellationToken cancellationToken)
  {
    _ = body;
    return Task.FromResult(Intent);
  }

  public Task<MembershipVerificationSubmissionResult> SubmitVerificationAsync(
      NativeMembershipVerificationSubmission body,
      CancellationToken cancellationToken)
  {
    VerificationBodies.Add(body);
    submitCount += 1;
    if (submitCount == 1 && SubmitError is not null)
    {
      throw SubmitError;
    }

    return Task.FromResult(Submissions.Dequeue());
  }

  public Task<NativeMembershipVerificationResponse> VerificationAsync(
      string id,
      CancellationToken cancellationToken)
  {
    _ = id;
    OnVerification?.Invoke();
    OnVerification = null;
    return Task.FromResult(Statuses.Dequeue());
  }

  public Task<NativeMembershipOverview> MembershipAsync(CancellationToken cancellationToken) =>
      Task.FromResult(Overviews.Count == 0
          ? MembershipFixture.Decode<NativeMembershipOverview>("native.memberships.me.default")
          : Overviews.Peek());
}

internal sealed class RecordingStore : IMembershipStoreAdapter
{
  private static readonly JsonElement Evidence = JsonSerializer.Deserialize<JsonElement>(
      """{"signed_transaction":"fixture-signed-transaction"}""");

  internal List<JsonElement> Launches { get; } = [];

  public Task<MembershipStorePurchase> PurchaseAsync(JsonElement launch, CancellationToken cancellationToken)
  {
    Launches.Add(launch);
    return Task.FromResult(new MembershipStorePurchase($"transaction-{Launches.Count}", Evidence));
  }
}

internal sealed class QueueKeys : IMembershipIdempotencyKeys
{
  private readonly Queue<string> values;

  internal List<string> Issued { get; } = [];

  internal QueueKeys(IEnumerable<string> values)
  {
    this.values = new Queue<string>(values);
  }

  public string NextKey()
  {
    var key = values.Dequeue();
    Issued.Add(key);
    return key;
  }
}

internal sealed class ManualClock : IMembershipPurchaseClock
{
  internal List<TimeSpan> Sleeps { get; } = [];

  internal ManualClock(DateTimeOffset now)
  {
    Now = now;
  }

  public DateTimeOffset Now { get; set; }

  public Task SleepAsync(TimeSpan delay, CancellationToken cancellationToken)
  {
    Sleeps.Add(delay);
    Now += delay;
    return Task.CompletedTask;
  }
}
