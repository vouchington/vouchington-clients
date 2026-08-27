import Foundation
import VouchaLocalization

enum NativeUserProfileScope: Equatable {
    enum PostFilter: CaseIterable {
        case all, reviews, discussions, comments

        var queryValue: String {
            switch self {
            case .all: "review,discussion,comment"
            case .reviews: "review"
            case .discussions: "discussion"
            case .comments: "comment"
            }
        }

    }

    enum SourceFilter: String, CaseIterable {
        case article, podcast, video
    }

    case overview
    case posts(PostFilter)
    case topicsFollowing
    case usersFollowing
    case usersFollowers
    case sourcesFollowing(SourceFilter?)
    case communitiesMember

    init?(match: NativeRouteMatch) {
        let suffix = match.path.split(separator: "/").dropFirst(2).joined(separator: "/")
        if suffix == "rss-feeds/following" {
            guard let rawFilter = match.queryValue("feed_type") else {
                self = .sourcesFollowing(nil)
                return
            }
            guard let filter = SourceFilter(rawValue: rawFilter) else { return nil }
            self = .sourcesFollowing(filter)
            return
        }
        guard let scope = Self.scopeBySuffix[suffix] else { return nil }
        self = scope
    }

    private static let scopeBySuffix: [String: NativeUserProfileScope] = [
        "": .overview,
        "posts": .posts(.all),
        "reviews": .posts(.reviews),
        "discussions": .posts(.discussions),
        "comments": .posts(.comments),
        "topics/following": .topicsFollowing,
        "users/following": .usersFollowing,
        "users/followers": .usersFollowers,
        "communities/member": .communitiesMember
    ]

    var primaryTitle: UiMessageKey {
        switch self {
        case .overview: .nativeSwiftProfileOverview
        case .posts: .nativeSwiftNavigationTitlesPosts
        case .topicsFollowing: .nativeSwiftNavigationTitlesTopics
        case .usersFollowing, .usersFollowers: .nativeSwiftNavigationTitlesFriends
        case .sourcesFollowing: .nativeSwiftRouteMetadataMainSourcesBrowseSourcesTitle
        case .communitiesMember: .nativeSwiftNavigationTitlesCommunities
        }
    }

    var emptyTitle: UiMessageKey {
        switch self {
        case .overview: .nativeSwiftProfileNoOverviewDetails
        case .posts: .nativeSwiftEmptyStateNoPosts
        case .topicsFollowing: .nativeSwiftProfileNoFollowedTopics
        case .usersFollowing: .nativeSwiftFriendsNoFollowing
        case .usersFollowers: .nativeSwiftFriendsNoFollowers
        case .sourcesFollowing: .nativeSwiftEmptyStateNoSources
        case .communitiesMember: .nativeSwiftProfileNoCommunities
        }
    }
}
