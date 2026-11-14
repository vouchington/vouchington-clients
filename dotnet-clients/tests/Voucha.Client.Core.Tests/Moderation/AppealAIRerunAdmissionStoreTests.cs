using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class AppealAIRerunAdmissionStoreTests
{
  [Fact]
  public void RunIdentityMutationRequiresTheCurrentOwnedTokenAndAValue()
  {
    var appeal = JsonSerializer.Deserialize<ModerationAppealResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.appeals.detail.default"),
        VouchaApiJson.Options)!.Appeal;
    var owner = AppealAIRerunAdmissionStore.ForOwner(
        "staff-1", new Uri("https://example.test/api"));
    var store = new AppealAIRerunAdmissionStore();
    Assert.True(store.TryAdmit(owner, appeal, out var admission));

    Assert.False(store.SetRunId(null, appeal.Id, admission.Token, "run-1"));
    Assert.False(store.SetRunId(owner, appeal.Id, Guid.NewGuid(), "run-1"));
    Assert.False(store.SetRunId(owner, appeal.Id, admission.Token, " "));
    Assert.True(store.SetRunId(owner, appeal.Id, admission.Token, "run-1"));
    Assert.True(store.TryGet(owner, appeal.Id, out var updated));
    Assert.Equal("run-1", updated.RunId);
  }

  [Fact]
  public void LateOldClaimRemovalCannotClearNewerAdmission()
  {
    var appeal = JsonSerializer.Deserialize<ModerationAppealResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.appeals.detail.default"),
        VouchaApiJson.Options)!.Appeal;
    var owner = AppealAIRerunAdmissionStore.ForOwner(
        "staff-1", new Uri("https://example.test/api"));
    var store = new AppealAIRerunAdmissionStore();
    Assert.True(store.TryAdmit(owner, appeal, out var first));
    store.SetRunId(owner, appeal.Id, first.Token, "run-1");
    store.Reconcile(owner, [appeal with
    {
      AiDraftedAt = appeal.AiDraftedAt?.AddMinutes(1),
      LatestLifecycleChangeId = "new-change",
      AiDraftRunId = "different-run",
    }]);
    Assert.True(store.TryGet(owner, appeal.Id, out var retainedFirst));
    Assert.Equal(first.Token, retainedFirst.Token);
    store.Remove(owner, appeal.Id, first.Token);
    Assert.True(store.TryAdmit(owner, appeal, out var second));

    store.Remove(owner, appeal.Id, first.Token);

    Assert.True(store.TryGet(owner, appeal.Id, out var retained));
    Assert.Equal(second.Token, retained.Token);
  }
}
