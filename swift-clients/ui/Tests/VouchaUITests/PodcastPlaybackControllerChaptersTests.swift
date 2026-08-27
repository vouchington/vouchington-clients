import Foundation
import VouchaAPI
import VouchaAuth
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class PodcastPlaybackControllerChaptersTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    private func makeClient() -> APIClient {
        APIClient(
            config: AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key"),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
    }

    private func makeSignedInSessionManager() async throws -> SessionManager {
        let client = makeClient()
        let sessionManager = SessionManager(client: client, cookieStorage: HTTPCookieStorage())
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(
                username: "listener",
                emailAddress: "listener@example.com",
                membershipPlan: nil,
                verificationStatus: nil
            ),
            200
        )
        _ = await sessionManager.refresh()
        return sessionManager
    }

    private func makeAudioItem(id: String = "item-1") -> RssFeedItem {
        RssFeedItem(
            id: id,
            rssFeedId: "feed-1",
            title: "Episode \(id)",
            description: "Episode summary",
            content: nil,
            link: "https://example.com/\(id)",
            url: .init(url: "https://example.com/\(id)"),
            publishedAt: Date(timeIntervalSince1970: 0),
            creator: "Host",
            categories: [],
            data: .init(
                title: "Episode \(id)",
                link: "https://example.com/\(id)",
                mediaType: "audio",
                enclosureURL: "https://example.com/\(id).mp3",
                enclosureType: "audio/mpeg",
                durationSeconds: 1_800,
                thumbnailURL: "https://example.com/\(id).jpg",
                videoID: nil,
                videoPlatform: nil
            ),
            rssFeed: .init(
                id: "feed-1",
                title: "Example Podcast",
                feedType: "podcast",
                rssFeedUrl: .init(url: "https://example.com/rss"),
                hostname: nil,
                topic: nil,
                publisherType: nil,
                podcastShow: nil
            ),
            mediaContent: .init(
                url: "https://example.com/\(id).mp3",
                type: "audio/mpeg",
                medium: "audio",
                duration: 1_800
            ),
            thumbnailURL: "https://example.com/\(id).jpg",
            mediaType: "audio",
            enclosureURL: "https://example.com/\(id).mp3",
            enclosureType: "audio/mpeg",
            durationSeconds: 1_800,
            videoID: nil,
            videoPlatform: nil
        )
    }

    private func waitForSuspendedResponse(path: String) async throws {
        for _ in 0 ..< 100 {
            if CannedFeedURLProtocol.hasSuspendedResponse(path: path) {
                return
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        XCTFail("Expected suspended response for \(path)")
    }

    private func waitForLoadedChapters(_ controller: PodcastPlaybackController, path: String) async throws {
        for _ in 0 ..< 100 {
            if CannedFeedURLProtocol.hasCapturedRequest(path: path), !controller.chapters.isEmpty {
                return
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        XCTFail("Expected loaded chapters for \(path)")
    }

    func testAudioPlaybackLoadsVisibleChapters() async throws {
        let controller = PodcastPlaybackController(
            client: makeClient(),
            sessionManager: SessionManager(client: makeClient(), cookieStorage: HTTPCookieStorage())
        )
        CannedFeedURLProtocol.handlers["/api/v1/podcast-episodes/item-1/chapters"] = (
            Data(
                #"""
                {
                  "chapters": [
                    {
                      "start_seconds": 0,
                      "end_seconds": 45,
                      "title": "Intro",
                      "url": null,
                      "image_url": null,
                      "is_visible": true
                    },
                    {
                      "start_seconds": 45,
                      "end_seconds": 90,
                      "title": "Hidden",
                      "url": null,
                      "image_url": null,
                      "is_visible": false
                    }
                  ]
                }
                """#
                .utf8
            ),
            200
        )

        await controller.togglePlayback(for: makeAudioItem())
        try await waitForLoadedChapters(controller, path: "/api/v1/podcast-episodes/item-1/chapters")

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/podcast-episodes/item-1/chapters"
        ])
        XCTAssertEqual(controller.chapters.count, 1)
        XCTAssertEqual(controller.chapters.first?.title, "Intro")
        XCTAssertEqual(try XCTUnwrap(controller.chapters.first?.startSeconds), 0, accuracy: 0.01)
    }

    func testStaleChapterResponseDoesNotOverrideNewerEpisodeChapters() async throws {
        let controller = PodcastPlaybackController(
            client: makeClient(),
            sessionManager: SessionManager(client: makeClient(), cookieStorage: HTTPCookieStorage())
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/podcast-episodes/item-1/chapters"] = [
            (
                Data(
                    #"""
                    {
                      "chapters": [
                        {
                          "start_seconds": 0,
                          "end_seconds": 60,
                          "title": "First",
                          "url": null,
                          "image_url": null,
                          "is_visible": true
                        }
                      ]
                    }
                    """#
                    .utf8
                ),
                200,
                0.2
            )
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/podcast-episodes/item-2/chapters"] = [
            (
                Data(
                    #"""
                    {
                      "chapters": [
                        {
                          "start_seconds": 0,
                          "end_seconds": 30,
                          "title": "Second",
                          "url": null,
                          "image_url": null,
                          "is_visible": true
                        }
                      ]
                    }
                    """#
                    .utf8
                ),
                200,
                0.05
            )
        ]
        let item1 = makeAudioItem(id: "item-1")
        let item2 = makeAudioItem(id: "item-2")

        let firstStart = Task { @MainActor in
            await controller.togglePlayback(for: item1)
        }
        await Task.yield()
        let secondStart = Task { @MainActor in
            await controller.togglePlayback(for: item2)
        }

        await firstStart.value
        await secondStart.value
        try await Task.sleep(nanoseconds: 300_000_000)

        XCTAssertEqual(controller.currentItem?.id, "item-2")
        XCTAssertEqual(controller.chapters.count, 1)
        XCTAssertEqual(controller.chapters.first?.title, "Second")
    }

    func testStartingANewEpisodeClearsStaleChaptersBeforeResumeLookupFinishes() async throws {
        let sessionManager = try await makeSignedInSessionManager()
        let controller = PodcastPlaybackController(
            client: makeClient(),
            sessionManager: sessionManager
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/podcast-episodes/item-2/playback-position"] = [
            (
                Data(
                    #"""
                    {"playback_position":{"position_seconds":0,"completed_at":null}}
                    """#
                    .utf8
                ),
                200,
                0.2
            )
        ]
        controller.chapters = [
            PodcastChapter(
                startSeconds: 0,
                endSeconds: 30,
                title: "Stale",
                url: nil,
                imageURL: nil,
                isVisible: true
            )
        ]

        let item2 = makeAudioItem(id: "item-2")
        let start = Task { @MainActor in
            await controller.start(item2)
        }

        try await Task.sleep(nanoseconds: 50_000_000)

        XCTAssertEqual(controller.currentItem?.id, "item-2")
        XCTAssertTrue(controller.isLoading)
        XCTAssertTrue(controller.chapters.isEmpty)

        await start.value
    }

    func testStartingANewEpisodeInvalidatesStaleChapterLoadBeforeFlushingPreviousProgress() async throws {
        let client = makeClient()
        let sessionManager = try await makeSignedInSessionManager()
        let controller = PodcastPlaybackController(client: client, sessionManager: sessionManager)
        let item1 = makeAudioItem(id: "item-1")
        let item2 = makeAudioItem(id: "item-2")
        let item1ChaptersPath = "/api/v1/podcast-episodes/item-1/chapters"
        CannedFeedURLProtocol.handlers[item1ChaptersPath] = (
            Data(
                #"""
                {
                  "chapters": [
                    {
                      "start_seconds": 0,
                      "end_seconds": 30,
                      "title": "Stale",
                      "url": null,
                      "image_url": null,
                      "is_visible": true
                    }
                  ]
                }
                """#
                .utf8
            ),
            200
        )
        CannedFeedURLProtocol.suspendResponse(path: item1ChaptersPath)
        CannedFeedURLProtocol.queuedHandlers["/api/v1/podcast-episodes/item-1/playback-position"] = [
            (Data("{}".utf8), 200, 0.2)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/podcast-episodes/item-2/playback-position"] = (
            Data(#"{"playback_position":{"position_seconds":0,"completed_at":null}}"#.utf8),
            200
        )

        controller.currentItem = item1
        controller.currentTimeSeconds = 123.4
        controller.currentItemStartedPlayback = true
        controller.startChapterLoad(for: item1)
        try await waitForSuspendedResponse(path: item1ChaptersPath)

        let start = Task { @MainActor in
            await controller.start(item2)
        }
        await Task.yield()
        CannedFeedURLProtocol.releaseResponse(path: item1ChaptersPath)
        try await Task.sleep(nanoseconds: 50_000_000)

        XCTAssertTrue(controller.chapters.isEmpty)

        await start.value
    }
}
