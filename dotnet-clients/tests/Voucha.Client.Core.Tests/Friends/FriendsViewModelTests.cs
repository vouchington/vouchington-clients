using Voucha.Client.Core.Api;
using Voucha.Client.Core.Friends;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Friends;

public sealed partial class FriendsViewModelTests
{
  [Fact]
  public async Task LoadAsyncLoadsFollowingByDefault()
  {
    var service = new RecordingFriendsService(
        following: CreateFollowingResponse(
            new User("friend-1", "friend", null, Name: "Friendly User")),
        followers: CreateFollowersResponse());
    var viewModel = new FriendsViewModel(service, "user-abc");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(FriendsTab.Following, viewModel.SelectedTab);
    Assert.True(viewModel.IsFollowingSelected);
    Assert.Single(viewModel.Items);
    Assert.True(viewModel.Items[0].IsFollowing);
    Assert.Equal("Friendly User", viewModel.Items[0].DisplayName);
    Assert.Null(viewModel.ErrorMessage);
    Assert.Equal(LoadState.Loaded, viewModel.State);
  }

  [Fact]
  public async Task LoadAsyncPrefersApiDisplayNameSource()
  {
    var service = new RecordingFriendsService(
        following: CreateFollowingResponse(new User("friend-1", "friend", null, DisplayNameSource: "Friendly User")),
        followers: CreateFollowersResponse());
    var viewModel = new FriendsViewModel(service, "user-abc");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Friendly User", viewModel.Items[0].DisplayName);
    Assert.Equal("Friendly User", viewModel.Items[0].PrimaryLabel);
  }

  [Fact]
  public async Task SelectTabAsyncPreservesKnownFollowingStateForFollowers()
  {
    var service = new RecordingFriendsService(
        following: CreateFollowingResponse(
            new User("friend-1", "friend", null, Name: "Friendly User")),
        followers: CreateFollowersResponse(
            new User("friend-1", "friend", null, Name: "Friendly User"),
            new User("friend-2", "other", null, Name: "Other User")));
    var viewModel = new FriendsViewModel(service, "user-abc");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.SelectTabAsync(FriendsTab.Followers, TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsFollowersSelected);
    Assert.Equal(2, viewModel.Items.Count);
    Assert.True(viewModel.Items.Single(item => item.Id == "friend-1").IsFollowing);
    Assert.False(viewModel.Items.Single(item => item.Id == "friend-2").IsFollowing);
  }

  [Fact]
  public async Task SelectTabAsyncClearsRowsWhileUncachedTabLoads()
  {
    var pendingFollowers = new TaskCompletionSource<UserFollowersResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingFriendsService(
        following: CreateFollowingResponse(
            new User("friend-1", "friend", null, Name: "Friendly User")))
    {
      PendingFollowersFetch = pendingFollowers,
    };
    var viewModel = new FriendsViewModel(service, "user-abc");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var selectTask = viewModel.SelectTabAsync(FriendsTab.Followers, TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.Items);
    Assert.False(viewModel.HasMore);

    pendingFollowers.SetResult(CreateFollowersResponse(new User("friend-2", "other", null, Name: "Other User")));
    await selectTask;

    Assert.Equal("friend-2", viewModel.Items.Single().Id);
  }

  [Fact]
  public async Task ReloadAsyncRefreshesTheActiveTab()
  {
    var service = new RecordingFriendsService(
        following: CreateFollowingResponse(new User("friend-1", "friend", null, Name: "Original")),
        followers: CreateFollowersResponse());
    var viewModel = new FriendsViewModel(service, "user-abc");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    service.FollowingResponse = CreateFollowingResponse(new User("friend-2", "friend2", null, Name: "Updated"));

    await viewModel.ReloadAsync(TestContext.Current.CancellationToken);

    Assert.Single(viewModel.Items);
    Assert.Equal("friend2", viewModel.Items[0].Username);
    Assert.Equal(2, service.FollowingFetchCount);
  }

