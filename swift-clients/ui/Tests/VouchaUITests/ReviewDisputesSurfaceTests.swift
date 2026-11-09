import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ReviewDisputesSurfaceTests: XCTestCase {
    func testStaffListRendersSummaryWithoutDraftEditorsOrActions() throws {
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            ReviewDisputeListResponse.self,
            from: ApiFixtureLoader.data("native.moderation.disputes.default")
        )
        let viewModel = ReviewDisputesViewModel(
            client: nil, isSignedIn: true, isAdministrator: true, isSiteModerator: false
        )
        viewModel.summaryPagination.reset(items: response.disputes)
        let inspection = try ReviewDisputesSurface(viewModel: viewModel).inspect()

        let dispute = try XCTUnwrap(response.disputes.first)
        XCTAssertNoThrow(try inspection.find(text: dispute.postLabel))
        XCTAssertThrowsError(try inspection.find(text: dispute.postId))
        XCTAssertThrowsError(try inspection.find(text: dispute.id))
        XCTAssertNoThrow(try inspection.find(text: "native-claimant"))
        XCTAssertNoThrow(try inspection.find(text: "Pending"))
        XCTAssertThrowsError(try inspection.find(text: "The published rating uses the wrong total."))
        XCTAssertThrowsError(try inspection.find(button: "Approve"))
        XCTAssertTrue(inspection.findAll(ViewType.TextEditor.self).isEmpty)
        XCTAssertTrue(viewModel.disputes.isEmpty)
    }

    func testCanonicalDetailRendersPostLabelWithoutOpaqueIds() throws {
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            ReviewDisputeResponse.self,
            from: ApiFixtureLoader.data("native.moderation.disputes.detail.default")
        )
        let dispute = response.dispute
        let viewModel = ReviewDisputesViewModel(
            client: nil, isSignedIn: true, isAdministrator: true, isSiteModerator: false
        )
        let inspection = try ReviewDisputeCard(viewModel: viewModel, dispute: dispute).inspect()

        XCTAssertNoThrow(try inspection.find(text: dispute.postLabel))
        XCTAssertThrowsError(try inspection.find(text: dispute.postId))
        XCTAssertThrowsError(try inspection.find(text: dispute.id))
    }

    func testStaffSurfaceRendersContextClaimDraftsAndLifecycleActions() throws {
        let dispute = try fixture()
        let viewModel = ReviewDisputesViewModel(
            client: nil, isSignedIn: true, isAdministrator: false, isSiteModerator: true
        )
        viewModel.disputes = [dispute]
        viewModel.seedDrafts(from: [dispute])

        let inspection = try ReviewDisputeCard(viewModel: viewModel, dispute: dispute).inspect()
        for text in [
            "Pending", "Quarterly review", "Example Rewards", "issuer",
            "The published rating uses the wrong total.", "Internal evidence",
            "Private staff notes"
        ] {
            XCTAssertNoThrow(try inspection.find(text: text))
        }
        XCTAssertNoThrow(try inspection.find(button: "Save"))
        XCTAssertNoThrow(try inspection.find(button: "Approve"))
        XCTAssertNoThrow(try inspection.find(button: "Deliver"))
        XCTAssertNoThrow(try inspection.find(button: "Re-run AI"))
        XCTAssertNoThrow(try inspection.find(button: "Remove"))
        XCTAssertNoThrow(try inspection.find(button: "Annotate"))
        XCTAssertNoThrow(try inspection.find(button: "Dismiss"))
    }

    func testClaimIsRenderedAsTextWithoutAnEditor() throws {
        let dispute = try fixture()
        let viewModel = ReviewDisputesViewModel(
            client: nil, isSignedIn: true, isAdministrator: true, isSiteModerator: false
        )
        viewModel.disputes = [dispute]
        viewModel.seedDrafts(from: [dispute])
        let inspection = try ReviewDisputeCard(viewModel: viewModel, dispute: dispute).inspect()

        XCTAssertNoThrow(try inspection.find(text: "The published rating uses the wrong total."))
        XCTAssertEqual(inspection.findAll(ViewType.TextEditor.self).count, 2)
    }

    func testMemberAndSignedOutSurfacesRenderDenialStates() throws {
        let member = ReviewDisputesSurface(
            client: nil, isSignedIn: true, isAdministrator: false, isSiteModerator: false
        )
        let signedOut = ReviewDisputesSurface(
            client: nil, isSignedIn: false, isAdministrator: false, isSiteModerator: false
        )
        XCTAssertNoThrow(try member.inspect().find(text: "Staff access required"))
        XCTAssertNoThrow(try signedOut.inspect().find(text: "Sign in required"))
    }

    func testDedicatedRouteSuppressesGenericLoader() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/disputes"))
        let surface = NativeRouteDestinationSurface(
            entry: route.entry,
            client: nil,
            routeMatch: route.match,
            routeQuery: nil,
            isSignedIn: true,
            isAdministrator: true,
            showSignIn: {}
        )

        XCTAssertTrue(surface.isDedicatedStaffDisputesRoute)
        XCTAssertFalse(surface.shouldLoadRouteSurfaceContent)
    }

    func testInitialLoadErrorIncludesRetryAction() throws {
        let viewModel = ReviewDisputesViewModel(
            client: nil, isSignedIn: true, isAdministrator: true, isSiteModerator: false
        )
        viewModel.errorMessage = .verbatim("Unavailable")

        let inspection = try ReviewDisputesSurface(viewModel: viewModel).inspect()

        XCTAssertNoThrow(try inspection.find(text: "Unavailable"))
        XCTAssertNoThrow(try inspection.find(button: "Try Again"))
    }

    private func fixture() throws -> ReviewDispute {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(
            ReviewDispute.self,
            from: Data(ReviewDisputesTestSupport.dispute().utf8)
        )
    }
}
