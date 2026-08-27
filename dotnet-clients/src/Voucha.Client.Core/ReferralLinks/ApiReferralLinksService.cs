using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.ReferralLinks;

public sealed class ApiReferralLinksService : IReferralLinksService
{
  private readonly VouchaApiClient client;

  public ApiReferralLinksService(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  public Task<ReferralLinkFeedResponse> FetchFeedAsync(
      FetchReferralLinksFeedRequest request,
      CancellationToken cancellationToken = default) =>
      client.FetchReferralLinksFeedAsync(request, cancellationToken);

  public Task<ReferralLinksResponse> FetchMineAsync(
      FetchReferralLinksRequest request,
      CancellationToken cancellationToken = default) =>
      client.FetchReferralLinksAsync(request, cancellationToken);

  public Task<ReferralClickLogResponse> FetchClicksAsync(
      FetchReferralClickLogsRequest request,
      CancellationToken cancellationToken = default) =>
      client.FetchMyReferralClicksAsync(request, cancellationToken);

  public Task<TopicSearchResponse> SearchReferralProgramsAsync(
      SearchTopicsRequest request,
      CancellationToken cancellationToken = default) =>
      client.SearchTopicsAsync(
          (request ?? throw new ArgumentNullException(nameof(request)))
          with
          {
            TopicTypes = "referral_program",
          },
          cancellationToken);

  public Task<ReferralProgramValidationInfoResponse> FetchValidationInfoAsync(
      string topicIdOrSlug,
      CancellationToken cancellationToken = default) =>
      client.FetchReferralProgramValidationInfoAsync(topicIdOrSlug, cancellationToken);

  public Task<PrioritizedReferralLinksResponse> FetchPrioritizedAsync(
      FetchPrioritizedReferralLinksRequest request,
      CancellationToken cancellationToken = default) =>
      client.FetchPrioritizedReferralLinksAsync(request, cancellationToken);

  public Task<TrendingReferralProgramsResponse> FetchTrendingProgramsAsync(
      FetchTrendingReferralProgramsRequest request,
      CancellationToken cancellationToken = default) =>
      client.FetchTrendingReferralProgramsAsync(request, cancellationToken);

  public Task CreateAsync(
      CreateReferralLinkBody body,
      CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.CreateReferralLink(body), cancellationToken);

  public Task UpdateAsync(
      string referralLinkId,
      UpdateReferralLinkBody body,
      CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.UpdateReferralLink(referralLinkId, body), cancellationToken);

  public Task DeleteAsync(string referralLinkId, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.DeleteReferralLink(referralLinkId), cancellationToken);

  public Task ActivateAsync(string referralLinkId, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.ActivateReferralLink(referralLinkId), cancellationToken);

  public Task DeactivateAsync(string referralLinkId, CancellationToken cancellationToken = default) =>
      client.SendAsync(VouchaApiEndpoints.DeactivateReferralLink(referralLinkId), cancellationToken);
}