  [Fact]
  public async Task ReloadAsyncOnFollowersDoesNotFetchPartialFollowingState()
  {
    var service = new RecordingFriendsService(
        following: CreateFollowingResponse(new User("friend-1", "friend1", null, Name: "First")),
        followers: CreateFollowersResponse(
            new User("friend-1", "friend1", null, Name: "First"),
            new User("friend-2", "friend2", null, Name: "Second")));
    var viewModel = new FriendsViewModel(service, "user-abc", FriendsTab.Followers);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    service.FollowingResponse = CreateFollowingResponse(new User("friend-2", "friend2", null, Name: "Second"));

    await viewModel.ReloadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(0, service.FollowingFetchCount);
    Assert.False(viewModel.Items.Single(item => item.Id == "friend-2").IsFollowing);
    Assert.False(viewModel.Items.Single(item => item.Id == "friend-1").IsFollowing);
  }

  [Fact]
  public async Task UpdateCurrentUserClearsRowsAndReloadsAgainstNewSession()
  {
    var service = new RecordingFriendsService(
        following: CreateFollowingResponse(new User("friend-1", "friend", null, Name: "Original")),
        followers: CreateFollowersResponse());
    var viewModel = new FriendsViewModel(service, "user-abc");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    service.FollowingResponse = CreateFollowingResponse(new User("friend-2", "friend2", null, Name: "Updated"));
    viewModel.UpdateCurrentUser("user-def");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Single(viewModel.Items);
    Assert.Equal("friend2", viewModel.Items[0].Username);
    Assert.Equal(["user-abc", "user-def"], service.FollowingFetchUserIds);
  }

  [Fact]
  public async Task ToggleFollowAsyncDoesNotRestoreOldSessionRowsAfterAccountChange()
  {
    var delayedMutation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingFriendsService(
        following: CreateFollowingResponse(new User("friend-1", "friend", null, Name: "Friendly User")),
        followers: CreateFollowersResponse())
    {
      FollowCompletions = new Dictionary<string, TaskCompletionSource>(StringComparer.Ordinal)
      {
        ["friend-1"] = delayedMutation,
      },
    };
    var viewModel = new FriendsViewModel(service, "user-abc");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var toggleTask = viewModel.ToggleFollowAsync(viewModel.Items[0], TestContext.Current.CancellationToken);
    viewModel.UpdateCurrentUser("user-def");
    delayedMutation.SetException(new InvalidOperationException("Mutation failed."));
    await toggleTask;

    Assert.Empty(viewModel.Items);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Null(viewModel.ErrorMessage);
  }

