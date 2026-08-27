using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.ReferralLinks;

public interface IReferralLinksService
{
  Task<ReferralLinkFeedResponse> FetchFeedAsync(
      FetchReferralLinksFeedRequest request,
      CancellationToken cancellationToken = default);

  Task<ReferralLinksResponse> FetchMineAsync(
      FetchReferralLinksRequest request,
      CancellationToken cancellationToken = default);

  Task<ReferralClickLogResponse> FetchClicksAsync(
      FetchReferralClickLogsRequest request,
      CancellationToken cancellationToken = default);

  Task<TopicSearchResponse> SearchReferralProgramsAsync(
      SearchTopicsRequest request,
      CancellationToken cancellationToken = default);

  Task<ReferralProgramValidationInfoResponse> FetchValidationInfoAsync(
      string topicIdOrSlug,
      CancellationToken cancellationToken = default);

  Task<PrioritizedReferralLinksResponse> FetchPrioritizedAsync(
      FetchPrioritizedReferralLinksRequest request,
      CancellationToken cancellationToken = default);

  Task<TrendingReferralProgramsResponse> FetchTrendingProgramsAsync(
      FetchTrendingReferralProgramsRequest request,
      CancellationToken cancellationToken = default);

  Task CreateAsync(CreateReferralLinkBody body, CancellationToken cancellationToken = default);

  Task UpdateAsync(
      string referralLinkId,
      UpdateReferralLinkBody body,
      CancellationToken cancellationToken = default);

  Task DeleteAsync(string referralLinkId, CancellationToken cancellationToken = default);

  Task ActivateAsync(string referralLinkId, CancellationToken cancellationToken = default);

  Task DeactivateAsync(string referralLinkId, CancellationToken cancellationToken = default);
}
