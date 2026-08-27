import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels

extension NativeRouteSurfaceViewModel {
    var focusedRssFeedItemId: String? {
        guard destination == .feedNews || destination == .feedPodcasts || destination == .feedVideos else {
            return nil
        }
        guard ["/news", "/podcast-episodes", "/videos"].contains(routeMatch?.path) else {
            return nil
        }
        let value = nativeRouteQueryItems(fromEncodedQuery: routeQuery)["rss_item"]?
            .trimmingCharacters(in: .whitespacesAndNewlines)
        return value?.isEmpty == false ? value : nil
    }

    func loadFocusedRssFeedItem(client: APIClient, id: String) async throws {
        let response: RssFeedItemDetailResponse = try await client.send(.rssFeedItem(id: id))
        let thumbnailURL = VouchaURLResolver.absoluteString(
            for: response.rssFeedItemThumbnailUrl?[response.rssFeedItem.id],
            relativeTo: client.baseURL
        )
        focusedRssFeedItem = response.rssFeedItem.replacingThumbnailURL(thumbnailURL)
        focusedRssFeedItemContentHtml = response.contentHtml
        focusedRssFeedItemElection = response.rssFeedItemElection
        focusedRssFeedItemBookmarks = response.bookmarks?[response.rssFeedItem.id] ?? [:]
        focusedRssFeedItemVote = response.electionVote?.choice ?? response.rssFeedItemElection?.myVote
    }
}
