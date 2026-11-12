using Voucha.Client.Core.Moderation;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class ReportJudgementAdmissionStoreTests
{
  [Fact]
  public void OwnerNormalizationAndTokenGuardKeepAdmissionsIsolated()
  {
    var store = new ReportJudgementAdmissionStore();
    var owner = ReportJudgementAdmissionStore.ForOwner("alice", new Uri("https://API.test/base/"));
    var same = ReportJudgementAdmissionStore.ForOwner("alice", new Uri("https://api.test:443/base"));
    var other = ReportJudgementAdmissionStore.ForOwner("bob", new Uri("https://api.test/base"));
    Assert.Equal(owner, same);
    Assert.NotNull(owner);
    var normalized = owner.Value;
    Assert.Equal(("alice", "HTTPS", "API.TEST", 443, "base"),
        (normalized.IdentityId, normalized.Scheme, normalized.Host, normalized.Port, normalized.BasePath));
    Assert.False(store.Contains(null, "report"));
    Assert.False(store.TryGet(null, "report", out _));
    Assert.False(store.TryAdmit(null, "report", out _));
    Assert.False(store.SetRunId(null, "report", Guid.NewGuid(), "run"));
    store.Remove(null, "report", Guid.NewGuid());
    Assert.True(store.TryAdmit(owner, "report", out var admission));
    Assert.False(store.TryAdmit(same, "report", out _));
    Assert.False(store.Contains(other, "report"));
    Assert.True(store.Contains(same, "report"));
    Assert.False(store.SetRunId(owner, "report", Guid.NewGuid(), "run"));
    Assert.False(store.SetRunId(owner, "report", admission.Token, ""));
    Assert.True(store.SetRunId(owner, "report", admission.Token, "run"));
    Assert.True(store.TryGet(same, "report", out var saved));
    Assert.Equal("run", saved.RunId);
    store.Remove(owner, "report", Guid.NewGuid());
    Assert.True(store.Contains(owner, "report"));
    store.Remove(owner, "report", admission.Token);
    Assert.False(store.Contains(owner, "report"));
  }

  [Fact]
  public void MissingOrUnsupportedOwnersFailClosed()
  {
    Assert.Null(ReportJudgementAdmissionStore.ForOwner(null, new Uri("https://api.test")));
    Assert.Null(ReportJudgementAdmissionStore.ForOwner("alice", null));
    Assert.Null(ReportJudgementAdmissionStore.ForOwner("alice", new Uri("ftp://api.test")));
  }
}
