@testable import VouchaAPI
import VouchaModels
import XCTest

final class IdentityVerificationAdministrationEndpointTests: XCTestCase {
    func testGrantEndpointEscapesTargetAndEncodesAuditableNote() {
        let endpoint = Endpoint.grantIdentityVerificationAttempt(
            userId: "user /1",
            note: "Provider terminal error reviewed by support."
        )

        assertEndpoint(
            endpoint,
            method: .POST,
            path: "/api/v1/admin/users/user%20%2F1/identity-verification-attempts",
            body: ["note": "Provider terminal error reviewed by support."]
        )
    }

    func testGrantResponseDecodesSharedFixtureShape() throws {
        let response = try JSONDecoder().decode(
            IdentityVerificationAttemptGrantResponse.self,
            from: Data(#"{"granted":true}"#.utf8)
        )

        XCTAssertTrue(response.granted)
    }
}
