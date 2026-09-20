import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class MembershipGrantErrorPresentationTests: NativeRouteSurfaceViewModelTestCase {
    func testGrantShowsIntentionalClientErrorMessage() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/memberships/plans"] = [(plansResponse, 200, 0)]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/membership-grants"] = [
            (Data(#"{"message":"Choose another SKU."}"#.utf8), 422, 0)
        ]
        let viewModel = try MembershipGrantViewModel(client: makeClient())
        viewModel.plans = try ["pro": [sku()]]
        viewModel.selectedUser = try user()
        viewModel.selectPlan(.pro)
        viewModel.selectedSkuId = "sku-pro"

        await viewModel.grant()

        XCTAssertEqual(viewModel.submissionMessage, .userContent("Choose another SKU."))
    }

    private func sku() throws -> MembershipSkuSummary {
        let response = try decoder().decode(
            MembershipPlansResponse.self,
            from: plansResponse
        )
        return try XCTUnwrap(response.plans["pro"]?.first)
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

    private func user() throws -> MembershipGrantUser {
        let page = try decoder().decode(
            Page<MembershipGrantUser>.self,
            from: Data(
                #"""
                {"results":[{"id":"user-1","username":"alice","roles":[],"profile_image_id":null,
                "markdown":null}],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}
                """#
                .utf8
            )
        )
        return try XCTUnwrap(page.results.first)
    }

    private func decoder() -> JSONDecoder {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return decoder
    }
}
