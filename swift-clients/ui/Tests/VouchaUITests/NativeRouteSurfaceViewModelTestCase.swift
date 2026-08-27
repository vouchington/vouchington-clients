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
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.contentTypes = [:]
        CannedFeedURLProtocol.errors = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    override func tearDown() {
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.contentTypes = [:]
        CannedFeedURLProtocol.errors = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
        CannedFeedURLProtocol.discardPendingResponses()
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

    func entry(for destination: NativeRouteDestinationIdentifier) throws -> NativeRouteCatalogEntry {
        try XCTUnwrap(NativeRouteCatalog.includedEntries.first { $0.destinationIdentifier == destination })
    }
}
