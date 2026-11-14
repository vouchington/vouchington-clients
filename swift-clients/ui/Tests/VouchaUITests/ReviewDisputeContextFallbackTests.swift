import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class ReviewDisputeContextFallbackTests: XCTestCase {
    private let locale = Locale.current

    private func dispute(patch: (inout [String: Any], inout [String: Any]) -> Void) throws -> ReviewDispute {
        var root = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("native.moderation.disputes.detail.default")
        ) as? [String: Any])
        var dispute = try XCTUnwrap(root["dispute"] as? [String: Any])
        var context = try XCTUnwrap(dispute["staff_context"] as? [String: Any])
        patch(&dispute, &context)
        if !(dispute["staff_context"] is NSNull) { dispute["staff_context"] = context }
        root["dispute"] = dispute
        return try JSONDecoder.vouchaFixtureDecoder.decode(
            ReviewDisputeResponse.self, from: JSONSerialization.data(withJSONObject: root)
        ).dispute
    }

    private func card(_ dispute: ReviewDispute) -> ReviewDisputeCard {
        ReviewDisputeCard(
            viewModel: ReviewDisputesViewModel(
                client: nil, isSignedIn: true, isAdministrator: true, isSiteModerator: false
            ),
            dispute: dispute
        )
    }

    func testMissingPostContentAndReviewFallsBackToLabelAndNotAvailable() throws {
        let value = try dispute { dispute, _ in
            dispute["post_content"] = NSNull()
            dispute["staff_context"] = NSNull()
        }
        let inspection = try card(value).inspect()
        let unavailable = UiMessages.string(.nativeSwiftRouteSurfaceNotAvailable, locale: locale)
        XCTAssertNoThrow(try inspection.find(text: value.postLabel))
        XCTAssertGreaterThanOrEqual(
            inspection.findAll(ViewType.Text.self).filter { (try? $0.string()) == unavailable }.count,
            2
        )
        XCTAssertThrowsError(try inspection.find(text: "The review being disputed."))
        XCTAssertThrowsError(try inspection.find(text: "Native topic"))
    }

    func testLifecycleRowsShowActorOnlyWhenAnActorIdIsPresent() throws {
        let value = try dispute { dispute, _ in
            dispute["edited_at"] = "2026-06-01T12:10:00.000Z"
            dispute["edited_by_id"] = "staff-1"
            dispute["drafted_at"] = "2026-06-01T12:05:00.000Z"
        }
        let inspection = try card(value).inspect()
        let actor = UiMessages.string(
            .nativeSwiftReviewDisputesByActor,
            parameters: ["actor": UiMessages.string(.nativeSwiftRouteSurfaceNotAvailable, locale: locale)],
            locale: locale
        )
        XCTAssertNoThrow(try inspection.find(text: UiMessages.string(.nativeSwiftReviewDisputesEdited, locale: locale)))
        XCTAssertNoThrow(try inspection.find(text: UiMessages.string(
            .nativeSwiftReviewDisputesDrafted,
            locale: locale
        )))
        XCTAssertEqual(inspection.findAll(ViewType.Text.self).filter { (try? $0.string()) == actor }.count, 1)
    }
}
