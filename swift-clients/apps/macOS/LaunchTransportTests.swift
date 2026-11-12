#if DEBUG
    import Foundation
    @testable import VouchaAPI
    import VouchaAuth
    import VouchaCore
    import XCTest

    @MainActor
    final class LaunchTransportTests: XCTestCase {
        private var previousEnvironment: [String: String?] = [:]
        private struct IdentityResponse: Decodable { let identity: Identity }
        private struct Identity: Decodable { let id: String }
        private struct Feed: Decodable { let results: [Row] }
        private struct Row: Decodable { let id: String }

        private func restoreEnvironment() {
            for (key, value) in previousEnvironment {
                if let value { setenv(key, value, 1) } else { unsetenv(key) }
            }
            previousEnvironment = [:]
        }

        func testSignInUsesProductionRenewalAndIdentityThroughIsolatedTransport() async throws {
            defer { restoreEnvironment() }
            try installCommonFixtures()
            environment("VOUCHA_UI_TESTING_SIGN_IN_EMAIL", "ui-test@example.com")
            environment("VOUCHA_UI_TESTING_SIGN_IN_CODE", "ABCDEFGH23")
            environment("VOUCHA_UI_TESTING_TURNSTILE_TOKEN", "test-challenge")
            let client = try client(protocolClass: SignInUITestingURLProtocol.self)
            let session = SessionManager(client: client)
            let signIn = SignInService(client: client, sessionManager: session)
            try await signIn.requestEmailOTP(email: "ui-test@example.com", turnstileToken: "test-challenge")
            let challenge = try await signIn.verifyEmailOTP(email: "ui-test@example.com", code: "ABCDEFGH23")
            XCTAssertNil(challenge)
            XCTAssertTrue(session.isSignedIn)
            XCTAssertNil(session.lastRefreshErrorMessage)
            let tokens = try JSONSerialization.jsonObject(with: fixture("native.auth.session.refresh.rotated"))
                as? [String: [String: Any]]
            let jar = await client.cookieStorage
            XCTAssertEqual(jar.cookies?.first { $0.name == "dt" }?.value, tokens?["session"]?["dt"] as? String)
            XCTAssertEqual(jar.cookies?.first { $0.name == "st" }?.value, tokens?["session"]?["st"] as? String)
        }

        func testFeedFixtureRestoresIdentityBeforeReadingCanonicalFeed() async throws {
            defer { restoreEnvironment() }
            try installCommonFixtures()
            try environment(
                "VOUCHA_UI_TESTING_FEED_RESPONSE_BASE64",
                fixture("swift.rss-feed-items.feed.default").base64EncodedString()
            )
            let client = try client(protocolClass: FeedFlowUITestingURLProtocol.self)
            let session = SessionManager(client: client)
            let restored = await session.refresh()
            XCTAssertTrue(restored)
            let expectedIdentity = try JSONDecoder().decode(
                IdentityResponse.self,
                from: fixture("swift.my.identity.default")
            )
            XCTAssertEqual(session.currentUserId, expectedIdentity.identity.id)
            let feed: Feed = try await client.send(Endpoint(path: "/api/v1/feeds/rss_feed_items/any", queryItems: [
                URLQueryItem(name: "limit", value: "20"), URLQueryItem(name: "media_type", value: "video")
            ]))
            let canonical = try JSONDecoder().decode(Feed.self, from: fixture("swift.rss-feed-items.feed.default"))
            XCTAssertEqual(feed.results.map(\.id), canonical.results.map(\.id))
            XCTAssertFalse(feed.results.isEmpty)
        }

        private func client(protocolClass: AnyClass) throws -> APIClient {
            try APIClient(
                config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://ui-testing.voucha.invalid"))),
                cookieStorage: HTTPCookieStorage(), protocolClasses: [protocolClass], bootstrapSession: false
            )
        }

        private func installCommonFixtures() throws {
            try environment(
                "VOUCHA_UI_TESTING_SESSION_RESPONSE_BASE64",
                fixture("native.auth.session.refresh.rotated").base64EncodedString()
            )
            try environment(
                "VOUCHA_UI_TESTING_IDENTITY_RESPONSE_BASE64",
                fixture("swift.my.identity.default").base64EncodedString()
            )
        }

        private func fixture(_ id: String) throws -> Data {
            let root = try XCTUnwrap(ProcessInfo.processInfo.environment["VOUCHA_CONTRACT_ROOT"])
            XCTAssertTrue(root.hasPrefix("/"))
            return try Data(contentsOf: URL(fileURLWithPath: root)
                .appendingPathComponent("api-fixtures/v1/responses/\(id).json"))
        }

        private func environment(_ key: String, _ value: String) {
            if !previousEnvironment.keys
                .contains(key) { previousEnvironment[key] = .some(ProcessInfo.processInfo.environment[key]) }
            setenv(key, value, 1)
        }
    }
#endif
