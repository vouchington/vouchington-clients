using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Friends;
using Voucha.Client.Core.Profiles;
using Voucha.Client.Core.Topics;

namespace Voucha.Client.App.Pages;

public partial class ProfilePage
{
  private async void OnFollowUserClicked(object? sender, EventArgs e)
  {
    await RunProfileActionAsync(async () =>
    {
      if (viewModel.IsSignedInViewer)
      {
        await viewModel.ToggleFollowUserAsync();
      }
      else if (Shell.Current is AppShell shell)
      {
        await shell.OpenNativePathAsync(ProfileNavigationTargets.SignIn);
      }
    });
  }

  private async void OnProfileScopeClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: ProfileScopeTabRow row })
    {
      await RunProfileActionAsync(() => viewModel.SelectScopeAsync(row.Collection));
    }
  }

  private async void OnLoadMoreClicked(object? sender, EventArgs e) =>
      await RunProfileActionAsync(() => viewModel.LoadMoreAsync());

  private async void OnProfileFriendClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: FriendRow row } && Shell.Current is AppShell shell)
      await RunProfileActionAsync(() => shell.OpenNativePathAsync(ProfileNavigationTargets.User(row)));
  }

  private async void OnProfileTopicClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: TopicRow row } && Shell.Current is AppShell shell)
      await RunProfileActionAsync(() => shell.OpenNativePathAsync(ProfileNavigationTargets.Topic(row)));
  }

  private async void OnProfileSourceClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: RssFeedSource row } && Shell.Current is AppShell shell)
      await RunProfileActionAsync(() => shell.OpenNativePathAsync(ProfileNavigationTargets.Source(row)));
  }

  private async void OnProfileCommunityClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: CommunityBrowseRow row } && Shell.Current is AppShell shell)
      await RunProfileActionAsync(() => shell.OpenNativePathAsync(ProfileNavigationTargets.Community(row)));
  }
}
