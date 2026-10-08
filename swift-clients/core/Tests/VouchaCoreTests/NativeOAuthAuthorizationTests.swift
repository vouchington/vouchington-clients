import Foundation
@testable import VouchaAPI
@testable import VouchaCore
import VouchaModels
import XCTest

final class NativeOAuthAuthorizationTests: XCTestCase {
    func testDiscardedAuthorizationRejectsLateFinalizationResult() throws {
        let (suiteName, defaults) = makeDefaults()
        defer { defaults.removePersistentDomain(forName: suiteName) }
        let store = makeStore(defaults: defaults)
        XCTAssertTrue(store.save(
            flowId: "superseded-oauth",
            provider: .github,
            purpose: .authenticate,
            completionProofVerifier: "verifier",
            expiresAt: Date().addingTimeInterval(300)
        ))
        let pending = try XCTUnwrap(store.pending())

        try store.discardAuthorization()

        XCTAssertFalse(store.complete(pending, with: .authenticated(provider: .github)))
        let snapshot = try store.snapshot()
        XCTAssertEqual(snapshot.pendingStatus, .none)
        XCTAssertNil(snapshot.result)

        try store.record(.connected(provider: .github))
        XCTAssertFalse(store.save(
            flowId: "blocked-by-result",
            provider: .facebook,
            purpose: .authenticate,
            completionProofVerifier: "verifier",
            expiresAt: Date().addingTimeInterval(300)
        ))
        XCTAssertEqual(store.result(), .connected(provider: .github))
    }

    func testMFAAttemptIdIsDerivedOnlyFromMFARequiredResults() {
        XCTAssertEqual(
            NativeOAuthAuthorizationResult.mfaRequired(
                provider: .facebook,
                loginAttemptId: "attempt-1"
            ).mfaLoginAttemptId,
            "attempt-1"
        )
        XCTAssertNil(NativeOAuthAuthorizationResult.authenticated(provider: .github).mfaLoginAttemptId)
        XCTAssertNil(NativeOAuthAuthorizationResult.connected(provider: .x).mfaLoginAttemptId)
        XCTAssertNil(NativeOAuthAuthorizationResult.expired(
            provider: .facebook,
            purpose: .authenticate
        ).mfaLoginAttemptId)
    }

