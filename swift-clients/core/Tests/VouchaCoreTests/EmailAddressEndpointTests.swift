import VouchaAPI
import VouchaCore
import VouchaModels
import XCTest

final class EmailAddressEndpointTests: XCTestCase {
    func testEmailAddressEndpoints() {
        assertEndpoint(.myEmailAddresses(), path: "/api/v1/my/email-addresses")
        assertEndpoint(
            .requestMyEmailAddressVerification(emailAddress: " User@Example.com "),
            method: .POST,
            path: "/api/v1/my/email-addresses",
            body: ["email_address": " User@Example.com "]
        )
        assertEndpoint(
            .verifyMyEmailAddress(emailAddress: "user+tag@example.com", token: "ABCD1234"),
            method: .POST,
            path: "/api/v1/my/email-addresses/user%2Btag%40example.com/verifications",
            body: ["token": "ABCD1234"]
        )
    }

    func testEmailAddressResponsesDecode() throws {
        let decoder = makeVouchaDecoder()
        let empty = try decoder.decode(
            EmailAddressListResponse.self,
            from: Data(#"{"results":[],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#
                .utf8)
        )
        XCTAssertTrue(empty.results.isEmpty)

        let requested = try decoder.decode(
            EmailAddressVerificationRequestResponse.self,
            from: Data(#"{"email_address":"user@example.com"}"#.utf8)
        )
        XCTAssertEqual(requested.emailAddress, "user@example.com")

        let verified = try decoder.decode(
            EmailAddressListResponse.self,
            from: Data(
                #"{"results":[{"email_address":"user@example.com","is_primary":true,"created_at":"2026-07-11T12:00:00Z"}],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#
                    .utf8
            )
        )
        XCTAssertEqual(verified.results.first?.emailAddress, "user@example.com")
        XCTAssertEqual(verified.results.first?.isPrimary, true)
    }

    func testEmailVerificationRequiredHelperMatchesEveryApiErrorShape() {
        XCTAssertTrue(VouchaError.forbidden(preconditionCode: "EMAIL_VERIFICATION_REQUIRED")
            .isEmailVerificationRequired)
        XCTAssertTrue(VouchaError.api(statusCode: 403, preconditionCode: "EMAIL_VERIFICATION_REQUIRED")
            .isEmailVerificationRequired)
        XCTAssertTrue(VouchaError.apiMessage(
            statusCode: 403,
            preconditionCode: "EMAIL_VERIFICATION_REQUIRED",
            message: "Verify"
        ).isEmailVerificationRequired)
        XCTAssertFalse(VouchaError.forbidden(preconditionCode: "IDENTITY_REQUIRED").isEmailVerificationRequired)
    }
}
