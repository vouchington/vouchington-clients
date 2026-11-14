import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class MemberAppealStaffSummaryTests: NativeRouteSurfaceViewModelTestCase {
    func testStaffPersonalAppealsTrackCanonicalSummaryWithoutDetailReads() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/appeals"] = [
            (ApiFixtureLoader.data("native.moderation.appeals.default"), 200, 0),
            (ApiFixtureLoader.data("native.moderation.appeals.page-2"), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/warnings"] = (
            Data(#"{"warnings":[],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}"#.utf8),
            200
        )
        let model = try MemberAppealsViewModel(
            client: makeClient(), isSignedIn: true, currentUserId: "user-1",
            route: .warnings, draftStore: MemberAppealDraftStore()
        )

        await model.load()

        XCTAssertNil(model.errorMessage)
        let appeal = try XCTUnwrap(model.appeals.first { $0.postId == "post-1" })
        guard case .summary = appeal else { return XCTFail("Expected typed staff summary") }
        let inspection = try MemberAppealTrackingCard(appeal: appeal).inspect()
        XCTAssertNoThrow(try inspection.find(text: "A removed review"))
        XCTAssertNoThrow(try inspection.find(text: "Pending"))
        XCTAssertThrowsError(try inspection.find(button: "Approve"))
        XCTAssertThrowsError(try inspection.find(button: "Send"))
        let requests = CannedFeedURLProtocol.capturedURLs.filter { $0.path.hasPrefix("/api/v1/appeals") }
        XCTAssertFalse(requests.isEmpty)
        for request in requests {
            XCTAssertEqual(request.path, "/api/v1/appeals")
            let query = try XCTUnwrap(URLComponents(url: request, resolvingAgainstBaseURL: false)?.queryItems)
            XCTAssertTrue(query.contains(URLQueryItem(name: "mine", value: "true")))
        }
        XCTAssertFalse(model.pendingAppealPagination.hasMore)
    }
}
