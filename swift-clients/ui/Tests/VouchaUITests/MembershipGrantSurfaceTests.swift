import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class MembershipGrantSurfaceTests: XCTestCase {
    func testRendersCandidatesSelectionAndSearchError() throws {
        let viewModel = MembershipGrantViewModel(client: nil)
        let user = try user(id: "user-1", username: "alice")
        viewModel.candidates = [user]
        viewModel.selectedUser = user
        viewModel.searchError = .verbatim("Search failed")

        let sut = MembershipGrantSurface(viewModel: viewModel)
            .environment(\.locale, Locale(identifier: "en_US"))

        XCTAssertNoThrow(try sut.inspect().find(button: "@alice"))
        XCTAssertNoThrow(try sut.inspect().find(text: "@alice"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Search failed"))
    }

    func testRendersIdentifierForSelectedUserWithoutUsername() throws {
        let viewModel = MembershipGrantViewModel(client: nil)
        viewModel.selectedUser = try decoder().decode(
            MembershipGrantUser.self,
            from: Data(#"{"id":"user-without-username","username":null}"#.utf8)
        )

        let sut = MembershipGrantSurface(viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(text: "user-without-username"))
    }

    func testRendersNoSkuPlanErrorAndSubmissionMessage() throws {
        let viewModel = MembershipGrantViewModel(client: nil)
        viewModel.selectPlan(.plus)
        viewModel.plansError = .verbatim("Plans failed")
        viewModel.submissionMessage = .verbatim("Membership granted")

        let sut = MembershipGrantSurface(viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(text: "No SKUs are available for this plan."))
        XCTAssertNoThrow(try sut.inspect().find(text: "Plans failed"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Retry"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Membership granted"))
    }

    func testRendersLocalizedSkuPriceLabelsForKnownIntervalsAndUnknownValues() throws {
        let viewModel = MembershipGrantViewModel(client: nil)
        viewModel.selectPlan(.pro)
        viewModel.plans = try [
            "pro": [
                sku(id: "month-sku", interval: "monthly", amount: 1_200, currency: "usd"),
                sku(id: "year-sku", interval: "yearly", amount: 12_000, currency: "usd"),
                sku(id: "other-sku", interval: "week", amount: 100, currency: "usd"),
                sku(id: "unknown-sku", interval: "monthly", amount: 100, currency: "xyz")
            ]
        ]

        let sut = MembershipGrantSurface(viewModel: viewModel)
            .environment(\.locale, Locale(identifier: "en_US"))

        XCTAssertNoThrow(try sut.inspect().find(text: "month-sku · $12.00 per month"))
        XCTAssertNoThrow(try sut.inspect().find(text: "year-sku · $120.00 per year"))
        XCTAssertNoThrow(try sut.inspect().find(text: "other-sku · $1.00 per week"))
        XCTAssertNoThrow(try sut.inspect().find(text: "unknown-sku · Price unavailable"))
    }

    private func sku(id: String, interval: String, amount: Int64, currency: String) throws -> MembershipSkuSummary {
        let response = try decoder().decode(
            MembershipPlansResponse.self,
            from: Data(
                #"""
                {"plans":{"pro":[{"id":"\#(id)","plan":"pro","price":{"amount":\#(amount),"currency":"\#(
                    currency
                )"},"interval":"\#(interval)","stripe_price_id":"price-\#(id)"}]}}
                """#
                .utf8
            )
        )
        return try XCTUnwrap(response.plans["pro"]?.first)
    }

    private func user(id: String, username: String) throws -> MembershipGrantUser {
        let page = try decoder().decode(
            Page<MembershipGrantUser>.self,
            from: Data(
                #"""
                {"results":[{"id":"\#(id)","username":"\#(
                    username
                )","roles":[],"profile_image_id":null,"markdown":null}],
                "page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}
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
