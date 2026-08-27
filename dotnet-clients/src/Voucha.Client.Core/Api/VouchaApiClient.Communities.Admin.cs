namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<CommunityMutationResponse> UpdateCommunityPostTypeSettingsAsync(
      string idOrSlug,
      UpdateCommunityPostTypeSettingsRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityMutationResponse>(
          VouchaApiEndpoints.UpdateCommunityPostTypeSettings(idOrSlug, Require(request)),
          cancellationToken);

  public Task<CommunityWarningResponse> IssueCommunityWarningAsync(
      string idOrSlug,
      IssueCommunityWarningRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityWarningResponse>(
          VouchaApiEndpoints.IssueCommunityWarning(idOrSlug, Require(request)),
          cancellationToken);

  public Task<CommunityAutomodActionsResponse> FetchCommunityAutomodRecentActionsAsync(
      string idOrSlug,
      string? after = null,
      int? limit = 25,
      string? window = null,
      string? source = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityAutomodActionsResponse>(
          VouchaApiEndpoints.CommunityAutomodRecentActions(idOrSlug, after, limit, window, source),
          cancellationToken);

  public Task ResolveCommunityModerationReportAsync(
      string idOrSlug,
      string reportId,
      string status,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.ResolveCommunityModerationReport(idOrSlug, reportId, status), cancellationToken);

  public Task<CommunityModerationResultsResponse> FetchCommunityModerationResultsAsync(
      string idOrSlug,
      string postId,
      CancellationToken cancellationToken = default) =>
      SendAsync<CommunityModerationResultsResponse>(
          VouchaApiEndpoints.CommunityModerationResults(idOrSlug, postId),
          cancellationToken);

  public Task ConfirmCommunityBanEvasionAsync(
      string idOrSlug,
      string userId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.ConfirmCommunityBanEvasion(idOrSlug, userId), cancellationToken);

  public Task DismissCommunityBanEvasionAsync(
      string idOrSlug,
      string userId,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DismissCommunityBanEvasion(idOrSlug, userId), cancellationToken);
}
