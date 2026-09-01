import VouchaModels

struct CommunityForwardRow: Identifiable {
    let id: String
    let row: NativeRouteDestinationRow
}

struct CommunityForwardPage {
    let items: [CommunityForwardRow]
    let endCursor: String?
    let hasMore: Bool
    /// Present only for post pages. An empty value means the accepted page has no post embeds.
    let postEmbedsByPostId: [String: UrlEmbed]?

    init(
        items: [CommunityForwardRow],
        endCursor: String?,
        hasMore: Bool,
        postEmbedsByPostId: [String: UrlEmbed]? = nil
    ) {
        self.items = items
        self.endCursor = endCursor
        self.hasMore = hasMore
        self.postEmbedsByPostId = postEmbedsByPostId
    }

    static func terminal(_ rows: [NativeRouteDestinationRow]) -> Self {
        .init(
            items: rows.enumerated().map { index, row in
                .init(id: "terminal-\(index)-\(row.title)-\(row.detail)", row: row)
            },
            endCursor: nil,
            hasMore: false,
            postEmbedsByPostId: nil
        )
    }
}
