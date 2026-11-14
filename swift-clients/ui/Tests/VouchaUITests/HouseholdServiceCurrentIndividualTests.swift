import Foundation
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class HouseholdServiceCurrentIndividualTests: XCTestCase {
    func testCurrentIndividualSendsLookupAndDecodesIdentity() async throws {
        CannedFeedURLProtocol.handlers = [
            "/api/v1/me/individual": (
                Data(#"{"individual":{"id":"current-person","updated_at":"2026-07-02T00:00:00Z"}}"#.utf8),
                200
            )
        ]
        CannedFeedURLProtocol.capturedURLs = []
        defer {
            CannedFeedURLProtocol.handlers = [:]
            CannedFeedURLProtocol.capturedURLs = []
        }
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "http://localhost:2999"))),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )

        let individual = try await HouseholdService(client: client).currentIndividual()

        XCTAssertEqual(individual.id, "current-person")
        XCTAssertNil(individual.userId)
        XCTAssertNil(individual.username)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), ["/api/v1/me/individual"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["GET"])
    }
}
