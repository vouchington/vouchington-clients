namespace Voucha.Client.Core.Topics;

public sealed partial class TopicDetailViewModel
{
  public TopicDetailViewModel CreateSourceCrawlDetailViewModel() => new(
      topicsService,
      bookmarkService,
      localization,
      localeController,
      ApiClient,
      CanViewPaidSourceCrawlHistory,
      CanManageSourceCrawls,
      RequiresAuthoritativeSourceCrawlMembership);
}