  [Fact]
  public async Task LoadAsyncWithoutCurrentUserReturnsEmptyLoadedState()
  {
    var viewModel = new FriendsViewModel(new RecordingFriendsService(), null);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.Items);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Null(viewModel.ErrorMessage);
  }

  [Fact]
  public async Task SelectTabAsyncSurfacesFollowersFailure()
  {
    var service = new RecordingFriendsService(
        following: CreateFollowingResponse(),
        followers: CreateFollowersResponse(new User("friend-1", "friend", null, Name: "Friendly User")))
    {
      FailFollowersFetch = true,
    };
    var viewModel = new FriendsViewModel(service, "user-abc");

    await viewModel.SelectTabAsync(FriendsTab.Followers, TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.Items);
    Assert.Equal("Followers fetch failed.", viewModel.ErrorMessage);
    Assert.Equal(LoadState.Error, viewModel.State);
  }

  [Fact]
  public async Task SelectTabAsyncClearsPreviousRowsAfterActiveTabLoadFailure()
  {
    var service = new RecordingFriendsService(
        following: CreateFollowingResponse(new User("friend-1", "friend", null, Name: "Friendly User")),
        followers: CreateFollowersResponse(new User("friend-2", "other", null, Name: "Other User")));
    var viewModel = new FriendsViewModel(service, "user-abc");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    service.FailFollowersFetch = true;
    await viewModel.SelectTabAsync(FriendsTab.Followers, TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsFollowersSelected);
    Assert.Empty(viewModel.Items);
    Assert.Equal("Followers fetch failed.", viewModel.ErrorMessage);
    Assert.Equal(LoadState.Error, viewModel.State);
  }

  [Fact]
  public async Task SelectTabAsyncDoesNotFetchFollowingBeforeFollowers()
  {
    var service = new RecordingFriendsService(
        following: CreateFollowingResponse(new User("friend-old", "old", null, Name: "Old")),
        followers: CreateFollowersResponse(
            new User("friend-old", "old", null, Name: "Old"),
            new User("friend-new", "new", null, Name: "New")));
    var viewModel = new FriendsViewModel(service, "user-abc", FriendsTab.Followers);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Empty(service.FollowingFetchUserIds);
    Assert.False(viewModel.Items.Single(item => item.Id == "friend-new").IsFollowing);
    Assert.False(viewModel.Items.Single(item => item.Id == "friend-old").IsFollowing);
  }

  [Fact]
  public async Task ToggleFollowAsyncOptimisticallyFollowsAUser()
  {
    var service = new RecordingFriendsService(
        following: CreateFollowingResponse(),
        followers: CreateFollowersResponse(new User("friend-1", "friend", null, Name: "Friendly User")));
    var viewModel = new FriendsViewModel(service, "user-abc", FriendsTab.Followers);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.ToggleFollowAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.True(viewModel.Items[0].IsFollowing);
    Assert.Equal([("friend-1", true)], service.FollowCalls);
    Assert.Equal(LoadState.Loaded, viewModel.State);
  }

  [Fact]
  public async Task ToggleFollowAsyncIgnoresConcurrentToggleForSameUser()
  {
    var delayedFollow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingFriendsService(
        following: CreateFollowingResponse(),
        followers: CreateFollowersResponse(new User("friend-1", "friend", null, Name: "Friendly User")))
    {
      FollowCompletions = new Dictionary<string, TaskCompletionSource>(StringComparer.Ordinal)
      {
        ["friend-1"] = delayedFollow,
      },
    };
    var viewModel = new FriendsViewModel(service, "user-abc", FriendsTab.Followers);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var row = viewModel.Items[0];
    var firstToggle = viewModel.ToggleFollowAsync(row, TestContext.Current.CancellationToken);
    await viewModel.ToggleFollowAsync(row, TestContext.Current.CancellationToken);
    delayedFollow.SetResult();
    await firstToggle;

    Assert.True(viewModel.Items[0].IsFollowing);
    Assert.Equal([("friend-1", true)], service.FollowCalls);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Null(viewModel.ErrorMessage);
  }

  [Fact]
  public async Task ToggleFollowAsyncPreventsStaleReloadFromOverwritingOptimisticState()
  {
    var delayedFollowers = new TaskCompletionSource<UserFollowersResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingFriendsService(
        following: CreateFollowingResponse(),
        followers: CreateFollowersResponse(new User("friend-1", "friend", null, Name: "Friendly User")));
    var viewModel = new FriendsViewModel(service, "user-abc", FriendsTab.Followers);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    service.PendingFollowersFetch = delayedFollowers;
    var reload = viewModel.ReloadAsync(TestContext.Current.CancellationToken);
    await viewModel.ToggleFollowAsync(viewModel.Items[0], TestContext.Current.CancellationToken);
    delayedFollowers.SetResult(CreateFollowersResponse(new User("friend-1", "friend", null, Name: "Friendly User")));
    await reload;

    Assert.True(viewModel.Items[0].IsFollowing);
    Assert.Equal([("friend-1", true)], service.FollowCalls);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Null(viewModel.ErrorMessage);
  }

  [Fact]
  public async Task SelectTabAsyncPreservesPendingFollowDuringFollowingRefresh()
  {
    var delayedFollow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingFriendsService(
        following: CreateFollowingResponse(),
        followers: CreateFollowersResponse(new User("friend-1", "friend", null, Name: "Friendly User")))
    {
      FollowCompletions = new Dictionary<string, TaskCompletionSource>(StringComparer.Ordinal)
      {
        ["friend-1"] = delayedFollow,
      },
    };
    var viewModel = new FriendsViewModel(service, "user-abc", FriendsTab.Followers);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var toggleTask = viewModel.ToggleFollowAsync(viewModel.Items[0], TestContext.Current.CancellationToken);
    await viewModel.SelectTabAsync(FriendsTab.Following, TestContext.Current.CancellationToken);

    Assert.Single(viewModel.Items);
    Assert.Equal("friend-1", viewModel.Items[0].Id);
    Assert.True(viewModel.Items[0].IsFollowing);

    delayedFollow.SetResult();
    await toggleTask;
  }

  [Fact]
  public async Task FollowingRefreshReconcilesCachedFollowerRowsAfterCompletedMutation()
  {
    var service = new RecordingFriendsService(
        following: CreateFollowingResponse(),
        followers: CreateFollowersResponse(new User("friend-1", "friend", null, Name: "Friendly User")));
    var viewModel = new FriendsViewModel(service, "user-abc", FriendsTab.Followers);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.ToggleFollowAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.True(viewModel.Items[0].IsFollowing);

    service.FollowingResponse = CreateFollowingResponse();
    await viewModel.SelectTabAsync(FriendsTab.Following, TestContext.Current.CancellationToken);
    await viewModel.SelectTabAsync(FriendsTab.Followers, TestContext.Current.CancellationToken);

    Assert.False(viewModel.Items[0].IsFollowing);
  }

  [Fact]
  public async Task ReloadAsyncPreservesPendingUnfollowDuringFollowingRefresh()
  {
    var delayedUnfollow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var friend = new User("friend-1", "friend", null, Name: "Friendly User");
    var service = new RecordingFriendsService(
        following: CreateFollowingResponse(friend),
        followers: CreateFollowersResponse())
    {
      FollowCompletions = new Dictionary<string, TaskCompletionSource>(StringComparer.Ordinal)
      {
        ["friend-1"] = delayedUnfollow,
      },
    };
    var viewModel = new FriendsViewModel(service, "user-abc");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var toggleTask = viewModel.ToggleFollowAsync(viewModel.Items[0], TestContext.Current.CancellationToken);
    await viewModel.ReloadAsync(TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.Items);

    delayedUnfollow.SetResult();
    await toggleTask;
  }

  [Fact]
  public async Task ToggleFollowAsyncOptimisticallyUnfollowsAUser()
  {
    var service = new RecordingFriendsService(
        following: CreateFollowingResponse(new User("friend-1", "friend", null, Name: "Friendly User")),
        followers: CreateFollowersResponse());
    var viewModel = new FriendsViewModel(service, "user-abc");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.ToggleFollowAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.Items);
    Assert.Equal([("friend-1", false)], service.FollowCalls);
  }

  [Fact]
  public async Task ToggleFollowAsyncRollsBackOnMutationFailure()
  {
    var service = new RecordingFriendsService(
        following: CreateFollowingResponse(),
        followers: CreateFollowersResponse(new User("friend-1", "friend", null, Name: "Friendly User")))
    {
      FailMutations = true,
    };
    var viewModel = new FriendsViewModel(service, "user-abc", FriendsTab.Followers);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.ToggleFollowAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.False(viewModel.Items[0].IsFollowing);
    Assert.Equal("Mutation failed.", viewModel.ErrorMessage);
    Assert.Equal(LoadState.Error, viewModel.State);
  }

  [Fact]
  public async Task ToggleFollowAsyncRollsBackOnlyTheFailedRowAfterLaterOptimisticMutation()
  {
    var delayedFollow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingFriendsService(
        following: CreateFollowingResponse(),
        followers: CreateFollowersResponse(
            new User("friend-1", "friend-1", null, Name: "Friend One"),
            new User("friend-2", "friend-2", null, Name: "Friend Two")))
    {
      FollowCompletions = new Dictionary<string, TaskCompletionSource>(StringComparer.Ordinal)
      {
        ["friend-1"] = delayedFollow,
      },
    };
    var viewModel = new FriendsViewModel(service, "user-abc", FriendsTab.Followers);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var firstToggle = viewModel.ToggleFollowAsync(viewModel.Items[0], TestContext.Current.CancellationToken);
    await viewModel.ToggleFollowAsync(viewModel.Items[1], TestContext.Current.CancellationToken);
    delayedFollow.SetException(new InvalidOperationException("Mutation failed."));
    await firstToggle;

    Assert.Collection(viewModel.Items, item =>
    {
      Assert.Equal("friend-1", item.Id);
      Assert.False(item.IsFollowing);
    }, item =>
    {
      Assert.Equal("friend-2", item.Id);
      Assert.True(item.IsFollowing);
    });
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Null(viewModel.ErrorMessage);
    Assert.Equal([("friend-1", true), ("friend-2", true)], service.FollowCalls);
  }

  [Fact]
  public async Task ToggleFollowAsyncIgnoresSelfRows()
  {
    var service = new RecordingFriendsService(
        following: CreateFollowingResponse(new User("user-abc", "me", null, Name: "Me")),
        followers: CreateFollowersResponse());
    var viewModel = new FriendsViewModel(service, "user-abc");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.ToggleFollowAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.True(viewModel.Items[0].IsSelf);
    Assert.Empty(service.FollowCalls);
    Assert.Equal(LoadState.Loaded, viewModel.State);
  }

}
