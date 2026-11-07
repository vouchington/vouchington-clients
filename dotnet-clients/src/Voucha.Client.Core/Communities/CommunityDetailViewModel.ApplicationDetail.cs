namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  public CommunityApplicationDetailViewModel CreateApplicationDetail(string applicationId) =>
      new(service, communityIdOrSlug, applicationId);
}
