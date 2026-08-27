import Foundation
import VouchaAPI
import VouchaLocalization

extension NativeRouteSurfaceViewModel {
    func loadUrlDetailRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        let id = urlRouteValue("id") ?? routeMatch?.path.routeLastSegment ?? ""
        let response: NativeUrlDetailResponse = try await client.send(.url(urlId: id))
        if let hostname = response.url.hostname, !hostname.blocked {
            detailReportTarget = .urlHostname(id: hostname.id)
        }

        if let requestedCrawlRows = try await requestedCrawlRows(client: client, urlId: id, response: response) {
            return requestedCrawlRows
        }

        var rows = [
            row("link", rawText(response.url.url), urlDetailDetail(response: response)),
            row(
                "clock.arrow.circlepath",
                appText(.nativeSwiftRouteSurfaceCrawlHistory),
                crawlHistoryDetail(response: response)
            )
        ]
        if response.canViewLatestCrawl, let latest = response.latestCrawl {
            rows.append(row(
                "doc.text.magnifyingglass",
                latest.title.map(rawText) ?? appText(.nativeSwiftRouteSurfaceLatestCrawl),
                latest.statusText
            ))
        }
        if response.canViewCrawlHistory {
            let page = try await crawlHistoryPage(client: client, endpoint: .url(id), after: nil)
            let suffix = response.canTriggerCrawl ? [row(
                "arrow.triangle.2.circlepath",
                appText(.nativeSwiftRouteSurfaceTriggerCrawl),
                appText(.nativeSwiftRouteSurfaceTriggerCrawlDetail)
            )] : []
            configureCrawlHistory(prefix: rows, suffix: suffix, endpoint: .url(id), page: page)
            return self.rows
        }
        if response.canTriggerCrawl {
            rows.append(row(
                "arrow.triangle.2.circlepath",
                appText(.nativeSwiftRouteSurfaceTriggerCrawl),
                appText(.nativeSwiftRouteSurfaceTriggerCrawlDetail)
            ))
        }
        return rows
    }

    private func requestedCrawlRows(
        client: APIClient,
        urlId: String,
        response: NativeUrlDetailResponse
    ) async throws -> [NativeRouteDestinationRow]? {
        guard let crawlId = urlRouteValue("crawlId") else { return nil }
        guard response.canViewCrawlHistory else {
            return [
                row("link", rawText(response.url.url), urlDetailDetail(response: response)),
                row(
                    "clock.arrow.circlepath",
                    appText(.nativeSwiftRouteSurfaceCrawlHistory),
                    appText(.nativeSwiftRouteSurfaceUnavailable)
                )
            ]
        }
        return try await loadUrlCrawlDetailRows(client: client, urlId: urlId, crawlId: crawlId)
    }

    private func loadUrlCrawlDetailRows(client: APIClient, urlId: String, crawlId: String) async throws
        -> [NativeRouteDestinationRow] {
        let response: NativeUrlCrawlDetailResponse = try await client.send(.urlCrawl(urlId: urlId, crawlId: crawlId))
        let crawl = response.crawl
        var rows = [
            row(
                "doc.text.magnifyingglass",
                crawl.title.map(rawText) ?? appText(.nativeSwiftRouteSurfaceCrawlDetails),
                crawl.statusText
            ),
            row(
                "calendar",
                appText(.nativeSwiftRouteSurfaceCreated),
                timestampText(crawl.createdAt)
            ),
            row(
                "checkmark.circle",
                appText(.nativeSwiftRouteSurfaceCompleted),
                crawl.completedAt.map { timestampText($0) }
                    ?? appText(.nativeSwiftRouteSurfaceNo)
            ),
            row(
                "character.book.closed",
                appText(.nativeSwiftCommunityRowsLanguage),
                crawl.lang.map(rawText) ?? appText(.nativeSwiftRouteSurfaceUnknown)
            )
        ]
        if let image = response.ogImageSideload {
            rows.append(row(
                "photo",
                appText(.nativeSwiftRouteSurfaceOpenGraphImage),
                rawText(image)
            ))
        }
        if let meta = crawl.metaTags, !meta.isEmpty {
            rows.append(row(
                "tag",
                appText(.nativeSwiftRouteSurfaceMetaTags),
                countText(meta.count, item: "field")
            ))
            rows.append(contentsOf: meta.sorted { $0.key < $1.key }.prefix(3).map {
                row("number", rawText($0.key), $0.value.displayText)
            })
        }
        if let markdown = crawl.markdown, !markdown.isEmpty {
            rows.append(row(
                "text.alignleft",
                appText(.nativeSwiftLandingPagesContent),
                rawText(markdown)
            ))
        }
        return rows
    }

    private func urlRouteValue(_ name: String) -> String? {
        routeMatch?.param(name)
    }

    private func urlDetailDetail(response: NativeUrlDetailResponse) -> UiVerbatimText {
        .joined([
            response.url.hostname?.hostname ?? response.url.pathname ?? response.url.url,
            response.url.pathname,
            response.urlType
        ].compactMap { $0 }.map(rawText))
    }

    private func crawlHistoryDetail(response: NativeUrlDetailResponse) -> UiVerbatimText {
        if response.canViewCrawlHistory {
            return appText(
                response.canViewLatestCrawl
                    ? .nativeSwiftRouteSurfaceCrawlHistoryAvailable
                    : .nativeSwiftRouteSurfaceHistoryAvailable
            )
        }
        return appText(.nativeSwiftRouteSurfaceUnavailable)
    }

    func timestampText(_ rawValue: String?, fallback: String? = nil) -> UiVerbatimText {
        guard let rawValue else {
            return fallback.map(rawText) ?? appText(.nativeSwiftRouteSurfaceUnknown)
        }
        guard let date = ISO8601DateFormatter().date(from: rawValue) else {
            return rawText(rawValue)
        }
        return appText(
            .nativeSwiftRouteSurfaceDateValue,
            dateParameters: [
                "date": UiMessageDateParameter(date, dateStyle: .abbreviated, timeStyle: .shortened)
            ]
        )
    }
}
