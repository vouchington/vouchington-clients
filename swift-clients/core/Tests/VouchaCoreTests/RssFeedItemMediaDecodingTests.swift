import Foundation
@testable import VouchaModels
import XCTest

final class RssFeedItemMediaDecodingTests: XCTestCase {
    private let decoder: JSONDecoder = {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return decoder
    }()

    func testDecodesRicherMediaFieldsAndPlaybackHelpers() throws {
        let json = Data(
            #"""
            {
              "id": "item-1",
              "rss_feed_id": "feed-1",
              "title": null,
              "description": "Episode summary",
              "content": null,
              "link": null,
              "published_at": "2026-01-01T00:00:00Z",
              "creator": "Host",
              "categories": [],
              "data": {
                "title": "Episode 1",
                "link": "https://example.com/episode-1",
                "media_type": "audio",
                "enclosure_url": "https://example.com/episode-1.mp3",
                "enclosure_type": "audio/mpeg",
                "duration_seconds": 1800,
                "thumbnail_url": "https://example.com/artwork.jpg",
                "video_id": null,
                "video_platform": null
              },
              "rss_feed": {
                "id": "feed-1",
                "title": "Example Podcast",
                "feed_type": "podcast",
                "rss_feed_url": { "url": "https://example.com/rss", "canonical_url_id": null },
                "etag": null,
                "home_page_url": null,
                "last_modified_at": null,
                "hostname": null,
                "topic": null,
                "publisher_type": null,
                "podcast_show": null
              },
              "url": { "url": "https://example.com/episode-1", "canonical_url_id": null },
              "media_content": {
                "url": "https://example.com/episode-1.mp3",
                "type": "audio/mpeg",
                "medium": "audio",
                "duration": 1800
              },
              "thumbnail_url": "https://example.com/proxy-artwork.jpg"
            }
            """#
            .utf8
        )

        let item = try decoder.decode(RssFeedItem.self, from: json)
        XCTAssertEqual(item.title, "Episode 1")
        XCTAssertEqual(item.link, "https://example.com/episode-1")
        XCTAssertEqual(item.sourceTitle, "Example Podcast")
        XCTAssertEqual(item.thumbnailURL, "https://example.com/proxy-artwork.jpg")
        XCTAssertEqual(item.displayThumbnailURLString, "https://example.com/proxy-artwork.jpg")
        XCTAssertEqual(item.mediaType, "audio")
        XCTAssertEqual(item.enclosureURL, "https://example.com/episode-1.mp3")
        XCTAssertEqual(item.enclosureType, "audio/mpeg")
        XCTAssertEqual(item.durationSeconds, 1_800)
        XCTAssertEqual(item.playbackKind, .audio(url: "https://example.com/episode-1.mp3"))
    }

    func testClassifiesYouTubeEmbedOnlyVideoWithoutPlaybackURL() throws {
        let json = Data(
            #"""
            {
              "id": "item-2",
              "rss_feed_id": "feed-2",
              "title": "Video Episode",
              "description": null,
              "content": null,
              "link": "https://example.com/video",
              "published_at": "2026-01-01T00:00:00Z",
              "creator": null,
              "categories": [],
              "media_type": "video",
              "video_id": "abc123",
              "video_platform": "youtube",
              "media_content": {
                "url": null,
                "type": null,
                "medium": "video",
                "duration": null
              }
            }
            """#
            .utf8
        )

        let item = try decoder.decode(RssFeedItem.self, from: json)
        XCTAssertEqual(
            item.playbackKind,
            .embedOnlyVideo(platform: "youtube", videoID: "abc123")
        )
        XCTAssertFalse(item.hasPlayableMedia)

        let updated = item.replacingThumbnailURL("https://example.com/proxy.jpg")
        XCTAssertEqual(updated.thumbnailURL, "https://example.com/proxy.jpg")
        XCTAssertEqual(updated.displayThumbnailURLString, "https://example.com/proxy.jpg")
    }

    func testClassifiesVimeoAndProviderlessVideoMetadataAsEmbedOnly() throws {
        let vimeo = try decoder.decode(RssFeedItem.self, from: Data(#"""
        {
          "id": "item-vimeo", "rss_feed_id": "feed-1", "title": "Vimeo",
          "published_at": "2026-01-01T00:00:00Z", "media_type": "video",
          "video_id": "vimeo-123", "video_platform": "vimeo"
        }
        """#.utf8))
        let providerless = try decoder.decode(RssFeedItem.self, from: Data(#"""
        {
          "id": "item-providerless", "rss_feed_id": "feed-1", "title": "Video",
          "published_at": "2026-01-01T00:00:00Z", "media_type": "video"
        }
        """#.utf8))
        let unknownProvider = try decoder.decode(RssFeedItem.self, from: Data(#"""
        {
          "id": "item-unknown", "rss_feed_id": "feed-1", "title": "Video",
          "published_at": "2026-01-01T00:00:00Z", "video_platform": "unknown"
        }
        """#.utf8))

        XCTAssertEqual(vimeo.playbackKind, .embedOnlyVideo(platform: "vimeo", videoID: "vimeo-123"))
        XCTAssertEqual(providerless.playbackKind, .embedOnlyVideo(platform: nil, videoID: nil))
        XCTAssertEqual(unknownProvider.playbackKind, .embedOnlyVideo(platform: "unknown", videoID: nil))
        XCTAssertFalse(vimeo.hasPlayableMedia)
        XCTAssertFalse(providerless.hasPlayableMedia)
        XCTAssertFalse(unknownProvider.hasPlayableMedia)
    }

    func testEffectiveAudioMIMETypeTakesPrecedenceOverProviderVideoMetadataWithoutDirectURL() throws {
        let item = try decoder.decode(RssFeedItem.self, from: Data(#"""
        {
          "id": "item-audio-mime", "rss_feed_id": "feed-1", "title": "Audio",
          "link": "https://example.com/audio", "published_at": "2026-01-01T00:00:00Z",
          "video_id": "abc123", "video_platform": "youtube",
          "media_content": { "url": null, "type": " AUDIO/MPEG ", "medium": null, "duration": null }
        }
        """#.utf8))

        XCTAssertEqual(item.playbackKind, .externalAudio(url: "https://example.com/audio"))
        XCTAssertFalse(item.hasPlayableMedia)
    }

    func testMediaContentMediumTakesPrecedenceOverConflictingAudioMIMEType() throws {
        let item = try decoder.decode(RssFeedItem.self, from: Data(#"""
        {
          "id": "item-video-medium", "rss_feed_id": "feed-1", "title": "Video",
          "link": "https://example.com/video", "published_at": "2026-01-01T00:00:00Z",
          "video_id": "abc123", "video_platform": "youtube",
          "media_content": { "url": null, "type": "audio/mpeg", "medium": "video", "duration": null }
        }
        """#.utf8))

        XCTAssertEqual(item.mediaType, "video")
        XCTAssertEqual(item.playbackKind, .embedOnlyVideo(platform: "youtube", videoID: "abc123"))
        XCTAssertFalse(item.hasPlayableMedia)
    }

    func testEffectiveVideoMIMETypeWithoutDirectURLIsEmbedOnly() throws {
        let item = try decoder.decode(RssFeedItem.self, from: Data(#"""
        {
          "id": "item-video-mime", "rss_feed_id": "feed-1", "title": "Video",
          "link": "https://example.com/video", "published_at": "2026-01-01T00:00:00Z",
          "media_content": { "url": null, "type": " VIDEO/MP4 ", "medium": null, "duration": null }
        }
        """#.utf8))

        XCTAssertEqual(item.mediaType, " VIDEO/MP4 ")
        XCTAssertEqual(item.playbackKind, .embedOnlyVideo(platform: nil, videoID: nil))
        XCTAssertFalse(item.hasPlayableMedia)
    }

    func testPlaybackKindCoversUnavailableAudioDirectVideoAndFallbackThumbnail() {
        let audioWithoutURL = RssFeedItem(
            id: "audio-missing",
            rssFeedId: "feed-1",
            title: "Audio",
            description: nil,
            content: nil,
            link: "https://example.com/audio",
            publishedAt: Date(timeIntervalSince1970: 0),
            creator: nil,
            categories: [],
            rssFeed: .init(
                id: "feed-1",
                title: "Podcast",
                feedType: "podcast",
                rssFeedUrl: .init(url: "https://example.com/rss"),
                hostname: nil,
                topic: nil,
                publisherType: nil,
                podcastShow: .init(coverArtUrl: "https://example.com/show.jpg")
            ),
            mediaContent: nil,
            mediaType: "audio"
        )
        let directVideo = RssFeedItem(
            id: "video-direct",
            rssFeedId: "feed-1",
            title: "Video",
            description: nil,
            content: nil,
            link: "https://example.com/video",
            publishedAt: Date(timeIntervalSince1970: 0),
            creator: nil,
            categories: [],
            mediaContent: .init(
                url: "https://example.com/video.mp4",
                type: "video/mp4",
                medium: "video",
                duration: 120
            ),
            mediaType: "video",
            videoID: "youtube-123",
            videoPlatform: "youtube"
        )

        XCTAssertEqual(audioWithoutURL.playbackKind, .externalAudio(url: "https://example.com/audio"))
        XCTAssertFalse(audioWithoutURL.hasPlayableMedia)
        XCTAssertEqual(audioWithoutURL.displayThumbnailURLString, "https://example.com/show.jpg")
        XCTAssertEqual(directVideo.playbackKind, .directVideo(url: "https://example.com/video.mp4"))
        XCTAssertTrue(directVideo.hasPlayableMedia)
    }

    func testMissingFlatAndEmbeddedFeedIdThrows() {
        let json = Data(
            #"""
            {
              "id": "item-missing-feed",
              "title": "Broken item",
              "published_at": "2026-01-01T00:00:00Z",
              "categories": []
            }
            """#
            .utf8
        )

        XCTAssertThrowsError(try decoder.decode(RssFeedItem.self, from: json)) { error in
            guard case DecodingError.keyNotFound = error else {
                return XCTFail("Expected keyNotFound, got \(error)")
            }
        }
    }

    func testEmptyVideoIdentifiersWithoutVideoMetadataRemainUnavailable() throws {
        let json = Data(
            #"""
            {
              "id": "item-empty-video",
              "rss_feed_id": "feed-1",
              "title": "Video",
              "link": null,
              "published_at": "2026-01-01T00:00:00Z",
              "media_type": null,
              "video_id": "   ",
              "video_platform": ""
            }
            """#
            .utf8
        )

        let item = try decoder.decode(RssFeedItem.self, from: json)
        XCTAssertEqual(item.playbackKind, .unavailable)
    }

    func testPartialPodcastSidecarPreservesCoverArtFallback() throws {
        let json = Data(
            #"""
            {
              "id": "item-cover-only",
              "rss_feed": {
                "id": "feed-1",
                "title": null,
                "feed_type": null,
                "rss_feed_url": null,
                "podcast_show": {
                  "cover_art_url": "https://images.example.com/sideload/show-cover.jpg"
                }
              },
              "title": "Episode",
              "published_at": "2026-01-01T00:00:00Z",
              "media_type": "audio"
            }
            """#
            .utf8
        )

        let item = try decoder.decode(RssFeedItem.self, from: json)
        XCTAssertEqual(item.displayThumbnailURLString, "https://images.example.com/sideload/show-cover.jpg")
    }
}
