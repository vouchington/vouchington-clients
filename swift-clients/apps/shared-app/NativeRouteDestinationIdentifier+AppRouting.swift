import VouchaFeatures

extension NativeRouteCatalogEntry {
    var nativeSection: AppSection? {
        destinationIdentifier?.nativeSection
    }
}

extension NativeRouteDestinationIdentifier {
    var nativeSection: AppSection? {
        switch self {
        case .signIn:
            nil
        case .feedPosts, .postsBrowse, .postDetail, .postCompose, .storiesBrowse:
            .posts
        case .feedNews, .rssFeedItemDetail:
            .news
        case .feedPodcasts:
            .podcasts
        case .feedVideos:
            .videos
        case .webSearch, .fediverseSearch, .fediverseInstances, .feedReferralLinks, .usersBrowse, .userProfile,
             .userAdmin:
            .discover
        case .membershipGrants:
            .settings
        case .engineeringQueues, .engineeringPostgresql, .engineeringValkey,
             .engineeringAiCosts, .engineeringDynamicConfig:
            .engineering
        case .growthDashboard:
            .growth
        case .moderationReports, .moderationAppeals, .moderationDisputes, .moderationReviewQueue,
             .moderationAdmin, .moderationIntegrity:
            .moderation
        case .topicsBrowse, .topicImportExport, .topicDetail, .topicManagement, .sourcesBrowse,
             .sourceImportExport, .sourceDetail, .domainsBrowse,
             .domainDetail, .urlsBrowse, .urlDetail, .compare:
            .topics
        case .communitiesBrowse, .communityDetail:
            .communities
        case .messages, .chat:
            .messages
        case .notifications:
            .notifications
        case .friendRecommendations:
            .friends
        case .accountSettings, .profileSettings, .household, .paymentCards, .pointValuations,
             .spendingCategories, .rewardsProgramStatuses, .advancedSettings, .notificationSettings:
            .settings
        case .referrals, .landingPages, .bookmarks, .lists:
            .library
        case .plans:
            .discover
        case .topicRecommendations, .moderationCases, .moderationTransparency:
            .actions
        }
    }

    func verticalSubsection(for routeMatch: NativeRouteMatch? = nil) -> VerticalSubsection? {
        switch self {
        case .signIn:
            nil
        case .feedPosts, .postsBrowse, .postDetail, .postCompose, .storiesBrowse:
            postsFeedSubsection(for: routeMatch?.path)
        case .feedNews, .rssFeedItemDetail:
            feedSubsection(for: routeMatch?.path, prefix: "/feed/news/")
        case .feedPodcasts:
            feedSubsection(for: routeMatch?.path, prefix: "/feed/podcasts/")
        case .feedVideos:
            feedSubsection(for: routeMatch?.path, prefix: "/feed/videos/")
        case .sourcesBrowse, .sourceDetail:
            sourceSubsection(for: routeMatch?.path)
        default:
            nil
        }
    }

    var verticalSubsection: VerticalSubsection? {
        verticalSubsection()
    }

    func requiresAuthentication(for routeMatch: NativeRouteMatch? = nil) -> Bool {
        if requiresAuthenticatedSession {
            return true
        }
        if routeMatch?.path.contains("/tags/") == true {
            return true
        }
        if routeMatch?.path == "/feed" || routeMatch?.path.hasPrefix("/feed/") == true {
            return true
        }
        if self == .topicManagement {
            return true
        }
        if self == .urlsBrowse || self == .urlDetail || self == .usersBrowse || self == .postCompose {
            return true
        }
        return switch verticalSubsection(for: routeMatch) {
        case .feed(.your), .sources(.your):
            true
        default:
            nativeSection?.requiresAuth ?? false
        }
    }

    private func postsFeedSubsection(for path: String?) -> VerticalSubsection {
        switch path {
        case "/feed/posts/friends", "/feed/posts/topics":
            .feed(.your)
        default:
            .feed(.all)
        }
    }

    private func feedSubsection(for path: String?, prefix: String) -> VerticalSubsection {
        if path?.hasPrefix(prefix) == true {
            .feed(.your)
        } else {
            .feed(.all)
        }
    }

    private func sourceSubsection(for path: String?) -> VerticalSubsection {
        if path?.hasPrefix("/my/") == true {
            .sources(.your)
        } else {
            .sources(.all)
        }
    }
}

extension AppSection {
    func allowsNativeDestination(
        _ destination: NativeRouteDestinationIdentifier,
        isSignedIn: Bool,
        userRoles: [String],
        featureFlags: [String: Bool] = [:]
    ) -> Bool {
        let groups = nativeParityGroups(featureFlags: featureFlags)
        guard groups.contains(where: { group in
            group.entries.contains { $0.destinationIdentifier == destination }
        }) else {
            return !destination.isFediverseDestination
        }
        return nativeParityGroups(
            isSignedIn: isSignedIn,
            userRoles: userRoles,
            featureFlags: featureFlags
        ).contains { group in
            group.entries.contains { $0.destinationIdentifier == destination }
        }
    }
}
