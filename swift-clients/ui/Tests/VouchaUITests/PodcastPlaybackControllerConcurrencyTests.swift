import Foundation
import VouchaAPI
import VouchaAuth
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

private final class DetachedReleaseBox<Value>: @unchecked Sendable {
    private var value: Value?

    init(_ value: Value) {
        self.value = value
    }

    func release() {
        value = nil
    }
}

@MainActor
final class PodcastPlaybackControllerConcurrencyTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.reset()
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

    func testFinalReleaseFromDetachedTaskUsesIsolatedDeinitializer() async throws {
        var controller: PodcastPlaybackController? = PodcastPlaybackController(
            client: makeClient(),
            sessionManager: SessionManager(client: makeClient(), cookieStorage: HTTPCookieStorage())
        )
        weak let weakController = controller
        let releaseBox = try DetachedReleaseBox(XCTUnwrap(controller))
        controller = nil

        await Task.detached {
            releaseBox.release()
        }.value
        for _ in 0 ..< 100 where weakController != nil {
            await Task.yield()
        }

        XCTAssertNil(weakController)
    }

    func testCurrentArtworkURLResolvesRelativeSidecarPaths() async throws {
        let sessionManager = try await makeSignedInSessionManager()
        let controller = PodcastPlaybackController(client: makeClient(), sessionManager: sessionManager)
        controller.currentItem = makeAudioItem(thumbnailURL: "/sideload/item-1.jpg")

        XCTAssertEqual(controller.currentArtworkURL, "http://localhost:2999/sideload/item-1.jpg")
    }

    func testLatestQueuedStartWinsWhilePreviousFlushIsInFlight() async throws {
        let client = makeClient()
        let sessionManager = try await makeSignedInSessionManager()
        CannedFeedURLProtocol.queuedHandlers["/api/v1/podcast-episodes/item-1/playback-position"] = [
            (
                Data(
                    #"""
                    {"playback_position":{"position_seconds":42.5,"completed_at":null}}
                    """#
                    .utf8
                ),
                200,
                0.1
            )
        ]
        let controller = PodcastPlaybackController(client: client, sessionManager: sessionManager)
        let item0 = makeAudioItem(id: "item-0")
        let item1 = makeAudioItem(id: "item-1")
        let item2 = makeAudioItem(id: "item-2")

        await controller.start(item0)
        controller.currentTimeSeconds = 123.4
        CannedFeedURLProtocol.queuedHandlers["/api/v1/podcast-episodes/item-0/playback-position"] = [
            (Data("{}".utf8), 200, 0.05),
            (Data("{}".utf8), 200, 0)
        ]

        let firstStart = Task { @MainActor in
            await controller.togglePlayback(for: item1)
        }
        await Task.yield()
        let secondStart = Task { @MainActor in
            await controller.togglePlayback(for: item2)
        }

        await firstStart.value
        await secondStart.value

        XCTAssertEqual(controller.currentItem?.id, item2.id)
        XCTAssertEqual(controller.currentTimeSeconds, 0, accuracy: 0.01)
        XCTAssertFalse(controller.isLoading)
        XCTAssertTrue(controller.isPlaying)
    }

    func testPausePersistsThePositionThatWasPaused() async throws {
        let client = makeClient()
        let sessionManager = try await makeSignedInSessionManager()
        CannedFeedURLProtocol.handlers["/api/v1/podcast-episodes/item-1/playback-position"] = (
            Data(#"{"playback_position":{"position_seconds":0,"completed_at":null}}"#.utf8),
            200
        )
        let controller = PodcastPlaybackController(client: client, sessionManager: sessionManager)
        let item = makeAudioItem()

        await controller.start(item)
        controller.currentTimeSeconds = 123.4
        CannedFeedURLProtocol.resetCapturedRequests()
        let path = "/api/v1/podcast-episodes/item-1/playback-position"
        CannedFeedURLProtocol.suspendResponse(path: path)
        let requestBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "PUT")

        controller.pause()
        addTeardownBlock { @MainActor [controller] in
            CannedFeedURLProtocol.releaseResponse(path: path)
            await controller.drainPersistenceOperations()
        }
        controller.currentTimeSeconds = 456.7

        let request = try await requestBarrier.wait()
        let body = try XCTUnwrap(request.body)
        let positionWrite = try JSONDecoder().decode(PodcastPositionWrite.self, from: Data(body.utf8))
        XCTAssertEqual(positionWrite.positionSeconds, 123.4)
        XCTAssertNotEqual(positionWrite.positionSeconds, 456.7)
    }

    func testPausedSeekPersistsNewPosition() async throws {
        let client = makeClient()
        let sessionManager = try await makeSignedInSessionManager()
        let controller = PodcastPlaybackController(client: client, sessionManager: sessionManager)
        let item = makeAudioItem()

        await controller.start(item)
        controller.pause()
        await controller.drainPersistenceOperations()
        CannedFeedURLProtocol.resetCapturedRequests()
        let path = "/api/v1/podcast-episodes/item-1/playback-position"
        CannedFeedURLProtocol.suspendResponse(path: path)
        let requestBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "PUT")

        controller.seek(to: 314.5)
        addTeardownBlock { @MainActor [controller] in
            CannedFeedURLProtocol.releaseResponse(path: path)
            await controller.drainPersistenceOperations()
        }

        let request = try await requestBarrier.wait()
        let body = try XCTUnwrap(request.body)
        let positionWrite = try JSONDecoder().decode(PodcastPositionWrite.self, from: Data(body.utf8))
        XCTAssertEqual(positionWrite.positionSeconds, 314.5)
        XCTAssertFalse(positionWrite.completed)
    }

    func testPlaybackPositionWritesAreSerialized() async throws {
        let client = makeClient()
        let sessionManager = try await makeSignedInSessionManager()
        let controller = PodcastPlaybackController(client: client, sessionManager: sessionManager)
        let item = makeAudioItem()

        await controller.start(item)
        controller.cancelProgressTask()
        controller.durationSeconds = 20
        CannedFeedURLProtocol.queuedHandlers["/api/v1/podcast-episodes/item-1/playback-position"] = [
            (Data("{}".utf8), 200, 0.05),
            (
                Data(
                    #"""
                    {"playback_position":{"position_seconds":20,"completed_at":"2026-01-01T00:00:00Z"}}
                    """#
                    .utf8
                ),
                200,
                0
            )
        ]

        let olderWrite = Task { @MainActor in
            await controller.persistProgress(for: item, completed: false, positionSeconds: 10)
        }
        await Task.yield()
        await controller.persistProgress(for: item, completed: true, positionSeconds: 20)
        await olderWrite.value

        let playbackWriteBodies = CannedFeedURLProtocol.capturedRequests.compactMap { request -> String? in
            guard request.url.path == "/api/v1/podcast-episodes/item-1/playback-position",
                  request.method == "PUT"
            else { return nil }
            return request.body
        }
        XCTAssertEqual(playbackWriteBodies.count, 2)
        XCTAssertTrue(playbackWriteBodies[0].contains(#""position_seconds":10"#))
        XCTAssertTrue(playbackWriteBodies[1].contains(#""position_seconds":20"#))
        XCTAssertTrue(playbackWriteBodies[1].contains(#""completed":true"#))
    }

    func testCanceledProgressTaskDoesNotPersistNewCurrentItem() async throws {
        let client = makeClient()
        let sessionManager = try await makeSignedInSessionManager()
        CannedFeedURLProtocol.resetCapturedRequests()

        let controller = PodcastPlaybackController(client: client, sessionManager: sessionManager)
        let originalItem = makeAudioItem(id: "item-1")
        let replacementItem = makeAudioItem(id: "item-2")

        controller.currentItem = originalItem
        controller.currentTimeSeconds = 123.4
        controller.startProgressTask()
        await Task.yield()

        controller.currentItem = replacementItem
        controller.currentTimeSeconds = 456.7
        controller.cancelProgressTask()

        try await Task.sleep(nanoseconds: 100_000_000)

        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/podcast-episodes/item-1/playback-position" })
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/podcast-episodes/item-2/playback-position" })
    }

    func testCompletedPreviousItemDoesNotFlushAsIncompleteWhenStartingNextItem() async throws {
        let client = makeClient()
        let sessionManager = try await makeSignedInSessionManager()
        let controller = PodcastPlaybackController(client: client, sessionManager: sessionManager)
        let completedItem = makeAudioItem(id: "item-completed")
        let nextItem = makeAudioItem(id: "item-next")
        CannedFeedURLProtocol.handlers["/api/v1/podcast-episodes/item-next/playback-position"] = (
            Data(#"{"playback_position":{"position_seconds":0,"completed_at":null}}"#.utf8),
            200
        )

        controller.currentItem = completedItem
        controller.currentTimeSeconds = 1_800
        controller.durationSeconds = 1_800
        controller.currentItemStartedPlayback = false
        CannedFeedURLProtocol.resetCapturedRequests()

        await controller.start(nextItem)

        let completedItemPlaybackWrites = CannedFeedURLProtocol.capturedRequests.filter { request in
            request.url.path == "/api/v1/podcast-episodes/item-completed/playback-position"
                && request.method == "PUT"
        }
        XCTAssertTrue(completedItemPlaybackWrites.isEmpty)
        XCTAssertEqual(controller.currentItem?.id, nextItem.id)
    }

    func testResumingEndedCurrentItemSeeksBackToZeroBeforePlaying() async {
        let controller = PodcastPlaybackController(client: makeClient(), sessionManager: SessionManager(
            client: makeClient(),
            cookieStorage: HTTPCookieStorage()
        ))
        let item = makeAudioItem()
        let endedDuration = Double(item.durationSeconds ?? 0)

        await controller.start(item)
        controller.durationSeconds = endedDuration
        controller.currentTimeSeconds = endedDuration
        controller.pause()

        await controller.togglePlayback(for: item)

        XCTAssertEqual(controller.currentItem?.id, item.id)
        XCTAssertEqual(controller.currentTimeSeconds, 0, accuracy: 0.01)
        XCTAssertTrue(controller.isPlaying)
    }
}
