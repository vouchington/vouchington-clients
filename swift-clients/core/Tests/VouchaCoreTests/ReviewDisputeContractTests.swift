import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class ReviewDisputeContractTests: XCTestCase {
    func testStaffContextDecodesFromFixtureAndRemainsOptionalForOldResponses() throws {
        let data = ApiFixtureLoader.data("native.moderation.disputes.detail.default")
        let response = try makeVouchaDecoder().decode(ReviewDisputeResponse.self, from: data)
        let dispute = try XCTUnwrap(Optional(response.dispute))
        XCTAssertEqual(dispute.staffContext?.disputant.username, "native-claimant")
        XCTAssertEqual(dispute.staffContext?.review.post.title, "A disputed review")
        XCTAssertEqual(dispute.staffContext?.review.topic?.name, "Native topic")
        XCTAssertEqual(dispute.staffContext?.review.rating, 1)

        var object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        var oldDispute = try XCTUnwrap(object["dispute"] as? [String: Any])
        oldDispute.removeValue(forKey: "staff_context")
        object["dispute"] = oldDispute
        let oldData = try JSONSerialization.data(withJSONObject: object)
        let oldResponse = try makeVouchaDecoder().decode(ReviewDisputeResponse.self, from: oldData)
        XCTAssertNil(oldResponse.dispute.staffContext)
    }

    func testStaffContextRejectsNullReviewRating() throws {
        let data = ApiFixtureLoader.data("native.moderation.disputes.detail.default")
        var object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        var dispute = try XCTUnwrap(object["dispute"] as? [String: Any])
        var staffContext = try XCTUnwrap(dispute["staff_context"] as? [String: Any])
        var review = try XCTUnwrap(staffContext["review"] as? [String: Any])
        review["rating"] = NSNull()
        staffContext["review"] = review
        dispute["staff_context"] = staffContext
        object["dispute"] = dispute

        let invalidData = try JSONSerialization.data(withJSONObject: object)
        XCTAssertThrowsError(
            try makeVouchaDecoder().decode(ReviewDisputeResponse.self, from: invalidData)
        )
    }

    func testCompleteDisputeLifecycleEndpointsEncodeExpectedRequests() {
        let id = "dispute-1"
        assertEndpoint(Endpoint.dispute(id: id), path: "/api/v1/disputes/dispute-1")
        assertEndpoint(
            Endpoint.updateDisputeDraft(
                id: id,
                publicResponse: "We reviewed your dispute.",
                internalNotes: "Policy reviewed."
            ),
            method: .PATCH,
            path: "/api/v1/disputes/dispute-1",
            body: [
                "public_response": "We reviewed your dispute.",
                "internal_notes": "Policy reviewed."
            ]
        )
        assertEndpoint(
            Endpoint.disputeApproval(id: id),
            method: .POST,
            path: "/api/v1/disputes/dispute-1/approval"
        )
        assertEndpoint(
            Endpoint.disputeDelivery(id: id),
            method: .POST,
            path: "/api/v1/disputes/dispute-1/delivery"
        )
        assertEndpoint(
            Endpoint.disputeResolutionDrafts(id: id),
            method: .POST,
            path: "/api/v1/disputes/dispute-1/resolution-drafts"
        )
        for action in [ReviewDisputeResolutionAction.remove, .dismiss] {
            assertEndpoint(
                Endpoint.disputeResolution(id: id, action: action),
                method: .POST,
                path: "/api/v1/disputes/dispute-1/resolution",
                body: ["action": action.rawValue]
            )
        }
        assertEndpoint(
            Endpoint.disputeResolution(id: id, action: .annotate, bodyText: "Visible annotation"),
            method: .POST,
            path: "/api/v1/disputes/dispute-1/resolution",
            body: ["action": "annotate", "body_text": "Visible annotation"]
        )
    }

    func testDisputeLifecycleResultEnvelopesDecodeFromSharedFixtures() throws {
        let envelopeFixtures = [
            "native.moderation.disputes.detail.default",
            "native.moderation.disputes.update.default",
            "native.moderation.disputes.approval.default",
            "native.moderation.disputes.delivery.default",
            "native.moderation.disputes.resolution.remove",
            "native.moderation.disputes.resolution.annotate",
            "native.moderation.disputes.resolution.dismiss"
        ]
        for fixture in envelopeFixtures {
            let response = try makeVouchaDecoder().decode(
                ReviewDisputeResponse.self,
                from: ApiFixtureLoader.data(fixture)
            )
            XCTAssertEqual(response.dispute.id, "00000000-0000-7000-8000-000000000201")
            XCTAssertNotNil(response.dispute.staffContext, fixture)
        }

        let rerun = try makeVouchaDecoder().decode(
            ReviewDisputeQueueResponse.self,
            from: ApiFixtureLoader.data("native.moderation.disputes.resolution-drafts.default")
        )
        XCTAssertTrue(rerun.queued)
        XCTAssertEqual(rerun.rerunById, "staff-1")
    }
}
