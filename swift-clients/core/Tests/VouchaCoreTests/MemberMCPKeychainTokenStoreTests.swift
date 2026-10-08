#if canImport(Security)
    import Foundation
    import Security
    @testable import VouchaAPI
    import XCTest

    final class MemberMCPKeychainTokenStoreTests: XCTestCase {
        func testPerAccountTokensSurviveStoreRecreationAndClearOnlyThatAccount() async throws {
            let account = "mcp-test-\(UUID().uuidString.lowercased())"
            let other = "mcp-test-\(UUID().uuidString.lowercased())"
            let store = KeychainMemberMCPOAuthTokenStore()
            let tokens = MemberMCPOAuthTokens(
                accessToken: "fake-access", refreshToken: "fake-refresh", expiresIn: 3_600,
                scope: "mcp.user:read", tokenType: "Bearer"
            )
            do {
                try await store.save(tokens, accountId: account)
                try await store.save(tokens, accountId: other)
            } catch let KeychainMemberMCPOAuthTokenStore.Failure.keychain(status)
                where status == errSecInteractionNotAllowed || status == errSecMissingEntitlement {
                try? await store.clear(accountId: account)
                throw XCTSkip("Keychain is unavailable to this test host.")
            }
            do {
                let reopened = KeychainMemberMCPOAuthTokenStore()
                let loaded = try await reopened.load(accountId: account)
                XCTAssertEqual(loaded?.refreshToken, "fake-refresh")
                XCTAssertEqual(loaded?.acquiredAt, tokens.acquiredAt)
                try await reopened.clear(accountId: account)
                let removed = try await reopened.load(accountId: account)
                XCTAssertNil(removed)
                let kept = try await reopened.load(accountId: other)
                XCTAssertEqual(kept?.refreshToken, "fake-refresh")
            } catch {
                try? await store.clear(accountId: account)
                try? await store.clear(accountId: other)
                throw error
            }
            try await store.clear(accountId: account)
            try await store.clear(accountId: other)
        }
    }
#endif
