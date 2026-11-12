import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension NativeUserProfileSurface {
    struct PrivateCollectionCount: Identifiable {
        let id: String
        let title: UiMessageKey
        let count: Int
        let capped: Bool
    }

    func privateCollectionCounts(_ profile: UserProfileResponse) -> [PrivateCollectionCount] {
        guard isSignedIn, currentUserId == profile.user.id, currentScope == .overview,
              let metrics = profile.userMetrics, let counts = metrics.privateCount else { return [] }
        let collections: [(String, UiMessageKey)] = [
            ("posts_saved", .nativeSwiftRouteSurfaceSavedPosts),
            ("communities_saved", .nativeSwiftRouteSurfaceSavedCommunities),
            ("rss_feed_items_saved", .nativeSwiftRouteSurfaceSavedFeedItems),
            ("urls_saved", .nativeSwiftRouteSurfaceSavedUrls),
            ("posts_hidden", .nativeDotnetBookmarksHiddenPosts),
            ("posts_following", .nativeDotnetBookmarksFollowedPosts),
            ("topics_blocked", .nativeDotnetBookmarksBlockedTopics),
            ("topics_muted", .nativeDotnetBookmarksMutedTopics),
            ("topics_viewed", .nativeDotnetBookmarksViewedTopics),
            ("topics_dismissed_recommendations", .nativeDotnetBookmarksDismissedTopicRecommendations),
            ("users_blocked", .nativeDotnetBookmarksBlockedUsers),
            ("users_muted", .nativeDotnetBookmarksMutedUsers),
            ("users_subscribed_posts", .nativeDotnetBookmarksSubscribedToPosts),
            ("users_dismissed_recommendations", .nativeDotnetBookmarksDismissedFriendRecommendations),
            ("rss_feed_items_hidden", .extractedUsersUserRssFeedItemRoutesHiddenRssItems28d66515),
            ("domains_blocked", .nativeDotnetBookmarksBlockedDomains),
            ("domains_muted", .nativeDotnetBookmarksMutedDomains),
            ("communities_proxy_following", .nativeDotnetBookmarksProxyFollowedCommunities),
            ("communities_proxy_muted", .nativeDotnetBookmarksProxyMutedCommunities)
        ]
        return collections.compactMap { key, title in
            counts[key].map { count in
                PrivateCollectionCount(
                    id: key, title: title, count: count,
                    capped: metrics.privateCountCapped?[key] == true
                )
            }
        }
    }

    func privateCollectionCountsView(_ profile: UserProfileResponse) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            ForEach(privateCollectionCounts(profile)) { collection in
                HStack {
                    Text(UiMessages.string(collection.title, locale: nativeUiLocale))
                    Spacer()
                    Text(verbatim: UiMessages.cappedNumber(
                        Double(collection.count), capped: collection.capped, locale: nativeUiLocale
                    ))
                }
                .accessibilityIdentifier("private-collection-count-\(collection.id)")
            }
        }
        .font(Typography.caption)
        .foregroundStyle(Colors.secondaryLabel)
    }
}
