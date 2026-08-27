import VouchaLocalization

struct CommunityWorkspaceSummary: Equatable {
    var title: UiVerbatimText = .message(.nativeSwiftCommunitiesCommunity)
    var detail: UiVerbatimText = .message(.nativeSwiftCommunitiesCommunityWorkspace)
    var activity: UiVerbatimText = .message(
        .nativeSwiftCommunitiesActivitySummary,
        numberParameters: ["members": 0, "posts": 0, "items": 0]
    )
    var isMember = false
    var isOwner = false
    var isArchived = false
    var hasPendingApplication = false
    var tabs: [CommunitySurfaceTab] = []
    var rows: [NativeRouteDestinationRow] = []

    var canJoin: Bool {
        !isMember && !isArchived && !hasPendingApplication
    }
}
