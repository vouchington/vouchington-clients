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

    func testRevealStaysGatedUntilTheServerAcceptsItAndRecordsExactlyOnce() async throws {
        CannedFeedURLProtocol.handlers[queuePath] = (queueData(), 200)
        CannedFeedURLProtocol.handlers[exposurePath] = (exposureData(count: 0, threshold: 10), 200)
        CannedFeedURLProtocol.handlers[revealPath] = (exposureData(count: 1, threshold: 10), 200)
        let viewModel = try makeViewModel()
        await viewModel.load()

        CannedFeedURLProtocol.suspendResponse(path: revealPath)
        let first = Task { await viewModel.revealMedia(postId: "sensitive") }
        let didSuspend = await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(path: revealPath)
        XCTAssertTrue(didSuspend)
        let duplicate = Task { await viewModel.revealMedia(postId: "sensitive") }

        XCTAssertFalse(viewModel.isMediaRevealed(postId: "sensitive"))
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(revealPath), 1)

        CannedFeedURLProtocol.releaseResponse(path: revealPath)
        await first.value
        await duplicate.value

        XCTAssertTrue(viewModel.isMediaRevealed(postId: "sensitive"))
        XCTAssertEqual(viewModel.exposureState?.count, 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(revealPath), 1)
        let body = try XCTUnwrap(CannedFeedURLProtocol.capturedBodies.compactMap(\.self).last)
        let object = try XCTUnwrap(
            JSONSerialization.jsonObject(with: Data(body.utf8)) as? [String: String]
        )
        XCTAssertEqual(object, ["postId": "sensitive", "surface": "review_queue"])
    }

    func testAppendOnlyPaginationDoesNotDiscardAnAcceptedReveal() async throws {
        CannedFeedURLProtocol.queuedHandlers[queuePath] = [
            (queueData(hasNextPage: true, endCursor: "next"), 200, 0),
            (queueData(results: [post(id: "page-two", requiresReveal: false)]), 200, 0)
        ]
        CannedFeedURLProtocol.handlers[exposurePath] = (exposureData(count: 0, threshold: 10), 200)
        CannedFeedURLProtocol.handlers[revealPath] = (exposureData(count: 1, threshold: 10), 200)
        CannedFeedURLProtocol.suspendResponse(path: revealPath)
        let viewModel = try makeViewModel()
        await viewModel.load()

        let reveal = Task { await viewModel.revealMedia(postId: "sensitive") }
        let didSuspend = await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(path: revealPath)
        XCTAssertTrue(didSuspend)

        await viewModel.loadMore()
        XCTAssertTrue(viewModel.items.contains(where: { $0.id == "sensitive" }))
        XCTAssertTrue(viewModel.items.contains(where: { $0.id == "page-two" }))

        CannedFeedURLProtocol.releaseResponse(path: revealPath)
        await reveal.value

        XCTAssertTrue(viewModel.isMediaRevealed(postId: "sensitive"))
        XCTAssertEqual(viewModel.exposureState?.count, 1)
        XCTAssertFalse(viewModel.exposureIsStale)
    }

    func testRevealDoesNotOverwriteExposureRefetchedAfterFailedQueueRefresh() async throws {
        CannedFeedURLProtocol.queuedHandlers[queuePath] = [
            (queueData(), 200, 0),
            (Data("{}".utf8), 500, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers[exposurePath] = [
            (exposureData(count: 0, threshold: 10), 200, 0),
            (exposureData(count: 4, threshold: 10), 200, 0)
        ]
        CannedFeedURLProtocol.handlers[revealPath] = (exposureData(count: 1, threshold: 10), 200)
        CannedFeedURLProtocol.suspendResponse(path: revealPath)
        let viewModel = try makeViewModel()
        await viewModel.load()

        let reveal = Task { await viewModel.revealMedia(postId: "sensitive") }
        let didSuspend = await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(path: revealPath)
        XCTAssertTrue(didSuspend)

        await viewModel.refresh()
        XCTAssertTrue(viewModel.items.contains(where: { $0.id == "sensitive" }))

        CannedFeedURLProtocol.releaseResponse(path: revealPath)
        await reveal.value

        XCTAssertFalse(viewModel.isMediaRevealed(postId: "sensitive"))
        XCTAssertEqual(viewModel.exposureState?.count, 4)
        XCTAssertFalse(viewModel.exposureIsStale)
    }

    func testFailedRevealKeepsMediaGatedAndGatesUntilRefetch() async throws {
        CannedFeedURLProtocol.handlers[queuePath] = (queueData(), 200)
        CannedFeedURLProtocol.queuedHandlers[exposurePath] = [
            (exposureData(count: 0, threshold: 10), 200, 0),
            (exposureData(count: 1, threshold: 10), 200, 0)
        ]
        CannedFeedURLProtocol.handlers[revealPath] = (Data("{}".utf8), 500)
        let viewModel = try makeViewModel()
        await viewModel.load()

        await viewModel.revealMedia(postId: "sensitive")

        XCTAssertFalse(viewModel.isMediaRevealed(postId: "sensitive"))
        XCTAssertTrue(viewModel.exposureIsStale)
        XCTAssertFalse(viewModel.canRevealMedia(postId: "another-sensitive"))
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(revealPath), 1)

        await viewModel.refetchExposure()

        XCTAssertFalse(viewModel.exposureIsStale)
        XCTAssertTrue(viewModel.canRevealMedia(postId: "another-sensitive"))
        XCTAssertTrue(viewModel.canRevealMedia(postId: "sensitive"))
    }

    func testCancelledRevealKeepsMediaGatedAndGatesUntilRefetch() async throws {
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

        XCTAssertFalse(viewModel.isMediaRevealed(postId: "sensitive"))
        XCTAssertTrue(viewModel.exposureIsStale)
        XCTAssertFalse(viewModel.canRevealMedia(postId: "another-sensitive"))
    }

    func testStaleSuccessfulRevealKeepsMediaGated() async throws {
        CannedFeedURLProtocol.handlers[queuePath] = (queueData(), 200)
        CannedFeedURLProtocol.handlers[exposurePath] = (exposureData(count: 0, threshold: 10), 200)
        CannedFeedURLProtocol.handlers[revealPath] = (exposureData(count: 1, threshold: 10), 200)
        CannedFeedURLProtocol.suspendResponse(path: revealPath)
        let viewModel = try makeViewModel()
        await viewModel.load()

        let reveal = Task { await viewModel.revealMedia(postId: "sensitive") }
        let didSuspend = await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(path: revealPath)
        XCTAssertTrue(didSuspend)
        viewModel.cancelListOperations()
        CannedFeedURLProtocol.releaseResponse(path: revealPath)
        await reveal.value

        XCTAssertFalse(viewModel.isMediaRevealed(postId: "sensitive"))
        XCTAssertTrue(viewModel.exposureIsStale)
    }

    func testObsoleteRevealDoesNotOverwriteANewerAcceptedExposureRefresh() async throws {
        CannedFeedURLProtocol.handlers[queuePath] = (queueData(), 200)
        CannedFeedURLProtocol.handlers[exposurePath] = (exposureData(count: 0, threshold: 10), 200)
        CannedFeedURLProtocol.handlers[revealPath] = (exposureData(count: 1, threshold: 10), 200)
        CannedFeedURLProtocol.suspendResponse(path: revealPath)
        let viewModel = try makeViewModel()
        await viewModel.load()

        let reveal = Task { await viewModel.revealMedia(postId: "sensitive") }
        let didSuspend = await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(path: revealPath)
        XCTAssertTrue(didSuspend)
        viewModel.cancelListOperations()
        CannedFeedURLProtocol.handlers[exposurePath] = (exposureData(count: 4, threshold: 10), 200)
        await viewModel.load()
        XCTAssertEqual(viewModel.exposureState?.count, 4)
        XCTAssertFalse(viewModel.exposureIsStale)

        CannedFeedURLProtocol.releaseResponse(path: revealPath)
        await reveal.value

        XCTAssertFalse(viewModel.isMediaRevealed(postId: "sensitive"))
        XCTAssertEqual(viewModel.exposureState?.count, 4)
        XCTAssertFalse(viewModel.exposureIsStale)
        XCTAssertTrue(viewModel.canRevealMedia(postId: "sensitive"))
    }

    func testRevealSuccessAfterApprovalRemovesRowAndKeepsMediaGated() async throws {
        CannedFeedURLProtocol.handlers[queuePath] = (queueData(), 200)
        CannedFeedURLProtocol.handlers[exposurePath] = (exposureData(count: 0, threshold: 10), 200)
        CannedFeedURLProtocol.handlers[revealPath] = (exposureData(count: 1, threshold: 10), 200)
        CannedFeedURLProtocol.handlers["/api/v1/posts/sensitive/clearances"] = (
            Data(#"{"clearance_status":"approved"}"#.utf8),
            200
        )
        CannedFeedURLProtocol.suspendResponse(path: revealPath)
        let viewModel = try makeViewModel()
        await viewModel.load()

        let reveal = Task { await viewModel.revealMedia(postId: "sensitive") }
        let didSuspend = await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(path: revealPath)
        XCTAssertTrue(didSuspend)
        await viewModel.perform(.approve, postId: "sensitive")
        XCTAssertFalse(viewModel.items.contains(where: { $0.id == "sensitive" }))

        CannedFeedURLProtocol.releaseResponse(path: revealPath)
        await reveal.value

        XCTAssertFalse(viewModel.isMediaRevealed(postId: "sensitive"))
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

    private func queueData(
        hasNextPage: Bool = false,
        endCursor: String? = nil,
        results: [String]? = nil
    ) -> Data {
        let cursor = endCursor.map { "\"\($0)\"" } ?? "null"
        let rows = results ?? [
            post(id: "sensitive", requiresReveal: true),
            post(id: "another-sensitive", requiresReveal: true),
            post(id: "safe", requiresReveal: false)
        ]
        return Data(
            """
            {"page_info":{"has_next_page":\(hasNextPage),"end_cursor":\(cursor),"start_cursor":null},
            "results":[\(rows.joined(separator: ","))]}
            """.utf8
        )
    }

    private func post(id: String, requiresReveal: Bool) -> String {
        """
        {
          "id":"\(id)","title":"\(id)","declared_language":null,"lingua_rs_detected_language":null,"slug":"\(
              id
          )","markdown_preview":"Preview",
          "post_type":"discussion","created_by_id":"author","created_at":"2026-06-01T11:30:00.000Z",
          "root_id":null,"root_post_type":null,"root_slug":null,"clearance_status":"rejected",
          "clearance_updated_at":null,
          "moderation_summary":{"disposition":"review","evidence_summary":{"flagged_category_count":1,"signal_count":2},"reason_codes":["provider_flagged"]},
          "media_reveal":{
            "requires_reveal":\(requiresReveal),
            "images":[
              {
                "image_id":"\(id)-1","placement_id":"\(id)-placement-1",
                "placement_revision":0,"order_index":0,"caption":"First"
              },
              {
                "image_id":"\(id)-2","placement_id":"\(id)-placement-2",
                "placement_revision":0,"order_index":1,"caption":"Second"
              }
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
