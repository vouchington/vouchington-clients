import Foundation
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

func verbatimRow(
    icon: String,
    title: String,
    detail: String,
    externalURL: URL? = nil
) -> NativeRouteDestinationRow {
    NativeRouteDestinationRow(
        icon: icon,
        title: .verbatim(title),
        detail: .verbatim(detail),
        externalURL: externalURL
    )
}

@MainActor
class NativeRouteSurfaceViewModelTestCase: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.reset()
    }

    func settleProductSignals(_ viewModel: NativeRouteSurfaceViewModel) async {
        await viewModel.productSignalTask?.value
    }

    override func tearDown() {
        CannedFeedURLProtocol.reset()
        super.tearDown()
    }

    func makeClient() throws -> APIClient {
        try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test-site-key"
            ),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
    }

    func registerEmptyCommunityModeratorStatsResponse(slug: String) {
        CannedFeedURLProtocol.handlers["/api/v1/communities/\(slug)/moderator-stats"] = (
            Data(#"{"window":30,"stats":[],"users":{}}"#.utf8), 200
        )
    }

    func entry(for destination: NativeRouteDestinationIdentifier) throws -> NativeRouteCatalogEntry {
        try XCTUnwrap(NativeRouteCatalog.includedEntries.first { $0.destinationIdentifier == destination })
    }
}
