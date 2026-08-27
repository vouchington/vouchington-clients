import VouchaLocalization

public enum AppSection: String, CaseIterable, Identifiable, Sendable {
    case news, videos, podcasts, posts
    case discover, topics, communities, messages, settings, library, actions
    case moderation, crm, engineering, growth
    case notifications, friends, profile

    public var id: String {
        rawValue
    }

    public var titleKey: UiMessageKey {
        switch self {
        case .news: .nativeSwiftNavigationTitlesNews
        case .videos: .nativeSwiftNavigationTitlesVideos
        case .podcasts: .nativeSwiftNavigationTitlesPodcasts
        case .posts: .nativeSwiftNavigationTitlesPosts
        case .discover: .nativeSwiftNavigationTitlesDiscover
        case .topics: .nativeSwiftNavigationTitlesTopics
        case .communities: .nativeSwiftNavigationTitlesCommunities
        case .messages: .nativeSwiftNavigationTitlesMessages
        case .settings: .nativeSwiftNavigationTitlesSettings
        case .library: .nativeSwiftNavigationTitlesLibrary
        case .actions: .nativeSwiftNavigationTitlesActions
        case .moderation: .nativeSwiftNavigationTitlesModeration
        case .crm: .nativeSwiftNavigationTitlesCrm
        case .engineering: .nativeSwiftNavigationTitlesEngineering
        case .growth: .nativeSwiftNavigationTitlesGrowth
        case .notifications: .nativeSwiftNavigationTitlesNotifications
        case .friends: .nativeSwiftNavigationTitlesFriends
        case .profile: .nativeSwiftNavigationTitlesProfile
        }
    }

    @MainActor
    public func title(using localeController: UiLocaleController) -> String {
        localeController.string(titleKey)
    }

    public var systemImage: String {
        switch self {
        case .news: "newspaper"
        case .videos: "play.rectangle"
        case .podcasts: "headphones"
        case .posts: "bubble.left.and.bubble.right"
        case .discover: "magnifyingglass"
        case .topics: "tag"
        case .communities: "person.3"
        case .messages: "message"
        case .settings: "gearshape"
        case .library: "bookmark"
        case .actions: "slider.horizontal.3"
        case .moderation: "shield.lefthalf.filled"
        case .crm: "person.text.rectangle"
        case .engineering: "wrench.and.screwdriver"
        case .growth: "chart.line.uptrend.xyaxis"
        case .notifications: "bell"
        case .friends: "person.2"
        case .profile: "person.circle"
        }
    }

    public var requiresAuth: Bool {
        switch self {
        case .messages, .settings, .library, .actions, .moderation, .crm, .engineering, .growth, .notifications,
             .friends, .profile: true
        default: false
        }
    }

    public var requiredRoles: [String] {
        switch self {
        case .crm:
            ["administrator"]
        case .engineering:
            ["administrator", "moderator", "developer", "customer_support", "investor"]
        case .growth:
            ["administrator", "investor"]
        default:
            []
        }
    }

    public func isVisible(isSignedIn: Bool, userRoles: [String]) -> Bool {
        if requiresAuth, !isSignedIn {
            return false
        }
        let roles = requiredRoles
        if roles.isEmpty {
            return true
        }
        return roles.contains { userRoles.contains($0) }
    }
}
