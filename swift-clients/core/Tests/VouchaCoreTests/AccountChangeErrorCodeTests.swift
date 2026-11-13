import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import VouchaCore
import XCTest

final class AccountChangeErrorCodeTests: XCTestCase {
    private func failure(status: Int, code: String?) async throws -> VouchaError {
        let client = try APIClient(config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://voucha.test"))))
        let response = try XCTUnwrap(HTTPURLResponse(
            url: XCTUnwrap(URL(string: "https://voucha.test")), statusCode: status, httpVersion: nil, headerFields: nil
        ))
        let body = try JSONSerialization.data(withJSONObject: code.map { ["code": $0] } ?? [:])
        do {
            try await client.validate(response: response, data: body)
        } catch {
            return try XCTUnwrap(error as? VouchaError)
        }
        throw XCTSkip("Expected a failure")
    }

    func testRemovalCodesAreDecodedOnBadRequestAndForbidden() async throws {
        let last = try await failure(status: 400, code: "LAST_SIGN_IN_METHOD_REMOVAL_FORBIDDEN")
        XCTAssertTrue(last.isLastSignInMethodRemovalForbidden)
        XCTAssertFalse(last.isAdministratorMfaRequired)
        let admin = try await failure(status: 403, code: "ADMINISTRATOR_MFA_REQUIRED")
        XCTAssertTrue(admin.isAdministratorMfaRequired)
        XCTAssertEqual(admin.apiStatusCode, 403)
    }

    func testReauthCodeSurvivesUnauthorizedGoneAndForbidden() async throws {
        for status in [401, 410, 403] {
            let error = try await failure(status: status, code: "MFA_REAUTH_REQUIRED")
            XCTAssertTrue(error.isMfaReauthRequired, "status \(status)")
            XCTAssertEqual(error.apiStatusCode, status)
        }
    }

    func testPlainUnauthorizedAndUnknownCodesAreUnchanged() async throws {
        let plain = try await failure(status: 401, code: nil)
        guard case .unauthorized = plain else { return XCTFail("401 without the code must stay unauthorized") }
        XCTAssertNil(plain.apiCode)
        XCTAssertNil(plain.apiStatusCode)
        let unknown = try await failure(status: 400, code: "SOMETHING_NEW")
        XCTAssertFalse(unknown.isLastSignInMethodRemovalForbidden)
        XCTAssertFalse(unknown.isMfaReauthRequired)
        XCTAssertEqual(unknown.apiStatusCode, 400)
        XCTAssertNil(VouchaError.notFound.apiStatusCode)
    }
}
