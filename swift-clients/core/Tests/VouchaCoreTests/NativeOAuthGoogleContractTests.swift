import Foundation
@testable import VouchaAPI
@testable import VouchaCore
import VouchaModels
import XCTest

final class NativeOAuthGoogleContractTests: XCTestCase {
    func testGoogleBrokerRoutesUseTheAdvertisedProviderSlug() {
        assertEndpoint(
            .beginNativeOAuthAuthorization(
                provider: .google,
                purpose: .authenticate,
                completionProofChallenge: "proof-challenge"
            ),
            method: .POST,
            path: "/api/v1/auth/oauth/google/authorizations",
            body: [
                "purpose": "authenticate",
                "callback_mode": "native",
                "completion_proof_challenge": "proof-challenge"
            ]
        )
        assertEndpoint(
            .disconnectOAuthAccount(provider: .google),
            method: .DELETE,
            path: "/api/v1/auth/oauth/google/connect"
        )
    }

    func testGoogleCapabilitiesRequireExplicitNativeAdvertise() throws {
        let enabled = try decodeCapabilities(google: """
        "google":{"version":1,"modes":{"web":true,"native":true},"purposes":["authenticate","connect"]}
        """)
        XCTAssertTrue(enabled[.google].supportsNative(.authenticate))
        XCTAssertTrue(enabled[.google].supportsNative(.connect))

        let absent = try decodeCapabilities(google: nil)
        XCTAssertFalse(absent[.google].supportsNative(.authenticate))
        XCTAssertFalse(absent[.google].supportsNative(.connect))
    }

    private func decodeCapabilities(google: String?) throws -> OAuthBrokerCapabilities {
        let googleField = google.map { ",\($0)" } ?? ""
        let response = try makeVouchaDecoder().decode(
            OAuthBrokerCapabilitiesResponse.self,
            from: Data("""
            {"providers":["facebook","x","github"],"broker_capabilities":{
              "facebook":{"version":1,"modes":{"web":false,"native":false},"purposes":[]},
              "x":{"version":1,"modes":{"web":false,"native":false},"purposes":[]},
              "github":{"version":1,"modes":{"web":false,"native":false},"purposes":[]}\(googleField)
            }}
            """.utf8)
        )
        return response.brokerCapabilities
    }
}
