using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Friends;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Friends;

public sealed partial class FriendRecommendationsViewModelTests
{
  [Fact]
  public async Task RelationLimitRestoresRecommendationAndReportsFailure()
  {
    var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingRecommendationService(Page(["user-1", "user-2"], null, hasMore: false))
    {
      FollowCompletions = { ["user-1"] = completion },
    };
    var model = new FriendRecommendationsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var mutation = model.FollowAsync(model.Items[0], TestContext.Current.CancellationToken);
    Assert.Equal(["user-2"], model.Items.Select(row => row.Id));
    completion.SetException(new VouchaApiException(HttpStatusCode.Conflict,
        "{\"error\":{\"code\":\"RELATION_LIMIT_REACHED\"}}"));
    await mutation;
    Assert.Equal(["user-1", "user-2"], model.Items.Select(row => row.Id));
    Assert.Equal(LoadState.Error, model.State);
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.NativeDotnetCsharpActionFailed), model.ErrorMessage);
  }
}
