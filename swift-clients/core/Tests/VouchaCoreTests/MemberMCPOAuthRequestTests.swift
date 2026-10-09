import Crypto
import Foundation
@testable import VouchaAPI
import XCTest

final class MemberMCPOAuthRequestTests: XCTestCase {
    func testAppleRequestUsesFreshPKCEAndExactResourceAndChecksCallback() throws {
        let site = try XCTUnwrap(URL(string: "https://example.test"))
        let authorization = try XCTUnwrap(URL(string: "https://example.test/authorize"))
        let callback = try XCTUnwrap(URL(string: "https://example.test/oauth/native/macos/callback"))
        let pending = try MemberMCPOAuthRequest(
            siteOrigin: site, app: .macos, issuerIdentifier: "https://example.test",
            authorizationEndpoint: authorization, redirectURI: callback
        )
        let second = try MemberMCPOAuthRequest(
            siteOrigin: site, app: .macos, issuerIdentifier: "https://example.test",
            authorizationEndpoint: authorization, redirectURI: callback
        )
        XCTAssertNotEqual(pending.state, second.state)
        XCTAssertNotEqual(pending.verifier, second.verifier)
        let parts = try XCTUnwrap(URLComponents(url: pending.authorizationURL, resolvingAgainstBaseURL: false))
        let query = Dictionary(uniqueKeysWithValues: (parts.queryItems ?? []).map { ($0.name, $0.value ?? "") })
        XCTAssertEqual(query["client_id"], "https://example.test/api/v1/oauth/native-clients/macos")
        XCTAssertEqual(query["resource"], "https://example.test/api/v1/mcp")
        XCTAssertEqual(query["redirect_uri"], callback.absoluteString)
        XCTAssertEqual(query["scope"], "mcp.user:read")
        XCTAssertEqual(query["code_challenge_method"], "S256")
        let digest = Data(SHA256.hash(data: Data(pending.verifier.utf8)))
        let expected = digest.base64EncodedString().replacingOccurrences(of: "+", with: "-")
            .replacingOccurrences(of: "/", with: "_").replacingOccurrences(of: "=", with: "")
        XCTAssertEqual(query["code_challenge"], expected)

        let valid =
            try XCTUnwrap(URL(string: "\(callback)?code=code-1&state=\(pending.state)&iss=https%3A%2F%2Fexample.test"))
        XCTAssertEqual(try pending.code(from: valid), "code-1")
        let wrongState =
            try XCTUnwrap(URL(string: "\(callback)?code=code-1&state=wrong&iss=https%3A%2F%2Fexample.test"))
        XCTAssertThrowsError(try pending.code(from: wrongState))
        let wrongIssuer =
            try XCTUnwrap(URL(string: "\(callback)?code=code-1&state=\(pending.state)&iss=https%3A%2F%2Fevil.test"))
        XCTAssertThrowsError(try pending.code(from: wrongIssuer))
        let duplicate =
            try XCTUnwrap(
                URL(string: "\(callback)?code=code-1&state=\(pending.state)&state=wrong&iss=https%3A%2F%2Fexample.test")
            )
        XCTAssertThrowsError(try pending.code(from: duplicate))
        let slashIssuer = try MemberMCPOAuthRequest(
            siteOrigin: site, app: .macos, issuerIdentifier: "https://example.test/",
            authorizationEndpoint: authorization, redirectURI: callback
        )
        let normalized =
            try XCTUnwrap(
                URL(string: "\(callback)?code=code-1&state=\(slashIssuer.state)&iss=https%3A%2F%2Fexample.test")
            )
        XCTAssertThrowsError(try slashIssuer.code(from: normalized))
        XCTAssertThrowsError(try MemberMCPOAuthRequest(
            siteOrigin: XCTUnwrap(URL(string: "https://user@example.test")),
            app: .macos, issuerIdentifier: "https://example.test",
            authorizationEndpoint: authorization, redirectURI: callback
        ))
        XCTAssertThrowsError(try MemberMCPOAuthRequest(
            siteOrigin: site, app: .macos,
            issuerIdentifier: "https://example.test/?bad=1",
            authorizationEndpoint: authorization, redirectURI: callback
        ))
        XCTAssertThrowsError(try pending.code(from: XCTUnwrap(URL(string:
            "\(callback)?code=code-1&state=\(pending.state)&iss=https%3A%2F%2Fexample.test#fragment"
        ))))
        let withLocale = try MemberMCPOAuthRequest(
            siteOrigin: site, app: .macos, issuerIdentifier: "https://example.test",
            authorizationEndpoint: XCTUnwrap(URL(string: "https://example.test/authorize?ui_locales=fr")),
            redirectURI: callback
        )
        let localeParts = try XCTUnwrap(URLComponents(url: withLocale.authorizationURL, resolvingAgainstBaseURL: false))
        XCTAssertEqual(localeParts.queryItems?.first(where: { $0.name == "ui_locales" })?.value, "fr")
        XCTAssertThrowsError(try MemberMCPOAuthRequest(
            siteOrigin: site, app: .macos, issuerIdentifier: "https://example.test",
            authorizationEndpoint: XCTUnwrap(URL(string: "https://example.test/authorize?state=evil")),
            redirectURI: callback
        ))
    }
}
