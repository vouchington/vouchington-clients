import Foundation
import ViewInspector
import VouchaAPI
import VouchaAuth
import VouchaCore
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class PodcastMiniPlayerViewTests: XCTestCase {
    private func makeController() -> PodcastPlaybackController {
        let client = APIClient(
            config: AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key"),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        return PodcastPlaybackController(
            client: client,
            sessionManager: SessionManager(client: client, cookieStorage: HTTPCookieStorage())
        )
    }

    private func makeItem(
        id: String = "item-1",
        mediaType: String = "audio",
        mediaURL: String? = "https://example.com/item.mp3",
        videoID: String? = nil,
        videoPlatform: String? = nil
    ) -> RssFeedItem {
        RssFeedItem(
            id: id,
            rssFeedId: "feed-1",
            title: "Episode \(id)",
            description: nil,
            content: nil,
            link: "https://example.com/\(id)",
            url: .init(url: "https://example.com/\(id)"),
            publishedAt: Date(timeIntervalSince1970: 0),
            creator: "Host",
            categories: [],
            rssFeed: .init(
                id: "feed-1",
                title: "Example Feed",
                feedType: mediaType == "audio" ? "podcast" : "video",
                rssFeedUrl: .init(url: "https://example.com/rss"),
                hostname: nil,
                topic: nil,
                publisherType: nil,
                podcastShow: nil
            ),
            mediaContent: mediaURL.map {
                .init(
                    url: $0,
                    type: mediaType == "audio" ? "audio/mpeg" : "video/mp4",
                    medium: mediaType,
                    duration: 120
                )
            },
            thumbnailURL: "/sideload/\(id).jpg",
            mediaType: mediaType,
            enclosureURL: mediaURL,
            enclosureType: mediaURL.map { _ in mediaType == "audio" ? "audio/mpeg" : "video/mp4" },
            videoID: videoID,
            videoPlatform: videoPlatform
        )
    }

    private func makeChapter(
        startSeconds: Double,
        endSeconds: Double? = nil,
        title: String,
        isVisible: Bool = true
    ) -> PodcastChapter {
        PodcastChapter(
            startSeconds: startSeconds,
            endSeconds: endSeconds,
            title: title,
            url: nil,
            imageURL: nil,
            isVisible: isVisible
        )
    }

    func testEmptyMiniPlayerRendersNoContent() throws {
        let sut = PodcastMiniPlayerView(controller: makeController())

        XCTAssertTrue(try sut.inspect().findAll(ViewType.Text.self).isEmpty)
    }

    func testAudioMiniPlayerRendersArtworkProgressAndSpeedControls() throws {
        let controller = makeController()
        controller.currentItem = makeItem()
        controller.currentTimeSeconds = 65
        controller.durationSeconds = 125
        controller.setPlaybackRate(1.5)
        let sut = PodcastMiniPlayerView(controller: controller)

        XCTAssertEqual(try sut.inspect().find(text: "Episode item-1").string(), "Episode item-1")
        XCTAssertEqual(try sut.inspect().find(text: "Example Feed").string(), "Example Feed")
        XCTAssertEqual(try sut.inspect().find(text: "1:05").string(), "1:05")
        XCTAssertEqual(try sut.inspect().find(text: "2:05").string(), "2:05")
        XCTAssertEqual(try sut.inspect().find(button: "1.5×").labelView().text().string(), "1.5×")
        let artwork = try sut.inspect().find(ViewType.View<AsyncImageView>.self).actualView()
        XCTAssertEqual(artwork.resolvedURLString, "http://localhost:2999/sideload/item-1.jpg")

        try sut.inspect().find(button: "1×").tap()
        XCTAssertEqual(controller.playbackRate, 1, accuracy: 0.01)
        try sut.inspect().find(button: "1.5×").tap()
        XCTAssertEqual(controller.playbackRate, 1.5, accuracy: 0.01)
    }

    func testEmbedOnlyVideoMiniPlayerRendersNoninteractiveUnavailableLabel() throws {
        let controller = makeController()
        controller.currentItem = makeItem(
            mediaType: "video",
            mediaURL: nil,
            videoID: "abc123",
            videoPlatform: "youtube"
        )
        let sut = PodcastMiniPlayerView(controller: controller)

        XCTAssertEqual(try sut.inspect().find(text: "Video unavailable").string(), "Video unavailable")
        XCTAssertEqual(try sut.inspect().find(text: "Open source").string(), "Open source")
        XCTAssertEqual(try sut.inspect().findAll(ViewType.Link.self).count, 1)
        XCTAssertTrue(try sut.inspect().findAll(ViewType.Button.self).isEmpty)
        XCTAssertTrue(try sut.inspect().findAll(ViewType.Slider.self).isEmpty)
        XCTAssertTrue(try sut.inspect().findAll(ViewType.VideoPlayer.self).isEmpty)
    }

    func testLinkOnlyAudioMiniPlayerRetainsExternalFallback() throws {
        let controller = makeController()
        controller.currentItem = makeItem(mediaURL: nil)
        let sut = PodcastMiniPlayerView(controller: controller)

        XCTAssertEqual(try sut.inspect().find(text: "Open source").string(), "Open source")
        XCTAssertTrue(try sut.inspect().findAll(ViewType.Button.self).isEmpty)
        XCTAssertTrue(try sut.inspect().findAll(ViewType.Slider.self).isEmpty)
        XCTAssertTrue(try sut.inspect().findAll(ViewType.VideoPlayer.self).isEmpty)
    }

    func testDirectVideoMiniPlayerOmitsExternalLink() throws {
        let controller = makeController()
        controller.currentItem = makeItem(mediaType: "video", mediaURL: "https://example.com/video.mp4")
        let sut = PodcastMiniPlayerView(controller: controller)

        XCTAssertTrue(try sut.inspect().findAll(ViewType.Link.self).isEmpty)
    }

    func testChapterButtonsRenderAndSeekToTheSelectedChapter() throws {
        let controller = makeController()
        let item = makeItem()
        controller.currentItem = item
        controller.chapters = [
            makeChapter(startSeconds: 0, endSeconds: 60, title: "Intro"),
            makeChapter(startSeconds: 120, endSeconds: nil, title: "Wrap")
        ]
        let sut = PodcastMiniPlayerView(controller: controller)

        XCTAssertNoThrow(try sut.inspect().find(button: "Intro"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Wrap"))

        try sut.inspect().find(button: "Wrap").tap()

        XCTAssertEqual(controller.currentTimeSeconds, 120, accuracy: 0.01)
    }
}
