import Foundation

extension RssFeedItem {
    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let data = try container.decodeIfPresent(RssFeedItemData.self, forKey: .data)
        let mediaContent = try container.decodeIfPresent(MediaContent.self, forKey: .mediaContent)
        let rssFeedSidecar = try container.decodeIfPresent(EmbeddedRssFeedSidecar.self, forKey: .rssFeed)
        guard let rssFeedId = try container.decodeIfPresent(String.self, forKey: .rssFeedId)
            ?? rssFeedSidecar?.id
        else {
            throw DecodingError.keyNotFound(
                CodingKeys.rssFeedId,
                .init(codingPath: container.codingPath, debugDescription: "Missing rss_feed_id and rss_feed.id")
            )
        }

        entityType = try container.decodeIfPresent(String.self, forKey: .entityType)
        id = try container.decode(String.self, forKey: .id)
        self.rssFeedId = rssFeedId
        title = try container.decodeIfPresent(String.self, forKey: .title) ?? data?.title
        description = try container.decodeIfPresent(String.self, forKey: .description)
        content = try container.decodeIfPresent(String.self, forKey: .content)
        link = try container.decodeIfPresent(String.self, forKey: .link) ?? data?.link
        url = try container.decodeIfPresent(RssFeedItemURL.self, forKey: .url)
        publishedAt = try container.decodeIfPresent(Date.self, forKey: .publishedAt)
        creator = try container.decodeIfPresent(String.self, forKey: .creator)
        categories = try container.decodeIfPresent([RssFeedItemCategory].self, forKey: .categories)
        self.data = data
        rssFeed = rssFeedSidecar?.resolvedSource
        self.mediaContent = mediaContent
        thumbnailURL = try container.decodeIfPresent(String.self, forKey: .thumbnailURL) ?? data?.thumbnailURL
        mediaType = try container.decodeIfPresent(String.self, forKey: .mediaType)
            ?? data?.mediaType
            ?? mediaContent?.medium
            ?? mediaContent?.type
        enclosureURL = try container.decodeIfPresent(String.self, forKey: .enclosureURL)
            ?? data?.enclosureURL
        enclosureType = try container.decodeIfPresent(String.self, forKey: .enclosureType)
            ?? data?.enclosureType
        durationSeconds = try container.decodeIfPresent(Int.self, forKey: .durationSeconds)
            ?? data?.durationSeconds
            ?? mediaContent?.duration
        let decodedVideoID = try container.decodeIfPresent(String.self, forKey: .videoID) ?? data?.videoID
        let decodedVideoPlatform = try container.decodeIfPresent(String.self, forKey: .videoPlatform)
            ?? data?.videoPlatform
        videoID = Self.trimmedNonEmpty(decodedVideoID)
        videoPlatform = Self.trimmedNonEmpty(decodedVideoPlatform)
        election = try container.decodeIfPresent(RssFeedItemElection.self, forKey: .election)
        guid = try container.decodeIfPresent(String.self, forKey: .guid)
    }

    private static func trimmedNonEmpty(_ value: String?) -> String? {
        guard let trimmed = value?.trimmingCharacters(in: .whitespacesAndNewlines), !trimmed.isEmpty else {
            return nil
        }
        return trimmed
    }
}
