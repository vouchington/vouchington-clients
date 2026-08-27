import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func row(
        _ icon: String,
        _ title: UiVerbatimText,
        _ detail: UiVerbatimText,
        targetPath: String? = nil
    ) -> NativeRouteDestinationRow {
        .init(icon: icon, title: title, detail: detail, targetPath: targetPath)
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
    private func loadPostRows(for destination: NativeRouteDestinationIdentifier, client: APIClient) async throws
        -> [NativeRouteDestinationRow] {
        if destination == .postDetail {
            return try await loadPostDetailRows(client: client)
        }

        let postTypes = postTypesFilter(for: destination)

        var publicPostQuery = [
            URLQueryItem(name: "limit", value: "10"),
            URLQueryItem(name: "sort", value: "hot")
        ]
        if let postTypes {
            publicPostQuery.append(URLQueryItem(name: "post_types", value: postTypes))
        }
        let endpoint = destination == .postsBrowse || destination == .storiesBrowse
            ? Endpoint(.GET, path: "/api/v1/posts", queryItems: publicPostQuery)
            : Endpoint.posts(feedType: postFeedType, limit: 10, postTypes: postTypes)
        let response: NativePostFeedResponse = try await client.send(endpoint)
        return response.results.compactMap { result in
            guard let post = response.posts[result.entityId ?? result.id] else { return nil }
            let subtitle: UiVerbatimText = if let userId = result.sharedByUserId {
                appText(.nativeSwiftRouteSurfaceSharedByUser, parameters: ["user": userId])
            } else if let userId = post.createdById {
                appText(.nativeSwiftRouteSurfaceByUser, parameters: ["user": userId])
            } else if let deliveryType = result.deliveryType {
                postTypeText(deliveryType)
            } else {
                postTypeText(post.postType)
            }
            return row(
                postTypeIcon(for: post.postType),
                post.title.map(rawText) ?? post.slug.map(rawText) ?? postTypeText(post.postType),
                subtitle
            )
        }
    }

    private func loadPostDetailRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        let idOrSlug = routeMatch?.param("id") ?? routeMatch?.path.routeLastSegment ?? ""
        let response: NativePostDetailResponse = try await client.send(.post(idOrSlug: idOrSlug))
        let post = response.post
        return [
            row(
                postTypeIcon(for: post.postType),
                post.title.map(rawText) ?? post.slug.map(rawText) ?? postTypeText(post.postType),
                rawText(post.markdown ?? post.createdById ?? post.id)
            )
        ]
    }

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
