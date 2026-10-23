using Voucha.Client.Core.Analytics;

namespace Voucha.Client.Core.Topics;

public sealed partial class TopicDetailViewModel
{
  private void RecordRssFeedViewOnce()
  {
    if (productSignals is null || SourceRssFeedId is not { Length: > 0 } feedId) return;
    if (string.Equals(recordedRssFeedViewId, feedId, StringComparison.Ordinal)) return;
    recordedRssFeedViewId = feedId;
    productSignals.RecordRssFeedView(feedId);
  }
}
