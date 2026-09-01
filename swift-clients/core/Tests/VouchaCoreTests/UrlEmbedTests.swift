import Foundation
@testable import VouchaModels
import XCTest

final class UrlEmbedTests: XCTestCase {
    func testDecodesRawNestedEmbedAndMetadataWithoutLoss() throws {
        let embed = try JSONDecoder.vouchaFixtureDecoder.decode(UrlEmbed.self, from: Data(#"""
        {
          "source_url": "https://www.youtube.com/watch?v=video-123",
          "title": "oEmbed title", "thumbnail_url": "https://safe.example/image.jpg",
          "player_url": "https://www.youtube-nocookie.com/embed/video-123",
          "embed_metadata": { "player": { "url": "https://www.youtube-nocookie.com/embed/video-123" } },
          "meta_tags": { "og:title": "OG title", "nested": [true, { "key": "value" }] },
          "embed_oembed_url": "https://www.youtube.com/oembed", "embed_oembed_resolved_at": "2026-01-02T00:01:00Z"
        }
        """#.utf8))

        XCTAssertEqual(embed.title, "oEmbed title")
        XCTAssertEqual(embed.thumbnailUrl, "https://safe.example/image.jpg")
        XCTAssertEqual(
            embed.embedMetadata,
            .object(["player": .object(["url": .string("https://www.youtube-nocookie.com/embed/video-123")])])
        )
        XCTAssertEqual(embed.metaTags?["nested"], .array([.bool(true), .object(["key": .string("value")])]))
        XCTAssertNotNil(embed.embedOembedResolvedAt)
    }

    func testAcceptsOnlyExactApprovedProviderPlayerURLs() throws {
        let allowed = [
            "https://www.youtube-nocookie.com/embed/video-123",
            "https://player.vimeo.com/video/123"
        ]
        let rejected = [
            "http://www.youtube-nocookie.com/embed/video-123",
            "https://www.youtube-nocookie.com:443/embed/video-123",
            "https://user@www.youtube-nocookie.com/embed/video-123",
            "https://WWW.youtube-nocookie.com/embed/video-123",
            "https://www.youtube.com/embed/video-123",
            "https://player.vimeo.com/channels/staffpicks/123"
        ]

        XCTAssertTrue(try allowed.allSatisfy { try UrlEmbed.isAllowedProviderURL(XCTUnwrap(URL(string: $0))) })
        XCTAssertTrue(try rejected.allSatisfy { try !UrlEmbed.isAllowedProviderURL(XCTUnwrap(URL(string: $0))) })
    }

    func testUsesNormalizedThenCaseInsensitiveOgThenTwitterThenFlattenedTitle() throws {
        let normalized = try embed(
            metadata: #"{ "title": "normalized", "description": "normalized description", "provider": { "name": "YouTube" } }"#,
            tags: #"{ "OG:TITLE": "og", "og:description": "og description", "twitter:title": "twitter" }"#,
            title: "flattened"
        )
        let og = try embed(
            metadata: #"{}"#,
            tags: #"{ "OG:TITLE": "og", "og:description": "og description", "twitter:title": "twitter" }"#,
            title: "flattened"
        )
        let twitter = try embed(
            metadata: #"{}"#,
            tags: #"{ "twitter:title": "twitter", "Twitter:Description": "twitter description" }"#,
            title: "flattened"
        )
        let flattened = try embed(metadata: #"{}"#, tags: #"{}"#, title: "flattened")

        XCTAssertEqual(normalized.previewTitle, "normalized")
        XCTAssertEqual(normalized.previewDescription, "normalized description")
        XCTAssertEqual(normalized.previewProvider, "YouTube")
        XCTAssertEqual(og.previewTitle, "og")
        XCTAssertEqual(og.previewDescription, "og description")
        XCTAssertEqual(twitter.previewTitle, "twitter")
        XCTAssertEqual(twitter.previewDescription, "twitter description")
        XCTAssertEqual(flattened.previewTitle, "flattened")
        XCTAssertNil(flattened.previewDescription)
    }

    func testUsesNormalizedThenOgSiteNameThenSourceHostForProvider() throws {
        let normalized = try JSONDecoder.vouchaFixtureDecoder.decode(UrlEmbed.self, from: Data(#"""
        { "source_url": "https://fallback.example/article", "embed_metadata": { "provider": { "name": "Normalized" } }, "meta_tags": { "og:site_name": "OG site" } }
        """#.utf8))
        let og = try JSONDecoder.vouchaFixtureDecoder.decode(UrlEmbed.self, from: Data(#"""
        { "source_url": "https://fallback.example/article", "embed_metadata": {}, "meta_tags": { "OG:SITE_NAME": "OG site" } }
        """#.utf8))
        let fallback = try JSONDecoder.vouchaFixtureDecoder.decode(UrlEmbed.self, from: Data(#"""
        { "source_url": "https://fallback.example/article", "embed_metadata": {}, "meta_tags": {} }
        """#.utf8))

        XCTAssertEqual(normalized.previewProvider, "Normalized")
        XCTAssertEqual(og.previewProvider, "OG site")
        XCTAssertEqual(fallback.previewProvider, "fallback.example")
    }

    func testDecodesCompleteAudioAndVideoSidecarFields() throws {
        let video = try JSONDecoder.vouchaFixtureDecoder.decode(UrlEmbed.self, from: Data(#"""
        {
          "rss_feed_item_id": "video-1", "media_type": "video", "video_id": "video-123",
          "video_platform": "youtube", "player_width": 640, "player_height": 360,
          "enclosure_url": "https://cdn.example/video.mp4", "enclosure_type": "video/mp4",
          "duration_seconds": 120, "show_id": "show-1", "show_title": "Show",
          "show_topic_slug": "show", "show_topic_type": "podcast"
        }
        """#.utf8))
        let audio = try JSONDecoder.vouchaFixtureDecoder.decode(UrlEmbed.self, from: Data(#"""
        { "rss_feed_item_id": "audio-1", "media_type": "audio", "duration_seconds": 42,
          "enclosure_url": "https://cdn.example/audio.mp3", "enclosure_type": "audio/mpeg" }
        """#.utf8))

        XCTAssertEqual(video.videoPlatform, "youtube")
        XCTAssertEqual(video.playerWidth, 640)
        XCTAssertEqual(video.showTopicType, "podcast")
        XCTAssertEqual(audio.mediaType, "audio")
        XCTAssertEqual(audio.enclosureType, "audio/mpeg")
    }

    func testPreservesFractionalFlattenedMediaNumbers() throws {
        let embed = try JSONDecoder.vouchaFixtureDecoder.decode(UrlEmbed.self, from: Data(#"""
        { "player_width": 640.5, "player_height": 360.25, "duration_seconds": 42.125 }
        """#.utf8))
        let encoded = try JSONEncoder().encode(embed)

        XCTAssertEqual(embed.playerWidth, Decimal(string: "640.5"))
        XCTAssertEqual(embed.playerHeight, Decimal(string: "360.25"))
        XCTAssertEqual(embed.durationSeconds, Decimal(string: "42.125"))
        XCTAssertTrue(String(decoding: encoded, as: UTF8.self).contains("42.125"))
    }

    func testPreservesHighPrecisionRawMetadataNumbers() throws {
        let raw = "1234567890123456789012345678.123456789"
        let decoded = try JSONDecoder.vouchaFixtureDecoder.decode(DecodedJSONValue.self, from: Data(raw.utf8))
        let encoded = try JSONEncoder().encode(decoded)

        XCTAssertEqual(decoded, try .number(XCTUnwrap(Decimal(string: raw))))
        XCTAssertEqual(String(decoding: encoded, as: UTF8.self), raw)
    }

    func testAllowsOnlyHttpsSourceURLs() throws {
        XCTAssertTrue(try UrlEmbed.isAllowedSourceURL(XCTUnwrap(URL(string: "https://example.com/article"))))
        XCTAssertFalse(try UrlEmbed.isAllowedSourceURL(XCTUnwrap(URL(string: "http://example.com/article"))))
        XCTAssertFalse(try UrlEmbed.isAllowedSourceURL(XCTUnwrap(URL(string: "https://user@example.com/article"))))
        XCTAssertFalse(try UrlEmbed.isAllowedSourceURL(XCTUnwrap(URL(string: "https://example.com:443/article"))))
    }

    private func embed(metadata: String, tags: String, title: String) throws -> UrlEmbed {
        try JSONDecoder.vouchaFixtureDecoder.decode(UrlEmbed.self, from: Data(#"""
        { "title": "\#(title)", "thumbnail_url": "https://safe.example/image.jpg", "embed_metadata": \#(
            metadata
        ), "meta_tags": \#(tags) }
        """#.utf8))
    }
}
