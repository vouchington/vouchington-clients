namespace Voucha.Client.Core.Moderation;

public sealed class ReportJudgementAdmissionStore
{
  internal readonly record struct OwnerKey(
      string IdentityId, string Scheme, string Host, int Port, string BasePath);

  internal sealed record Admission(Guid Token, string? RunId);

  private readonly Dictionary<(OwnerKey Owner, string Report), Admission> pending = [];

  internal static OwnerKey? ForOwner(string? identityId, Uri? apiBaseUrl)
  {
    if (string.IsNullOrWhiteSpace(identityId) || apiBaseUrl is null ||
        (apiBaseUrl.Scheme != Uri.UriSchemeHttp && apiBaseUrl.Scheme != Uri.UriSchemeHttps))
      return null;
    var path = apiBaseUrl.GetComponents(UriComponents.Path, UriFormat.UriEscaped).Trim('/');
    return new OwnerKey(identityId, apiBaseUrl.Scheme.ToUpperInvariant(),
        apiBaseUrl.IdnHost.ToUpperInvariant(), apiBaseUrl.Port, path);
  }

  internal bool Contains(OwnerKey? owner, string reportId)
  {
    if (owner is null) return false;
    lock (pending) return pending.ContainsKey((owner.Value, reportId));
  }

  internal bool TryGet(OwnerKey? owner, string reportId, out Admission admission)
  {
    if (owner is null)
    {
      admission = null!;
      return false;
    }
    lock (pending) return pending.TryGetValue((owner.Value, reportId), out admission!);
  }

  internal bool TryAdmit(OwnerKey? owner, string reportId, out Admission admission)
  {
    admission = null!;
    if (owner is null) return false;
    lock (pending)
    {
      if (pending.ContainsKey((owner.Value, reportId))) return false;
      admission = new Admission(Guid.NewGuid(), null);
      pending[(owner.Value, reportId)] = admission;
    }
    return true;
  }

  internal bool SetRunId(OwnerKey? owner, string reportId, Guid token, string runId)
  {
    if (owner is null || string.IsNullOrWhiteSpace(runId)) return false;
    lock (pending)
    {
      if (!pending.TryGetValue((owner.Value, reportId), out var current) || current.Token != token)
        return false;
      pending[(owner.Value, reportId)] = current with { RunId = runId };
    }
    return true;
  }

  internal void Remove(OwnerKey? owner, string reportId, Guid token)
  {
    if (owner is null) return;
    lock (pending)
    {
      _ = pending.TryGetValue((owner.Value, reportId), out var current) &&
          current.Token == token && pending.Remove((owner.Value, reportId));
    }
  }
}
