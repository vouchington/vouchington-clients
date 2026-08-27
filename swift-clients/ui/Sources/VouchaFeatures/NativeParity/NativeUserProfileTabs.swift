import VouchaLocalization
import VouchaModels

extension NativeUserProfileSurface {
    struct TabItem {
        let title: UiMessageKey
        let path: String
        let selected: Bool
    }

    struct PrimaryTabCandidate {
        let title: UiMessageKey
        let path: String
        let count: Int?
    }

    func visiblePrimaryTabs(_ profile: UserProfileResponse) -> [TabItem] {
        let metrics = profile.userMetrics
        let postsCount = ["reviews", "discussions", "comments"]
            .reduce(0) { $0 + (metrics?.visibleCount($1) ?? 0) }
        let friendsCount = ["users_following", "users_followers"]
            .reduce(0) { $0 + (metrics?.visibleCount($1) ?? 0) }
        let candidates: [PrimaryTabCandidate] = [
            .init(title: .nativeSwiftProfileOverview, path: basePath, count: nil),
            .init(title: .nativeSwiftNavigationTitlesPosts, path: "\(basePath)/posts", count: postsCount),
            .init(
                title: .nativeSwiftNavigationTitlesTopics,
                path: "\(basePath)/topics/following",
                count: metrics?.visibleCount("topics_following")
            ),
            .init(title: .nativeSwiftNavigationTitlesFriends, path: friendsPrimaryPath(metrics), count: friendsCount),
            .init(
                title: .nativeSwiftRouteMetadataMainSourcesBrowseSourcesTitle,
                path: "\(basePath)/rss-feeds/following",
                count: metrics?.visibleCount("rss_feeds_following")
            ),
            .init(
                title: .nativeSwiftNavigationTitlesCommunities,
                path: "\(basePath)/communities/member",
                count: metrics?.visibleCount("communities_member")
            )
        ]
        return candidates.compactMap { candidate in
            let selected = currentScope?.primaryTitle == candidate.title
            let hasVisibleItems = candidate.count.map { $0 > 0 } ?? true
            guard hasVisibleItems || selected else { return nil }
            return TabItem(title: candidate.title, path: candidate.path, selected: selected)
        }
    }

    private func friendsPrimaryPath(_ metrics: UserProfileMetrics?) -> String {
        if metrics?.visibleCount("users_following") ?? 0 > 0 {
            return "\(basePath)/users/following"
        }
        if metrics?.visibleCount("users_followers") ?? 0 > 0 {
            return "\(basePath)/users/followers"
        }
        return currentScope == .usersFollowers
            ? "\(basePath)/users/followers"
            : "\(basePath)/users/following"
    }

    func contextualTabs(for scope: NativeUserProfileScope, profile: UserProfileResponse?) -> [TabItem] {
        let metrics = profile?.userMetrics
        return switch scope {
        case let .posts(filter):
            postTabs(filter: filter, metrics: metrics)
        case .usersFollowing, .usersFollowers:
            friendTabs(scope: scope, metrics: metrics)
        case let .sourcesFollowing(filter):
            [sourceTab(.nativeSwiftNavigationTitlesAll, filter: nil, selected: filter)]
                + NativeUserProfileScope.SourceFilter.allCases.map { sourceFilter in
                    sourceTab(sourceFilter.titleKey, filter: sourceFilter, selected: filter)
                }
        default:
            []
        }
    }

    private func postTabs(
        filter: NativeUserProfileScope.PostFilter,
        metrics: UserProfileMetrics?
    ) -> [TabItem] {
        [
            postTab(
                .nativeSwiftNavigationTitlesAll,
                path: "posts",
                filter: .all,
                selectedFilter: filter,
                count: postCount(metrics)
            ),
            postTab(
                .nativeSwiftNavigationTitlesReviews,
                path: "reviews",
                filter: .reviews,
                selectedFilter: filter,
                count: metrics?.visibleCount("reviews") ?? 0
            ),
            postTab(
                .nativeSwiftNavigationTitlesDiscussions,
                path: "discussions",
                filter: .discussions,
                selectedFilter: filter,
                count: metrics?.visibleCount("discussions") ?? 0
            ),
            postTab(
                .nativeSwiftCommentThreadComments,
                path: "comments",
                filter: .comments,
                selectedFilter: filter,
                count: metrics?.visibleCount("comments") ?? 0
            )
        ].compactMap { $0 }
    }

    private func friendTabs(
        scope: NativeUserProfileScope,
        metrics: UserProfileMetrics?
    ) -> [TabItem] {
        [
            friendTab(
                .nativeSwiftNavigationTitlesFollowing,
                path: "users/following",
                scope: .usersFollowing,
                current: scope,
                count: metrics?.visibleCount("users_following") ?? 0
            ),
            friendTab(
                .nativeSwiftNavigationTitlesFollowers,
                path: "users/followers",
                scope: .usersFollowers,
                current: scope,
                count: metrics?.visibleCount("users_followers") ?? 0
            )
        ].compactMap { $0 }
    }

    private func postTab(
        _ title: UiMessageKey,
        path: String,
        filter: NativeUserProfileScope.PostFilter,
        selectedFilter: NativeUserProfileScope.PostFilter,
        count: Int
    ) -> TabItem? {
        let selected = filter == selectedFilter
        guard count > 0 || selected else { return nil }
        return .init(title: title, path: "\(basePath)/\(path)", selected: selected)
    }

    private func friendTab(
        _ title: UiMessageKey,
        path: String,
        scope: NativeUserProfileScope,
        current: NativeUserProfileScope,
        count: Int
    ) -> TabItem? {
        let selected = scope == current
        guard count > 0 || selected else { return nil }
        return .init(title: title, path: "\(basePath)/\(path)", selected: selected)
    }

    private func postCount(_ metrics: UserProfileMetrics?) -> Int {
        ["reviews", "discussions", "comments"].reduce(0) { $0 + (metrics?.visibleCount($1) ?? 0) }
    }

    private func sourceTab(
        _ title: UiMessageKey,
        filter: NativeUserProfileScope.SourceFilter?,
        selected: NativeUserProfileScope.SourceFilter?
    ) -> TabItem {
        let query = filter.map { "?feed_type=\($0.rawValue)" } ?? ""
        return .init(
            title: title,
            path: "\(basePath)/rss-feeds/following\(query)",
            selected: filter == selected
        )
    }
}

private extension NativeUserProfileScope.SourceFilter {
    var titleKey: UiMessageKey {
        switch self {
        case .article: .nativeSwiftNavigationTitlesNews
        case .podcast: .nativeSwiftNavigationTitlesPodcasts
        case .video: .nativeSwiftNavigationTitlesVideos
        }
    }
}
