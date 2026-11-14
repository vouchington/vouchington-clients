using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.NativeBilling;
using Xunit;

namespace Voucha.Client.Core.Tests.NativeBilling;

public sealed class MembershipPurchaseSessionTests
{
  [Fact]
  public async Task ConflictDoesNotOpenASheetOrPortal()
  {
    var conflict = MembershipFixture.Decode<NativeMembershipPurchaseConflict>(
        "native.memberships.purchase-intent.conflict.default");
    var harness = Harness.For(new MembershipIntentSubmission(MembershipIntentKind.Conflict, null, conflict));
    var outcome = await harness.PurchaseAsync(harness.Empty, TestContext.Current.CancellationToken);

    Assert.False(outcome.SheetRequested);
    Assert.Equal(MembershipPurchaseRefusal.Conflict, outcome.Refusal);
    Assert.Equal("2027-01-01T00:00:00.000Z", outcome.EligibleAt);
    Assert.Equal("billing_portal", outcome.ManagementDestination);
    Assert.Empty(harness.Store.Launches);
    Assert.DoesNotContain("http", outcome.ManagementDestination, StringComparison.Ordinal);
  }

  [Fact]
  public async Task AdmittedApplePurchasePostsFixtureEvidenceAndRemovesTheOutbox()
  {
    var harness = Harness.Created("native.memberships.purchase-intent.apple.default");
    harness.Transport.Submissions.Enqueue(Created("native.memberships.verification.pending.default"));
    harness.Transport.Statuses.Enqueue(Verification("native.memberships.verification-status.verified.default"));
    var outcome = await harness.PurchaseAsync(harness.Empty, TestContext.Current.CancellationToken);

    Assert.True(outcome.SheetRequested);
    Assert.Equal("verified", outcome.VerificationStatus);
    Assert.False(outcome.Replayed);
    var launch = Assert.Single(harness.Store.Launches);
    Assert.Equal(["idem-1"], harness.Keys.Issued);
    var body = Assert.Single(harness.Transport.VerificationBodies);
    Assert.Equal("idem-1", body.IdempotencyKey);
    Assert.Equal("00000000-0000-7000-8000-000000000803", body.PurchaseIntentId);
    Assert.Equal("apple_app_store", body.Provider);
    Assert.Equal("fixture-signed-transaction", body.Evidence.GetProperty("signed_transaction").GetString());
    Assert.Equal("00000000-0000-7000-8000-000000000803", launch.GetProperty("app_account_token").GetString());
    Assert.False(await harness.Outbox.ContainsAsync("idem-1", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task StripeGoogleAndMicrosoftFixturesDoNotOpenBilling()
  {
    (string Name, NativeMembershipStore Surface)[] cases =
    [
      ("native.memberships.purchase-intent.stripe.default", NativeMembershipStore.AppleIos),
      ("native.memberships.purchase-intent.google.default", NativeMembershipStore.GooglePlay),
      ("native.memberships.purchase-intent.microsoft.default", NativeMembershipStore.MicrosoftStore),
    ];
    foreach (var (name, surface) in cases)
    {
      var harness = Harness.Created(name);
      var outcome = await harness.PurchaseAsync(harness.Empty, surface, TestContext.Current.CancellationToken);
      Assert.False(outcome.SheetRequested);
      Assert.NotNull(outcome.Refusal);
      Assert.Empty(harness.Store.Launches);
    }
  }

  [Fact]
  public async Task ReplacementReplayAndRetryReuseTheAttemptKey()
  {
    var lifecycle = MembershipFixture.Decode<NativeMembershipOverview>("native.memberships.me.lifecycle.default");
    var blocked = Harness.Created("native.memberships.purchase-intent.apple.default");
    var blockedOutcome = await blocked.PurchaseAsync(lifecycle, TestContext.Current.CancellationToken);
    Assert.Equal(MembershipPurchaseRefusal.ReplacementUnavailable, blockedOutcome.Refusal);
    Assert.Empty(blocked.Store.Launches);

    var replay = Harness.Created("native.memberships.purchase-intent.apple.default");
    replay.Transport.Submissions.Enqueue(Replayed("native.memberships.verification-status.verified.default"));
    var replayed = await replay.PurchaseAsync(replay.Empty, TestContext.Current.CancellationToken);
    Assert.True(replayed.Replayed);
    Assert.Equal("verified", replayed.VerificationStatus);
    Assert.False(await replay.Outbox.ContainsAsync("idem-1", TestContext.Current.CancellationToken));

    var retried = Harness.Created("native.memberships.purchase-intent.apple.default");
    retried.Transport.SubmitError = new InvalidOperationException("offline");
    retried.Transport.Submissions.Enqueue(Created("native.memberships.verification-status.verified.default"));
    var retriedOutcome = await retried.PurchaseAsync(retried.Empty, TestContext.Current.CancellationToken);
    Assert.Equal(["idem-1", "idem-1"], retried.Transport.VerificationBodies.Select(body => body.IdempotencyKey));
    Assert.Equal("verified", retriedOutcome.VerificationStatus);
    Assert.False(await retried.Outbox.ContainsAsync("idem-1", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task ANewPurchaseUsesANewIdempotencyKey()
  {
    var harness = Harness.Created("native.memberships.purchase-intent.apple.default");
    harness.Transport.Submissions.Enqueue(Created("native.memberships.verification-status.verified.default"));
    harness.Transport.Submissions.Enqueue(Created("native.memberships.verification-status.verified.default"));
    await harness.PurchaseAsync(harness.Empty, TestContext.Current.CancellationToken);
    await harness.PurchaseAsync(harness.Empty, TestContext.Current.CancellationToken);
    Assert.Equal(["idem-1", "idem-2"], harness.Keys.Issued);
  }

  [Fact]
  public async Task PollingWaitsForProcessingTimeBackoffAndForegroundRefresh()
  {
    var pending = Verification("native.memberships.verification-status.pending.default");
    var verified = Verification("native.memberships.verification-status.verified.default");
    var harness = Harness.Created("native.memberships.purchase-intent.apple.default");
    harness.Transport.Submissions.Enqueue(new MembershipVerificationSubmissionResult(
        MembershipVerificationKind.Created,
        pending));
    for (var index = 0; index < 5; index += 1)
    {
      harness.Transport.Statuses.Enqueue(pending);
    }

    harness.Transport.Statuses.Enqueue(verified);
    var outcome = await harness.PurchaseAsync(harness.Empty, TestContext.Current.CancellationToken);
    Assert.Equal(
        new[] { 1, 2, 4, 8, 15 }.Select(seconds => TimeSpan.FromSeconds(seconds)),
        harness.Clock.Sleeps);
    Assert.Equal("verified", outcome.VerificationStatus);

    var scheduled = Harness.Created("native.memberships.purchase-intent.apple.default");
    scheduled.Transport.Submissions.Enqueue(new MembershipVerificationSubmissionResult(
        MembershipVerificationKind.Created,
        pending));
    scheduled.Transport.Statuses.Enqueue(Pending("purchase_pending"));
    scheduled.Transport.Statuses.Enqueue(verified);
    scheduled.Transport.Overviews.Enqueue(ProcessingOverview("2026-10-21T18:00:00.000Z"));
    scheduled.Clock.Now = DateTimeOffset.Parse("2026-10-21T17:00:00Z");
    await scheduled.PurchaseAsync(scheduled.Empty, TestContext.Current.CancellationToken);
    Assert.Equal([TimeSpan.FromHours(1)], scheduled.Clock.Sleeps);

    var foreground = Harness.Created("native.memberships.purchase-intent.apple.default");
    foreground.Transport.Submissions.Enqueue(new MembershipVerificationSubmissionResult(
        MembershipVerificationKind.Created,
        pending));
    foreground.Transport.Statuses.Enqueue(pending);
    foreground.Transport.Statuses.Enqueue(verified);
    foreground.Transport.OnVerification = foreground.Signals.NoteForeground;
    await foreground.PurchaseAsync(foreground.Empty, TestContext.Current.CancellationToken);
    Assert.Empty(foreground.Clock.Sleeps);
  }

  [Fact]
  public async Task PollingStopsAfterEightUnfinishedAttempts()
  {
    var pending = Verification("native.memberships.verification-status.pending.default");
    var harness = Harness.Created("native.memberships.purchase-intent.apple.default");
    harness.Transport.Submissions.Enqueue(new MembershipVerificationSubmissionResult(
        MembershipVerificationKind.Created,
        pending));
    for (var index = 0; index < 8; index += 1)
    {
      harness.Transport.Statuses.Enqueue(pending);
    }

    var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        harness.PurchaseAsync(harness.Empty, TestContext.Current.CancellationToken));
    Assert.Equal(nameof(MembershipPurchaseError.VerificationDidNotFinish), error.Message);
  }

  [Fact]
  public async Task ReplayRefreshesMembershipAndPendingPollingSurvivesAFailedHint()
  {
    var replay = Harness.Created("native.memberships.purchase-intent.apple.default");
    replay.Transport.Submissions.Enqueue(Replayed("native.memberships.verification-status.verified.default"));
    var replayed = await replay.PurchaseAsync(replay.Empty, TestContext.Current.CancellationToken);
    Assert.True(replayed.Replayed);
    Assert.Equal(1, replay.Transport.MembershipCalls);

    var pending = Verification("native.memberships.verification-status.pending.default");
    var verified = Verification("native.memberships.verification-status.verified.default");
    var interrupted = Harness.Created("native.memberships.purchase-intent.apple.default");
    interrupted.Transport.Submissions.Enqueue(new MembershipVerificationSubmissionResult(
        MembershipVerificationKind.Created,
        pending));
    interrupted.Transport.Statuses.Enqueue(pending);
    interrupted.Transport.Statuses.Enqueue(verified);
    interrupted.Transport.MembershipError = new InvalidOperationException("offline");
    var outcome = await interrupted.PurchaseAsync(interrupted.Empty, TestContext.Current.CancellationToken);
    Assert.Equal("verified", outcome.VerificationStatus);
    Assert.Equal([TimeSpan.FromSeconds(1)], interrupted.Clock.Sleeps);
  }

  [Fact]
  public async Task CancelledPurchaseKeepsPaidEvidence()
  {
    var harness = Harness.Created("native.memberships.purchase-intent.apple.default");
    harness.Transport.Submissions.Enqueue(Created("native.memberships.verification-status.verified.default"));
    using var cancellation = new CancellationTokenSource();
    harness.Store.AfterPurchase = cancellation.Cancel;
    harness.Transport.SubmitError = new OperationCanceledException();
    await Assert.ThrowsAsync<OperationCanceledException>(() =>
        harness.PurchaseAsync(harness.Empty, cancellation.Token));
    Assert.True(await harness.Outbox.ContainsAsync("idem-1", CancellationToken.None));
  }

  private static MembershipVerificationSubmissionResult Created(string name) =>
      new(MembershipVerificationKind.Created, Verification(name));

  private static MembershipVerificationSubmissionResult Replayed(string name) =>
      new(MembershipVerificationKind.Replayed, Verification(name));

  private static NativeMembershipVerificationResponse Verification(string name) =>
      MembershipFixture.Decode<NativeMembershipVerificationResponse>(name);

  private static NativeMembershipVerificationResponse Pending(string reason)
  {
    var json = """
    {"verification":{"created_at":"2026-09-09T12:00:00.000Z","id":"00000000-0000-7000-8000-000000000807","provider":"apple_app_store","reason_code":"REASON","status":"pending"}}
    """.Replace("REASON", reason, StringComparison.Ordinal);
    return JsonSerializer.Deserialize<NativeMembershipVerificationResponse>(json, VouchaApiJson.Options)
        ?? throw new InvalidOperationException("Pending verification did not decode.");
  }

  private static NativeMembershipOverview ProcessingOverview(string instant)
  {
    var json = """
    {"management":null,"membership":null,"pending":{"financial_operations":[],"grants":0,"switches":[],"verifications":[{"id":"00000000-0000-7000-8000-000000000807","next_processing_at":"INSTANT","provider":"apple_app_store","reason_code":"purchase_pending","status":"pending"}]},"sources":[]}
    """.Replace("INSTANT", instant, StringComparison.Ordinal);
    return JsonSerializer.Deserialize<NativeMembershipOverview>(json, VouchaApiJson.Options)
        ?? throw new InvalidOperationException("Processing overview did not decode.");
  }
}
