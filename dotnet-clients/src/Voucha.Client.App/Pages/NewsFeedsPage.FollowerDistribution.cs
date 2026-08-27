using Voucha.Client.Core.FollowerDistributions;
using Voucha.Client.Core.NewsFeeds;

namespace Voucha.Client.App.Pages;

public partial class NewsFeedsPage
{
  private async void OnFollowerSendClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: NewsFeedItem item } && item.HasItemActions &&
        FollowerDistributionActions.CanSendRssItem(sessionStore))
      await FollowerDistributionActions.ShowAsync(
          this, serviceProvider, sessionStore, FollowerDistributionTargetKind.RssFeedItem, item.Id);
  }
}
