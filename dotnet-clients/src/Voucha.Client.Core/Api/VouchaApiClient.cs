using Voucha.Client.Core.Content;

namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  private readonly HttpClient httpClient;

  public VouchaApiClient(HttpClient httpClient) =>
      this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

  public Task<CommunitySearchResponse> SearchCommunitiesAsync(
      SearchCommunitiesRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunitySearchResponse>(
          VouchaApiEndpoints.SearchCommunities(Require(request).Query),
          cancellationToken);

  public Task<CommunityResponse> ShowCommunityAsync(
      ShowCommunityRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityResponse>(
          VouchaApiEndpoints.ShowCommunity(Require(request).Slug),
          cancellationToken);

  public Task<UpsertCommunityAiAgentResponse> UpsertCommunityAiAgentAsync(
      UpsertCommunityAiAgentRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<UpsertCommunityAiAgentResponse>(
          VouchaApiEndpoints.UpsertCommunityAiAgent(
              Require(request).CommunitySlug,
              request.AgentSlug,
              request.Body),
          cancellationToken);

  public Task<TopicSearchResponse> SearchTopicsAsync(
      SearchTopicsRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<TopicSearchResponse>(
          VouchaApiEndpoints.SearchTopics(Require(request).Query, request.TopicTypes, request.Limit),
          cancellationToken);

  public Task<CombinedSearchResponse> CombinedSearchAsync(
      string query,
      int limit = 10,
      CancellationToken cancellationToken = default) =>
      SendAsync<CombinedSearchResponse>(
          VouchaApiEndpoints.CombinedSearch(query, limit),
          cancellationToken);

  public Task<FeatureFlagsResponse> FetchFeatureFlagsAsync(CancellationToken cancellationToken = default) =>
      SendAsync<FeatureFlagsResponse>(VouchaApiEndpoints.FeatureFlags(), cancellationToken);

  public Task<CaptchaConfigResponse> FetchCaptchaConfigAsync(CancellationToken cancellationToken = default) =>
      SendAsync<CaptchaConfigResponse>(VouchaApiEndpoints.CaptchaConfig(), cancellationToken);

  public Task<FediverseSearchResponse> FediverseSearchAsync(
      string query,
      string? providers = null,
      string? type = null,
      int? limit = null,
      string? after = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<FediverseSearchResponse>(
          VouchaApiEndpoints.FediverseSearch(query, providers, type, limit, after),
          cancellationToken);

  public Task<TopicMutationResponse> CreateTopicAsync(
      CreateTopicRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<TopicMutationResponse>(
          VouchaApiEndpoints.CreateTopic(Require(request)),
          cancellationToken);

  public Task<ReferralLinkFeedResponse> FetchReferralLinksFeedAsync(
      FetchReferralLinksFeedRequest request,
      CancellationToken cancellationToken = default)
  {
    var requiredRequest = Require(request);
    return SendAsync<ReferralLinkFeedResponse>(
        VouchaApiEndpoints.ReferralLinksFeed(
            requiredRequest.FeedType,
            requiredRequest.After,
            requiredRequest.Limit),
        cancellationToken);
  }

  public Task<ReferralLinksResponse> FetchReferralLinksAsync(
      FetchReferralLinksRequest request,
      CancellationToken cancellationToken = default)
  {
    var requiredRequest = Require(request);
    return SendAsync<ReferralLinksResponse>(
        VouchaApiEndpoints.ReferralLinks(requiredRequest.After, requiredRequest.Limit),
        cancellationToken);
  }

  public Task<ReferralClickLogResponse> FetchMyReferralClicksAsync(
      FetchReferralClickLogsRequest request,
      CancellationToken cancellationToken = default)
  {
    var requiredRequest = Require(request);
    return SendAsync<ReferralClickLogResponse>(
        VouchaApiEndpoints.MyReferralClicks(requiredRequest.After, requiredRequest.Limit),
        cancellationToken);
  }

  public Task<TrendingReferralProgramsResponse> FetchTrendingReferralProgramsAsync(
      FetchTrendingReferralProgramsRequest request,
      CancellationToken cancellationToken = default)
  {
    var requiredRequest = Require(request);
    return SendAsync<TrendingReferralProgramsResponse>(
        VouchaApiEndpoints.TrendingReferralPrograms(requiredRequest.After, requiredRequest.Limit),
        cancellationToken);
  }

  public Task<PrioritizedReferralLinksResponse> FetchPrioritizedReferralLinksAsync(
      FetchPrioritizedReferralLinksRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<PrioritizedReferralLinksResponse>(
          VouchaApiEndpoints.PrioritizedReferralLinks(Require(request).ReferralProgramId, request.All),
          cancellationToken);

  public Task<PostsFeedResponse> FetchPostsFeedAsync(
      FetchPostsFeedRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<PostsFeedResponse>(
          VouchaApiEndpoints.Posts(
              Require(request).Feed,
              request.After,
              request.Limit,
              request.PostTypes),
          cancellationToken);

  public Task<NotificationsResponse> FetchNotificationsAsync(CancellationToken cancellationToken = default) =>
      FetchNotificationsAsync(new FetchNotificationsRequest(), cancellationToken);

  public Task<GrowthMetricsResponse> FetchGrowthMetricsAsync(
      GrowthMetricsRange range = default,
      CancellationToken cancellationToken = default) =>
      SendAsync<GrowthMetricsResponse>(
          VouchaApiEndpoints.GrowthMetrics(range),
          cancellationToken);

  public Task<NotificationsResponse> FetchNotificationsAsync(
      FetchNotificationsRequest request,
      CancellationToken cancellationToken = default)
  {
    var requiredRequest = Require(request);
    return SendAsync<NotificationsResponse>(
          VouchaApiEndpoints.Notifications(requiredRequest.After, requiredRequest.Limit),
          cancellationToken);
  }

  public Task MarkNotificationReadAsync(
      string notificationId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.MarkNotificationRead(notificationId), cancellationToken);

  public Task<NotificationRedirectTargetResponse> FetchNotificationRedirectTargetAsync(
      string notificationId,
      CancellationToken cancellationToken = default) =>
      SendAsync<NotificationRedirectTargetResponse>(
          VouchaApiEndpoints.NotificationRedirectTarget(notificationId),
          cancellationToken);

  public Task MarkAllNotificationsReadAsync(CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.MarkAllNotificationsRead(), cancellationToken);

  public Task<MyIdentityResponse> FetchMyIdentityAsync(CancellationToken cancellationToken = default) =>
      SendAsync<MyIdentityResponse>(
          VouchaApiEndpoints.MyIdentity(),
          cancellationToken);

  public Task<MyProfileResponse> FetchMyProfileAsync(CancellationToken cancellationToken = default) =>
      SendAsync<MyProfileResponse>(
          VouchaApiEndpoints.MyProfile(),
          cancellationToken);

  public Task<MarkdownPreviewResponse> PreviewMarkdownAsync(
      string markdown,
      CancellationToken cancellationToken = default) =>
      SendAsync<MarkdownPreviewResponse>(
          VouchaApiEndpoints.MarkdownPreview(new MarkdownPreviewRequest(markdown)),
          cancellationToken);
}
