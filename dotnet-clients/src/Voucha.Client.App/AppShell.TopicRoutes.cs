using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Topics;
using Voucha.Client.Core.TopicRecommendations;
using Voucha.Client.Core.Fediverse;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private Task? TryOpenTopicRouteAsync(NativeDeepLinkResolution resolution)
  {
    if (resolution.Match is { Path: var instancePath } match &&
        instancePath.StartsWith("/instance/", StringComparison.Ordinal) &&
        match.Param("idOrSlug", "id") is { Length: > 0 } instanceId)
    {
      return Navigation.PushAsync(new FediverseInstanceDetailPage(
          new FediverseInstanceDetailViewModel(
              serviceProvider.GetRequiredService<VouchaApiClient>(),
              serviceProvider.GetRequiredService<IBookmarkService>(),
              serviceProvider.GetRequiredService<IUiLocalization>(),
              serviceProvider.GetRequiredService<ITopicsService>(),
              serviceProvider.GetRequiredService<IUiLocaleController>()),
          serviceProvider.GetRequiredService<ISessionStore>(),
          instanceId));
    }

    if (resolution.DestinationId == NativeRouteDestinationId.TopicManagement)
    {
      return Navigation.PushAsync(CreateTopicManagementPage(resolution.Match));
    }

    if (TopicRecommendationRoute.TryGetDetailId(resolution.Match, out var recommendationId))
    {
      return Navigation.PushAsync(new TopicRecommendationDetailPage(
          serviceProvider.GetRequiredService<ITopicRecommendationDetailService>(),
          recommendationId,
          serviceProvider.GetRequiredService<IUiLocalization>(),
          serviceProvider.GetRequiredService<IUiLocaleController>(),
          serviceProvider.GetRequiredService<ISessionStore>(),
          serviceProvider.GetRequiredService<EmailVerificationRecoveryCoordinator>()));
    }

    if (TopicDetailRoute.TryGetTopicId(resolution.DestinationId, resolution.Match, out var topicId))
    {
      return Navigation.PushAsync(new TopicDetailPage(
          new TopicDetailViewModel(
              serviceProvider.GetRequiredService<ITopicsService>(),
              serviceProvider.GetRequiredService<IBookmarkService>(),
              serviceProvider.GetRequiredService<IUiLocalization>(),
              serviceProvider.GetRequiredService<IUiLocaleController>(),
              serviceProvider.GetRequiredService<VouchaApiClient>(),
              canViewSourceCrawlHistory: false,
              canManageSourceCrawls: serviceProvider.GetRequiredService<ISessionStore>().Current.CanManageTopics(),
              requiresAuthoritativeSourceCrawlMembership: true),
          serviceProvider.GetRequiredService<ISessionStore>(),
          topicId,
          serviceProvider.GetRequiredService<EmailVerificationRecoveryCoordinator>(),
          resolution.Match?.Template is "/source/:idOrSlug/crawls" or "/source/:idOrSlug/crawls/:crawlId",
          resolution.Match?.Param("crawlId")));
    }

    return null;
  }

  private TopicManagementPage CreateTopicManagementPage(NativeRouteMatch? match) =>
      new(
          serviceProvider.GetRequiredService<ITopicsService>(),
          serviceProvider.GetRequiredService<IImageUploadService>(),
          serviceProvider.GetRequiredService<VouchaApiClient>(),
          match?.Param("idOrSlug", "id"));
}
