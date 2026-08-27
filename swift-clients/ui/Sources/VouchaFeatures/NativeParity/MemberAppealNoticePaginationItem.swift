import VouchaAPI

struct MemberAppealNoticePaginationItem: Identifiable {
    let kind: MemberAppealNoticeKind
    let hasMore: Bool
    let isLoading: Bool
    let hasError: Bool
    let isRelevant: Bool

    var id: String {
        switch kind {
        case .warnings: "warnings"
        case .bans: "bans"
        case .removedPosts: "removed-posts"
        }
    }

    init<Item: Identifiable & Sendable>(
        kind: MemberAppealNoticeKind,
        pagination: CursorPaginationState<Item>
    ) where Item.ID: Hashable & Sendable {
        self.kind = kind
        hasMore = pagination.hasMore
        isLoading = pagination.isLoading
        hasError = pagination.lastError != nil
        isRelevant = pagination.hasLoadedPage && (hasMore || hasError)
    }
}
