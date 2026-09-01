import VouchaAPI
import VouchaLocalization
import VouchaModels

extension CommunityDetailViewModel {
    func loadNewsRows(client: APIClient, after: String?) async throws -> CommunityForwardPage {
        let response: NativeRssFeedItemsResponse = try await client.send(
            .communityNews(idOrSlug: slug, after: after, limit: 10)
        )
        rssFeedItemEmbedsById.merge(response.rssFeedItemEmbeds ?? [:]) { _, new in new }
        let items = response.results.compactMap { result -> CommunityForwardRow? in
            guard let item = response.rssFeedItems[result.entityId ?? result.id] else { return nil }
            return .init(
                id: result.entityId ?? result.id,
                row: .init(
                    id: result.entityId ?? result.id,
                    icon: "newspaper",
                    title: item.title.map(UiVerbatimText.verbatim)
                        ?? item.data?.title.map(UiVerbatimText.verbatim)
                        ?? .message(.nativeSwiftCommunitiesNewsItem),
                    detail: item.rssFeed?.title.map(UiVerbatimText.verbatim)
                        ?? .message(.nativeSwiftCommunitiesCommunityNews)
                )
            )
        }
        return .init(
            items: items,
            endCursor: response.pageInfo?.endCursor,
            hasMore: response.pageInfo?.hasNextPage ?? false
        )
    }
}
