import Foundation
import SwiftUI
import ViewInspector
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeUsersBrowseAccountTypeTests: NativeRouteSurfaceViewModelTestCase {
    func testUserSearchRendersEveryAccountClassificationAndOmitsNull() async throws {
        let cases: [(AccountType?, String?)] = [
            (.official, "Official"), (nil, nil), (.system, "System"), (.aiAgent, "AI Agent")
        ]
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/users?q=alice"))

        for (accountType, expectedLabel) in cases {
            let value = accountType.map { "\"\($0.rawValue)\"" } ?? "null"
            CannedFeedURLProtocol.handlers["/api/v1/users"] = (
                Data(
                    """
                    {
                      "results": [{"id":"user-1","username":"alice","account_type":\(value)}],
                      "page_info": {"has_next_page":false,"start_cursor":null,"end_cursor":null}
                    }
                    """.utf8
                ),
                200
            )
            let viewModel = try NativeRouteSurfaceViewModel(
                entry: route.entry, client: makeClient(), routeMatch: route.match
            )

            await viewModel.load()

            let row = try XCTUnwrap(viewModel.rows.first)
            XCTAssertEqual(row.accountType, accountType)
            let badge = try NativeSurfaceRow(row: row)
                .environment(\.locale, uiEnglishTestLocale)
                .inspect()
                .find(AccountTypeBadge.self)
            if let expectedLabel {
                XCTAssertEqual(try badge.find(text: expectedLabel).string(), expectedLabel)
            } else {
                XCTAssertTrue(badge.findAll(ViewType.Text.self).isEmpty)
            }
        }
    }
}
