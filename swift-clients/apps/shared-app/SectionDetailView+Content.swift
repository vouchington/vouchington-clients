import SwiftUI
import VouchaFeatures
import VouchaLocalization

extension SectionDetailView {
    var canCastPublicVotes: Bool {
        guard factory.sessionManager.isSignedIn else { return false }
        return !factory.sessionManager.currentUserIsOfficialAccount
    }

    @ViewBuilder
    func verticalContent(subsection: VerticalSubsection) -> some View {
        switch section {
        case .news: newsContent(subsection: subsection)
        case .podcasts: podcastsContent(subsection: subsection)
        case .videos: videosContent(subsection: subsection)
        case .posts: postsContent(subsection: subsection)
        default: EmptyView()
        }
    }

    @ViewBuilder
    func newsContent(subsection: VerticalSubsection) -> some View {
        switch subsection {
        case .feed(.your): RSSFeedListView(
                viewModel: newsFeedYourVM,
                playbackController: playbackController,
                isSignedIn: factory.sessionManager.isSignedIn,
                canVote: canCastPublicVotes,
                currentUserId: factory.sessionManager.currentUserId,
                showSignIn: showSignIn
            )
        case .feed(.all): RSSFeedListView(
                viewModel: newsFeedAllVM,
                playbackController: playbackController,
                isSignedIn: factory.sessionManager.isSignedIn,
                canVote: canCastPublicVotes,
                currentUserId: factory.sessionManager.currentUserId,
                showSignIn: showSignIn
            )
        case let .sources(scope):
            SourcesListView(
                viewModel: newsSourcesVM,
                scope: scope,
                navigationTitle: UiMessages.string(
                    AppSection.news.subsectionTitle(.sources(scope)),
                    locale: nativeUiLocale
                ),
                isSignedIn: factory.sessionManager.isSignedIn,
                canVote: canCastPublicVotes,
                showSignIn: showSignIn
            )
        }
    }

    @ViewBuilder
    func podcastsContent(subsection: VerticalSubsection) -> some View {
        switch subsection {
        case .feed(.your): RSSFeedListView(
                viewModel: podcastFeedYourVM,
                playbackController: playbackController,
                isSignedIn: factory.sessionManager.isSignedIn,
                canVote: canCastPublicVotes,
                currentUserId: factory.sessionManager.currentUserId,
                showSignIn: showSignIn
            )
        case .feed(.all): RSSFeedListView(
                viewModel: podcastFeedAllVM,
                playbackController: playbackController,
                isSignedIn: factory.sessionManager.isSignedIn,
                canVote: canCastPublicVotes,
                currentUserId: factory.sessionManager.currentUserId,
                showSignIn: showSignIn
            )
        case let .sources(scope):
            SourcesListView(
                viewModel: podcastSourcesVM,
                scope: scope,
                navigationTitle: UiMessages.string(
                    AppSection.podcasts.subsectionTitle(.sources(scope)),
                    locale: nativeUiLocale
                ),
                isSignedIn: factory.sessionManager.isSignedIn,
                canVote: canCastPublicVotes,
                showSignIn: showSignIn
            )
        }
    }

    @ViewBuilder
    func videosContent(subsection: VerticalSubsection) -> some View {
        switch subsection {
        case .feed(.your): RSSFeedListView(
                viewModel: videoFeedYourVM,
                playbackController: playbackController,
                isSignedIn: factory.sessionManager.isSignedIn,
                canVote: canCastPublicVotes,
                currentUserId: factory.sessionManager.currentUserId,
                showSignIn: showSignIn
            )
        case .feed(.all): RSSFeedListView(
                viewModel: videoFeedAllVM,
                playbackController: playbackController,
                isSignedIn: factory.sessionManager.isSignedIn,
                canVote: canCastPublicVotes,
                currentUserId: factory.sessionManager.currentUserId,
                showSignIn: showSignIn
            )
        case let .sources(scope):
            SourcesListView(
                viewModel: videoSourcesVM,
                scope: scope,
                navigationTitle: UiMessages.string(
                    AppSection.videos.subsectionTitle(.sources(scope)),
                    locale: nativeUiLocale
                ),
                isSignedIn: factory.sessionManager.isSignedIn,
                canVote: canCastPublicVotes,
                showSignIn: showSignIn
            )
        }
    }

    @ViewBuilder
    func postsContent(subsection: VerticalSubsection) -> some View {
        switch subsection {
        case .feed(.your): PostsListView(
                viewModel: postsYourVM,
                isSignedIn: factory.sessionManager.isSignedIn,
                canVote: canCastPublicVotes,
                currentUserId: factory.sessionManager.currentUserId,
                hideDownCount: hideDownCount,
                showSignIn: showSignIn
            )
        case .feed(.all): PostsListView(
                viewModel: postsAllVM,
                isSignedIn: factory.sessionManager.isSignedIn,
                canVote: canCastPublicVotes,
                currentUserId: factory.sessionManager.currentUserId,
                hideDownCount: hideDownCount,
                showSignIn: showSignIn
            )
        case .sources: EmptyView()
        }
    }

    @ViewBuilder
    var nonVerticalContent: some View {
        let userRoles = factory.sessionManager.currentUserRoles
        switch section {
        case .discover, .topics, .communities, .messages, .settings, .library, .actions, .moderation, .crm,
             .engineering,
             .growth:
            NativeFeatureFlagAwareDirectoryView(
                section: section,
                client: factory.apiClient,
                isSignedIn: factory.sessionManager.isSignedIn,
                userRoles: userRoles,
                canCastPublicVotes: canCastPublicVotes,
                featureFlags: factory.featureFlagState,
                onNavigateToTargetPath: onNavigateToTargetPath,
                showSignIn: showSignIn
            )
        case .notifications: NotificationsListView(
                viewModel: notificationsVM,
                onNavigateToTargetPath: onNavigateToTargetPath
            )
        case .friends: FriendsListView(viewModel: friendsVM)
        case .profile: ProfileView(viewModel: profileVM)
        default: EmptyView()
        }
    }

    var hideDownCount: Bool {
        let session = factory.sessionManager
        return !session.currentUserRoles.contains("administrator") && session.currentMembershipPlan == nil
    }
}
