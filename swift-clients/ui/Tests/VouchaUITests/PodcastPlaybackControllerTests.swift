import Foundation
import VouchaAPI
import VouchaAuth
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class PodcastPlaybackControllerTests: XCTestCase {
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

    private func makeAudioItem(id: String = "item-1", thumbnailURL: String? = nil) -> RssFeedItem {
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
                thumbnailURL: thumbnailURL ?? "https://example.com/\(id).jpg",
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
            thumbnailURL: thumbnailURL ?? "https://example.com/\(id).jpg",
            mediaType: "audio",
            enclosureURL: "https://example.com/\(id).mp3",
            enclosureType: "audio/mpeg",
            durationSeconds: 1_800,
            videoID: nil,
            videoPlatform: nil
        )
    }

    func testStartsAudioFromSavedPositionForSignedInUsers() async throws {
        let client = makeClient()
        let sessionManager = try await makeSignedInSessionManager()
        CannedFeedURLProtocol.handlers["/api/v1/podcast-episodes/item-1/playback-position"] = (
            Data(
                #"""
                {"playback_position":{"position_seconds":42.5,"completed_at":null}}
                """#
                .utf8
            ),
            200
        )
        let controller = PodcastPlaybackController(client: client, sessionManager: sessionManager)
        let item = makeAudioItem()

        await controller.togglePlayback(for: item)

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/my/identity",
            "/api/v1/podcast-episodes/item-1/playback-position"
        ])
        XCTAssertEqual(controller.currentItem?.id, "item-1")
        XCTAssertEqual(controller.currentTimeSeconds, 42.5, accuracy: 0.01)
        XCTAssertTrue(controller.isPlaying)
    }

    func testRestartsCompletedAudioEpisodesFromZero() async throws {
        let client = makeClient()
        let sessionManager = try await makeSignedInSessionManager()
        CannedFeedURLProtocol.handlers["/api/v1/podcast-episodes/item-1/playback-position"] = (
            Data(
                #"""
                {"playback_position":{"position_seconds":120,"completed_at":"2026-01-01T00:00:00Z"}}
                """#
                .utf8
            ),
            200
        )
        let controller = PodcastPlaybackController(client: client, sessionManager: sessionManager)
        let item = makeAudioItem()

        await controller.togglePlayback(for: item)

        XCTAssertEqual(controller.currentTimeSeconds, 0, accuracy: 0.01)
        XCTAssertTrue(controller.isPlaying)
    }

    func testDoesNotFetchPlaybackPositionWhenSignedOut() async {
        let controller = PodcastPlaybackController(client: makeClient(), sessionManager: SessionManager(
            client: makeClient(),
            cookieStorage: HTTPCookieStorage()
        ))
        let item = makeAudioItem()

        await controller.togglePlayback(for: item)

        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/podcast-episodes/item-1/playback-position" })
    }

    func testCurrentExternalURLFallsBackToTheEpisodeLinkWhenAudioHasNoEnclosure() async throws {
        let sessionManager = try await makeSignedInSessionManager()
        let controller = PodcastPlaybackController(
            client: makeClient(),
            sessionManager: sessionManager
        )
        let item = RssFeedItem(
            id: "item-link-only",
            rssFeedId: "feed-1",
            title: "Episode item-link-only",
            description: nil,
            content: nil,
            link: "https://example.com/item-link-only",
            publishedAt: Date(timeIntervalSince1970: 0),
            creator: "Host",
            categories: [],
            mediaContent: nil,
            mediaType: "audio"
        )
        controller.currentItem = item

        XCTAssertEqual(controller.currentExternalURL, URL(string: "https://example.com/item-link-only"))
    }

    func testCurrentExternalURLIsNilForEmbedOnlyVideo() {
        let controller = PodcastPlaybackController(client: makeClient(), sessionManager: SessionManager(
            client: makeClient(),
            cookieStorage: HTTPCookieStorage()
        ))
        controller.currentItem = RssFeedItem(
            id: "item-embed-only-video",
            rssFeedId: "feed-1",
            title: "Embed-only video",
            description: nil,
            content: nil,
            link: "https://example.com/video",
            publishedAt: Date(timeIntervalSince1970: 0),
            creator: nil,
            categories: [],
            mediaContent: nil,
            mediaType: "video",
            videoID: "abc123",
            videoPlatform: "youtube"
        )

        XCTAssertNil(controller.currentExternalURL)
    }

    func testLateResumeLookupDoesNotOverrideNewerPlaybackStart() async throws {
        let client = makeClient()
        let sessionManager = try await makeSignedInSessionManager()
        CannedFeedURLProtocol.queuedHandlers["/api/v1/podcast-episodes/item-1/playback-position"] = [(
            Data(
                #"""
                {"playback_position":{"position_seconds":42.5,"completed_at":null}}
                """#
                .utf8
            ),
            200,
            0.05
        )]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/podcast-episodes/item-2/playback-position"] = [(
            Data(
                #"""
                {"playback_position":{"position_seconds":0,"completed_at":null}}
                """#
                .utf8
            ),
            200,
            0.2
        )]
        let controller = PodcastPlaybackController(client: client, sessionManager: sessionManager)
        let item1 = makeAudioItem(id: "item-1")
        let item2 = makeAudioItem(id: "item-2")

        let firstStart = Task { @MainActor in
            await controller.togglePlayback(for: item1)
        }
        await Task.yield()
        let secondStart = Task { @MainActor in
            await controller.togglePlayback(for: item2)
        }

        try await Task.sleep(nanoseconds: 100_000_000)

        XCTAssertEqual(controller.currentItem?.id, "item-2")
        XCTAssertEqual(controller.currentTimeSeconds, 0, accuracy: 0.01)
        XCTAssertTrue(controller.isLoading)

        await firstStart.value
        await secondStart.value

        let item1PlaybackWrites = CannedFeedURLProtocol.capturedRequests.filter { request in
            request.url.path == "/api/v1/podcast-episodes/item-1/playback-position"
                && request.method == "PUT"
        }
        XCTAssertTrue(item1PlaybackWrites.isEmpty)
        XCTAssertEqual(controller.currentItem?.id, "item-2")
        XCTAssertEqual(controller.currentTimeSeconds, 0, accuracy: 0.01)
        XCTAssertTrue(controller.isPlaying)
        XCTAssertFalse(controller.isLoading)
    }

    func testCurrentItemControlsAreIgnoredWhileResumeLookupIsLoading() async throws {
        let client = makeClient()
        let sessionManager = try await makeSignedInSessionManager()
        CannedFeedURLProtocol.queuedHandlers["/api/v1/podcast-episodes/item-1/playback-position"] = [(
            Data(
                #"""
                {"playback_position":{"position_seconds":42.5,"completed_at":null}}
                """#
                .utf8
            ),
            200,
            0.2
        )]
        let controller = PodcastPlaybackController(client: client, sessionManager: sessionManager)
        let item = makeAudioItem()

        let start = Task { @MainActor in
            await controller.togglePlayback(for: item)
        }
        await Task.yield()

        await controller.togglePlayback(for: item)
        controller.pause()

        await start.value

        let playbackWrites = CannedFeedURLProtocol.capturedRequests.filter { request in
            request.url.path == "/api/v1/podcast-episodes/item-1/playback-position"
                && request.method == "PUT"
        }
        XCTAssertTrue(playbackWrites.isEmpty)
        XCTAssertEqual(controller.currentTimeSeconds, 42.5, accuracy: 0.01)
        XCTAssertTrue(controller.isPlaying)
    }

    func testSwitchingEpisodesFlushesPreviousPlaybackPositionBeforeLoadingTheNextOne() async throws {
        let client = makeClient()
        let sessionManager = try await makeSignedInSessionManager()
        CannedFeedURLProtocol.handlers["/api/v1/podcast-episodes/item-2/playback-position"] = (
            Data(
                #"""
                {"playback_position":{"position_seconds":0,"completed_at":null}}
                """#
                .utf8
            ),
            200
        )
        let controller = PodcastPlaybackController(client: client, sessionManager: sessionManager)
        let item1 = makeAudioItem(id: "item-1")
        let item2 = makeAudioItem(id: "item-2")

        await controller.start(item1)
        controller.currentTimeSeconds = 123.4

        await controller.togglePlayback(for: item2)

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.suffix(2).map(\.path), [
            "/api/v1/podcast-episodes/item-1/playback-position",
            "/api/v1/podcast-episodes/item-2/playback-position"
        ])
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.suffix(2), ["PUT", "GET"])
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains { body in
            guard let body else { return false }
            return body.contains(#""position_seconds":123.4"#) && body.contains(#""completed":false"#)
        })
        XCTAssertEqual(controller.currentItem?.id, "item-2")
        XCTAssertEqual(controller.currentTimeSeconds, 0, accuracy: 0.01)
        XCTAssertTrue(controller.isPlaying)
    }

    func testEndedNotificationIgnoresStalePlayerItems() async throws {
        let client = makeClient()
        let sessionManager = try await makeSignedInSessionManager()
        let controller = PodcastPlaybackController(client: client, sessionManager: sessionManager)
        let item1 = makeAudioItem(id: "item-1")
        let item2 = makeAudioItem(id: "item-2")

        await controller.start(item1)
        let stalePlayerItem = controller.player.currentItem
        await controller.start(item2)

        guard let stalePlayerItem else {
            return XCTFail("Expected an active player item for the first episode")
        }

        NotificationCenter.default.post(
            name: .AVPlayerItemDidPlayToEndTime,
            object: stalePlayerItem
        )
        await Task.yield()

        XCTAssertEqual(controller.currentItem?.id, "item-2")
        XCTAssertTrue(controller.isPlaying)
    }

    func testFinishedPlaybackUsesRSSDurationWhenPlayerDurationIsUnavailable() async throws {
        let client = makeClient()
        let sessionManager = try await makeSignedInSessionManager()
        let controller = PodcastPlaybackController(client: client, sessionManager: sessionManager)
        let item = makeAudioItem()

        await controller.start(item)
        guard let playerItem = controller.player.currentItem else {
            return XCTFail("Expected an active player item")
        }
        CannedFeedURLProtocol.resetCapturedRequests()
        let path = "/api/v1/podcast-episodes/item-1/playback-position"
        CannedFeedURLProtocol.suspendResponse(path: path)
        let requestBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "PUT")

        NotificationCenter.default.post(
            name: .AVPlayerItemDidPlayToEndTime,
            object: playerItem
        )
        addTeardownBlock { @MainActor [controller] in
            CannedFeedURLProtocol.releaseResponse(path: path)
            await controller.drainPersistenceOperations()
        }

        let request = try await requestBarrier.wait()
        let body = try XCTUnwrap(request.body)
        let positionWrite = try JSONDecoder().decode(PodcastPositionWrite.self, from: Data(body.utf8))
        XCTAssertEqual(positionWrite.positionSeconds, 1_800)
        XCTAssertTrue(positionWrite.completed)
    }

    func testNormalizesHttpMediaURLsToHttpsBeforePlayback() {
        let controller = PodcastPlaybackController(client: makeClient(), sessionManager: SessionManager(
            client: makeClient(),
            cookieStorage: HTTPCookieStorage()
        ))

        XCTAssertEqual(
            controller.normalizedPlayableMediaURLString("http://example.com/item-http.mp3"),
            "https://example.com/item-http.mp3"
        )
        XCTAssertEqual(
            controller.normalizedPlayableMediaURLString("HTTP://example.com/item-http.mp3"),
            "https://example.com/item-http.mp3"
        )
    }

    func testCompletedPlaybackPositionRequiresKnownDuration() async throws {
        let client = makeClient()
        let sessionManager = try await makeSignedInSessionManager()
        let controller = PodcastPlaybackController(client: client, sessionManager: sessionManager)
        let item = makeAudioItem()

        await controller.persistProgress(for: item, completed: true, positionSeconds: 0)

        let body = try XCTUnwrap(CannedFeedURLProtocol.capturedBodies.last.flatMap { $0 })
        XCTAssertTrue(body.contains(#""position_seconds":0"#))
        XCTAssertTrue(body.contains(#""completed":false"#))
    }
}
