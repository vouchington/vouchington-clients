struct CommunityForwardRow: Identifiable {
    let id: String
    let row: NativeRouteDestinationRow
}

struct CommunityForwardPage {
    let items: [CommunityForwardRow]
    let endCursor: String?
    let hasMore: Bool

    static func terminal(_ rows: [NativeRouteDestinationRow]) -> Self {
        .init(
            items: rows.enumerated().map { index, row in
                .init(id: "terminal-\(index)-\(row.title)-\(row.detail)", row: row)
            },
            endCursor: nil,
            hasMore: false
        )
    }
}
