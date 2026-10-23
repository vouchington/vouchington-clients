using Voucha.Client.Core.Analytics;

namespace Voucha.Client.Core.Topics;

public sealed partial class TopicDetailViewModel
{
  private void RecordRssFeedViewOnce()
  {
    if (productSignals is null || SourceRssFeedId is not { Length: > 0 } feedId) return;
    if (!recordedRssFeedViewIds.Add(feedId)) return;
    productSignals.RecordRssFeedView(feedId);
  }
}
