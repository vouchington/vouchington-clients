@testable import VouchaAPI
import VouchaModels
import XCTest

final class ReAuthContractTests: XCTestCase {
    private func decode<T: Decodable>(_ id: String, as _: T.Type = T.self) throws -> T {
        try makeVouchaDecoder().decode(T.self, from: ApiFixtureLoader.data(id))
    }

    func testMethodsFixtureReportsEveryConfirmationMethod() throws {
        let methods: ReAuthMethodsResponse = try decode("native.auth.re-auth.methods.default")
        XCTAssertTrue(methods.hasEmail && methods.hasPasskey && methods.hasTotp)
        XCTAssertEqual(methods.providers, ["google"])
    }

    func testEveryVerificationFixtureReturnsTheSameOneUseProof() throws {
        for id in [
            "native.auth.re-auth.email.verification", "native.auth.re-auth.totp.verification",
            "native.auth.re-auth.passkeys.verification", "native.auth.re-auth.providers.verification.google",
            "native.auth.re-auth.providers.proof.github"
        ] {
            XCTAssertEqual(try decode(id, as: ReAuthTokenResponse.self).reAuthToken, "fresh-auth-fixture", id)
        }
    }

    func testPasskeyOptionsKeepFlowAndAssertionOptions() throws {
        let response: ReAuthPasskeyOptionsResponse = try decode("native.auth.re-auth.passkeys.options")
        XCTAssertEqual(response.flowId, "00000000-0000-7000-8000-000000000742")
        XCTAssertEqual(response.options.rpId, "voucha.ai")
        XCTAssertEqual(response.options.timeout, 60_000)
        XCTAssertEqual(response.options.userVerification, "preferred")
        XCTAssertEqual(response.options.allowCredentials?.first?.transports, ["internal"])
    }

    func testProviderFixturesKeepNonceAndBrokerHandoff() throws {
        let challenge: ReAuthProviderChallengeResponse = try decode("native.auth.re-auth.providers.challenge.google")
        XCTAssertEqual(challenge.nonce, "fixture-provider-nonce")
        let authorization: BeginNativeOAuthAuthorizationResponse =
            try decode("native.auth.re-auth.providers.authorization.github")
        XCTAssertEqual(authorization.flowId, challenge.flowId)
        XCTAssertTrue(authorization.redirectUrl.hasPrefix("https://github.com/"))
    }

    func testRotatedSessionCarriesBothReplacementTokensAndLifetimes() throws {
        let session = try decode("native.auth.session.refresh.rotated", as: NativeSessionRefreshResponse.self).session
        XCTAssertEqual(session.deviceToken, "fixture-rotated-device-jwt")
        XCTAssertEqual(session.sessionToken, "fixture-rotated-session-jwt")
        XCTAssertEqual(session.deviceExpiration, 2_592_000)
        XCTAssertEqual(session.sessionExpiration, 172_800)
        XCTAssertTrue(session.secure)
        XCTAssertEqual(session.session.sid, session.sid)
        XCTAssertEqual(session.session.client, "native")
    }

    func testAnonymousRotatedSessionDecodesWithoutAUser() throws {
        let json = Data("""
        {"session":{"did":"d","dt":"dt","dte":1,"secure":true,"sid":"s","st":"st","ste":1,"uid":null,
        "session":{"client":"native","did":"d","rca":0,"rol":[],"sca":0,"sid":"s","uid":null}}}
        """.utf8)
        let session = try makeVouchaDecoder().decode(NativeSessionRefreshResponse.self, from: json).session
        XCTAssertNil(session.uid)
        XCTAssertNil(session.session.uid)
    }

    func testBotChallengeFixtureIsTheTypedRequiredBody() throws {
        let body: BotChallengeRequiredResponse = try decode("native.bot-challenge.required")
        XCTAssertEqual(body.code, BotChallengeRequiredResponse.requiredCode)
        XCTAssertEqual(body.action, "bot-challenge.mutation")
    }

    func testRegisteredEndpointsMatchPublishedRequests() throws {
        for (id, endpoint) in nativeReAuthFixtureEndpoints {
            let fixture = try XCTUnwrap(ApiFixtureLoader.swiftRouteFixtures.first { $0.id == id })
            XCTAssertEqual(endpoint.method.rawValue, fixture.method, id)
            XCTAssertEqual(endpoint.path, fixture.path, id)
            XCTAssertEqual(try requestBodyJSON(from: endpoint), fixture.requestBody, id)
        }
    }

    func testProviderSegmentIsPercentEncoded() {
        XCTAssertEqual(
            Endpoint.reAuthProviderChallenge(provider: "a/b").path,
            "/api/v1/auth/re-auth/providers/a%2Fb/challenges"
        )
    }
}
