import Foundation
import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    static func bookmarkedFeedPath(_ feed: NativeRssFeedSummary) -> String {
        NativeEntityDetailPath.source(
            feedId: feed.id,
            topicId: feed.topic?.id,
            topicSlug: feed.topic?.slug
        )
    }

    func bookmarkedFeedItemTitle(_ item: NativeRssFeedItemSummary) -> UiVerbatimText {
        .userContent(item.title ?? item.data?.title ?? item.rssFeed?.title
            ?? URL(string: item.link ?? item.data?.link ?? item.rssFeed?.rssFeedUrl.url ?? "")?.host
            ?? UiMessages.string(.nativeSwiftHouseholdsBookmarksRssItem, locale: .english))
    }

    func bookmarkedFeedItemDetail(_ item: NativeRssFeedItemSummary) -> UiVerbatimText {
        if let title = item.rssFeed?.title {
            return .userContent(title)
        }
        return item.mediaType.map(listItemMediaTypeText)
            ?? .message(.nativeSwiftHouseholdsBookmarksArticle)
    }

}

struct NativeBookmarkedPostsResponse: Decodable {
    let results: [NativePostSummary]
    let pageInfo: Page<FixtureReference>.PageInfo?
}

struct NativeBookmarkedRssFeedItemsResponse: Decodable {
    let results: [NativeRssFeedItemSummary]
    let pageInfo: Page<FixtureReference>.PageInfo?
}
