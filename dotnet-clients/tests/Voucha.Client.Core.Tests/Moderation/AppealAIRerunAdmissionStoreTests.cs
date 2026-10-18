using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class AppealAIRerunAdmissionStoreTests
{
  [Fact]
  public void LateOldClaimRemovalCannotClearNewerAdmission()
  {
    var appeal = JsonSerializer.Deserialize<ModerationAppealListResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.appeals.default"),
        VouchaApiJson.Options)!.Appeals.Single();
    var owner = AppealAIRerunAdmissionStore.ForOwner(
        "staff-1", new Uri("https://example.test/api"));
    var store = new AppealAIRerunAdmissionStore();
    Assert.True(store.TryAdmit(owner, appeal, out var first));
    store.Reconcile(owner, [appeal with
    {
      AiDraftedAt = appeal.AiDraftedAt?.AddMinutes(1),
      LatestLifecycleChangeId = "new-change",
    }]);
    Assert.True(store.TryAdmit(owner, appeal, out var second));

    store.Remove(owner, appeal.Id, first.Token);

    Assert.True(store.TryGet(owner, appeal.Id, out var retained));
    Assert.Equal(second.Token, retained.Token);
  }
}
