import VouchaAPI
import VouchaCore
import VouchaModels

extension RSSFeedListViewModel {
    func relatedArticles(rssFeedItemId: String) -> StoryRelatedArticles? {
        guard let storyId = storyIdsByItemId[rssFeedItemId],
              let related = storyRelatedArticlesByStoryId[storyId],
              related.primaryItemId == rssFeedItemId
        else { return nil }
        return related
    }

    func loadMoreStoryArticles(rssFeedItemId: String) async {
        guard let storyId = storyIdsByItemId[rssFeedItemId],
              let related = relatedArticles(rssFeedItemId: rssFeedItemId),
              let request = related.pagination.beginNextPage()
        else { return }
        do {
            let page: StoryPageResponse = try await client.send(.story(
                storyId: storyId, after: request.cursor, excludeItemId: related.primaryItemId
            ))
            guard storyRelatedArticlesByStoryId[storyId] === related,
                  related.pagination.isCurrent(request)
            else { return }
            let items = hydrateStoryItems(
                ids: page.itemIds.filter { $0 != related.primaryItemId },
                items: page.rssFeedItems, thumbnails: page.rssFeedItemThumbnailUrl
            )
            itemElectionsById.merge(page.rssFeedItemElections ?? [:], uniquingKeysWith: { _, new in new })
            embedsByItemId.merge(page.rssFeedItemEmbeds ?? [:], uniquingKeysWith: { _, new in new })
            storyPostIdsByStoryId.merge(page.storyPostIds ?? [:], uniquingKeysWith: { _, new in new })
            applyBookmarkState(from: page.bookmarks)
            mergeVotes(page.electionVotes)
            related.pagination.complete(
                request, items: items, endCursor: page.pageInfo.endCursor, hasNextPage: page.pageInfo.hasNextPage
            )
        } catch {
            guard storyRelatedArticlesByStoryId[storyId] === related else { return }
            if Task.isCancelled {
                related.pagination.cancel(request)
            } else {
                related.pagination.fail(
                    request,
                    error: error as? VouchaError ?? .api(statusCode: 0, preconditionCode: nil)
                )
            }
        }
    }

    func hydrateStoryItems(
        ids: [String],
        items: [String: RssFeedItem],
        thumbnails: [String: String]?
    ) -> [RssFeedItem] {
        ids.compactMap { id in
            items[id]?.replacingThumbnailURL(VouchaURLResolver.absoluteString(
                for: thumbnails?[id], relativeTo: apiBaseURL
            ))
        }
    }
}
