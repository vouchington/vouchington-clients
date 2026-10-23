import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class NativeSessionTokenContractTests: XCTestCase {
    func testPublishedNativeSessionFixturesPreserveTypedFields() throws {
        let decoder = makeVouchaDecoder()
        let request = try decoder.decode(
            NativeEmailTokenResponse.self,
            from: ApiFixtureLoader.data("native.auth.email-address.tokens")
        )
        XCTAssertEqual(request.emailAddress, "fixture@example.test")
        let mfa = try decoder.decode(
            NativeEmailMFAResponse.self,
            from: ApiFixtureLoader.data("native.auth.email-address.login.mfa")
        )
        XCTAssertTrue(mfa.mfaRequired)
        XCTAssertEqual(mfa.loginAttemptId, "00000000-0000-7000-8000-000000000731")
        let login = try decoder.decode(
            NativeEmailAuthenticatedResponse.self,
            from: ApiFixtureLoader.data("native.auth.email-address.login.authenticated")
        )
        XCTAssertEqual(login.deviceToken.payload.did, login.did)
        XCTAssertEqual(login.sessionToken.payload.sid, login.sid)
        XCTAssertEqual(login.session.uid, login.uid)
        XCTAssertEqual(login.user.entityType, .user)
        XCTAssertEqual(login.user.emailAddress, "fixture@example.test")
        let token = try decoder.decode(
            NativeSessionMCPTokenResponse.self,
            from: ApiFixtureLoader.data("native.oauth.session-mcp-token.ios")
        )
        XCTAssertEqual(token.accessToken, "voucha_access_fixture-secret")
        XCTAssertEqual(token.tokenType, .bearer)
        XCTAssertEqual(token.expiresIn, 3_600)
        XCTAssertEqual(token.resource, "https://voucha.ai/api/v1/mcp")
        XCTAssertEqual(token.scope, "mcp.user:read mcp.user:write")
    }

    func testNativeLiteralValuesAndRequiredTokenFieldsAreValidated() {
        let decoder = makeVouchaDecoder()
        XCTAssertThrowsError(try decoder.decode(
            NativeEmailMFAResponse.self,
            from: Data(#"{"login_attempt_id":"a","mfa_required":false}"#.utf8)
        ))
        XCTAssertThrowsError(try decoder.decode(
            NativeEmailAuthenticatedUser.self,
            from: Data(#"{"id":"u","__entity_type":"post"}"#.utf8)
        ))
        XCTAssertThrowsError(try decoder.decode(
            NativeSessionMCPTokenResponse.self,
            from: Data(#"{"access_token":"t","expires_in":3600,"resource":"mcp","scope":"read","token_type":"Basic"}"#
                .utf8)
        ))
        XCTAssertThrowsError(try decoder.decode(
            NativeSessionMCPTokenResponse.self,
            from: Data(#"{"expires_in":3600,"resource":"mcp","scope":"read","token_type":"Bearer"}"#.utf8)
        ))
    }

    func testLiteralNamesAreCaseSensitive() {
        let decoder = makeVouchaDecoder()
        XCTAssertThrowsError(try decoder.decode(NativeAuthenticatedEntityType.self, from: Data(#""USER""#.utf8)))
        XCTAssertThrowsError(try decoder.decode(NativeSessionTokenType.self, from: Data(#""bearer""#.utf8)))
    }

    func testNativeTokenMintEndpointIncludesExplicitMintHeader() {
        let endpoint = Endpoint.mintNativeSessionMCPToken(clientApp: "ios")
        XCTAssertEqual(endpoint.headers["x-voucha-native-mcp-mint"], "1")
        assertEndpoint(
            endpoint,
            method: .POST,
            path: "/api/v1/oauth/native-session-tokens",
            body: ["client_app": "ios"]
        )
        assertEndpoint(
            .requestNativeEmailOTP(email: "a@example.test"),
            method: .POST,
            path: "/api/v1/auth/native/email-address/tokens",
            body: ["emailAddress": "a@example.test"]
        )
        assertEndpoint(
            .verifyNativeEmailOTP(email: "a@example.test", code: "123456"),
            method: .POST,
            path: "/api/v1/auth/native/email-address/login",
            body: ["emailAddress": "a@example.test", "token": "123456"]
        )
    }
}
