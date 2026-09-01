import Foundation
import VouchaAPI
import VouchaLocalization

extension NativeRouteSurfaceViewModel {
    func loadBookmarkCollectionPage(
        _ collection: NativeBookmarkCollection,
        client: APIClient,
        after: String?,
        rankOffset: Int,
        excluding existingIds: Set<String>
    ) async throws -> NativeBookmarkPage {
        let endpoint = collection.endpoint.appendingAfter(after)
        switch collection.kind {
        case .posts:
            return try await loadPostBookmarkPage(
                collection, client: client, endpoint: endpoint, rankOffset: rankOffset, excluding: existingIds
            )
        case .rssFeedItems:
            return try await loadFeedItemBookmarkPage(
                collection, client: client, endpoint: endpoint, rankOffset: rankOffset, excluding: existingIds
            )
        case .rssFeeds:
            return try await loadFeedBookmarkPage(
                collection, client: client, endpoint: endpoint, rankOffset: rankOffset, excluding: existingIds
            )
        case .generic, .users:
            return try await loadGenericBookmarkPage(
                collection, client: client, endpoint: endpoint, rankOffset: rankOffset, excluding: existingIds
            )
        }
    }

    private func loadPostBookmarkPage(
        _ collection: NativeBookmarkCollection,
        client: APIClient,
        endpoint: Endpoint,
        rankOffset: Int,
        excluding existingIds: Set<String>
    ) async throws -> NativeBookmarkPage {
        let response: NativeBookmarkedPostsResponse = try await client.send(endpoint)
        let rows = distinctNew(response.results, excluding: existingIds, id: \.id).enumerated().map { rank, post in
            bookmarkedPostRow(post, collection: collection, rank: rankOffset + rank)
        }
        return NativeBookmarkPage(
            rows: rows,
            pageInfo: response.pageInfo,
            embedsByEntityId: response.postLinkEmbeds ?? [:]
        )
    }

    private func loadFeedItemBookmarkPage(
        _ collection: NativeBookmarkCollection,
        client: APIClient,
        endpoint: Endpoint,
        rankOffset: Int,
        excluding existingIds: Set<String>
    ) async throws -> NativeBookmarkPage {
        let response: NativeBookmarkedRssFeedItemsResponse = try await client.send(endpoint)
        let rows = distinctNew(response.results, excluding: existingIds, id: \.id).enumerated().map { rank, item in
            NativeBookmarkRow(
                entityType: collection.entityType,
                entityId: item.id,
                icon: itemIcon(for: item.mediaType),
                title: bookmarkedFeedItemTitle(item),
                detail: bookmarkedFeedItemDetail(item),
                destination: .path(NativeBookmarkRow.rssItemPath(id: item.id, mediaType: item.mediaType)),
                inverseAction: collection.inverseAction,
                rank: rankOffset + rank
            )
        }
        return NativeBookmarkPage(
            rows: rows,
            pageInfo: response.pageInfo,
            embedsByEntityId: response.rssFeedItemEmbeds ?? [:]
        )
    }

    private func loadFeedBookmarkPage(
        _ collection: NativeBookmarkCollection,
        client: APIClient,
        endpoint: Endpoint,
        rankOffset: Int,
        excluding existingIds: Set<String>
    ) async throws -> NativeBookmarkPage {
        let response: NativeRssFeedsPageResponse = try await client.send(endpoint)
        let rows = distinctNew(response.results, excluding: existingIds, id: \.id).enumerated().map { rank, feed in
            bookmarkedFeedRow(feed, collection: collection, rank: rankOffset + rank)
        }
        return NativeBookmarkPage(rows: rows, pageInfo: response.pageInfo)
    }

    private func loadGenericBookmarkPage(
        _ collection: NativeBookmarkCollection,
        client: APIClient,
        endpoint: Endpoint,
        rankOffset: Int,
        excluding existingIds: Set<String>
    ) async throws -> NativeBookmarkPage {
        let response: NativeGenericListResponse = try await client.send(endpoint)
        let rows = distinctNew(response.results, excluding: existingIds, id: \.id).enumerated().map { rank, result in
            bookmarkedGenericRow(
                response.hydratedEntity(for: result),
                response: response,
                collection: collection,
                rank: rankOffset + rank
            )
        }
        return NativeBookmarkPage(rows: rows, pageInfo: response.pageInfo)
    }

    private func distinctNew<Value>(
        _ values: [Value],
        excluding existingIds: Set<String>,
        id: (Value) -> String
    ) -> [Value] {
        var seenIds = existingIds
        return values.filter { seenIds.insert(id($0)).inserted }
    }

    private func bookmarkedPostRow(
        _ post: NativePostSummary,
        collection: NativeBookmarkCollection,
        rank: Int
    ) -> NativeBookmarkRow {
        let destination: NativeBookmarkRow.Destination = post.postType == .comment
            ? .comment(id: post.id, rootId: post.rootId)
            : .path(NativeBookmarkRow.postPath(type: post.postType, id: post.id, slug: post.slug)
                ?? "/discussion/\(post.id)")
        return NativeBookmarkRow(
            entityType: collection.entityType,
            entityId: post.id,
            icon: postTypeIcon(for: post.postType),
            title: NativeBookmarkRow.postTitle(type: post.postType, title: post.title),
            detail: post.createdById.map(UiVerbatimText.userContent) ?? .message(post.postType.titleKey),
            destination: destination,
            inverseAction: collection.inverseAction,
            rank: rank
        )
    }

    private func bookmarkedFeedRow(
        _ feed: NativeRssFeedSummary,
        collection: NativeBookmarkCollection,
        rank: Int
    ) -> NativeBookmarkRow {
        NativeBookmarkRow(
            entityType: collection.entityType,
            entityId: feed.id,
            icon: "newspaper",
            title: .userContent(feed.title ?? URL(string: feed.rssFeedUrl.url)?.host
                ?? UiMessages.string(.nativeSwiftHouseholdsBookmarksSource, locale: .english)),
            detail: listItemMediaTypeText(feed.feedType),
            destination: .path("/source/\(feed.topic?.slug ?? feed.id)"),
            inverseAction: collection.inverseAction,
            rank: rank
        )
    }

    private func bookmarkedGenericRow(
        _ entity: NativeGenericEntity,
        response: NativeGenericListResponse,
        collection: NativeBookmarkCollection,
        rank: Int
    ) -> NativeBookmarkRow {
        let entityType = collection.kind == .users ? "user" : collection.entityType
        return NativeBookmarkRow(
            entityType: collection.entityType,
            entityId: entity.id,
            icon: collection.kind == .users
                ? "person"
                : genericBookmarkIcon(entity, response: response, fallback: collection.icon),
            title: genericBookmarkTitle(entity, entityType: entityType),
            detail: genericBookmarkDetail(entity, entityType: entityType),
            destination: .path(genericBookmarkPath(entity, entityType: entityType)),
            inverseAction: collection.inverseAction,
            rank: rank
        )
    }
}

private extension Endpoint {
    func appendingAfter(_ after: String?) -> Endpoint {
        guard let after else { return self }
        return Endpoint(
            method,
            path: path,
            queryItems: queryItems + [.init(name: "after", value: after)],
            headers: headers,
            body: body,
            bodyKeyEncodingStrategy: bodyKeyEncodingStrategy
        )
    }
}
