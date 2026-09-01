import Foundation
@testable import VouchaModels
import XCTest

final class UrlEmbedTests: XCTestCase {
    func testDecodesRawNestedEmbedAndMetadataWithoutLoss() throws {
        let embed = try JSONDecoder.vouchaFixtureDecoder.decode(UrlEmbed.self, from: Data(#"""
        {
          "source_url": "https://www.youtube.com/watch?v=video-123",
          "title": "oEmbed title", "description": "Safe description", "provider_name": "Safe provider",
          "thumbnail_url": "https://safe.example/image.jpg",
          "player_url": "https://www.youtube-nocookie.com/embed/video-123",
          "embed_metadata": {
            "player": { "url": "https://www.youtube-nocookie.com/embed/video-123" },
            "large_id": 9007199254740993
          },
          "meta_tags": {
            "og:title": "OG title",
            "nested": [true, { "key": "value" }],
            "unsigned_id": 18446744073709551615
          },
          "embed_oembed_url": "https://www.youtube.com/oembed", "embed_oembed_resolved_at": "2026-01-02T00:01:00Z"
        }
        """#.utf8))

        XCTAssertEqual(embed.title, "oEmbed title")
        XCTAssertEqual(embed.description, "Safe description")
        XCTAssertEqual(embed.providerName, "Safe provider")
        XCTAssertEqual(embed.thumbnailUrl, "https://safe.example/image.jpg")
        guard case let .object(metadata)? = embed.embedMetadata,
              case let .object(player)? = metadata["player"],
              case let .string(playerURL)? = player["url"]
        else {
            return XCTFail("Expected raw oEmbed metadata to retain its nested player value")
        }
        XCTAssertEqual(playerURL, "https://www.youtube-nocookie.com/embed/video-123")
        guard case let .array(nested)? = embed.metaTags?["nested"],
              case .bool(true) = nested.first
        else {
            return XCTFail("Expected raw meta tags to retain nested JSON values")
        }
        XCTAssertNotNil(embed.embedOembedResolvedAt)

        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let encodedData = try encoder.encode(embed)
        let encoded = try XCTUnwrap(JSONSerialization.jsonObject(with: encodedData) as? [String: Any])
        XCTAssertEqual(encoded["description"] as? String, "Safe description")
        XCTAssertEqual(encoded["provider_name"] as? String, "Safe provider")
        let raw = String(decoding: encodedData, as: UTF8.self)
        XCTAssertTrue(raw.contains("9007199254740993"))
        XCTAssertTrue(raw.contains("18446744073709551615"))
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

    func testUsesSafeProjectionsThenRawTagsForPreviewText() throws {
        let safe = try JSONDecoder.vouchaFixtureDecoder.decode(UrlEmbed.self, from: Data(#"""
        {
          "title": "Safe title", "description": "safe description", "provider_name": "Safe provider",
          "embed_metadata": { "title": "Raw title", "description": "raw description", "provider": { "name": "Raw provider" } },
          "meta_tags": { "og:title": "OG title", "og:description": "OG description", "og:site_name": "OG provider" }
        }
        """#.utf8))
        let og = try JSONDecoder.vouchaFixtureDecoder.decode(UrlEmbed.self, from: Data(#"""
        {
          "source_url": "https://fallback.example/article",
          "embed_metadata": { "title": "Raw title", "description": "raw description", "provider": { "name": "Raw provider" } },
          "meta_tags": { "OG:TITLE": "og", "og:description": "og description", "og:site_name": "OG provider", "twitter:title": "twitter" }
        }
        """#.utf8))
        let twitter = try embed(
            metadata: #"{ "title": "raw title", "description": "raw description" }"#,
            tags: #"{ "twitter:title": "twitter", "Twitter:Description": "twitter description" }"#,
            title: ""
        )
        let rawMetadataOnly = try embed(
            metadata: #"{ "title": "raw title", "description": "raw description", "provider": { "name": "Raw provider" } }"#,
            tags: #"{}"#,
            title: ""
        )
        let flattened = try embed(metadata: #"{}"#, tags: #"{}"#, title: "flattened")

        XCTAssertEqual(safe.previewTitle, "Safe title")
        XCTAssertEqual(safe.previewDescription, "safe description")
        XCTAssertEqual(safe.previewProvider, "Safe provider")
        XCTAssertEqual(og.previewTitle, "og")
        XCTAssertEqual(og.previewDescription, "og description")
        XCTAssertEqual(og.previewProvider, "OG provider")
        XCTAssertEqual(twitter.previewTitle, "twitter")
        XCTAssertEqual(twitter.previewDescription, "twitter description")
        XCTAssertNil(twitter.previewProvider)
        XCTAssertNil(rawMetadataOnly.previewTitle)
        XCTAssertNil(rawMetadataOnly.previewDescription)
        XCTAssertNil(rawMetadataOnly.previewProvider)
        XCTAssertEqual(flattened.previewTitle, "flattened")
        XCTAssertNil(flattened.previewDescription)
    }

    func testUsesRawOgSiteNameThenSourceHostForProvider() throws {
        let og = try JSONDecoder.vouchaFixtureDecoder.decode(UrlEmbed.self, from: Data(#"""
        { "source_url": "https://fallback.example/article", "embed_metadata": { "provider": { "name": "Normalized" } }, "meta_tags": { "og:site_name": "OG site" } }
        """#.utf8))
        let fallback = try JSONDecoder.vouchaFixtureDecoder.decode(UrlEmbed.self, from: Data(#"""
        { "source_url": "https://fallback.example/article", "embed_metadata": { "provider": { "name": "Raw provider" } }, "meta_tags": {} }
        """#.utf8))

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
        let embed = try JSONDecoder.vouchaFixtureDecoder.decode(UrlEmbed.self, from: Data(#"""
        {
          "embed_metadata": { "high_precision": \#(raw) },
          "meta_tags": { "high_precision": \#(raw) }
        }
        """#.utf8))
        let encoded = try JSONEncoder().encode(embed)

        let encodedText = String(decoding: encoded, as: UTF8.self)
        XCTAssertEqual(encodedText.components(separatedBy: raw).count, 3)
    }

    func testAllowsOnlyHttpsSourceURLs() throws {
        XCTAssertTrue(try UrlEmbed.isAllowedSourceURL(XCTUnwrap(URL(string: "https://example.com/article"))))
        XCTAssertFalse(try UrlEmbed.isAllowedSourceURL(XCTUnwrap(URL(string: "http://example.com/article"))))
        XCTAssertFalse(try UrlEmbed.isAllowedSourceURL(XCTUnwrap(URL(string: "https://user@example.com/article"))))
        XCTAssertFalse(try UrlEmbed.isAllowedSourceURL(XCTUnwrap(URL(string: "https://example.com:443/article"))))
        for value in ["https:///path", "https://?q=x", "https://#fragment"] {
            XCTAssertFalse(URL(string: value).map(UrlEmbed.isAllowedSourceURL) ?? false)
        }
    }

    private func embed(metadata: String, tags: String, title: String) throws -> UrlEmbed {
        try JSONDecoder.vouchaFixtureDecoder.decode(UrlEmbed.self, from: Data(#"""
        { "title": "\#(title)", "thumbnail_url": "https://safe.example/image.jpg", "embed_metadata": \#(
            metadata
        ), "meta_tags": \#(tags) }
        """#.utf8))
    }
}
