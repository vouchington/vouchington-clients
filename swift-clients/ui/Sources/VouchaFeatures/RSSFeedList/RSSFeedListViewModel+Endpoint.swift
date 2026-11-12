import VouchaAPI

extension RSSFeedListViewModel {
    func feedEndpoint(after cursor: String?) -> Endpoint {
        switch feedSource {
        case .your:
            Endpoint.rssFeedItems(
                feedType: contentType.feedType,
                after: cursor,
                mediaType: contentType.mediaType,
                hasHackerNewsReferences: hasHackerNewsReferences ? true : nil
            )
        case .all:
            Endpoint.allRssFeedItems(
                after: cursor,
                mediaType: contentType.mediaType,
                hasHackerNewsReferences: hasHackerNewsReferences ? true : nil
            )
        }
    }

}
