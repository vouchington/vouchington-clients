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
            ("urls_saved", .nativeSwiftRouteSurfaceSavedUrls)
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
