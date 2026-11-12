using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Friends;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Friends;

public sealed partial class FriendsViewModelTests
{
  [Fact]
  public async Task RelationLimitRollsBackOptimisticFollowWithoutSuccess()
  {
    var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingFriendsService(followers: CreateFollowersResponse(new User("friend-1", "friend", null)))
    {
      FollowCompletions = new Dictionary<string, TaskCompletionSource>(StringComparer.Ordinal) { ["friend-1"] = completion },
    };
    var model = new FriendsViewModel(service, "viewer", FriendsTab.Followers);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var mutation = model.ToggleFollowAsync(model.Items[0], TestContext.Current.CancellationToken);
    Assert.True(model.Items[0].IsFollowing);
    completion.SetException(new VouchaApiException(HttpStatusCode.Conflict,
        "{\"error\":{\"code\":\"RELATION_LIMIT_REACHED\"}}"));
    await mutation;
    Assert.False(model.Items[0].IsFollowing);
    Assert.Equal(LoadState.Error, model.State);
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.NativeDotnetCsharpActionFailed), model.ErrorMessage);
  }
}
