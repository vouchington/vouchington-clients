import Observation
import VouchaAPI
import VouchaModels

@Observable
@MainActor
final class StoryRelatedArticles {
    let primaryItemId: String
    var pagination: CursorPaginationState<RssFeedItem>
    var isExpanded = false

    init(primaryItemId: String, items: [RssFeedItem], pageInfo: Page<RssFeedItem>.PageInfo) {
        self.primaryItemId = primaryItemId
        pagination = CursorPaginationState(items: items.filter { $0.id != primaryItemId })
        pagination.restoreContinuation(endCursor: pageInfo.endCursor, hasMore: pageInfo.hasNextPage)
    }
}
