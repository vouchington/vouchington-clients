import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class ModerationExposureContractTests: XCTestCase {
    func testReviewModerationSummaryAndMediaRevealDecode() throws {
        let data = ApiFixtureLoader.data("native.moderation.review-queue.default")
        let response = try makeVouchaDecoder().decode(AdminReviewQueueResponse.self, from: data)
        XCTAssertEqual(response.results.first?.mediaReveal.requiresReveal, false)
        XCTAssertEqual(response.results.last?.mediaReveal.requiresReveal, true)
        XCTAssertEqual(
            response.results.last?.mediaReveal.images.first?.imageId,
            "019e82f2-a2c0-7000-8000-000000000001"
        )

        XCTAssertEqual(response.results.first?.moderationSummary.disposition, .review)
        XCTAssertEqual(response.results.last?.moderationSummary.evidenceSummary.flaggedCategoryCount, 1)
        XCTAssertEqual(response.results.last?.moderationSummary.evidenceSummary.signalCount, 0)
        XCTAssertFalse(response.results.last?.moderationSummary.reasonCodes.isEmpty ?? true)
    }

    func testExposureFixturesDecodeUsingServerDateConventions() throws {
        let response = try makeVouchaDecoder().decode(
            ModerationExposureResponse.self,
            from: ApiFixtureLoader.data("native.moderation.exposure.default")
        )
        XCTAssertEqual(response.exposure.count, 1)
        XCTAssertEqual(response.exposure.threshold, 10)
        XCTAssertFalse(response.exposure.inCooldown)
        XCTAssertNil(response.exposure.cooldownEndsAt)

        let dateResponse = try makeVouchaDecoder().decode(
            ModerationExposureResponse.self,
            from: Data("""
            {"exposure":{"count":10,"threshold":10,"in_cooldown":true,\
            "cooldown_ends_at":"2026-07-29T20:00:00.000Z"}}
            """.utf8)
        )
        XCTAssertNotNil(dateResponse.exposure.cooldownEndsAt)
    }

    func testExposureEndpointsUseCamelCaseRevealIdentifiers() {
        assertEndpoint(Endpoint.moderationExposure(), path: "/api/v1/moderation/exposure")
        assertEndpoint(
            Endpoint.recordModerationReveal(
                postId: "post-1",
                reportId: "report-1",
                surface: .reviewQueue
            ),
            method: .POST,
            path: "/api/v1/moderation/reveals",
            body: [
                "postId": "post-1",
                "reportId": "report-1",
                "surface": "review_queue"
            ]
        )
    }
}
