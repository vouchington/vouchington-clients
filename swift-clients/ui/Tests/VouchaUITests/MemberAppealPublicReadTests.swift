import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class MemberAppealPublicReadTests: NativeRouteSurfaceViewModelTestCase {
    func testCanonicalMemberAppealsLoadAndForwardTheOwnedCursor() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/appeals"] = [
            (ApiFixtureLoader.data("native.moderation.appeals.member.default"), 200, 0),
            (ApiFixtureLoader.data("native.moderation.appeals.member.page-2"), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/warnings"] = (
            Data(#"{"warnings":[],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}"#.utf8),
            200
        )
        let viewModel = try MemberAppealsViewModel(
            client: makeClient(), isSignedIn: true, currentUserId: "user-1",
            route: .warnings, draftStore: MemberAppealDraftStore()
        )

        await viewModel.load()

        XCTAssertNil(viewModel.errorMessage)
        XCTAssertEqual(viewModel.appeals.map(\.id), [
            "00000000-0000-7000-8000-000000000101",
            "00000000-0000-7000-8000-000000000102"
        ])
        XCTAssertFalse(viewModel.pendingAppealPagination.hasMore)
        let requests = CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/appeals" }
        XCTAssertEqual(requests.count, 2)
        let components = try XCTUnwrap(requests.last.flatMap { URLComponents(url: $0, resolvingAgainstBaseURL: false) })
        let query = Dictionary(uniqueKeysWithValues: (components.queryItems ?? []).map { ($0.name, $0.value) })
        let fixture = try JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("native.moderation.appeals.member.default")
        ) as? [String: Any]
        let page = try XCTUnwrap(fixture?["page_info"] as? [String: Any])
        XCTAssertEqual(query["after"] ?? nil, page["end_cursor"] as? String)
        XCTAssertEqual(query["mine"] ?? nil, "true")
        XCTAssertEqual(query["status"] ?? nil, "pending")
    }

    func testMyAppealsReadRouteLoadsPublicPagesWithoutPrivateTitles() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/appeals"] = [
            (ApiFixtureLoader.data("native.moderation.appeals.member.default"), 200, 0),
            (ApiFixtureLoader.data("native.moderation.appeals.member.page-2"), 200, 0)
        ]
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/appeals")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .moderationCases), client: makeClient(), routeMatch: route
        )

        await viewModel.load()
        await viewModel.loadMoreForwardRows()
        await viewModel.loadMoreForwardRows()

        guard case .loaded = viewModel.state else { return XCTFail("Expected loaded member appeals") }
        XCTAssertEqual(viewModel.rows.map(\.title), [
            "00000000-0000-7000-8000-000000000102",
            "00000000-0000-7000-8000-000000000101"
        ])
        let urls = CannedFeedURLProtocol.capturedURLs
        XCTAssertEqual(urls.count, 2)
        let query = try XCTUnwrap(URLComponents(url: XCTUnwrap(urls.last), resolvingAgainstBaseURL: false)?.queryItems)
        let fixture = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("native.moderation.appeals.member.default")
        ) as? [String: Any])
        let cursor = try XCTUnwrap((fixture["page_info"] as? [String: Any])?["end_cursor"] as? String)
        XCTAssertTrue(query.contains(URLQueryItem(name: "mine", value: "true")))
        XCTAssertTrue(query.contains(URLQueryItem(name: "after", value: cursor)))
    }

    func testCanonicalSentCardRendersPublicResponseWithoutStaffControls() throws {
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            MemberAppealReadResponse.self,
            from: ApiFixtureLoader.data("native.moderation.appeals.detail.member.sent")
        )
        let inspection = try MemberAppealTrackingCard(appeal: response.appeal).inspect()
        XCTAssertNoThrow(try inspection.find(text: "We reviewed your appeal and reduced the action."))
        XCTAssertNoThrow(try inspection.find(text: "Sent"))
        XCTAssertNoThrow(try inspection.find(text: "Resolved"))
        XCTAssertThrowsError(try inspection.find(button: "Approve"))
        XCTAssertThrowsError(try inspection.find(button: "Send"))
    }

    func testCanonicalMemberSubmissionClearsTheSubmittedDraft() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            ApiFixtureLoader.data("native.moderation.appeals.create.member.default"), 201
        )
        let store = MemberAppealDraftStore()
        let viewModel = try MemberAppealsViewModel(
            client: makeClient(), isSignedIn: true, currentUserId: "user-1",
            route: .suspension, draftStore: store
        )
        ModerationAppealsTestSupport.markPendingAppealsReconciled(in: viewModel)
        let target = MemberAppealTarget.suspension(date: Date(timeIntervalSince1970: 1_780_000_000))
        viewModel.beginAppeal(target)
        viewModel.setReason(.other)
        viewModel.setDetails("Please review this again.")

        await viewModel.submitAppeal()

        guard case .succeeded(isDuplicate: false) = viewModel.submissionState else {
            return XCTFail("The canonical redacted submission must succeed")
        }
        XCTAssertEqual(viewModel.appeals.map(\.id), ["00000000-0000-7000-8000-000000000103"])
        XCTAssertNil(store.draft(for: target, currentUserId: "user-1").reason)
        XCTAssertEqual(store.draft(for: target, currentUserId: "user-1").details, "")
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST"])
        let body = try XCTUnwrap(CannedFeedURLProtocol.capturedBodies.first ?? nil)
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(body.utf8)) as? [String: Any])
        XCTAssertEqual(object["appeal_reason"] as? String, "[other] Please review this again.")
        XCTAssertNil(object["target_id"])
    }

}
