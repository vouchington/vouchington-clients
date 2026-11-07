import Foundation
import SwiftUI
import ViewInspector
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class SavedLabelsPresentationTests: NativeRouteSurfaceViewModelTestCase {
    func testSavedListRowsRenderFixtureLabelAndLocalizedUntitledPost() throws {
        let response = try decoder().decode(
            ListItemsResponse.self, from: ApiFixtureLoader.data("native.list-items.default")
        )
        let article = try ListsItemRow(item: XCTUnwrap(response.listItems["list-item-1"]))
        XCTAssertNoThrow(try article.inspect().find(text: "Saved article"))
        XCTAssertThrowsError(try article.inspect().find(text: "item-1"))
        let post = try ListsItemRow(item: XCTUnwrap(response.listItems["list-item-2"]))
        XCTAssertNoThrow(try post.inspect().find(text: "Untitled post"))
        XCTAssertThrowsError(try post.inspect().find(text: "post-1"))
        let french = post.environment(\.locale, Locale(identifier: "fr"))
        XCTAssertNoThrow(try french.inspect().find(text: UiMessages.string(
            .nativeSwiftModerationReportsReviewQueueUntitledPost, locale: Locale(identifier: "fr")
        )))
    }

    func testApplicantRowsJoinNonmemberProfilesAndOfferSelectionWithoutIdInput() async throws {
        let fixture = ApiFixtureLoader.data("web.communities.applications.default")
        let client = try makeClient()
        let viewModel = CommunityDetailViewModel(client: client, slug: "builders", initialTab: .applications)
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/applications"] = (fixture, 200)
        let page = try await viewModel.loadApplicationRows(client: client, after: nil)
        XCTAssertEqual(page.items.first?.row.title, "Application testuser")
        XCTAssertEqual(page.items.first?.id, "application-1")
        viewModel.rowPagination.reset(items: page.items)
        let controls = CommunityApplicationReviewControls(viewModel: viewModel, isSignedIn: true, showSignIn: {})
        XCTAssertNoThrow(try controls.inspect().find(ViewType.Picker.self).find(text: "Application testuser"))
        XCTAssertEqual(try controls.inspect().findAll(ViewType.TextField.self).count, 1)
        XCTAssertEqual(
            try controls.inspect().find(ViewType.TextField.self).labelView().text().string(),
            "Rejection reason"
        )
        XCTAssertTrue(try controls.inspect().find(button: "Approve").isDisabled())
        try await ViewHosting.host(controls) {
            try controls.inspect().find(ViewType.Picker.self).select(value: "application-1")
            XCTAssertEqual(viewModel.selectedApplicationId, "application-1")
            XCTAssertFalse(try controls.inspect().find(button: "Approve").isDisabled())
            XCTAssertFalse(try controls.inspect().find(button: "Reject").isDisabled())
            viewModel.rowPagination.reset()
            XCTAssertTrue(try controls.inspect().find(button: "Approve").isDisabled())
        }
        XCTAssertNoThrow(try NativeSurfaceRow(row: XCTUnwrap(page.items.first?.row))
            .inspect().find(text: "Application testuser"))
    }

    func testApplicantDisplayNamePrecedesUsernameAndMissingProfileIsLocalized() async throws {
        let fixture = ApiFixtureLoader.data("web.communities.applications.default")
        var object = try XCTUnwrap(JSONSerialization.jsonObject(with: fixture) as? [String: Any])
        let client = try makeClient()
        let viewModel = CommunityDetailViewModel(client: client, slug: "builders", initialTab: .applications)
        for (profile, expected) in [
            (["id": "user-1", "name": "Applicant Name", "username": "testuser"], "Applicant Name"),
            (["id": "user-1", "name": " ", "username": "testuser"], "testuser"),
            (["id": "user-1"], "Deleted user")
        ] {
            object["users"] = ["user-1": profile]
            CannedFeedURLProtocol.handlers["/api/v1/communities/builders/applications"] = try (
                JSONSerialization.data(withJSONObject: object), 200
            )
            let page = try await viewModel.loadApplicationRows(client: client, after: nil)
            XCTAssertEqual(page.items.first?.row.title, "Application \(expected)")
        }
        object["users"] = [:] as [String: Any]
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/applications"] = try (
            JSONSerialization.data(withJSONObject: object), 200
        )
        let page = try await viewModel.loadApplicationRows(client: client, after: nil)
        let row = try XCTUnwrap(page.items.first?.row)
        XCTAssertEqual(row.title, "Application Deleted user")
        XCTAssertNoThrow(try NativeSurfaceRow(row: row).environment(\.locale, Locale(identifier: "fr"))
            .inspect().find(text: UiMessages.string(
                .message(
                    .nativeSwiftCommunitiesApplicationValue,
                    textParameters: ["id": .message(.nativeSwiftDynamicConfigFeatureFlagsDeletedUser)]
                ),
                locale: Locale(identifier: "fr")
            )))
    }

    private func decoder() -> JSONDecoder {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return decoder
    }
}
