#if canImport(Security)
    import Foundation
    import Security
    @testable import VouchaAPI
    import XCTest

    final class MemberMCPKeychainTokenStoreTests: XCTestCase {
        func testAccountKeySeparatesAccountIssuerResourceAndClient() throws {
            let original = try scope(account: "member", issuer: "https://example.test")
            let otherAccount = try scope(account: "other", issuer: "https://example.test")
            let otherIssuer = try scope(account: "member", issuer: "https://staging.example.test")
            let otherResource = try MemberMCPOAuthTokenScope(
                accountId: original.accountId, issuerIdentifier: original.issuerIdentifier,
                resource: XCTUnwrap(URL(string: "https://example.test/api/v2/mcp")), clientId: original.clientId
            )
            let otherClient = try MemberMCPOAuthTokenScope(
                accountId: original.accountId, issuerIdentifier: original.issuerIdentifier,
                resource: original.resource,
                clientId: XCTUnwrap(URL(string: "https://example.test/api/v1/oauth/native-clients/ios"))
            )
            let key = KeychainMemberMCPOAuthTokenStore.accountKey(for: original)
            for scope in [otherAccount, otherIssuer, otherResource, otherClient] {
                XCTAssertNotEqual(key, KeychainMemberMCPOAuthTokenStore.accountKey(for: scope))
            }
        }

        func testPerAccountTokensSurviveStoreRecreationAndClearOnlyThatAccount() async throws {
            let account = "mcp-test-\(UUID().uuidString.lowercased())"
            let other = "mcp-test-\(UUID().uuidString.lowercased())"
            let accountScope = try scope(account: account, issuer: "https://example.test")
            let otherScope = try scope(account: other, issuer: "https://example.test")
            let otherIssuerScope = try scope(account: account, issuer: "https://staging.example.test")
            let otherClientScope = try MemberMCPOAuthTokenScope(
                accountId: account, issuerIdentifier: "https://example.test",
                resource: XCTUnwrap(URL(string: "https://example.test/api/v1/mcp")),
                clientId: XCTUnwrap(URL(string: "https://example.test/api/v1/oauth/native-clients/ios"))
            )
            let store = KeychainMemberMCPOAuthTokenStore()
            let tokens = MemberMCPOAuthTokens(
                accessToken: "fake-access", refreshToken: "fake-refresh", expiresIn: 3_600,
                scope: "mcp.user:read", tokenType: "Bearer"
            )
            do {
                try await store.save(tokens, scope: accountScope)
                try await store.save(tokens, scope: otherScope)
                try await store.save(MemberMCPOAuthTokens(
                    accessToken: "staging-access", refreshToken: "staging-refresh", expiresIn: 3_600,
                    scope: "mcp.user:read", tokenType: "Bearer"
                ), scope: otherIssuerScope)
                try await store.save(MemberMCPOAuthTokens(
                    accessToken: "ios-access", refreshToken: "ios-refresh", expiresIn: 3_600,
                    scope: "mcp.user:read", tokenType: "Bearer"
                ), scope: otherClientScope)
            } catch let KeychainMemberMCPOAuthTokenStore.Failure.keychain(status)
                where status == errSecInteractionNotAllowed || status == errSecMissingEntitlement
                || status == errSecNotAvailable {
                try? await store.clear(scope: accountScope)
                try? await store.clear(scope: otherScope)
                try? await store.clear(scope: otherIssuerScope)
                try? await store.clear(scope: otherClientScope)
                throw XCTSkip("Keychain is unavailable to this test host.")
            }
            do {
                let reopened = KeychainMemberMCPOAuthTokenStore()
                let loaded = try await reopened.load(scope: accountScope)
                XCTAssertEqual(loaded?.refreshToken, "fake-refresh")
                XCTAssertEqual(loaded?.acquiredAt, tokens.acquiredAt)
                try await reopened.clear(scope: accountScope)
                let removed = try await reopened.load(scope: accountScope)
                XCTAssertNil(removed)
                let kept = try await reopened.load(scope: otherScope)
                XCTAssertEqual(kept?.refreshToken, "fake-refresh")
                let staging = try await reopened.load(scope: otherIssuerScope)
                XCTAssertEqual(staging?.refreshToken, "staging-refresh")
                let ios = try await reopened.load(scope: otherClientScope)
                XCTAssertEqual(ios?.refreshToken, "ios-refresh")
            } catch {
                try? await store.clear(scope: accountScope)
                try? await store.clear(scope: otherScope)
                try? await store.clear(scope: otherIssuerScope)
                try? await store.clear(scope: otherClientScope)
                throw error
            }
            try await store.clear(scope: accountScope)
            try await store.clear(scope: otherScope)
            try await store.clear(scope: otherIssuerScope)
            try await store.clear(scope: otherClientScope)
        }

        private func scope(account: String, issuer: String) throws -> MemberMCPOAuthTokenScope {
            try MemberMCPOAuthTokenScope(
                accountId: account, issuerIdentifier: issuer,
                resource: XCTUnwrap(URL(string: "\(issuer)/api/v1/mcp")),
                clientId: XCTUnwrap(URL(string: "\(issuer)/api/v1/oauth/native-clients/macos"))
            )
        }
    }
#endif
