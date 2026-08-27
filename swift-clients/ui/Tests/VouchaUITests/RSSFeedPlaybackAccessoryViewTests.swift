import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class RSSFeedPlaybackAccessoryViewTests: XCTestCase {
    private func makeAudioItem() -> RssFeedItem {
        RssFeedItem(
            id: "item-1",
            rssFeedId: "feed-1",
            title: "Episode 1",
            description: nil,
            content: nil,
            link: "https://example.com/item-1",
            url: .init(url: "https://example.com/item-1"),
            publishedAt: Date(timeIntervalSince1970: 0),
            creator: nil,
            categories: [],
            data: .init(
                title: "Episode 1",
                link: "https://example.com/item-1",
                mediaType: "audio",
                enclosureURL: "https://example.com/item-1.mp3",
                enclosureType: "audio/mpeg",
                durationSeconds: 600,
                thumbnailURL: nil,
                videoID: nil,
                videoPlatform: nil
            ),
            rssFeed: nil,
            mediaContent: .init(
                url: "https://example.com/item-1.mp3",
                type: "audio/mpeg",
                medium: "audio",
                duration: 600
            ),
            mediaType: "audio",
            enclosureURL: "https://example.com/item-1.mp3",
            enclosureType: "audio/mpeg",
            durationSeconds: 600
        )
    }

    private func makeVideoItem(
        id: String = "item-2",
        mediaURL: String? = nil,
        link: String? = "https://youtube.com/watch?v=abc123",
        videoID: String? = "abc123",
        videoPlatform: String? = "youtube"
    ) -> RssFeedItem {
        RssFeedItem(
            id: id,
            rssFeedId: "feed-2",
            title: "Video 2",
            description: nil,
            content: nil,
            link: link,
            url: link.map { .init(url: $0) },
            publishedAt: Date(timeIntervalSince1970: 0),
            creator: nil,
            categories: [],
            data: .init(
                title: "Video 2",
                link: link,
                mediaType: "video",
                enclosureURL: mediaURL,
                enclosureType: mediaURL == nil ? nil : "video/mp4",
                durationSeconds: nil,
                thumbnailURL: nil,
                videoID: videoID,
                videoPlatform: videoPlatform
            ),
            rssFeed: nil,
            mediaContent: mediaURL.map {
                .init(url: $0, type: "video/mp4", medium: "video", duration: 120)
            },
            mediaType: "video",
            enclosureURL: mediaURL,
            enclosureType: mediaURL == nil ? nil : "video/mp4",
            videoID: videoID,
            videoPlatform: videoPlatform
        )
    }

    func testRendersPlayButtonForPlayableAudio() throws {
        let sut = RSSFeedPlaybackAccessoryView(
            item: makeAudioItem(),
            isCurrentItem: false,
            isPlaying: false,
            onPlayPauseTap: {}
        )

        _ = try sut.inspect().find(button: "Play")
    }

    func testRendersNoninteractiveUnavailableLabelForEmbedOnlyVideo() throws {
        let sut = RSSFeedPlaybackAccessoryView(
            item: makeVideoItem(),
            isCurrentItem: false,
            isPlaying: false,
            onPlayPauseTap: {}
        )

        XCTAssertEqual(try sut.inspect().find(text: "Video unavailable").string(), "Video unavailable")
        XCTAssertEqual(try sut.inspect().find(text: "Open source").string(), "Open source")
        XCTAssertEqual(try sut.inspect().findAll(ViewType.Link.self).count, 1)
        XCTAssertTrue(try sut.inspect().findAll(ViewType.Button.self).isEmpty)
    }

    func testRendersPauseButtonForCurrentDirectVideo() throws {
        let sut = RSSFeedPlaybackAccessoryView(
            item: makeVideoItem(mediaURL: "https://example.com/video.mp4"),
            isCurrentItem: true,
            isPlaying: true,
            onPlayPauseTap: {}
        )

        _ = try sut.inspect().find(button: "Pause video")
    }

    func testRendersUnavailableLabelForProviderlessEmbedOnlyVideo() throws {
        let sut = RSSFeedPlaybackAccessoryView(
            item: makeVideoItem(link: nil, videoID: nil, videoPlatform: nil),
            isCurrentItem: false,
            isPlaying: false,
            onPlayPauseTap: {}
        )

        XCTAssertEqual(try sut.inspect().find(text: "Video unavailable").string(), "Video unavailable")
        XCTAssertTrue(try sut.inspect().findAll(ViewType.Link.self).isEmpty)
        XCTAssertTrue(try sut.inspect().findAll(ViewType.Button.self).isEmpty)
    }

    func testRendersExternalFallbackLinkForAudioWithoutDirectPlayback() throws {
        let item = RssFeedItem(
            id: "item-audio-link-only",
            rssFeedId: "feed-1",
            title: "Episode",
            description: nil,
            content: nil,
            link: "https://example.com/item-audio-link-only",
            publishedAt: Date(timeIntervalSince1970: 0),
            creator: nil,
            categories: [],
            mediaContent: nil,
            mediaType: "audio"
        )
        let sut = RSSFeedPlaybackAccessoryView(
            item: item,
            isCurrentItem: false,
            isPlaying: false,
            onPlayPauseTap: {}
        )

        XCTAssertEqual(try sut.inspect().find(text: "Open episode").string(), "Open episode")
    }
}
