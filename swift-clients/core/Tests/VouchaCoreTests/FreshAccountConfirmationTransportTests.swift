import Foundation
@testable import VouchaAPI
import VouchaAuth
import VouchaCore
import XCTest

final class FreshAccountConfirmationTransportTests: XCTestCase {
    func testEveryExistingProtectedMutationCarriesItsOwnFreshProof() throws {
        let endpoints: [Endpoint] = [
            .requestMyEmailAddressVerification(emailAddress: "user@example.com", reAuthToken: "email-request"),
            .verifyMyEmailAddress(emailAddress: "user@example.com", token: "abcdefgh23", reAuthToken: "email-verify"),
            .beginNativeOAuthAuthorization(
                provider: .github,
                purpose: .connect,
                completionProofChallenge: "challenge",
                reAuthToken: "connect"
            ),
            .disconnectOAuthAccount(provider: .github, reAuthToken: "disconnect"),
            .beginNativeBlueskyAccountLink(
                handle: "example.test",
                completionProofChallenge: "challenge",
                reAuthToken: "bluesky-connect"
            ),
            .disconnectBlueskyAccount(reAuthToken: "bluesky-disconnect")
        ]
        let expected = [
            "email-request",
            "email-verify",
            "connect",
            "disconnect",
            "bluesky-connect",
            "bluesky-disconnect"
        ]
        for (endpoint, proof) in zip(endpoints, expected) {
            guard case let .object(body) = try requestBodyJSON(from: endpoint) else {
                return XCTFail("Expected protected JSON body")
            }
            XCTAssertEqual(body["re_auth_token"], .string(proof))
        }
    }

    func testAppleNonceHashesTheFreshServerChallengeBeforeSDKAuthorization() {
        XCTAssertEqual(
            FreshAccountConfirmationService.appleNonceHash("abc"),
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"
        )
    }

    func testConfirmationPurposeUsesDistinctEmailTOTPAndProviderRoutes() {
        XCTAssertEqual(
            Endpoint.verifyReAuthEmailCode("abcdefgh23").path,
            "/api/v1/auth/mfa/re-auth/email/verification"
        )
        XCTAssertEqual(
            Endpoint.verifyReAuthTOTP("123456").path,
            "/api/v1/auth/mfa/re-auth/totp/verification"
        )
        XCTAssertEqual(
            Endpoint.proveReAuthProvider(provider: "github", flowId: "flow").path,
            "/api/v1/auth/re-auth/providers/github/proofs"
        )
    }
}
