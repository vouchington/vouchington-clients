using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Profiles;

public sealed partial class ProfileViewModelSafetyTests
{
  [Fact]
  public async Task SignedOutProfileShowsFollowAuthenticationActionOnly()
  {
    var safety = new RecordingProfileSafetyService();
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), safety);

    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(viewModel.CanShowFollowAction);
    Assert.True(viewModel.CanFollowUser);
    Assert.False(viewModel.CanUnfollowUser);
    Assert.False(viewModel.IsSignedInViewer);
    Assert.False(viewModel.CanActOnUser);
    Assert.Empty(safety.FetchedUserIds);
  }

  [Fact]
  public async Task SelfProfileHidesAllRelationshipAndSafetyActions()
  {
    var viewModel = NewViewModel(new User("user-1", "alice", "Hello"));
    viewModel.SetCurrentViewer("user-1", "alice");

    await viewModel.LoadPublicAsync("alice", cancellationToken: TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanShowFollowAction);
    Assert.False(viewModel.CanFollowUser);
    Assert.False(viewModel.CanUnfollowUser);
    Assert.False(viewModel.CanActOnUser);
  }

  [Fact]
  public async Task FollowAndBlockUpdateHeaderRelationshipState()
  {
    var safety = new RecordingProfileSafetyService();
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), safety);
    viewModel.SetCurrentViewer("user-1", "alice");
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    await viewModel.ToggleFollowUserAsync(TestContext.Current.CancellationToken);
    Assert.False(viewModel.CanFollowUser);
    Assert.True(viewModel.CanUnfollowUser);
    await viewModel.ToggleBlockUserAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsBlockedUser);
    Assert.False(viewModel.CanFollowUser);
    Assert.False(viewModel.CanUnfollowUser);
    await viewModel.ToggleFollowUserAsync(TestContext.Current.CancellationToken);

    var changedProperties = new List<string?>();
    viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
    await viewModel.ToggleBlockUserAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.IsFollowingUser);
    Assert.False(viewModel.IsBlockedUser);
    Assert.True(viewModel.CanFollowUser);
    Assert.Contains(nameof(viewModel.CanFollowUser), changedProperties);
    Assert.Contains(nameof(viewModel.CanUnfollowUser), changedProperties);
    Assert.Equal(
        [
          ("user-2", BookmarkPredicate.Follow, true),
          ("user-2", BookmarkPredicate.Block, true),
          ("user-2", BookmarkPredicate.Block, false),
        ],
        safety.BookmarkCalls);
  }
}
