import VouchaLocalization

// swiftlint:disable file_length
import VouchaModels

enum CommunitySurfaceMode: Equatable {
    case browse
    case create
    case invite(code: String)
    case detail(slug: String, tab: CommunitySurfaceTab, isApplicationFormRoute: Bool, modmailThreadId: String?)

    static func mode(for routeMatch: NativeRouteMatch?) -> CommunitySurfaceMode {
        guard let routeMatch else { return .browse }
        let path = routeMatch.path
        if path == "/communities/create" {
            return .create
        }
        if path.hasPrefix("/communities/invite/") {
            return .invite(code: routeMatch.param("code") ?? path.routeLastSegment ?? "")
        }
        guard let slug = routeMatch.param("slug") else { return .browse }
        return .detail(
            slug: slug,
            tab: CommunitySurfaceTab(path: path),
            isApplicationFormRoute: path.communityRouteSegments == ["apply"],
            modmailThreadId: routeMatch.param("threadId")
        )
    }
}

// swiftlint:disable:next type_body_length
enum CommunitySurfaceTab: String, CaseIterable, Identifiable {
    case posts
    case news
    case members
    case lists
    case listTopics
    case listSources
    case listPosts
    case listDomains
    case listUrls
    case pinnedPosts
    case applications
    case invites
    case settings
    case moderation
    case modlog
    case modmail
    case moderatorVacation
    case bans
    case restrictions
    case moderationAnalytics

    var id: String {
        rawValue
    }

    var titleKey: UiMessageKey {
        switch self {
        case .posts:
            .nativeSwiftNavigationTitlesPosts
        case .news:
            .nativeSwiftNavigationTitlesNews
        case .members:
            .nativeSwiftNavigationTitlesMembers
        case .lists:
            .nativeSwiftNavigationTitlesLists
        case .listTopics:
            .nativeSwiftNavigationTitlesListTopics
        case .listSources:
            .nativeSwiftNavigationTitlesListSources
        case .listPosts:
            .nativeSwiftNavigationTitlesListPosts
        case .listDomains:
            .nativeSwiftNavigationTitlesListDomains
        case .listUrls:
            .nativeSwiftNavigationTitlesListUrls
        case .pinnedPosts:
            .nativeSwiftNavigationTitlesPinned
        case .applications:
            .nativeSwiftNavigationTitlesApplications
        case .invites:
            .nativeSwiftNavigationTitlesInvites
        case .settings:
            .nativeSwiftNavigationTitlesSettings
        case .moderation:
            .nativeSwiftNavigationTitlesModeration
        case .modlog:
            .nativeSwiftNavigationTitlesModlog
        case .modmail:
            .nativeSwiftNavigationTitlesModmail
        case .moderatorVacation:
            .nativeSwiftNavigationTitlesVacation
        case .bans:
            .nativeSwiftNavigationTitlesBans
        case .restrictions:
            .nativeSwiftNavigationTitlesRestrictions
        case .moderationAnalytics:
            .nativeSwiftNavigationTitlesAnalytics
        }
    }

    var listItemType: CommunityListItemType? {
        switch self {
        case .listTopics:
            .topic
        case .listSources:
            .rssFeed
        case .listPosts:
            .post
        case .listDomains:
            .urlHostname
        case .listUrls:
            .url
        default:
            nil
        }
    }

    init(path: String) {
        let segments = path.communityRouteSegments
        self = Self.managementTab(for: segments)
            ?? Self.listTab(for: segments)
            ?? Self.contentTab(for: segments)
    }

    private static func managementTab(for segments: [String]) -> Self? {
        if segments == ["settings"] {
            return .settings
        }
        if segments == ["settings", "moderation"] {
            return .moderation
        }
        if segments.contains("modlog") {
            return .modlog
        }
        if segments.contains("modmail") {
            return .modmail
        }
        if segments.contains("moderator-vacation") {
            return .moderatorVacation
        }
        if segments.contains("bans") {
            return .bans
        }
        if segments.contains("restrictions") {
            return .restrictions
        }
        if segments.contains("moderation-analytics") || segments == ["settings", "moderation", "analytics"] {
            return .moderationAnalytics
        }
        if segments.isCommunityModerationRoute {
            return .moderation
        }
        return nil
    }

    private static func listTab(for segments: [String]) -> Self? {
        if segments == ["lists", "topics"] {
            return .listTopics
        }
        if segments == ["lists", "rss-feeds"] {
            return .listSources
        }
        if segments == ["lists", "posts"] {
            return .listPosts
        }
        if segments == ["lists", "domains"] {
            return .listDomains
        }
        if segments == ["lists", "urls"] {
            return .listUrls
        }
        if segments.contains("lists") {
            return .lists
        }
        return nil
    }

    private static func contentTab(for segments: [String]) -> Self {
        if segments.contains("news") {
            .news
        } else if segments.contains("members") {
            .members
        } else if segments.contains("pinned-posts") {
            .pinnedPosts
        } else if segments.contains("applications") || segments.contains("apply") {
            .applications
        } else if segments.contains("invites") {
            .invites
        } else {
            .posts
        }
    }
}

enum CommunitySurfaceState: Equatable {
    case idle
    case loading
    case loaded
    case requiredTurnstile
    case error(UiMessage)
}

struct CommunityBrowseItem: Identifiable, Equatable {
    let id: String
    let title: String
    let detail: String
    let metrics: UiVerbatimText
    var provenance: PublicContentProvenance?
}
