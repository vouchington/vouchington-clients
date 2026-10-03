import VouchaLocalization

enum NativeRouteRemoteSurfaceKind: Equatable {
    case none
    case search
    case feed
    case rssFeeds
    case sources
    case discovery
    case library
    case messages
    case notifications
    case moderation
    case account
    case entities
}

public enum NativeRouteSurfaceAction: String, Identifiable, Hashable, Sendable {
    case compareTopics
    case compareHostnames

    public var id: String {
        rawValue
    }

    public var icon: String {
        switch self {
        case .compareTopics:
            "tag"
        case .compareHostnames:
            "globe"
        }
    }

    public var title: UiMessage {
        switch self {
        case .compareTopics:
            UiMessage(.nativeSwiftRouteSurfaceCompareTopTopics)
        case .compareHostnames:
            UiMessage(.nativeSwiftRouteSurfaceCompareTopDomains)
        }
    }

    public var detail: UiMessage {
        switch self {
        case .compareTopics:
            UiMessage(.nativeSwiftRouteSurfaceLoadCurrentTopicComparison)
        case .compareHostnames:
            UiMessage(.nativeSwiftRouteSurfaceLoadCurrentHostnameComparison)
        }
    }
}

extension NativeRouteDestinationIdentifier {
    var supportsRemoteNativeSurface: Bool {
        remoteSurfaceKind != .none
    }

    var remoteSurfaceKind: NativeRouteRemoteSurfaceKind {
        switch self {
        case .webSearch, .fediverseSearch:
            .search
        case .feedPosts, .postsBrowse, .storiesBrowse, .postDetail:
            .feed
        case .feedNews, .feedPodcasts, .feedVideos:
            .rssFeeds
        case .sourcesBrowse, .sourceDetail:
            .sources
        case .communitiesBrowse, .feedReferralLinks, .topicRecommendations, .fediverseInstances:
            .discovery
        case .topicsBrowse, .topicDetail, .domainsBrowse, .domainDetail, .urlsBrowse,
             .urlDetail, .usersBrowse, .userProfile, .communityDetail, .compare:
            .entities
        case .referrals, .plans, .bookmarks, .landingPages, .lists:
            .library
        case .messages, .chat:
            .messages
        case .notifications:
            .notifications
        case .moderationReports, .moderationAppeals, .moderationDisputes,
             .moderationReviewQueue, .moderationAdmin, .moderationIntegrity,
             .moderationCases, .moderationTransparency:
            .moderation
        case .accountSettings, .profileSettings, .advancedSettings, .notificationSettings:
            .account
        case .household, .paymentCards, .pointValuations, .spendingCategories, .rewardsProgramStatuses,
             .friendRecommendations:
            .none
        default:
            .none
        }
    }

    var nativeActions: [NativeRouteSurfaceAction] {
        switch self {
        case .compare:
            [.compareTopics, .compareHostnames]
        default:
            []
        }
    }
}
