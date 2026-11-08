import Foundation
import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class CommunityApplicationDetailTests: NativeRouteSurfaceViewModelTestCase {
    func testApplicationDetailHidesRetainedDataAfterCommunityModerationAccessIsRemoved() async throws {
        let model = try CommunityDetailViewModel(client: makeClient(), slug: "test-community")
        var community = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("web.communities.show.default")
        ) as? [String: Any])
        community["membership"] = [
            "id": "membership-1", "community_id": "community-1", "user_id": "user-1", "role": "moderator",
            "created_at": "2026-01-01T00:00:00Z", "updated_at": "2026-01-01T00:00:00Z"
        ]
        model.communityDetail = try JSONDecoder.vouchaFixtureDecoder.decode(
            CommunityResponse.self, from: JSONSerialization.data(withJSONObject: community)
        )
        let path = "/api/v1/communities/test-community/applications/application-1"
        CannedFeedURLProtocol.handlers[path] = (ApiFixtureLoader.data("web.communities.application.default"), 200)
        await model.loadApplicationDetail(id: "application-1")
        XCTAssertNoThrow(try CommunityApplicationDetail(viewModel: model, id: "application-1")
            .inspect().find(text: "I would like to join."))
        model.communityDetail = nil
        XCTAssertNil(model.applicationDetail)
        XCTAssertThrowsError(try CommunityApplicationDetail(viewModel: model, id: "application-1")
            .inspect().find(text: "I would like to join."))
        let requestCount = CannedFeedURLProtocol.capturedPathCount(path)
        await model.loadApplicationDetail(id: "application-1")
        XCTAssertNil(model.applicationDetail)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), requestCount)
    }

    func testMismatchedApplicationIdentityShowsDetailError() async throws {
        let model = try CommunityDetailViewModel(client: makeClient(), slug: "test-community", isAdministrator: true)
        let path = "/api/v1/communities/test-community/applications/different-application"
        CannedFeedURLProtocol.handlers[path] = (ApiFixtureLoader.data("web.communities.application.default"), 200)
        await model.loadApplicationDetail(id: "different-application")
        XCTAssertTrue(model.applicationDetailFailed)
        XCTAssertNil(model.applicationDetail)
        XCTAssertNoThrow(try CommunityApplicationDetail(viewModel: model, id: "different-application")
            .inspect().find(button: "Try Again"))
    }

    func testApplicationResponseAfterTabChangeCannotReplaceSidecars() async throws {
        let client = try makeClient()
        let model = CommunityDetailViewModel(
            client: client, slug: "test-community", initialTab: .applications, isAdministrator: true
        )
        let path = "/api/v1/communities/test-community/applications"
        CannedFeedURLProtocol.handlers[path] = (ApiFixtureLoader.data("web.communities.applications.default"), 200)
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let loading = Task {
            do {
                _ = try await model.loadApplicationRows(client: client, after: nil)
                return false
            } catch is CancellationError {
                return true
            } catch {
                XCTFail("Unexpected application loading error: \(error)")
                return false
            }
        }
        guard await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(path: path) else {
            CannedFeedURLProtocol.releaseResponse(path: path)
            _ = await loading.value
            return
        }
        model.selectedTab = .members
        CannedFeedURLProtocol.releaseResponse(path: path)
        let rejectedStaleResponse = await loading.value
        XCTAssertTrue(rejectedStaleResponse)
        XCTAssertTrue(model.applicationSummaries.isEmpty)
        XCTAssertTrue(model.applicationUsers.isEmpty)
    }

    func testDetailWithoutQuestionSidecarRendersAnswerWithOrdinalInsteadOfQuestionId() async throws {
        let model = try CommunityDetailViewModel(client: makeClient(), slug: "test-community", isAdministrator: true)
        let questionId = "00000000-0000-7000-8000-000000000999"
        let path = "/api/v1/communities/test-community/applications/application-1"
        CannedFeedURLProtocol.handlers[path] = (Data("""
        {"community_application":{"__entity_type":"community_application","id":"application-1","community_id":"community-1","user_id":"user-1",
        "answers":{"\(questionId)":"I will contribute useful examples."},
        "reviewed_at":null,"reviewed_by_id":null,"approved_at":null,"rejected_at":null,"rejection_reason":null,
        "message":"Full application message","created_at":"2026-01-01T00:00:00Z"}}
        """.utf8), 200)
        await model.loadApplicationDetail(id: "application-1")
        let inspection = try CommunityApplicationDetail(viewModel: model, id: "application-1").inspect()
        XCTAssertNoThrow(try inspection.find(text: "1: I will contribute useful examples."))
        let rendered = try inspection.findAll(ViewType.Text.self).map { try $0.string() }
        XCTAssertFalse(rendered.contains { $0.contains(questionId) })
    }

    func testCommunityDetailShowsLoadingErrorAndFullMessage() async throws {
        let model = try CommunityDetailViewModel(client: makeClient(), slug: "test-community", isAdministrator: true)
        let path = "/api/v1/communities/test-community/applications/application-1"
        CannedFeedURLProtocol.handlers[path] = (ApiFixtureLoader.data("web.communities.application.default"), 200)
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let opening = Task { await model.loadApplicationDetail(id: "application-1") }
        guard await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(path: path) else {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await opening.value
            return
        }
        XCTAssertTrue(model.isLoadingApplicationDetail)
        XCTAssertNoThrow(try CommunityApplicationDetail(viewModel: model, id: "application-1")
            .inspect().find(ViewType.ProgressView.self))
        CannedFeedURLProtocol.releaseResponse(path: path)
        await opening.value
        XCTAssertEqual(model.applicationDetail?.message, "I would like to join.")
        XCTAssertNoThrow(try CommunityApplicationDetail(viewModel: model, id: "application-1")
            .inspect().find(text: "I would like to join."))
        CannedFeedURLProtocol.handlers[path] = (Data(#"{"error":"unavailable"}"#.utf8), 500)
        await model.loadApplicationDetail(id: "application-1")
        XCTAssertTrue(model.applicationDetailFailed)
        XCTAssertNil(model.applicationDetail)
        XCTAssertNoThrow(try CommunityApplicationDetail(viewModel: model, id: "application-1")
            .inspect().find(button: "Try Again"))
    }

}
