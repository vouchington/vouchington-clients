using Voucha.Client.Core.Api;
using Voucha.Client.Core.Friends;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Friends;

public sealed partial class FriendsViewModelTests
{
  [Fact]
  public async Task LoadAsyncFetchesFirstFollowingPage()
  {
    var service = new RecordingFriendsService();
    service.FollowingResponses.Enqueue(CreateFollowingResponse(
        new PageInfo("cursor-1", true, null),
        new User("friend-1", "friend1", null, Name: "Friend One")));
    service.FollowingResponses.Enqueue(CreateFollowingResponse(
        new PageInfo(null, false, "cursor-1"),
        new User("friend-2", "friend2", null, Name: "Friend Two")));
    var viewModel = new FriendsViewModel(service, "user-abc");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["friend-1"], viewModel.Items.Select(item => item.Id).ToArray());
    Assert.True(viewModel.HasMore);
    Assert.Equal([null], service.FollowingFetchAfterValues);
  }

  [Fact]
  public async Task LoadMoreAsyncAppendsFollowingPage()
  {
    var service = new RecordingFriendsService();
    service.FollowingResponses.Enqueue(CreateFollowingResponse(
        new PageInfo("cursor-1", true, null),
        new User("friend-1", "friend1", null, Name: "Friend One")));
    service.FollowingResponses.Enqueue(CreateFollowingResponse(
        new PageInfo(null, false, "cursor-1"),
        new User("friend-2", "friend2", null, Name: "Friend Two")));
    var viewModel = new FriendsViewModel(service, "user-abc");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["friend-1", "friend-2"], viewModel.Items.Select(item => item.Id).ToArray());
    Assert.False(viewModel.HasMore);
    Assert.Equal([null, "cursor-1"], service.FollowingFetchAfterValues);
  }

  [Fact]
  public async Task LoadAsyncFetchesFirstFollowerPage()
  {
    var service = new RecordingFriendsService();
    service.FollowersResponses.Enqueue(CreateFollowersResponse(
        new PageInfo("cursor-1", true, null),
        new User("friend-1", "friend1", null, Name: "Friend One")));
    service.FollowersResponses.Enqueue(CreateFollowersResponse(
        new PageInfo(null, false, "cursor-1"),
        new User("friend-2", "friend2", null, Name: "Friend Two")));
    var viewModel = new FriendsViewModel(service, "user-abc", FriendsTab.Followers);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["friend-1"], viewModel.Items.Select(item => item.Id).ToArray());
    Assert.True(viewModel.HasMore);
    Assert.Equal([null], service.FollowersFetchAfterValues);
  }

  [Fact]
  public async Task LoadMoreAsyncAppendsFollowerPage()
  {
    var service = new RecordingFriendsService();
    service.FollowersResponses.Enqueue(CreateFollowersResponse(
        new PageInfo("cursor-1", true, null),
        new User("friend-1", "friend1", null, Name: "Friend One")));
    service.FollowersResponses.Enqueue(CreateFollowersResponse(
        new PageInfo(null, false, "cursor-1"),
        new User("friend-2", "friend2", null, Name: "Friend Two")));
    var viewModel = new FriendsViewModel(service, "user-abc", FriendsTab.Followers);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["friend-1", "friend-2"], viewModel.Items.Select(item => item.Id).ToArray());
    Assert.False(viewModel.HasMore);
    Assert.Equal([null, "cursor-1"], service.FollowersFetchAfterValues);
  }

  [Fact]
  public async Task LoadMoreAsyncKeepsLoadedRowsWhenFollowingFetchFails()
  {
    var service = new RecordingFriendsService();
    service.FollowingResponses.Enqueue(CreateFollowingResponse(
        new PageInfo("cursor-1", true, null),
        new User("friend-1", "friend1", null, Name: "Friend One")));
    var viewModel = new FriendsViewModel(service, "user-abc");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    service.FailFollowingFetch = true;

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["friend-1"], viewModel.Items.Select(item => item.Id).ToArray());
    Assert.True(viewModel.HasMore);
    Assert.Equal("Following fetch failed.", viewModel.ErrorMessage);
    Assert.Equal(LoadState.Error, viewModel.State);
  }

  [Fact]
  public async Task StaleSessionMutationDoesNotClearNewSessionPendingState()
  {
    var oldMutation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var newMutation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingFriendsService(
        following: CreateFollowingResponse(),
        followers: CreateFollowersResponse(new User("friend-1", "friend", null, Name: "Friendly User")))
    {
      FollowCompletionQueues = new Dictionary<string, Queue<TaskCompletionSource>>(StringComparer.Ordinal)
      {
        ["friend-1"] = new([oldMutation, newMutation]),
      },
    };
    var viewModel = new FriendsViewModel(service, "user-abc", FriendsTab.Followers);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var oldToggle = viewModel.ToggleFollowAsync(viewModel.Items[0], TestContext.Current.CancellationToken);
    viewModel.UpdateCurrentUser("user-def");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var newToggle = viewModel.ToggleFollowAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    oldMutation.SetResult();
    await oldToggle;
    await viewModel.SelectTabAsync(FriendsTab.Following, TestContext.Current.CancellationToken);

    Assert.Single(viewModel.Items);
    Assert.Equal("friend-1", viewModel.Items[0].Id);

    newMutation.SetResult();
    await newToggle;
  }
}
