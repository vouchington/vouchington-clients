import SwiftUI
import VouchaDesignSystem

extension NativeRouteDestinationSurface {
    @ViewBuilder
    var destinationContent: some View {
        if viewModel.focusedRssFeedItemId != nil {
            NativeFocusedRssFeedItemSurface(
                viewModel: viewModel,
                playbackController: playbackController,
                currentUserId: currentUserId
            )
        } else {
            switch entry.destinationIdentifier {
            case .signIn:
                NativeListSurface(viewModel: viewModel, onNavigateToTargetPath: onNavigateToTargetPath)
            case .landingPages:
                if isOwnerLandingPageManagementRoute {
                    EmptyView()
                } else {
                    NativeListSurface(viewModel: viewModel)
                }
            case .webSearch, .fediverseSearch:
                NativeSearchSurface(
                    viewModel: viewModel,
                    initialQuery: routeSearchQuery,
                    onNavigateToTargetPath: onNavigateToTargetPath
                )
                .id("\(routeMatch?.path ?? "")?\(routeQuery ?? "")")
            case .referrals:
                NativeReferralLinksManagementView(client: viewModel.client, mode: .fromRoutePath(routeMatch?.path))
            case .growthDashboard:
                GrowthDashboardSurface(client: viewModel.client, initialRange: growthRange)
            case .engineeringQueues, .engineeringPostgresql, .engineeringValkey, .engineeringAiCosts,
                 .engineeringDynamicConfig, .topicImportExport, .sourceImportExport:
                EmptyView()
            case .moderationReviewQueue:
                NativeReviewQueueSurface(viewModel: reviewQueueViewModel)
            case .membershipGrants, .userAdmin:
                EmptyView()
            case .moderationReports:
                ModerationReportsSurface(
                    client: moderationReportsClient,
                    isSignedIn: isSignedIn,
                    viewerTier: moderationReportsViewerTier,
                    onNavigate: onNavigateToTargetPath,
                    showSignIn: showSignIn
                )
                .id(moderationReportsSurfaceIdentity)
            case .feedPosts, .feedNews, .feedPodcasts, .feedVideos, .feedReferralLinks,
                 .fediverseInstances, .postsBrowse, .storiesBrowse, .topicsBrowse, .sourcesBrowse, .domainsBrowse,
                 .urlsBrowse, .usersBrowse, .notifications,
                 .lists, .bookmarks, .plans, .moderationCases, .moderationTransparency, .compare,
                 .moderationAppeals,
                 .moderationDisputes, .moderationAdmin:
                NativeListSurface(viewModel: viewModel, onNavigateToTargetPath: onNavigateToTargetPath)
            case .moderationIntegrity:
                EmptyView()
            case .communitiesBrowse:
                communitySurface
            case .messages:
                messagesDestinationContent
            case .chat:
                EmptyView()
            case .topicRecommendations:
                topicRecommendationContent
            case .postDetail:
                postDetailContent
            case .communityDetail:
                communitySurface
            case .userProfile:
                userProfileContent
            case .rssFeedItemDetail, .topicDetail, .sourceDetail, .domainDetail, .urlDetail:
                detailDestinationContent
            case .postCompose:
                postComposeContent
            case .topicManagement:
                NativeTopicManagementSurface(client: viewModel.client, routeMatch: viewModel.routeMatch)
            case .accountSettings, .profileSettings, .advancedSettings, .notificationSettings:
                NativeSettingsSurface(
                    viewModel: viewModel,
                    nativeOAuthAuthorizationCoordinator: nativeOAuthAuthorizationCoordinator,
                    fediverseEnabled: featureFlags?.effective["fediverse"] == true,
                    focusedSection: entry.destinationIdentifier == .notificationSettings ? .notifications : nil,
                    a11yActivator: notificationA11yActivator,
                    notificationActivationToken: notificationActivationToken,
                    onNavigateToTargetPath: onNavigateToTargetPath,
                    onLogoutRequired: showSignIn
                )
            case .household, .paymentCards, .pointValuations, .spendingCategories, .rewardsProgramStatuses,
                 .friendRecommendations:
                EmptyView()
            case nil:
                EmptyStateView(
                    icon: "square.dashed",
                    title: .message(.nativeSwiftEmptyStateNoNativeDestination),
                    message: .message(.nativeSwiftEmptyStateNoNativeDestinationMessage)
                )
            }
        }
    }
}
