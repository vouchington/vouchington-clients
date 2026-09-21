using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Voucha.Client.Core;
using Voucha.Client.App.Support;
using Voucha.Client.App.Pages;
using Voucha.Client.App.Controls;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Growth;
using Voucha.Client.Core.HnDiscussions;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.ImportExport;
using Voucha.Client.Core.LandingPages;
using Voucha.Client.Core.Lists;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Messages;
using Voucha.Client.Core.MembershipAdministration;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Notifications;
using Voucha.Client.Core.Friends;
using Voucha.Client.Core.Fediverse;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Profiles;
using Voucha.Client.Core.ReferralLinks;
using Voucha.Client.Core.Relations;
using Voucha.Client.Core.Search;
using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Topics;
using Voucha.Client.Core.TopicRecommendations;
using Voucha.Client.Core.Tags;

namespace Voucha.Client.App;

public static partial class MauiProgram
{
  public static MauiApp CreateMauiApp()
  {
    var builder = MauiApp.CreateBuilder();
    builder
        .UseMauiApp<App>()
        .UseMauiCommunityToolkitMediaElement();
#if MACCATALYST || WINDOWS
    builder.ConfigureMauiHandlers(handlers => handlers.AddHandler<ProviderEmbedWebView, ProviderEmbedWebViewHandler>());
#endif

    builder.Services.AddSingleton(AppConfig.FromEnvironment());
    AddAuthSessionServices(builder.Services);
    builder.Services.AddSingleton<IDeviceLanguageProvider, MauiDeviceLanguageProvider>();
    builder.Services.AddSingleton<IUiThreadDispatcher, MauiUiThreadDispatcher>();
    builder.Services.AddSingleton<UiLocaleController>();
    builder.Services.AddSingleton<IUiLocaleController>(sp => sp.GetRequiredService<UiLocaleController>());
    builder.Services.AddSingleton<IUiLocalization, UiLocalization>();
    builder.Services.AddSingleton<IHnDiscussionsSettings>(sp =>
        new SessionHnDiscussionsSettings(sp.GetRequiredService<ISessionStore>()));
    builder.Services.AddSingleton(_ => new HnDiscussionsClient(new HttpClient()));
    builder.Services.AddSingleton<BottomTabPreferenceStore>();
    builder.Services.AddSingleton<OmnisearchWebSearchRouteContextStore>();
    builder.Services.AddSingleton<FriendsRouteContextStore>();
    builder.Services.AddSingleton<INativeBlueskyLinkPersistence, MauiNativeBlueskyLinkPersistence>();
    builder.Services.AddSingleton<INativeOAuthAuthorizationPersistence, MauiNativeOAuthAuthorizationPersistence>();
    builder.Services.AddSingleton<INativeExternalBrowser, MauiNativeExternalBrowser>();
    builder.Services.AddSingleton<NativeBlueskyLinkCoordinator>();
    builder.Services.AddSingleton<NativeOAuthAuthorizationCoordinator>();
    builder.Services.AddSingleton<ISupersededNativeOAuthAuthorizationDiscarder>(sp =>
        sp.GetRequiredService<NativeOAuthAuthorizationCoordinator>());
    builder.Services.AddSingleton<ReferralLinksRouteContextStore>();
    AddAuthFeatureServices(builder.Services);
    builder.Services.AddSingleton<ITurnstileTokenProvider, MauiTurnstileTokenProvider>();
    builder.Services.AddSingleton(CreateNavigationViewerProvider);
    builder.Services.AddSingleton<INavigationViewerProvider>(
        sp => sp.GetRequiredService<MutableNavigationViewerProvider>());
    builder.Services.AddSingleton<AppShell>();
    builder.Services.AddSingleton<INewsFeedService, ApiNewsFeedService>();
    builder.Services.AddSingleton<IRssFeedItemDetailService>(sp =>
        (ApiNewsFeedService)sp.GetRequiredService<INewsFeedService>());
    builder.Services.AddSingleton<IBookmarkService, ApiBookmarkService>();
    builder.Services.AddSingleton<IProfileSafetyService, ApiProfileSafetyService>();
    builder.Services.AddSingleton<IPostsService, ApiPostsService>();
    builder.Services.AddSingleton<ICommentThreadService, ApiPostsService>();
    builder.Services.AddSingleton<IEntityRelationsService, ApiEntityRelationsService>();
    builder.Services.AddSingleton<ImageUploadHttpClient>();
    builder.Services.AddSingleton<IImageUploadService>(sp =>
        new ApiImageUploadService(
            sp.GetRequiredService<VouchaApiClient>(),
            sp.GetRequiredService<ImageUploadHttpClient>()));
    builder.Services.AddSingleton<IDirectMessagesService, ApiDirectMessagesService>();
    builder.Services.AddSingleton<IMembershipAdministrationService, ApiMembershipAdministrationService>();
    AddIdentityVerificationServices(builder.Services);
    builder.Services.AddSingleton<INotificationsService, ApiNotificationsService>();
    AddChatServices(builder.Services);
    AddHouseholdServices(builder.Services);
    AddPaymentCardServices(builder.Services);
    AddPointValuationServices(builder.Services);
    AddSpendingCategoryServices(builder.Services);
    AddRewardsProgramStatusServices(builder.Services);
    builder.Services.AddSingleton<IFriendsService, ApiFriendsService>();
    builder.Services.AddSingleton<IFriendRecommendationsService, ApiFriendRecommendationsService>();
    builder.Services.AddSingleton<ApiSettingsService>();
    builder.Services.AddSingleton<ISettingsService>(sp => sp.GetRequiredService<ApiSettingsService>());
    builder.Services.AddSingleton<INotificationPreferencesService>(sp => sp.GetRequiredService<ApiSettingsService>());
    builder.Services.AddSingleton<INativeSemanticFocus, MauiNativeSemanticFocus>();
    builder.Services.AddSingleton<INotificationAccessibilityPlatform, MauiNotificationAccessibilityPlatform>();
    builder.Services.AddTransient<INotificationAccessibilityRetryScheduler, MauiNotificationAccessibilityRetryScheduler>();
    builder.Services.AddTransient<INotificationSettingsAccessibilityCoordinator, NotificationSettingsAccessibilityCoordinator>();
    builder.Services.AddSingleton<INotificationTimezoneResolver, SystemNotificationTimezoneResolver>();
    builder.Services.AddSingleton<IEmailAddressService, ApiEmailAddressService>();
    builder.Services.AddSingleton<EmailVerificationRecoveryCoordinator>();
    builder.Services.AddSingleton<ICommunitiesService, ApiCommunitiesService>();
    builder.Services.AddSingleton<ApiModerationService>();
    builder.Services.AddSingleton<IModerationService>(sp => sp.GetRequiredService<ApiModerationService>());
    builder.Services.AddSingleton<IModerationExposureService>(
        sp => sp.GetRequiredService<ApiModerationService>());
    AddModerationAppealServices(builder.Services);
    AddModerationDisputeServices(builder.Services);
    AddModerationIntegrityServices(builder.Services);
    AddEngineeringServices(builder.Services);
    builder.Services.AddSingleton<IImportExportService, ApiImportExportService>();
    builder.Services.AddSingleton<IImportExportSharePresenter, MauiImportExportSharePresenter>();
    builder.Services.AddSingleton<IImportExportFilePicker, MauiImportExportFilePicker>();
    builder.Services.AddSingleton<IImportExportFileReader, StrictUtf8ImportExportFileReader>();
    builder.Services.AddSingleton<IImportExportDocumentSharer, MauiImportExportDocumentSharer>();
    builder.Services.AddSingleton<IImportExportFileAdapter, ImportExportFileAdapter>();
    builder.Services.AddTransient(sp => new NewsFeedsViewModel(
        sp.GetRequiredService<INewsFeedService>(),
        NewsFeedKind.News,
        sp.GetRequiredService<ISessionStore>().Current.IsAuthenticated
            ? NewsFeedScope.YourFeed
            : NewsFeedScope.AllNews,
        sp.GetRequiredService<IBookmarkService>(),
        sp.GetRequiredService<IUiLocalization>(),
        sp.GetRequiredService<IUiLocaleController>()));
    builder.Services.AddTransient<PostsListViewModel>();
    builder.Services.AddTransient<PostComposeViewModel>();
    builder.Services.AddTransient<BookmarkCollectionViewModel>();
    builder.Services.AddTransient<ProfileViewModel>();
    builder.Services.AddSingleton<IProfileCollectionsService, ApiProfileCollectionsService>();
    AddTopicAndLandingPageServices(builder.Services);
    builder.Services.AddTransient(sp => new OmnisearchViewModel(
        sp.GetRequiredService<VouchaApiClient>(),
        sp.GetRequiredService<OmnisearchWebSearchRouteContextStore>(),
        sp.GetRequiredService<INavigationViewerProvider>(),
        sp.GetRequiredService<ISessionStore>(),
        localization: sp.GetRequiredService<IUiLocalization>(),
        localeController: sp.GetRequiredService<IUiLocaleController>()));
    builder.Services.AddTransient<FediverseInstancesViewModel>();
    builder.Services.AddTransient<TagManagementViewModel>();
    builder.Services.AddTransient<MembershipGrantViewModel>();
    builder.Services.AddTransient<CommunityBrowseViewModel>();
    builder.Services.AddTransient<ModerationViewModel>();
    builder.Services.AddTransient(sp => new ModerationReportsViewModel(
        sp.GetRequiredService<IModerationService>(),
        sp.GetRequiredService<INavigationViewerProvider>().CurrentViewer,
        sp.GetRequiredService<IUiLocaleController>()));
    builder.Services.AddTransient(sp => new ReviewQueueViewModel(
        sp.GetRequiredService<IModerationService>(),
        sp.GetRequiredService<IModerationExposureService>(),
        sp.GetRequiredService<AppConfig>(),
        sp.GetRequiredService<IUiLocalization>(),
        sp.GetRequiredService<IUiLocaleController>()));
    builder.Services.AddTransient(sp => new FriendsViewModel(
        sp.GetRequiredService<IFriendsService>(),
        sp.GetRequiredService<ISessionStore>().Current.Identity?.Id,
        localization: sp.GetRequiredService<IUiLocalization>(),
        localeController: sp.GetRequiredService<IUiLocaleController>()));
    builder.Services.AddTransient(sp => new FriendRecommendationsViewModel(
        sp.GetRequiredService<IFriendRecommendationsService>(),
        sp.GetRequiredService<IUiLocalization>(),
        sp.GetRequiredService<IUiLocaleController>()));
    builder.Services.AddTransient<ListsViewModel>();
    builder.Services.AddTransient(sp => new DirectMessagesViewModel(
        sp.GetRequiredService<IDirectMessagesService>(),
        sp.GetRequiredService<ISessionStore>().Current.Identity?.Id,
        sp.GetRequiredService<IUiLocalization>(),
        sp.GetRequiredService<IUiLocaleController>()));
    builder.Services.AddTransient<NotificationsViewModel>();
    builder.Services.AddTransient<SettingsViewModel>();
    builder.Services.AddTransient(sp => new NotificationPreferencesViewModel(
        sp.GetRequiredService<INotificationPreferencesService>(),
        sp.GetRequiredService<INotificationTimezoneResolver>(),
        sp.GetRequiredService<IUiLocalization>(),
        sp.GetRequiredService<IUiLocaleController>()));
    builder.Services.AddTransient<CommunityDetailViewModel>();
    AddPageServices(builder.Services);
#if DEBUG
    builder.Logging.AddDebug();
#endif

    return builder.Build();
  }
}
