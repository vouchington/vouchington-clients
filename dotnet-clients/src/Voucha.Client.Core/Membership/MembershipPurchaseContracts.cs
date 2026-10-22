using System.Text.Json;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.NativeBilling;

public enum MembershipPurchaseError
{
  VerificationDidNotFinish,
}

public enum MembershipIntentKind
{
  Created,
  Conflict,
}

public sealed record MembershipIntentSubmission(
    MembershipIntentKind Kind,
    NativeMembershipPurchaseIntentResponse? Created,
    NativeMembershipPurchaseConflict? Conflict);

public enum MembershipVerificationKind
{
  Created,
  Replayed,
}

public sealed record MembershipVerificationSubmissionResult(
    MembershipVerificationKind Kind,
    NativeMembershipVerificationResponse Verification)
{
  public bool Replayed => Kind == MembershipVerificationKind.Replayed;
}

public sealed record MembershipStorePurchase(string TransactionIdentity, JsonElement Evidence);

public sealed record MembershipPurchaseOutcome(
    bool SheetRequested,
    string IdempotencyKey,
    MembershipPurchaseRefusal? Refusal,
    string? EligibleAt,
    string? ManagementDestination,
    string? VerificationStatus,
    string? VerificationReason,
    bool Replayed);

public interface IMembershipPurchaseTransport
{
  Task<MembershipIntentSubmission> CreateIntentAsync(
      NativeMembershipIntentBody body,
      CancellationToken cancellationToken);

  Task<MembershipVerificationSubmissionResult> SubmitVerificationAsync(
      NativeMembershipVerificationSubmission body,
      CancellationToken cancellationToken);

  Task<NativeMembershipVerificationResponse> VerificationAsync(string id, CancellationToken cancellationToken);

  Task<NativeMembershipOverview> MembershipAsync(CancellationToken cancellationToken);
}

public interface IMembershipStoreAdapter
{
  Task<MembershipStorePurchase> PurchaseAsync(JsonElement launch, CancellationToken cancellationToken);
}

public interface IMembershipEvidenceOutbox
{
  Task SaveAsync(string key, JsonElement evidence, CancellationToken cancellationToken);

  Task RemoveAsync(string key, CancellationToken cancellationToken);

  Task<bool> ContainsAsync(string key, CancellationToken cancellationToken);
}

public interface IMembershipIdempotencyKeys
{
  string NextKey();
}

public interface IMembershipPurchaseClock
{
  DateTimeOffset Now { get; }

  Task SleepAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public sealed class MembershipPurchaseSignals
{
  private readonly Lock gate = new();
  private bool refresh;

  public void NoteForeground()
  {
    lock (gate)
    {
      refresh = true;
    }
  }

  public void NoteStoreUpdate() => NoteForeground();

  internal bool ConsumeRefresh()
  {
    lock (gate)
    {
      var value = refresh;
      refresh = false;
      return value;
    }
  }
}
