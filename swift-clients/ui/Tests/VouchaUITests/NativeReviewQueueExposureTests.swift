import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeReviewQueueExposureTests: NativeRouteSurfaceViewModelTestCase {
    private let queuePath = "/api/v1/posts/review-queue"
    private let exposurePath = "/api/v1/moderation/exposure"
    private let revealPath = "/api/v1/moderation/reveals"

    func testLoadHydratesQueueAndExposure() async throws {
        CannedFeedURLProtocol.handlers[queuePath] = (queueData(), 200)
        CannedFeedURLProtocol.handlers[exposurePath] = (exposureData(count: 2, threshold: 10), 200)
        let viewModel = try makeViewModel()

        await viewModel.load()

        XCTAssertEqual(viewModel.items.map(\.id), ["sensitive", "another-sensitive", "safe"])
        XCTAssertEqual(viewModel.exposureState?.count, 2)
        XCTAssertFalse(viewModel.exposureIsStale)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(exposurePath), 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(queuePath), 1)
    }

    func testRefreshRehydratesExposureWithoutAllowingAnOlderGetToOverwriteAReveal() async throws {
        CannedFeedURLProtocol.handlers[queuePath] = (queueData(), 200)
        CannedFeedURLProtocol.handlers[exposurePath] = (exposureData(count: 0, threshold: 10), 200)
        CannedFeedURLProtocol.handlers[revealPath] = (
            exposureData(count: 10, threshold: 10, inCooldown: true),
            200
        )
        let viewModel = try makeViewModel()
        await viewModel.load()

        CannedFeedURLProtocol.suspendResponse(path: exposurePath)
        let refresh = Task { await viewModel.refresh() }
        let didSuspend = await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(
            path: exposurePath
        )
        XCTAssertTrue(didSuspend)

        await viewModel.revealMedia(postId: "sensitive")
        XCTAssertFalse(viewModel.canRevealMedia(postId: "another-sensitive"))
        CannedFeedURLProtocol.releaseResponse(path: exposurePath)
        await refresh.value

        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(exposurePath), 2)
        XCTAssertTrue(viewModel.exposureState?.inCooldown == true)
        XCTAssertFalse(viewModel.canRevealMedia(postId: "another-sensitive"))
    }

    func testRevealShowsWholeGroupAndRecordsExactlyOnce() async throws {
        CannedFeedURLProtocol.handlers[queuePath] = (queueData(), 200)
        CannedFeedURLProtocol.handlers[exposurePath] = (exposureData(count: 0, threshold: 10), 200)
        CannedFeedURLProtocol.handlers[revealPath] = (exposureData(count: 1, threshold: 10), 200)
        let viewModel = try makeViewModel()
        await viewModel.load()

        async let first: Void = viewModel.revealMedia(postId: "sensitive")
        async let duplicate: Void = viewModel.revealMedia(postId: "sensitive")
        _ = await (first, duplicate)

        XCTAssertTrue(viewModel.isMediaRevealed(postId: "sensitive"))
        XCTAssertEqual(viewModel.exposureState?.count, 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(revealPath), 1)
        let body = try XCTUnwrap(CannedFeedURLProtocol.capturedBodies.compactMap(\.self).last)
        let object = try XCTUnwrap(
            JSONSerialization.jsonObject(with: Data(body.utf8)) as? [String: String]
        )
        XCTAssertEqual(object, ["postId": "sensitive", "surface": "review_queue"])
    }

    func testAmbiguousRevealFailureKeepsMediaVisibleAndGatesUntilRefetch() async throws {
        CannedFeedURLProtocol.handlers[queuePath] = (queueData(), 200)
        CannedFeedURLProtocol.queuedHandlers[exposurePath] = [
            (exposureData(count: 0, threshold: 10), 200, 0),
            (exposureData(count: 1, threshold: 10), 200, 0)
        ]
        CannedFeedURLProtocol.handlers[revealPath] = (Data("{}".utf8), 500)
        let viewModel = try makeViewModel()
        await viewModel.load()

        await viewModel.revealMedia(postId: "sensitive")

        XCTAssertTrue(viewModel.isMediaRevealed(postId: "sensitive"))
        XCTAssertTrue(viewModel.exposureIsStale)
        XCTAssertFalse(viewModel.canRevealMedia(postId: "another-sensitive"))
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(revealPath), 1)

        await viewModel.refetchExposure()

        XCTAssertFalse(viewModel.exposureIsStale)
        XCTAssertTrue(viewModel.canRevealMedia(postId: "another-sensitive"))
        await viewModel.revealMedia(postId: "sensitive")
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(revealPath), 1)
    }

    func testCancelledRevealKeepsMediaVisibleAndGatesUntilRefetch() async throws {
        CannedFeedURLProtocol.handlers[queuePath] = (queueData(), 200)
        CannedFeedURLProtocol.handlers[exposurePath] = (exposureData(count: 0, threshold: 10), 200)
        CannedFeedURLProtocol.handlers[revealPath] = (exposureData(count: 1, threshold: 10), 200)
        CannedFeedURLProtocol.suspendResponse(path: revealPath)
        let viewModel = try makeViewModel()
        await viewModel.load()

        let reveal = Task { await viewModel.revealMedia(postId: "sensitive") }
        let didSuspend = await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(path: revealPath)
        XCTAssertTrue(didSuspend)
        reveal.cancel()
        CannedFeedURLProtocol.releaseResponse(path: revealPath)
        await reveal.value

        XCTAssertTrue(viewModel.isMediaRevealed(postId: "sensitive"))
        XCTAssertTrue(viewModel.exposureIsStale)
        XCTAssertFalse(viewModel.canRevealMedia(postId: "another-sensitive"))
    }

    func testCooldownGatesOnlyRevealsAndSchedulesExpiryRefetch() async throws {
        CannedFeedURLProtocol.handlers[queuePath] = (queueData(), 200)
        CannedFeedURLProtocol.handlers[exposurePath] = (
            exposureData(
                count: 10,
                threshold: 10,
                inCooldown: true,
                cooldownEndsAt: "2099-07-29T20:00:00.000Z"
            ),
            200
        )
        let viewModel = try makeViewModel()

        await viewModel.load()

        XCTAssertFalse(viewModel.canRevealMedia(postId: "sensitive"))
        XCTAssertTrue(viewModel.hasScheduledCooldownRefetch)
        XCTAssertFalse(viewModel.actionsAreDisabled(for: "sensitive"))
    }

    func testPastCooldownUsesMinimumDelayAndCancelsReplacedSchedule() async throws {
        CannedFeedURLProtocol.handlers[queuePath] = (queueData(), 200)
        CannedFeedURLProtocol.queuedHandlers[exposurePath] = [
            (pastCooldownExposureData(), 200, 0),
            (pastCooldownExposureData(), 200, 0)
        ]
        let sleepProbe = CooldownSleepProbe()
        let viewModel = try NativeReviewQueueViewModel(
            client: makeClient(),
            isSignedIn: true,
            isAdministrator: true,
            cooldownSleep: { try await sleepProbe.sleep(nanoseconds: $0) }
        )

        await viewModel.load()
        let scheduledInitialCooldown = await sleepProbe.waitForInvocationCount(1)
        XCTAssertTrue(scheduledInitialCooldown)

        await viewModel.refetchExposure()
        let scheduledReplacementCooldown = await sleepProbe.waitForInvocationCount(2)
        let cancelledInitialCooldown = await sleepProbe.waitForCancellationCount(1)
        XCTAssertTrue(scheduledReplacementCooldown)
        XCTAssertTrue(cancelledInitialCooldown)
        let delays = await sleepProbe.delays
        XCTAssertEqual(delays, [5_000_000_000, 5_000_000_000])
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(exposurePath), 2)
        XCTAssertTrue(viewModel.hasScheduledCooldownRefetch)

        viewModel.cancelListOperations()

        let cancelledReplacementCooldown = await sleepProbe.waitForCancellationCount(2)
        XCTAssertTrue(cancelledReplacementCooldown)
        XCTAssertFalse(viewModel.hasScheduledCooldownRefetch)
    }

    func testScheduledCooldownExpiryAcceptsRefetchAndUngatesReveals() async throws {
        CannedFeedURLProtocol.handlers[queuePath] = (queueData(), 200)
        CannedFeedURLProtocol.queuedHandlers[exposurePath] = [
            (pastCooldownExposureData(), 200, 0),
            (exposureData(count: 3, threshold: 10), 200, 0)
        ]
        let sleepProbe = CooldownSleepProbe(suspends: false)
        let viewModel = try NativeReviewQueueViewModel(
            client: makeClient(),
            isSignedIn: true,
            isAdministrator: true,
            cooldownSleep: { try await sleepProbe.sleep(nanoseconds: $0) }
        )

        await viewModel.load()
        let refetchCompleted = await waitForExposureCount(3, viewModel: viewModel)
        let delays = await sleepProbe.delays

        XCTAssertTrue(refetchCompleted)
        XCTAssertEqual(delays, [5_000_000_000])
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(exposurePath), 2)
        XCTAssertEqual(viewModel.exposureState?.count, 3)
        XCTAssertFalse(viewModel.exposureState?.inCooldown == true)
        XCTAssertFalse(viewModel.exposureIsStale)
        XCTAssertTrue(viewModel.canRevealMedia(postId: "sensitive"))
        XCTAssertFalse(viewModel.hasScheduledCooldownRefetch)
    }

    func testFailedExpiryRefetchRemainsGated() async throws {
        CannedFeedURLProtocol.handlers[queuePath] = (queueData(), 200)
        CannedFeedURLProtocol.queuedHandlers[exposurePath] = [
            (
                exposureData(
                    count: 10,
                    threshold: 10,
                    inCooldown: true,
                    cooldownEndsAt: "2099-07-29T20:00:00.000Z"
                ),
                200,
                0
            ),
            (Data("{}".utf8), 500, 0)
        ]
        let viewModel = try makeViewModel()
        await viewModel.load()

        await viewModel.cooldownExpiryReached()

        XCTAssertTrue(viewModel.exposureIsStale)
        XCTAssertFalse(viewModel.canRevealMedia(postId: "sensitive"))
        XCTAssertFalse(viewModel.hasScheduledCooldownRefetch)
    }

    func testResumeRehydratesExposureAfterScheduledRefreshCancellation() async throws {
        CannedFeedURLProtocol.handlers[queuePath] = (queueData(), 200)
        CannedFeedURLProtocol.handlers[exposurePath] = (
            exposureData(
                count: 10,
                threshold: 10,
                inCooldown: true,
                cooldownEndsAt: "2099-07-29T20:00:00.000Z"
            ),
            200
        )
        let viewModel = try makeViewModel()
        await viewModel.load()
        XCTAssertTrue(viewModel.hasScheduledCooldownRefetch)

        CannedFeedURLProtocol.suspendResponse(path: exposurePath)
        let inFlightRefetch = Task { await viewModel.refetchExposure() }
        let didSuspend = await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(
            path: exposurePath
        )
        XCTAssertTrue(didSuspend)
        viewModel.cancelListOperations()
        XCTAssertTrue(viewModel.exposureIsStale)
        XCTAssertFalse(viewModel.hasScheduledCooldownRefetch)
        inFlightRefetch.cancel()
        CannedFeedURLProtocol.releaseResponse(path: exposurePath)
        await inFlightRefetch.value

        CannedFeedURLProtocol.handlers[exposurePath] = (Data("{}".utf8), 500)
        await viewModel.load()
        XCTAssertTrue(viewModel.exposureIsStale)
        XCTAssertFalse(viewModel.canRevealMedia(postId: "sensitive"))
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(queuePath), 1)

        CannedFeedURLProtocol.handlers[exposurePath] = (exposureData(count: 1, threshold: 10), 200)
        await viewModel.load()

        XCTAssertEqual(viewModel.exposureState?.count, 1)
        XCTAssertFalse(viewModel.exposureIsStale)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(queuePath), 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(exposurePath), 4)
    }

    private func makeViewModel() throws -> NativeReviewQueueViewModel {
        try NativeReviewQueueViewModel(client: makeClient(), isSignedIn: true, isAdministrator: true)
    }

    private func queueData() -> Data {
        Data(
            """
            {"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null},"results":[
              \(post(id: "sensitive", requiresReveal: true)),
              \(post(id: "another-sensitive", requiresReveal: true)),
              \(post(id: "safe", requiresReveal: false))
            ]}
            """.utf8
        )
    }

    private func post(id: String, requiresReveal: Bool) -> String {
        """
        {
          "id":"\(id)","title":"\(id)","slug":"\(id)","markdown_preview":"Preview",
          "post_type":"discussion","created_by_id":"author","created_at":"2026-06-01T11:30:00.000Z",
          "root_id":null,"root_post_type":null,"root_slug":null,"clearance_status":"rejected",
          "clearance_updated_at":null,"spam_detection_flagged":false,"spam_detection_score":null,
          "spam_detection_results":{},"openai_omni_moderation_flagged":false,
          "openai_omni_moderation_results":{},
          "media_context":{
            "requires_reveal":\(requiresReveal),
            "images":[
              {"image_id":"\(id)-1","order_index":0,"caption":"First"},
              {"image_id":"\(id)-2","order_index":1,"caption":"Second"}
            ]
          }
        }
        """
    }

    private func exposureData(
        count: Int,
        threshold: Int,
        inCooldown: Bool = false,
        cooldownEndsAt: String? = nil
    ) -> Data {
        let end = cooldownEndsAt.map { "\"\($0)\"" } ?? "null"
        return Data(
            #"{"exposure":{"count":\#(count),"threshold":\#(threshold),"in_cooldown":\#(inCooldown),"cooldown_ends_at":\#(end)}}"#
                .utf8
        )
    }

    private func pastCooldownExposureData() -> Data {
        exposureData(
            count: 10,
            threshold: 10,
            inCooldown: true,
            cooldownEndsAt: "1970-01-01T00:00:00.000Z"
        )
    }

    private func waitForExposureCount(
        _ expectedCount: Int,
        viewModel: NativeReviewQueueViewModel
    ) async -> Bool {
        let deadline = ContinuousClock.now + .seconds(2)
        while viewModel.exposureState?.count != expectedCount, ContinuousClock.now < deadline {
            await Task.yield()
        }
        return viewModel.exposureState?.count == expectedCount
    }
}

private actor CooldownSleepProbe {
    private(set) var delays: [UInt64] = []
    private var cancellationCount = 0
    private let suspends: Bool

    init(suspends: Bool = true) {
        self.suspends = suspends
    }

    func sleep(nanoseconds: UInt64) async throws {
        delays.append(nanoseconds)
        guard suspends else { return }
        do {
            try await Task.sleep(nanoseconds: 60_000_000_000)
        } catch {
            cancellationCount += 1
            throw error
        }
    }

    func waitForInvocationCount(_ expectedCount: Int) async -> Bool {
        let deadline = ContinuousClock.now + .seconds(2)
        while delays.count < expectedCount, ContinuousClock.now < deadline {
            await Task.yield()
        }
        return delays.count >= expectedCount
    }

    func waitForCancellationCount(_ expectedCount: Int) async -> Bool {
        let deadline = ContinuousClock.now + .seconds(2)
        while cancellationCount < expectedCount, ContinuousClock.now < deadline {
            await Task.yield()
        }
        return cancellationCount >= expectedCount
    }
}
