using Voucha.Client.App.Pages;

namespace Voucha.Client.App;

public static partial class MauiProgram
{
  private static void AddPageServices(IServiceCollection services)
  {
    AddUsersBrowseServices(services);
    services.AddTransient<UsersBrowsePage>();
    services.AddTransient<AuthPage>();
    services.AddTransient<NewsFeedsPage>();
    services.AddTransient<PodcastsPage>();
    services.AddTransient<VideosPage>();
    services.AddTransient<PostsPage>();
    services.AddTransient<PostComposePage>();
    services.AddTransient<BookmarkCollectionPage>();
    services.AddTransient<ProfilePage>();
    services.AddTransient<OmnisearchPage>();
    services.AddTransient<FediverseInstancesPage>();
    services.AddTransient<TopicsPage>();
    services.AddTransient<TopicManagementPage>();
    services.AddTransient<TagManagementPage>();
    services.AddTransient<ReferralLinksPage>();
    services.AddTransient<ModerationPage>();
    services.AddTransient<ModerationReportsPage>();
    services.AddTransient<ReviewQueuePage>();
    services.AddTransient<LandingPagesPage>();
    services.AddTransient<GrowthDashboardPage>();
    services.AddTransient<LandingPageAnalyticsPage>();
    services.AddTransient<FriendsPage>();
    services.AddTransient<FriendRecommendationsPage>();
    services.AddTransient<ListsPage>();
    services.AddTransient<CommunityBrowsePage>();
    services.AddTransient<CommunityOverviewPage>();
    services.AddTransient<CommunityPostsPage>();
    services.AddTransient<CommunityNewsPage>();
    services.AddTransient<CommunityListsPage>();
    services.AddTransient<CommunityMembersPage>();
    services.AddTransient<CommunitySettingsPage>();
    services.AddTransient<CommunityPinnedPostsPage>();
    services.AddTransient<CommunityApplicationsPage>();
    services.AddTransient<CommunityInvitesPage>();
    services.AddTransient<CommunityModerationPage>();
    services.AddTransient<CommunityBansPage>();
    services.AddTransient<CommunityRestrictionsPage>();
    services.AddTransient<CommunityModeratorVacationPage>();
    services.AddTransient<CommunityAiAgentsPage>();
    services.AddTransient<CommunityAgentPromptsPage>();
    services.AddTransient<CommunityModlogPage>();
    services.AddTransient<CommunityModmailPage>();
    services.AddTransient<CommunityModerationAnalyticsPage>();
    services.AddTransient<CommunityDetailPage>();
    services.AddTransient<EngineeringPage>();
    services.AddTransient<EngineeringQueuesPage>();
    services.AddTransient<EngineeringPostgreSqlPage>();
    services.AddTransient<EngineeringValkeyPage>();
    services.AddTransient<DynamicConfigPage>();
    services.AddTransient<FeatureFlagOverridesPage>();
    services.AddTransient<DirectMessagesPage>();
    services.AddTransient<NotificationsPage>();
    services.AddTransient<SettingsPage>();
    services.AddTransient<BottomTabCustomizePage>();
    services.AddTransient<MembershipGrantPage>();
    services.AddTransient<IdentityVerificationAttemptGrantPage>();
  }
}
