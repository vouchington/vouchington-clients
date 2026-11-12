import Foundation
import VouchaAPI
import VouchaLocalization

extension NativeRouteSurfaceViewModel {
    func loadPostPage(client: APIClient, after: String?) async throws -> NativeForwardPage {
        let endpoint = hackerNewsPostFeedEndpoint(after: after)
        let response: NativePostFeedResponse = try await client.send(endpoint)
        let rows = response.results.compactMap { result -> NativeForwardRow? in
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
            let title = NormalizedAuthoredText(
                text: post.title,
                declaredLanguage: post.declaredLanguage,
                detectedLanguage: post.linguaRsDetectedLanguage
            )
            return forwardRow(
                id: result.id,
                icon: postTypeIcon(for: post.postType),
                title: title?.text ?? post.slug.map(rawText) ?? postTypeText(post.postType),
                detail: hackerNewsDetail(subtitle, references: response.postHackerNewsReferences?[post.id]),
                declaredLanguage: title?.declaredLanguage,
                detectedLanguage: title?.detectedLanguage,
                provenance: post.provenance,
                staffProvenance: post.staffProvenance
            )
        }
        return NativeForwardPage(rows: rows, pageInfo: response.pageInfo)
    }

    func loadRssFeedItemPage(client: APIClient, after: String?) async throws -> NativeForwardPage {
        let mediaType: String? = switch destination {
        case .feedNews: "article"
        case .feedPodcasts: "audio"
        case .feedVideos: "video"
        default: nil
        }
        let endpoint = routeMatch?.path.hasPrefix("/feed/") == false
            ? Endpoint.allRssFeedItems(
                after: after,
                limit: 10,
                mediaType: mediaType,
                hasHackerNewsReferences: hasHackerNewsReferences && isHackerNewsFilterVisible ? true : nil
            )
            : Endpoint.rssFeedItems(
                feedType: rssFeedItemFeedType,
                after: after,
                limit: 10,
                mediaType: mediaType,
                hasHackerNewsReferences: hasHackerNewsReferences && isHackerNewsFilterVisible ? true : nil
            )
        let response: NativeRssFeedItemsResponse = try await client.send(endpoint)
        let rows = response.results.compactMap { result -> NativeForwardRow? in
            guard let item = response.rssFeedItems[result.entityId ?? result.id] else { return nil }
            let title = item.title ?? item.data?.title ?? item.rssFeed?.title
                ?? URL(string: item.link ?? item.data?.link ?? item.rssFeed?.rssFeedUrl?.url ?? "")?.host
            return forwardRow(
                id: result.id,
                icon: itemIcon(for: item.mediaType),
                title: title.map(rawText) ?? appText(.nativeSwiftRouteSurfaceRssItem),
                detail: hackerNewsDetail(
                    item.rssFeed?.title.map(rawText) ?? listItemMediaTypeText(item.mediaType),
                    references: response.rssFeedItemHackerNewsReferences?[item.id]
                )
            )
        }
        return NativeForwardPage(rows: rows, pageInfo: response.pageInfo)
    }
}
