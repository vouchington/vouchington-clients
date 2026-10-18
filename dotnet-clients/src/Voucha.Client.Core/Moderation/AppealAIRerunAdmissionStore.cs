using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

public sealed class AppealAIRerunAdmissionStore
{
  internal readonly record struct OwnerKey(
      string IdentityId, string Scheme, string Host, int Port, string BasePath);

  internal sealed record Admission(Guid Token, DateTimeOffset? AiDraftedAt, string? LifecycleChangeId)
  {
    internal bool IsReconciled(ModerationAppeal appeal) =>
        appeal.AiDraftedAt is not null &&
        appeal.AiDraftedAt != AiDraftedAt &&
        !string.Equals(appeal.LatestLifecycleChangeId, LifecycleChangeId, StringComparison.Ordinal) ||
        appeal.ApprovedAt is not null || appeal.SentAt is not null ||
        appeal.Status != ModerationAppealStatus.Pending;
  }

  private readonly Dictionary<(OwnerKey Owner, string Appeal), Admission> pending = [];

  internal static OwnerKey? ForOwner(string? identityId, Uri? apiBaseUrl)
  {
    if (string.IsNullOrWhiteSpace(identityId) || apiBaseUrl is null ||
        (apiBaseUrl.Scheme != Uri.UriSchemeHttp && apiBaseUrl.Scheme != Uri.UriSchemeHttps))
      return null;
    var path = apiBaseUrl.GetComponents(UriComponents.Path, UriFormat.UriEscaped).Trim('/');
    return new OwnerKey(identityId, apiBaseUrl.Scheme.ToUpperInvariant(),
        apiBaseUrl.IdnHost.ToUpperInvariant(), apiBaseUrl.Port, path);
  }

  internal bool Contains(OwnerKey? owner, string appealId)
  {
    if (owner is null) return false;
    lock (pending) return pending.ContainsKey((owner.Value, appealId));
  }

  internal bool TryGet(OwnerKey? owner, string appealId, out Admission admission)
  {
    if (owner is null)
    {
      admission = null!;
      return false;
    }
    lock (pending) return pending.TryGetValue((owner.Value, appealId), out admission!);
  }

  internal bool TryAdmit(OwnerKey? owner, ModerationAppeal appeal, out Admission admission)
  {
    admission = null!;
    if (owner is null) return false;
    lock (pending)
    {
      if (pending.ContainsKey((owner.Value, appeal.Id))) return false;
      admission = new Admission(Guid.NewGuid(), appeal.AiDraftedAt, appeal.LatestLifecycleChangeId);
      pending[(owner.Value, appeal.Id)] = admission;
      return true;
    }
  }

  internal void Remove(OwnerKey? owner, string appealId, Guid token)
  {
    if (owner is null) return;
    lock (pending)
    {
      if (pending.TryGetValue((owner.Value, appealId), out var current) && current.Token == token)
        pending.Remove((owner.Value, appealId));
    }
  }

  internal void Reconcile(OwnerKey? owner, IEnumerable<ModerationAppeal> appeals)
  {
    if (owner is null) return;
    lock (pending)
    {
      foreach (var appeal in appeals)
      {
        if (pending.TryGetValue((owner.Value, appeal.Id), out var current) && current.IsReconciled(appeal))
          pending.Remove((owner.Value, appeal.Id));
      }
    }
  }
}