    func testBrokerEndpointsEncodeExactNativeContract() throws {
        assertEndpoint(
            .beginNativeOAuthAuthorization(
                provider: .github,
                purpose: .authenticate,
                completionProofChallenge: "proof-challenge"
            ),
            method: .POST,
            path: "/api/v1/auth/oauth/github/authorizations",
            body: [
                "purpose": "authenticate",
                "callback_mode": "native",
                "completion_proof_challenge": "proof-challenge"
            ]
        )
        assertEndpoint(
            .completeNativeOAuthAuthorization(
                flowId: "flow/one",
                completionToken: "completion-token",
                completionProofVerifier: "proof-verifier"
            ),
            method: .POST,
            path: "/api/v1/auth/oauth/authorizations/flow%2Fone/complete",
            body: [
                "completion_token": "completion-token",
                "completion_proof_verifier": "proof-verifier"
            ]
        )
        assertEndpoint(
            .disconnectOAuthAccount(provider: .x),
            method: .DELETE,
            path: "/api/v1/auth/oauth/x/connect"
        )

        let capabilities = try makeVouchaDecoder().decode(
            OAuthBrokerCapabilitiesResponse.self,
            from: Data("""
            {
              "providers": ["facebook", "x", "github"],
              "broker_capabilities": {
                "facebook": {
                  "version": 1,
                  "modes": { "web": true, "native": true },
                  "purposes": ["authenticate", "connect"]
                },
                "x": {
                  "version": 1,
                  "modes": { "web": true, "native": false },
                  "purposes": ["authenticate", "connect"]
                },
                "github": {
                  "version": 1,
                  "modes": { "web": true, "native": true },
                  "purposes": ["authenticate"]
                }
              }
            }
            """.utf8)
        )

        XCTAssertTrue(capabilities.brokerCapabilities.facebook.supportsNative(.authenticate))
        XCTAssertTrue(capabilities.brokerCapabilities.facebook.supportsNative(.connect))
        XCTAssertFalse(capabilities.brokerCapabilities.x.supportsNative(.authenticate))
        XCTAssertFalse(capabilities.brokerCapabilities.github.supportsNative(.connect))

        let rollingCompatibility = try makeVouchaDecoder().decode(
            OAuthBrokerCapabilitiesResponse.self,
            from: Data(#"{"providers":["facebook","x","github"]}"#.utf8)
        )
        for provider in NativeOAuthProvider.allCases {
            XCTAssertFalse(rollingCompatibility.brokerCapabilities[provider].supportsNative(.authenticate))
            XCTAssertFalse(rollingCompatibility.brokerCapabilities[provider].supportsNative(.connect))
        }
    }

    func testCompletionResponsesDecodeEveryDiscriminatedOutcome() throws {
        let decoder = makeVouchaDecoder()

        XCTAssertEqual(
            try decoder.decode(
                NativeOAuthCompletionResponse.self,
                from: Data(#"{"status":"pending"}"#.utf8)
            ),
            .pending
        )
        let authenticated = try decoder.decode(
            NativeOAuthCompletionResponse.self,
            from: Data("""
            {
              "user": {
                "id": "user-1",
                "username": "alice",
                "email_address": "alice@example.com",
                "roles": ["user"],
                "profile_image_id": "image-1"
              }
            }
            """.utf8)
        )
        guard case let .authenticated(user) = authenticated else {
            return XCTFail("Expected authenticated completion response")
        }
        XCTAssertEqual(user.id, "user-1")
        XCTAssertEqual(user.username, "alice")
        XCTAssertEqual(user.emailAddress, "alice@example.com")
        XCTAssertEqual(user.roles, ["user"])
        XCTAssertEqual(user.profileImageId, "image-1")
        let usernameLessAuthentication = try decoder.decode(
            NativeOAuthCompletionResponse.self,
            from: Data("""
            {
              "user": {
                "id": "user-2",
                "email_address": null,
                "roles": [],
                "profile_image_id": null
              }
            }
            """.utf8)
        )
        guard case let .authenticated(usernameLessUser) = usernameLessAuthentication else {
            return XCTFail("Expected username-less authenticated completion response")
        }
        XCTAssertNil(usernameLessUser.username)
        XCTAssertEqual(
            try decoder.decode(
                NativeOAuthCompletionResponse.self,
                from: Data(#"{"mfa_required":true,"login_attempt_id":"attempt-1"}"#.utf8)
            ),
            .mfaRequired(loginAttemptId: "attempt-1")
        )
        XCTAssertEqual(
            try decoder.decode(
                NativeOAuthCompletionResponse.self,
                from: Data("""
                {
                  "oauth_account": {
                    "id": "github-1",
                    "name": "Alice",
                    "email_address": "alice@example.com"
                  }
                }
                """.utf8)
            ),
            .connected(OAuthAccountInfo(
                id: "github-1",
                name: "Alice",
                emailAddress: "alice@example.com"
            ))
        )
        XCTAssertThrowsError(try decoder.decode(
            NativeOAuthCompletionResponse.self,
            from: Data(#"{"status":"pending","mfa_required":true,"login_attempt_id":"attempt-1"}"#.utf8)
        ))
        XCTAssertThrowsError(try decoder.decode(
            NativeOAuthCompletionResponse.self,
            from: Data(#"{"mfa_required":true}"#.utf8)
        ))

        var identityEnvelope = try XCTUnwrap(
            JSONSerialization.jsonObject(
                with: ApiFixtureLoader.data("swift.my.identity.default")
            ) as? [String: Any]
        )
        var identityObject = try XCTUnwrap(identityEnvelope["identity"] as? [String: Any])
        identityObject["facebook_account"] = [
            "id": "facebook-1",
            "name": "Alice Facebook",
            "email_address": "facebook@example.com"
        ]
        identityObject["x_account"] = [
            "id": "x-1",
            "name": "Alice X",
            "email_address": NSNull()
        ]
        identityObject["github_account"] = [
            "id": "github-1",
            "name": NSNull(),
            "email_address": "github@example.com"
        ]
        identityEnvelope["identity"] = identityObject
        let identityData = try JSONSerialization.data(withJSONObject: identityEnvelope)
        let identity = try decoder.decode(NativeOAuthIdentityEnvelope.self, from: identityData).identity

        XCTAssertEqual(identity.facebookAccount?.name, "Alice Facebook")
        XCTAssertEqual(identity.xAccount?.id, "x-1")
        XCTAssertEqual(identity.githubAccount?.emailAddress, "github@example.com")

        let encodedIdentity = try JSONEncoder().encode(identity)
        let encodedObject = try XCTUnwrap(
            JSONSerialization.jsonObject(with: encodedIdentity) as? [String: Any]
        )
        XCTAssertEqual(
            (encodedObject["facebook_account"] as? [String: Any])?["id"] as? String,
            "facebook-1"
        )
        XCTAssertEqual(
            (encodedObject["x_account"] as? [String: Any])?["name"] as? String,
            "Alice X"
        )
        XCTAssertEqual(
            (encodedObject["github_account"] as? [String: Any])?["email_address"] as? String,
            "github@example.com"
        )
    }

    func testStoreClaimsOnlyMatchingFixedCallbackAndRejectsReplacement() throws {
        let (suiteName, defaults) = makeDefaults()
        defer { defaults.removePersistentDomain(forName: suiteName) }
        let store = makeStore(defaults: defaults)
        let now = Date(timeIntervalSince1970: 1_800_000_000)

        XCTAssertTrue(store.save(
            flowId: "flow-1",
            provider: .facebook,
            purpose: .authenticate,
            completionProofVerifier: "verifier-1",
            expiresAt: now.addingTimeInterval(300),
            now: now
        ))
        XCTAssertFalse(store.save(
            flowId: "flow-2",
            provider: .github,
            purpose: .connect,
            completionProofVerifier: "verifier-2",
            expiresAt: now.addingTimeInterval(300),
            now: now
        ))
        let wrongHostURL = try XCTUnwrap(
            URL(string: "voucha://other/oauth/callback?flow_id=flow-1&completion_token=token")
        )
        XCTAssertNil(try store.claimCallback(for: wrongHostURL, now: now))
        let wrongFlowURL = try XCTUnwrap(
            URL(string: "voucha://auth/oauth/callback?flow_id=flow-2&completion_token=token")
        )
        XCTAssertNil(try store.claimCallback(for: wrongFlowURL, now: now))

        let callbackURL = try XCTUnwrap(
            URL(string: "voucha://auth/oauth/callback?flow_id=flow-1&completion_token=completion-token")
        )
        let claimed = try XCTUnwrap(try store.claimCallback(for: callbackURL, now: now))

        XCTAssertEqual(claimed.flowId, "flow-1")
        XCTAssertEqual(claimed.provider, .facebook)
        XCTAssertEqual(claimed.completionToken, "completion-token")
        let replayURL = try XCTUnwrap(
            URL(string: "voucha://auth/oauth/callback?flow_id=flow-1&completion_token=replayed-token")
        )
        XCTAssertNil(try store.claimCallback(for: replayURL, now: now))
    }

    func testStorePersistsFinalizingFlowAcrossRecreationAndRejectsReplay() throws {
        let (suiteName, defaults) = makeDefaults()
        defer { defaults.removePersistentDomain(forName: suiteName) }
        let now = Date(timeIntervalSince1970: 1_800_000_000)
        let store = makeStore(defaults: defaults)
        XCTAssertTrue(store.save(
            flowId: "flow-1",
            provider: .github,
            purpose: .connect,
            completionProofVerifier: "proof-verifier",
            expiresAt: now.addingTimeInterval(300),
            now: now
        ))
        let callbackURL = try XCTUnwrap(
            URL(string: "voucha://auth/oauth/callback?flow_id=flow-1&completion_token=completion-token")
        )
        let claimed = try XCTUnwrap(try store.claimCallback(for: callbackURL, now: now))

        let restoredStore = makeStore(defaults: defaults)
        XCTAssertEqual(restoredStore.pending(now: now), claimed)
        XCTAssertTrue(restoredStore.complete(claimed, with: .connected(provider: .github)))
        XCTAssertFalse(restoredStore.complete(claimed, with: .connected(provider: .github)))

        let completedStore = makeStore(defaults: defaults)
        XCTAssertEqual(completedStore.result(), .connected(provider: .github))
        XCTAssertNil(completedStore.pending(now: now))
        let replayURL = try XCTUnwrap(
            URL(string: "voucha://auth/oauth/callback?flow_id=flow-1&completion_token=replayed-token")
        )
        XCTAssertNil(try completedStore.claimCallback(for: replayURL, now: now))

        try completedStore.acknowledgeResult()
        XCTAssertNil(makeStore(defaults: defaults).result())

        XCTAssertTrue(completedStore.save(
            flowId: "expired-flow",
            provider: .facebook,
            purpose: .authenticate,
            completionProofVerifier: "expired-verifier",
            expiresAt: now.addingTimeInterval(-1),
            now: now.addingTimeInterval(-2)
        ))
        let expiredStore = makeStore(defaults: defaults)
        XCTAssertNil(expiredStore.pending(now: now))
        XCTAssertEqual(
            expiredStore.pendingStatus(now: now),
            .expired(PendingNativeOAuthAuthorization(
                flowId: "expired-flow",
                provider: .facebook,
                purpose: .authenticate,
                completionProofVerifier: "expired-verifier",
                expiresAt: now.addingTimeInterval(-1),
                completionToken: nil
            ))
        )
    }

    #if !canImport(Security)
        func testInjectedPlatformStorePersistsAcrossRecreationWithoutSecurity() throws {
            let now = Date(timeIntervalSince1970: 1_800_000_000)
            let persistence = DurableTestOAuthState()
            let store = NativeOAuthAuthorizationStore(secureState: persistence)
            try store.clearPending()
            try store.acknowledgeResult()
            defer {
                XCTAssertNoThrow(try store.clearPending())
                XCTAssertNoThrow(try store.acknowledgeResult())
            }

            XCTAssertTrue(store.save(
                flowId: "durable-production-flow",
                provider: .github,
                purpose: .authenticate,
                completionProofVerifier: "durable-verifier",
                expiresAt: now.addingTimeInterval(300),
                now: now
            ))

            let restored = NativeOAuthAuthorizationStore(secureState: persistence)
            XCTAssertEqual(restored.pending(now: now)?.flowId, "durable-production-flow")
        }
    #endif

    private func makeDefaults() -> (suiteName: String, defaults: UserDefaults) {
        let suiteName = "NativeOAuthAuthorizationTests.\(UUID().uuidString)"
        return (suiteName, UserDefaults(suiteName: suiteName)!)
    }

    private func makeStore(defaults: UserDefaults) -> NativeOAuthAuthorizationStore {
        NativeOAuthAuthorizationStore(secureState: UserDefaultsNativePendingState(
            key: "nativeOAuthPendingAuthorization",
            defaults: defaults
        ))
    }
}

private struct NativeOAuthIdentityEnvelope: Decodable {
    let identity: PrivateUser
}

#if !canImport(Security)
    private final class DurableTestOAuthState: NativeOAuthSecureStatePersisting, @unchecked Sendable {
        private let lock = NSLock()
        private var data: Data?

        func read() -> Data? {
            lock.withLock { data }
        }

        func write(_ data: Data) -> Bool {
            lock.withLock { self.data = data }
            return true
        }

        func delete() throws {
            lock.withLock { data = nil }
        }
    }
#endif
