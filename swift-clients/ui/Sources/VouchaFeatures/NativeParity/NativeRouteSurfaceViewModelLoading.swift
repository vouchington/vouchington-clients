import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func row(
        _ icon: String,
        _ title: UiVerbatimText,
        _ detail: UiVerbatimText,
        declaredLanguage: String? = nil,
        detectedLanguage: String? = nil,
        detailDeclaredLanguage: String? = nil,
        detailDetectedLanguage: String? = nil,
        targetPath: String? = nil,
        provenance: PublicContentProvenance? = nil
    ) -> NativeRouteDestinationRow {
        .init(
            icon: icon,
            title: title,
            detail: detail,
            declaredLanguage: declaredLanguage,
            detectedLanguage: detectedLanguage,
            detailDeclaredLanguage: detailDeclaredLanguage,
            detailDetectedLanguage: detailDetectedLanguage,
            targetPath: targetPath,
            provenance: provenance
        )
    }

    func appText(
        _ key: UiMessageKey,
        parameters: [String: String] = [:],
        textParameters: [String: UiVerbatimText] = [:],
        numberParameters: [String: Double] = [:],
        percentParameters: [String: Double] = [:],
        currencyParameters: [String: UiMessageCurrencyParameter] = [:],
        dateParameters: [String: UiMessageDateParameter] = [:],
        selectedCase: String? = nil
    ) -> UiVerbatimText {
        .message(
            key,
            parameters: parameters,
            textParameters: textParameters,
            numberParameters: numberParameters,
            percentParameters: percentParameters,
            currencyParameters: currencyParameters,
            dateParameters: dateParameters,
            selectedCase: selectedCase
        )
    }

    func rawText(_ value: String) -> UiVerbatimText {
        .verbatim(value)
    }

    func countText(_ count: Int, item: String) -> UiVerbatimText {
        .count(count, item: item)
    }

    func loadRows(for destination: NativeRouteDestinationIdentifier, client: APIClient) async throws
        -> [NativeRouteDestinationRow] {
        if let path = routeMatch?.path,
           NativeRouteCatalogPatterns.bookmarkPaths.contains(path)
           || ["/my/news-sources", "/my/podcasts", "/my/channels"].contains(path) {
            return try await loadBookmarkRows(client: client)
        }
        if let focusedRssFeedItemId {
            try await loadFocusedRssFeedItem(client: client, id: focusedRssFeedItemId)
            return []
        }
        let kind = destination.remoteSurfaceKind
        if kind == .entities {
            return try await loadEntityRows(for: destination, client: client)
        }
        if kind == .account {
            return try await loadAccountRows(for: destination, client: client)
        }
        if kind == .feed {
            return try await loadPostRows(for: destination, client: client)
        }
        if kind == .rssFeeds {
            return try await loadRssFeedItemRows(for: destination, client: client)
        }
        return try await loadRows(for: kind, destination: destination, client: client)
    }

    private func loadRows(
        for kind: NativeRouteRemoteSurfaceKind,
        destination: NativeRouteDestinationIdentifier,
        client: APIClient
    ) async throws -> [NativeRouteDestinationRow] {
        switch kind {
        case .none, .search:
            []
        case .feed, .rssFeeds, .entities, .account:
            []
        case .sources:
            try await loadSourceRows(client: client)
        case .discovery:
            try await loadDiscoveryRows(for: destination, client: client)
        case .library:
            try await loadLibraryRows(for: destination, client: client)
        case .messages:
            try await loadMessageRows(for: destination, client: client)
        case .notifications:
            try await loadNotificationRows(client: client)
        case .moderation:
            try await loadModerationRows(client: client)
        }
    }
}

extension NativeRouteSurfaceViewModel {
    private func loadRssFeedItemRows(for destination: NativeRouteDestinationIdentifier, client: APIClient) async throws
        -> [NativeRouteDestinationRow] {
        let mediaType: String? = switch destination {
        case .feedNews:
            "article"
        case .feedPodcasts:
            "audio"
        case .feedVideos:
            "video"
        default:
            nil
        }

        let endpoint = routeMatch?.path.hasPrefix("/feed/") == false
            ? Endpoint.allRssFeedItems(limit: 10, mediaType: mediaType)
            : Endpoint.rssFeedItems(feedType: rssFeedItemFeedType, limit: 10, mediaType: mediaType)
        let response: NativeRssFeedItemsResponse = try await client.send(endpoint)
        return response.results.compactMap { result in
            guard let item = response.rssFeedItems[result.entityId ?? result.id] else { return nil }
            let title = item.title ?? item.data?.title ?? item.rssFeed?.title
                ?? URL(string: item.link ?? item.data?.link ?? item.rssFeed?.rssFeedUrl.url ?? "")?.host
            return row(
                itemIcon(for: item.mediaType),
                title.map(rawText) ?? appText(.nativeSwiftRouteSurfaceRssItem),
                item.rssFeed?.title.map(rawText)
                    ?? listItemMediaTypeText(item.mediaType)
            )
        }
    }

}
