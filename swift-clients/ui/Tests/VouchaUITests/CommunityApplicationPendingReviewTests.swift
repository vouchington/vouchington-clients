import Foundation
import SwiftUI
import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class CommunityApplicationPendingReviewTests: NativeRouteSurfaceViewModelTestCase {
    func testApprovedAndRejectedApplicationsCannotBeSelectedOrSentForReview() async throws {
        let client = try makeClient()
        let model = CommunityDetailViewModel(client: client, slug: "builders", initialTab: .applications)
        for status in ["approved", "rejected"] {
            CannedFeedURLProtocol.handlers[applicationsPath] = try (applicationFixture(status: status), 200)
            let page = try await model.loadApplicationRows(client: client, after: nil)
            XCTAssertTrue(page.items.isEmpty)
            model.rowPagination.reset(items: page.items)
            model.selectedApplicationId = "application-1"
            let controls = CommunityApplicationReviewControls(viewModel: model, isSignedIn: true, showSignIn: {})
            XCTAssertThrowsError(try controls.inspect().find(ViewType.Picker.self).find(text: "Application testuser"))
            XCTAssertTrue(try controls.inspect().find(button: "Approve").isDisabled())
            XCTAssertTrue(try controls.inspect().find(button: "Reject").isDisabled())
            await model.approveApplication(applicationId: "application-1")
            await model.rejectApplication(applicationId: "application-1", reason: "Already reviewed")
        }
        XCTAssertFalse(CannedFeedURLProtocol.capturedMethods.contains("PATCH"))
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.allSatisfy {
            $0.query?.contains("status=pending") == true
        })
    }

    func testSuccessfulReviewReloadRemovesSelectionAndPreventsAnotherReview() async throws {
        let client = try makeClient()
        let model = CommunityDetailViewModel(
            client: client, slug: "builders", initialTab: .applications, isAdministrator: true
        )
        CannedFeedURLProtocol.handlers[applicationsPath] = try (applicationFixture(status: "pending"), 200)
        let page = try await model.loadApplicationRows(client: client, after: nil)
        model.rowPagination.reset(items: page.items)
        model.selectedApplicationId = "application-1"
        let controls = CommunityApplicationReviewControls(viewModel: model, isSignedIn: true, showSignIn: {})
        XCTAssertFalse(try controls.inspect().find(button: "Approve").isDisabled())
        CannedFeedURLProtocol.handlers[applicationsPath + "/application-1"] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders"] = (
            ApiFixtureLoader.data("web.communities.show.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/list-items/counts"] = (
            ApiFixtureLoader.data("web.communities.list-items.counts.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/application-questions"] = (
            ApiFixtureLoader.data("web.communities.application-questions.default"), 200
        )
        CannedFeedURLProtocol.handlers[applicationsPath] = try (applicationFixture(status: "approved"), 200)

        await model.approveApplication(applicationId: model.selectedApplicationId)

        XCTAssertEqual(model.state, .loaded)
        XCTAssertTrue(model.rowPagination.items.isEmpty)
        XCTAssertTrue(try controls.inspect().find(button: "Approve").isDisabled())
        XCTAssertTrue(try controls.inspect().find(button: "Reject").isDisabled())
        await model.approveApplication(applicationId: model.selectedApplicationId)
        await model.rejectApplication(applicationId: model.selectedApplicationId, reason: "Already reviewed")
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.filter { $0 == "PATCH" }.count, 1)
    }

    private var applicationsPath: String {
        "/api/v1/communities/builders/applications"
    }

    private func applicationFixture(status: String) throws -> Data {
        let fixture = ApiFixtureLoader.data("web.communities.applications.default")
        var object = try XCTUnwrap(JSONSerialization.jsonObject(with: fixture) as? [String: Any])
        var summaries = try XCTUnwrap(object["community_application_summaries"] as? [String: [String: Any]])
        summaries["application-1"]?["status"] = status
        object["community_application_summaries"] = summaries
        return try JSONSerialization.data(withJSONObject: object)
    }
}
