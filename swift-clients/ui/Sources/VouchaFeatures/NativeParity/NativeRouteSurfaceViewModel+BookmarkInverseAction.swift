import VouchaLocalization

private struct BookmarkInverseActionSpecification {
    let predicate: String
    let label: UiMessageKey
}

private let bookmarkInverseActionSpecifications: [String: BookmarkInverseActionSpecification] = [
    "saved": .init(predicate: "save", label: .nativeSwiftHouseholdsBookmarksUnsave),
    "hidden": .init(predicate: "hide", label: .nativeSwiftHouseholdsBookmarksUnhide),
    "following": .init(predicate: "follow", label: .nativeSwiftHouseholdsBookmarksUnfollow),
    "muted": .init(predicate: "mute", label: .nativeSwiftHouseholdsBookmarksUnmute),
    "blocked": .init(predicate: "block", label: .nativeSwiftHouseholdsBookmarksUnblock),
    "subscribed": .init(predicate: "subscribe", label: .nativeSwiftHouseholdsBookmarksUnsubscribe),
    "subscribed-posts": .init(predicate: "subscribe", label: .nativeSwiftHouseholdsBookmarksUnsubscribe),
    "dismissed": .init(
        predicate: "dismiss_recommendation",
        label: .nativeSwiftHouseholdsBookmarksRestore
    ),
    "dismissed-recommendations": .init(
        predicate: "dismiss_recommendation",
        label: .nativeSwiftHouseholdsBookmarksRestore
    ),
    "proxy-following": .init(
        predicate: "proxy_follow",
        label: .nativeSwiftHouseholdsBookmarksRemoveProxyFollow
    ),
    "proxy-muted": .init(
        predicate: "proxy_mute",
        label: .nativeSwiftHouseholdsBookmarksRemoveProxyMute
    )
]

extension NativeRouteSurfaceViewModel {
    func inverseAction(entityType: String, listType: String) -> NativeBookmarkRow.InverseAction? {
        if listType == "viewed" || listType == "followers" {
            return nil
        }
        guard let specification = bookmarkInverseActionSpecifications[listType] else { return nil }
        return .init(
            entityType: entityType,
            predicate: specification.predicate,
            label: specification.label
        )
    }
}
