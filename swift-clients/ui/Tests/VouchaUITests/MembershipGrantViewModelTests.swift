import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class MembershipGrantViewModelTests: NativeRouteSurfaceViewModelTestCase {
    func testPlansLoadOnceAndKeepCachedResponse() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/memberships/plans"] = [(plansResponse, 200, 0)]
        let viewModel = try MembershipGrantViewModel(client: makeClient())

        await viewModel.loadPlans()
        await viewModel.loadPlans()

        XCTAssertEqual(viewModel.plans["pro"]?.map(\.id), ["sku-pro"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/memberships/plans" }.count, 1)
    }

    func testFailedPlanLoadCanRetry() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/memberships/plans"] = [
            (Data("{}".utf8), 500, 0),
            (plansResponse, 200, 0)
        ]
        let viewModel = try MembershipGrantViewModel(client: makeClient())

        await viewModel.loadPlans()
        XCTAssertNotNil(viewModel.plansError)
        await viewModel.loadPlans()

        XCTAssertNil(viewModel.plansError)
        XCTAssertEqual(viewModel.plans["pro"]?.first?.id, "sku-pro")
    }

    func testChangingPlanClearsSkuAndFiltersMismatchedRows() throws {
        let viewModel = MembershipGrantViewModel(client: nil)
        let pro = try sku()
        viewModel.plans = ["pro": [pro], "plus": [pro]]
        viewModel.selectPlan(.pro)
        viewModel.selectedSkuId = pro.id

        viewModel.selectPlan(.plus)

        XCTAssertNil(viewModel.selectedSkuId)
        XCTAssertTrue(viewModel.availableSkus.isEmpty)
    }

    func testGrantSuccessResetsFormAndPreservesCachedPlans() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/memberships/plans"] = [
            (plansResponse, 200, 0),
            (plansResponse, 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/membership-grants"] = [(
            Data(#"{"grant":{"id":"g-1"},"membership":{"id":"m-1"},"queued":false}"#.utf8),
            201,
            0
        )]
        let viewModel = try MembershipGrantViewModel(client: makeClient())
        let user = try user()
        await viewModel.loadPlans()
        viewModel.selectedUser = user
        viewModel.selectPlan(.pro)
        viewModel.selectedSkuId = "sku-pro"

        await viewModel.grant()

        XCTAssertNil(viewModel.selectedUser)
        XCTAssertNil(viewModel.selectedPlan)
        XCTAssertEqual(viewModel.plans["pro"]?.first?.id, "sku-pro")
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.last, "POST")
        let body = try XCTUnwrap(CannedFeedURLProtocol.capturedBodies.last ?? nil)
        XCTAssertTrue(body.contains("sku-pro"))
    }

    func testGrantRefreshesCatalogAndRejectsRetiredSelectionBeforePosting() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/memberships/plans"] = [
            (plansResponse, 200, 0),
            (Data(#"{"plans":{"plus":[]}}"#.utf8), 200, 0)
        ]
        let viewModel = try MembershipGrantViewModel(client: makeClient())
        await viewModel.loadPlans()
        viewModel.selectedUser = try user()
        viewModel.selectPlan(.pro)
        viewModel.selectedSkuId = "sku-pro"

        await viewModel.grant()

        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/memberships/plans" }.count,
            2
        )
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/membership-grants" })
        XCTAssertNil(viewModel.selectedSkuId)
        XCTAssertEqual(viewModel.submissionMessage, .message(.nativeSwiftMembershipMembershipGrantValidation))
    }

    func testGrantPostsWhenSkuRemainsActiveAfterCatalogRefresh() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/memberships/plans"] = [
            (plansResponse, 200, 0),
            (plansResponse, 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/membership-grants"] = [
            (Data(#"{"grant":{"id":"grant-1"},"membership":{"id":"membership-1"},"queued":false}"#.utf8), 201, 0)
        ]
        let viewModel = try MembershipGrantViewModel(client: makeClient())
        await viewModel.loadPlans()
        viewModel.selectedUser = try user()
        viewModel.selectPlan(.pro)
        viewModel.selectedSkuId = "sku-pro"

        await viewModel.grant()

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.last, "POST")
        XCTAssertEqual(viewModel.submissionMessage, .message(.nativeSwiftMembershipMembershipGrantSuccess))
    }

    func testGrantSubmitsTheSelectionCapturedBeforeTheCatalogRefresh() async throws {
        let plansPath = "/api/v1/memberships/plans"
        CannedFeedURLProtocol.queuedHandlers[plansPath] = [
            (multiplePlansResponse, 200, 0),
            (multiplePlansResponse, 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/membership-grants"] = [
            (Data(#"{"grant":{"id":"grant-1"},"membership":{"id":"membership-1"},"queued":false}"#.utf8), 201, 0)
        ]
        let viewModel = try MembershipGrantViewModel(client: makeClient())
        await viewModel.loadPlans()
        viewModel.selectedUser = try user()
        viewModel.selectPlan(.pro)
        viewModel.selectedSkuId = "sku-pro"

        CannedFeedURLProtocol.suspendResponse(path: plansPath)
        defer { CannedFeedURLProtocol.releaseResponse(path: plansPath) }
        let grant = Task { await viewModel.grant() }
        try await waitForCapturedPlans(count: 2)

        viewModel.selectedUser = try user(id: "user-2", username: "bob")
        viewModel.selectPlan(.plus)
        viewModel.selectedSkuId = "sku-plus"
        CannedFeedURLProtocol.releaseResponse(path: plansPath)
        await grant.value

        let body = try XCTUnwrap(CannedFeedURLProtocol.capturedBodies.last ?? nil)
        XCTAssertTrue(body.contains(#""user_id":"user-1""#))
        XCTAssertTrue(body.contains(#""plan":"pro""#))
        XCTAssertTrue(body.contains(#""sku_id":"sku-pro""#))
        XCTAssertFalse(body.contains("user-2"))
        XCTAssertFalse(body.contains("sku-plus"))
    }

    func testGrantIsDisabledWhileAnotherCatalogRefreshIsInFlight() async throws {
        let plansPath = "/api/v1/memberships/plans"
        CannedFeedURLProtocol.queuedHandlers[plansPath] = [
            (multiplePlansResponse, 200, 0),
            (multiplePlansResponse, 200, 0)
        ]
        let viewModel = try MembershipGrantViewModel(client: makeClient())
        await viewModel.loadPlans()
        viewModel.selectedUser = try user()
        viewModel.selectPlan(.pro)
        viewModel.selectedSkuId = "sku-pro"

        CannedFeedURLProtocol.suspendResponse(path: plansPath)
        defer { CannedFeedURLProtocol.releaseResponse(path: plansPath) }
        let refresh = Task { await viewModel.loadPlans(force: true) }
        try await waitForCapturedPlans(count: 2)

        XCTAssertFalse(viewModel.canSubmit)
        await viewModel.grant()
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/membership-grants"), 0)

        CannedFeedURLProtocol.releaseResponse(path: plansPath)
        await refresh.value
    }

    func testFailedGrantCatalogRefreshAllowsNormalPlansRetry() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/memberships/plans"] = [
            (plansResponse, 200, 0),
            (Data("{}".utf8), 500, 0),
            (Data(#"{"plans":{"pro":[]}}"#.utf8), 200, 0)
        ]
        let viewModel = try MembershipGrantViewModel(client: makeClient())
        await viewModel.loadPlans()
        viewModel.selectedUser = try user()
        viewModel.selectPlan(.pro)
        viewModel.selectedSkuId = "sku-pro"

        await viewModel.grant()

        XCTAssertNotNil(viewModel.plansError)
        XCTAssertNil(viewModel.submissionMessage)
        await viewModel.grant()
        XCTAssertNil(viewModel.selectedSkuId)
        XCTAssertEqual(viewModel.submissionMessage, .message(.nativeSwiftMembershipMembershipGrantValidation))
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/memberships/plans" }.count,
            3
        )
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/membership-grants" })
    }

    func testGrantFailurePreservesSelections() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/memberships/plans"] = [
            (plansResponse, 200, 0),
            (plansResponse, 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/membership-grants"] = [(Data("{}".utf8), 500, 0)]
        let viewModel = try MembershipGrantViewModel(client: makeClient())
        let user = try user()
        await viewModel.loadPlans()
        viewModel.selectedUser = user
        viewModel.selectPlan(.pro)
        viewModel.selectedSkuId = "sku-pro"

        await viewModel.grant()

        XCTAssertEqual(viewModel.selectedUser?.id, user.id)
        XCTAssertEqual(viewModel.selectedPlan, .pro)
        XCTAssertEqual(viewModel.selectedSkuId, "sku-pro")
        XCTAssertEqual(viewModel.submissionMessage, .message(.nativeSwiftMembershipMembershipGrantFailure))
    }

    func testGrantWithoutCompleteSelectionShowsValidationMessage() async {
        let viewModel = MembershipGrantViewModel(client: nil)

        await viewModel.grant()

        XCTAssertNotNil(viewModel.submissionMessage)
    }

    private var plansResponse: Data {
        Data(
            #"""
            {"plans":{"pro":[{"id":"sku-pro","plan":"pro","price":{"amount":1200,"currency":"usd"},
            "interval":"monthly","stripe_price_id":"price-pro"}]}}
            """#
            .utf8
        )
    }

    private var multiplePlansResponse: Data {
        Data(
            #"""
            {"plans":{"pro":[{"id":"sku-pro","plan":"pro","price":{"amount":1200,"currency":"usd"},
            "interval":"monthly","stripe_price_id":"price-pro"}],"plus":[{"id":"sku-plus","plan":"plus",
            "price":{"amount":2400,"currency":"usd"},"interval":"monthly","stripe_price_id":"price-plus"}]}}
            """#
            .utf8
        )
    }

    var userSearchPage: Data {
        Data(
            #"""
            {"results":[{"id":"user-1","username":"alice","roles":[],"profile_image_id":null,
            "markdown":null}],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}
            """#
            .utf8
        )
    }

    private func sku() throws -> MembershipSkuSummary {
        let response = try decoder().decode(MembershipPlansResponse.self, from: plansResponse)
        return try XCTUnwrap(response.plans["pro"]?.first)
    }

    func user() throws -> MembershipGrantUser {
        try user(id: "user-1", username: "alice")
    }

    private func user(id: String, username: String) throws -> MembershipGrantUser {
        try decoder().decode(
            MembershipGrantUser.self,
            from: Data(#"{"id":"\#(id)","username":"\#(username)","name":null}"#.utf8)
        )
    }

    private func decoder() -> JSONDecoder {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return decoder
    }

    private func waitForCapturedPlans(count: Int) async throws {
        for _ in 0 ..< 40 {
            if CannedFeedURLProtocol.capturedPathCount("/api/v1/memberships/plans") >= count {
                return
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        XCTFail("Timed out waiting for membership plans request.")
    }

}
