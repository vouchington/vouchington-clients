import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class NativeSessionContractTests: XCTestCase {
    func testFixturesDecodeThroughTheirDTOs() throws {
        for fixture in nativeSessionFixtureCoverage {
            try fixture.run(fixture.id)
        }
        let decoder = makeVouchaDecoder()
        let login = try decoder.decode(
            NativeEmailLoginAuthenticatedResponse.self,
            from: ApiFixtureLoader.data("native.auth.email-address.login.authenticated")
        )
        XCTAssertEqual(login.user.entityType, "user")
        XCTAssertNil(login.user.accountType)
        XCTAssertEqual(login.session.sid, login.sid)
        XCTAssertEqual(login.sessionToken.payload.uid, login.uid)
        let mfa = try decoder.decode(
            NativeEmailLoginMFAResponse.self,
            from: ApiFixtureLoader.data("native.auth.email-address.login.mfa")
        )
        XCTAssertTrue(mfa.mfaRequired)
        let mint = try decoder.decode(
            NativeSessionMCPTokenResponse.self,
            from: ApiFixtureLoader.data("native.oauth.session-mcp-token.ios")
        )
        XCTAssertEqual(mint.tokenType, "Bearer")
        XCTAssertEqual(mint.expiresIn, 3_600)
    }

    func testRegisteredEndpointsMatchPublishedRequests() throws {
        for (id, endpoint) in nativeSessionFixtureEndpoints {
            let fixture = try XCTUnwrap(ApiFixtureLoader.swiftRouteFixtures.first { $0.id == id })
            XCTAssertEqual(endpoint.method.rawValue, fixture.method, id)
            XCTAssertEqual(endpoint.path, fixture.path, id)
            XCTAssertEqual(try requestBodyJSON(from: endpoint), fixture.requestBody, id)
        }
    }

    func testMintSendsTheNativeMintHeaderForEveryApp() {
        for app in [MemberMCPApp.ios, .macos, .windows] {
            let endpoint = Endpoint.mintNativeSessionMCPToken(app: app)
            XCTAssertEqual(endpoint.headers["x-voucha-native-mcp-mint"], "1")
            XCTAssertEqual(endpoint.method, .POST)
        }
    }
}
