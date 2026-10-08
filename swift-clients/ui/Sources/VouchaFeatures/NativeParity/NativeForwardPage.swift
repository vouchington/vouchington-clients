import VouchaLocalization
import VouchaModels

struct NativeForwardRow: Identifiable {
    let id: String
    let row: NativeRouteDestinationRow
}

struct NativeForwardPage {
    let rows: [NativeForwardRow]
    let endCursor: String?
    let hasMore: Bool

    init(rows: [NativeForwardRow], pageInfo: Page<some Decodable & Sendable>.PageInfo?) {
        self.rows = rows
        endCursor = pageInfo?.endCursor
        hasMore = pageInfo?.hasNextPage ?? false
    }
}

struct NativeBookmarkPage {
    let rows: [NativeBookmarkRow]
    let embedsByEntityId: [String: UrlEmbed]
    let endCursor: String?
    let hasMore: Bool

    init(
        rows: [NativeBookmarkRow],
        pageInfo: Page<some Decodable & Sendable>.PageInfo?,
        embedsByEntityId: [String: UrlEmbed] = [:]
    ) {
        self.rows = rows
        self.embedsByEntityId = embedsByEntityId
        endCursor = pageInfo?.endCursor
        hasMore = pageInfo?.hasNextPage ?? false
    }
}

extension NativeRouteSurfaceViewModel {
    func forwardRow(
        id: String,
        icon: String,
        title: UiVerbatimText,
        detail: UiVerbatimText,
        declaredLanguage: String? = nil,
        detectedLanguage: String? = nil,
        targetPath: String? = nil,
        provenance: PublicContentProvenance? = nil
    ) -> NativeForwardRow {
        .init(
            id: id,
            row: .init(
                icon: icon,
                title: title,
                detail: detail,
                declaredLanguage: declaredLanguage,
                detectedLanguage: detectedLanguage,
                targetPath: targetPath,
                provenance: provenance
            )
        )
    }
}
