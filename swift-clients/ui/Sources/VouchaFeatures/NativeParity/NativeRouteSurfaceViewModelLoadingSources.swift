import Foundation
import VouchaAPI
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func loadSourceRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        if routeMatch?.path.hasPrefix("/source/") == true {
            return try await loadSourceDetailRows(client: client)
        }
        if let feedType = mySourceFeedType {
            let identity: NativeIdentityResponse = try await client.send(.myIdentity)
            let response: NativeRssFeedsPageResponse = try await client.send(
                .userRssFeeds(userId: identity.identity.id, feedType: feedType, limit: 25)
            )
            return sourceRows(from: response.results)
        }
        if let feedType = sourceBrowseFeedType {
            let response: NativeRssFeedsPageResponse = try await client.send(
                .allRssFeeds(feedType: feedType, limit: 25, category: sourceBrowseCategory)
            )
            return sourceRows(from: response.results)
        }

        let response: NativeRssFeedsTrendingResponse = try await client.send(
            Endpoint(.GET, path: "/api/v1/rss-feeds/trending", queryItems: [.init(name: "limit", value: "8")])
        )
        guard !response.results.isEmpty else { return [] }

        let feeds = response.rssFeeds.sorted { $0.key < $1.key }.map(\.value)
        return sourceRows(from: feeds)
    }

    private func loadSourceDetailRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        let raw = routeMatch?.param("idOrSlug", "id") ?? routeMatch?.path.routeLastSegment ?? ""
        let loadedResponse: NativeRssFeedsPageResponse = try await client.send(.rssFeeds(topicIdentifier: raw))
        guard loadedResponse.results.count == 1, let feed = loadedResponse.results.first else {
            throw URLError(.resourceUnavailable)
        }
        let bookmarks: EntityBookmarksResponse? = await (try? client.send(.bookmarks(
            entityType: "rss_feed",
            entityId: feed.id
        )))
        let relationBookmarks = bookmarks?.bookmarks ?? [:]
        detailRelationEntityType = "rss_feed"
        detailRelationEntityId = feed.id
        detailRelationBookmarks = relationBookmarks
        detailRelationIsSelfProfile = false
        if let crawlId = routeMatch?.param("crawlId") {
            let response: NativeRssFeedCrawlResponse = try await client.send(.rssFeedCrawl(
                id: feed.id,
                crawlId: crawlId
            ))
            return [
                row(
                    "doc.text.magnifyingglass",
                    appText(.nativeSwiftRouteSurfaceCrawlDetails),
                    response.crawl.statusText
                ),
                row(
                    "calendar",
                    appText(.nativeSwiftRouteSurfaceCreated),
                    timestampText(response.crawl.createdAt, fallback: response.crawl.id)
                )
            ]
        }

        var rows = sourceDetailHeaderRows(feed)
        if let crawlHistoryRows = try await sourceCrawlHistoryRows(
            client: client,
            feed: feed,
            prefix: rows
        ) {
            return crawlHistoryRows
        }
        let detail: NativeRssFeedDetailResponse? = try? await client.send(.rssFeed(id: feed.id))
        if detail?.canViewLatestCrawl == true {
            rows.append(row(
                "clock.arrow.circlepath",
                appText(.nativeSwiftRouteSurfaceCrawlHistory),
                appText(.nativeSwiftRouteSurfaceCrawlHistoryAvailable),
                targetPath: "\(sourceRouteBasePath)/crawls"
            ))
        }
        return rows
    }

    private func sourceCrawlHistoryRows(
        client: APIClient,
        feed: NativeRssFeedSummary,
        prefix: [NativeRouteDestinationRow]
    ) async throws -> [NativeRouteDestinationRow]? {
        guard routeMatch?.template == "/source/:idOrSlug/crawls" else { return nil }
        let detailPathPrefix = "\(sourceRouteBasePath)/crawls"
        let endpoint = NativeCrawlHistoryEndpoint.rssFeed(id: feed.id, detailPathPrefix: detailPathPrefix)
        let page = try await crawlHistoryPage(client: client, endpoint: endpoint, after: nil)
        configureCrawlHistory(prefix: prefix, suffix: [], endpoint: endpoint, page: page)
        return rows
    }

    private func sourceDetailHeaderRows(_ feed: NativeRssFeedSummary) -> [NativeRouteDestinationRow] {
        [
            row(
                "newspaper",
                (feed.title ?? URL(string: feed.rssFeedUrl.url)?.host).map(rawText)
                    ?? appText(.nativeSwiftRouteSurfaceRssFeed),
                listItemMediaTypeText(feed.feedType)
            ),
            row("link", appText(.nativeSwiftRouteSurfaceFeedUrl), rawText(feed.rssFeedUrl.url))
        ]
    }

    private var sourceRouteBasePath: String {
        guard let path = routeMatch?.path else { return "/source" }
        return routeMatch?.template == "/source/:idOrSlug/crawls"
            ? String(path.dropLast("/crawls".count))
            : path
    }

    private func sourceRows(from feeds: [NativeRssFeedSummary]) -> [NativeRouteDestinationRow] {
        feeds.compactMap { feed in
            row(
                "newspaper",
                (feed.title ?? URL(string: feed.rssFeedUrl.url)?.host).map(rawText)
                    ?? appText(.nativeSwiftRouteSurfaceRssFeed),
                listItemMediaTypeText(feed.feedType)
            )
        }
    }
}
