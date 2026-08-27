import Foundation

extension Notification.Name {
    static let vouchaMembershipEntitlementDidChange = Notification.Name("vouchaMembershipEntitlementDidChange")
}

extension NativeRouteSurfaceViewModel {
    func reloadAfterMembershipEntitlementChange() async {
        guard routeMatch?.path == "/moderation-transparency" else { return }
        moderationTransparencyLoadRevision += 1
        moderationTransparencyPageRevision += 1
        moderationTransparencyContinuationToken += 1
        moderationTransparencyBuckets = []
        moderationTransparencyNextCursor = nil
        moderationTransparencyIsLoadingOlder = false
        moderationTransparencyLoadMoreError = nil
        rows = []
        state = .idle
        await load()
    }
}
