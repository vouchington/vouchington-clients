#if canImport(AuthenticationServices)
    import AuthenticationServices
    import Foundation
    @testable import VouchaAuth
    import XCTest

    @MainActor
    final class PasskeyAssertionRequestTests: XCTestCase {
        func testOwnedCredentialChallengeAndVerificationApplyToPlatformAndSecurityKey() throws {
            let options = PasskeyAuthenticationOptions(
                challenge: "AQID", rpId: "voucha.ai", userVerification: "required",
                allowCredentials: [.init(id: "BAUG")]
            )
            let requests = try options.authorizationRequests()
            XCTAssertEqual(requests.count, 2)
            let platform = try XCTUnwrap(requests[0] as? ASAuthorizationPlatformPublicKeyCredentialAssertionRequest)
            let securityKey =
                try XCTUnwrap(requests[1] as? ASAuthorizationSecurityKeyPublicKeyCredentialAssertionRequest)
            XCTAssertEqual(platform.challenge, Data([1, 2, 3]))
            XCTAssertEqual(securityKey.challenge, platform.challenge)
            XCTAssertEqual(platform.relyingPartyIdentifier, "voucha.ai")
            XCTAssertEqual(securityKey.relyingPartyIdentifier, platform.relyingPartyIdentifier)
            XCTAssertEqual(platform.userVerificationPreference, .required)
            XCTAssertEqual(securityKey.userVerificationPreference, .required)
            XCTAssertEqual(platform.allowedCredentials.map(\.credentialID), [Data([4, 5, 6])])
            XCTAssertEqual(securityKey.allowedCredentials.map(\.credentialID), [Data([4, 5, 6])])
            XCTAssertEqual(securityKey.allowedCredentials.first?.transports, [.usb, .nfc, .bluetooth])
        }

        func testDiscoverableCredentialsDoNotRestrictAuthenticatorClass() throws {
            for (preference, expected) in [
                ("discouraged", ASAuthorizationPublicKeyCredentialUserVerificationPreference.discouraged),
                ("preferred", .preferred)
            ] {
                let options = PasskeyAuthenticationOptions(
                    challenge: "AQID",
                    rpId: "voucha.ai",
                    userVerification: preference,
                    allowCredentials: nil
                )
                let requests = try options.authorizationRequests()
                let platform = try XCTUnwrap(requests
                    .first as? ASAuthorizationPlatformPublicKeyCredentialAssertionRequest)
                let securityKey = try XCTUnwrap(requests
                    .last as? ASAuthorizationSecurityKeyPublicKeyCredentialAssertionRequest)
                XCTAssertTrue(platform.allowedCredentials.isEmpty)
                XCTAssertTrue(securityKey.allowedCredentials.isEmpty)
                XCTAssertEqual(platform.userVerificationPreference, expected)
                XCTAssertEqual(securityKey.userVerificationPreference, expected)
            }
        }

        func testInvalidOwnedCredentialNeverWidensToDiscoverableAssertion() {
            let options = PasskeyAuthenticationOptions(
                challenge: "AQID",
                rpId: "voucha.ai",
                userVerification: "required",
                allowCredentials: [.init(id: "not valid base64!")]
            )
            XCTAssertThrowsError(try options.authorizationRequests())
        }

        func testCommonAssertionProtocolNormalizesEitherAuthenticatorToOneWebAuthnBody() throws {
            let response = PasskeyAuthenticationResponse(assertion: TestPublicKeyAssertion())
            let data = try JSONEncoder().encode(response)
            let body = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
            XCTAssertEqual(body["id"] as? String, "AQID")
            XCTAssertEqual(body["rawId"] as? String, "AQID")
            XCTAssertEqual(body["type"] as? String, "public-key")
            let assertion = try XCTUnwrap(body["response"] as? [String: String])
            XCTAssertEqual(assertion["authenticatorData"], "BAU")
            XCTAssertEqual(assertion["clientDataJSON"], "Bgc")
            XCTAssertEqual(assertion["signature"], "CAk")
            XCTAssertEqual(assertion["userHandle"], "Cgs")
        }
    }

    @objc(VouchaFreshConfirmationTestPublicKeyAssertion)
    private final class TestPublicKeyAssertion: NSObject, ASAuthorizationPublicKeyCredentialAssertion {
        static var supportsSecureCoding: Bool {
            true
        }

        let credentialID = Data([1, 2, 3])
        let rawAuthenticatorData = Data([4, 5])
        let rawClientDataJSON = Data([6, 7])
        let signature = Data([8, 9])
        let userID = Data([10, 11])
        override init() {
            super.init()
        }

        required init?(coder _: NSCoder) {
            nil
        }

        func encode(with _: NSCoder) {}
        func copy(with _: NSZone? = nil) -> Any {
            self
        }
    }
#endif
