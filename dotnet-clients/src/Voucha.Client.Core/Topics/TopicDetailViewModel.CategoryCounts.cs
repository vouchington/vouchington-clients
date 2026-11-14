using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Profiles;

namespace Voucha.Client.Core.Topics;

public sealed partial class TopicDetailViewModel
{
  private TopicMetrics? topicMetrics;

  public string PostsCountLabel => CategoryCount(UiMessageKey.ExtractedIntentsProductPostsPostsA80811cf,
      TopicMetricKeys.Discussions, TopicMetricKeys.Reviews, TopicMetricKeys.DataPoints);
  public string ReviewsCountLabel => CategoryCount(UiMessageKey.ExtractedIntentsProductPostsReviews84cb7871, TopicMetricKeys.Reviews);
  public string DataPointsCountLabel => CategoryCount(UiMessageKey.ExtractedIntentsProductPostsDataPoints1da65e3a, TopicMetricKeys.DataPoints);

  private string CategoryCount(UiMessageKey label, params string[] categories)
  {
    var publicCount = categories.Sum(category => topicMetrics?.Count?.GetValueOrDefault(category) ?? 0);
    var viewerCount = categories.Sum(category => topicMetrics?.ViewerCount?.GetValueOrDefault(category) ?? 0);
    var capped = categories.Any(category => topicMetrics?.CountCapped?.GetValueOrDefault(category) == true ||
        topicMetrics?.ViewerCountCapped?.GetValueOrDefault(category) == true);
    return localization.Format(UiMessageKey.NativeDotnetProfileProfileTabCount,
        ("label", localization.Localize(label)),
        ("count", ProfileMetricCount.Format(publicCount, capped || viewerCount > publicCount, localization)));
  }

  private void NotifyCategoryCountsChanged()
  {
    OnPropertyChanged(nameof(PostsCountLabel));
    OnPropertyChanged(nameof(ReviewsCountLabel));
    OnPropertyChanged(nameof(DataPointsCountLabel));
  }
}
