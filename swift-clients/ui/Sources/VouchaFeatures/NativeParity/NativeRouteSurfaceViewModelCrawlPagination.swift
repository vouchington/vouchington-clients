import VouchaAPI
import VouchaCore

extension NativeRouteSurfaceViewModel {
    func configureCrawlHistory(
        prefix: [NativeRouteDestinationRow],
        suffix: [NativeRouteDestinationRow],
        endpoint: NativeCrawlHistoryEndpoint,
        page: NativeForwardPage
    ) {
        crawlHistoryPrefix = prefix
        crawlHistorySuffix = suffix
        crawlHistoryEndpoint = endpoint
        crawlHistoryPagination.reset(items: page.rows)
        crawlHistoryPagination.restoreContinuation(endCursor: page.endCursor, hasMore: page.hasMore)
        applyCrawlHistoryRows()
    }

    public func loadMoreCrawlHistory() async {
        guard let client, let endpoint = crawlHistoryEndpoint,
              let request = crawlHistoryPagination.beginNextPage() else { return }
        do {
            let page = try await crawlHistoryPage(client: client, endpoint: endpoint, after: request.cursor)
            guard crawlHistoryPagination.complete(
                request,
                items: page.rows,
                endCursor: page.endCursor,
                hasNextPage: page.hasMore
            ) else { return }
            applyCrawlHistoryRows()
        } catch let error as VouchaError {
            _ = crawlHistoryPagination.fail(request, error: error)
        } catch {
            _ = crawlHistoryPagination.fail(request, error: .api(statusCode: 0, preconditionCode: nil))
        }
    }

    func crawlHistoryPage(
        client: APIClient,
        endpoint: NativeCrawlHistoryEndpoint,
        after: String?
    ) async throws -> NativeForwardPage {
        switch endpoint {
        case let .url(id):
            let response: NativeUrlCrawlListResponse = try await client.send(.urlCrawls(
                urlId: id,
                after: after,
                limit: 20
            ))
            return NativeForwardPage(rows: response.results.map {
                forwardRow(
                    id: $0.id,
                    icon: "doc.text.magnifyingglass",
                    title: timestampText($0.createdAt, fallback: $0.id),
                    detail: $0.statusText,
                    targetPath: "/url/\(id)/crawls/\($0.id)"
                )
            }, pageInfo: response.pageInfo)
        case let .rssFeed(id, detailPathPrefix):
            let response: NativeRssFeedCrawlsResponse = try await client.send(.rssFeedCrawls(
                id: id,
                after: after,
                limit: 20
            ))
            return NativeForwardPage(rows: response.results.map {
                forwardRow(
                    id: $0.id,
                    icon: "doc.text.magnifyingglass",
                    title: timestampText($0.createdAt, fallback: $0.id),
                    detail: $0.statusText,
                    targetPath: "\(detailPathPrefix)/\($0.id)"
                )
            }, pageInfo: response.pageInfo)
        }
    }

    private func applyCrawlHistoryRows() {
        rows = crawlHistoryPrefix + crawlHistoryPagination.items.map(\.row) + crawlHistorySuffix
    }
}
