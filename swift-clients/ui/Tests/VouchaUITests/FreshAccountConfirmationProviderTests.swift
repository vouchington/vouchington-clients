import Foundation
import VouchaAPI
import VouchaAuth
import VouchaCore
@testable import VouchaFeatures
import XCTest

extension FreshAccountConfirmationTests {
    func testAppleCallbackMustMatchTheInitiatingFreshChallenge() async throws {
        let fixture = try makeFixture()
        CannedFeedURLProtocol.handlers["/api/v1/auth/re-auth/methods"] = (
            Data(#"{"has_email":false,"has_passkey":false,"has_totp":false,"providers":["apple"]}"#.utf8), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/auth/re-auth/providers/apple/challenges"] = (
            Data(#"{"flow_id":"apple-flow","nonce":"fresh-nonce","expires_at":"2027-01-01T00:00:00Z"}"#.utf8), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/auth/re-auth/providers/apple/verification"] = (
            Data(#"{"re_auth_token":"apple-proof"}"#.utf8), 200
        )
        let waiting = Task { try await fixture.model.authorize() }
        await waitForMethods(fixture.model)
        await fixture.model.prepareApple()
        XCTAssertEqual(fixture.model.appleChallenge?.nonce, "fresh-nonce")
        await fixture.model.verifyApple(credential: "stale-assertion", flowId: "old-flow")
        XCTAssertFalse(paths.contains("/api/v1/auth/re-auth/providers/apple/verification"))
        await fixture.model.verifyApple(credential: "fresh-assertion", flowId: "apple-flow")
        let token = try await waiting.value
        XCTAssertEqual(token, "apple-proof")
        XCTAssertNil(fixture.model.appleChallenge)
        XCTAssertFalse(paths.contains("/api/v1/my/identity"))
        XCTAssertFalse(paths.contains("/api/v1/session"))
    }

    func testLinkedBrokerUsesExistingCallbackThenPurposeScopedProofWithoutSignIn() async throws {
        let fixture = try makeFixture(brokerEnabled: true)
        let broker = try XCTUnwrap(fixture.broker)
        seedFreshBroker()
        let waiting = Task { try await fixture.model.authorize() }
        await waitForMethods(fixture.model)
        var opened: URL?
        await fixture.model.beginBroker(provider: .github) { opened = $0 }
        XCTAssertEqual(opened?.host, "github.com")
        XCTAssertFalse(paths.contains("/api/v1/auth/oauth/github/authorizations"))
        XCTAssertTrue(try broker.handleIncomingURL(XCTUnwrap(URL(
            string: "voucha://auth/oauth/callback?flow_id=broker-flow&completion_token=completion"
        ))))
        await waitForAccountConfirmationState { broker.result != nil }
        await fixture.model.consumeBrokerResult(broker.result)
        let token = try await waiting.value
        XCTAssertEqual(token, "broker-proof")
        XCTAssertNil(broker.result)
        XCTAssertTrue(paths.contains("/api/v1/auth/re-auth/providers/github/proofs"))
        XCTAssertFalse(paths.contains("/api/v1/my/identity"))
        XCTAssertFalse(paths.contains("/api/v1/session"))
    }

    func testCancelBrokerDiscardsCallbackAndNeverRequestsProof() async throws {
        let fixture = try makeFixture(brokerEnabled: true)
        let broker = try XCTUnwrap(fixture.broker)
        seedFreshBroker()
        let waiting = Task { try await fixture.model.authorize() }
        await waitForMethods(fixture.model)
        await fixture.model.beginBroker(provider: .github) { _ in }
        fixture.model.cancel()
        XCTAssertNil(broker.pending)
        _ = try broker.handleIncomingURL(XCTUnwrap(URL(
            string: "voucha://auth/oauth/callback?flow_id=broker-flow&completion_token=late"
        )))
        do { _ = try await waiting.value
            XCTFail("Canceled broker must fail")
        } catch is CancellationError {}
        XCTAssertFalse(paths.contains("/api/v1/auth/re-auth/providers/github/proofs"))
    }

    func testFailedBrokerCompletionIsVisibleAndOnlyExplicitRetryStartsANewFlow() async throws {
        let fixture = try makeFixture(brokerEnabled: true)
        let broker = try XCTUnwrap(fixture.broker)
        seedFreshBroker()
        CannedFeedURLProtocol.handlers["/api/v1/auth/oauth/authorizations/broker-flow/complete"] = (
            Data(#"{"message":"Provider subject rejected"}"#.utf8), 403
        )
        let waiting = Task { try await fixture.model.authorize() }
        await waitForMethods(fixture.model)
        await fixture.model.beginBroker(provider: .github) { _ in }
        _ = try broker.handleIncomingURL(XCTUnwrap(URL(
            string: "voucha://auth/oauth/callback?flow_id=broker-flow&completion_token=completion"
        )))
        await waitForAccountConfirmationState { broker.errorMessage != nil && !broker.isWorking }
        await fixture.model.consumeBrokerResult(broker.result)
        XCTAssertNotNil(fixture.model.errorMessage)
        XCTAssertFalse(paths.contains("/api/v1/auth/re-auth/providers/github/proofs"))
        XCTAssertEqual(paths.filter { $0.hasSuffix("broker-flow/complete") }.count, 1)
        CannedFeedURLProtocol.handlers["/api/v1/auth/re-auth/providers/github/authorizations"] = (
            Data(
                #"{"flow_id":"retry-flow","redirect_url":"https://github.com/authorize","expires_at":"2027-01-01T00:00:00Z"}"#
                    .utf8
            ), 200
        )
        await fixture.model.beginBroker(provider: .github) { _ in }
        XCTAssertEqual(broker.pending?.flowId, "retry-flow")
        XCTAssertNil(fixture.model.errorMessage)
        fixture.model.cancel()
        do { _ = try await waiting.value
            XCTFail("Canceled retry must fail")
        } catch is CancellationError {}
    }

    func testOwnedPasskeyChooserForwardsAndAppendsOpaqueCursor() async throws {
        let fixture = try makeFixture()
        CannedFeedURLProtocol.handlers["/api/v1/auth/re-auth/methods"] = (
            Data(#"{"has_email":false,"has_passkey":true,"has_totp":false,"providers":[]}"#.utf8), 200
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/auth/passkeys"] = [
            (
                Data(
                    #"{"results":[{"id":"key-1","name":"Device"}],"page_info":{"has_next_page":true,"end_cursor":"opaque"}}"#
                        .utf8
                ),
                200,
                0
            ),
            (
                Data(
                    #"{"results":[{"id":"key-2","name":"Security key"}],"page_info":{"has_next_page":false,"end_cursor":null}}"#
                        .utf8
                ),
                200,
                0
            )
        ]
        let waiting = Task { try await fixture.model.authorize() }
        await waitForAccountConfirmationState { fixture.model.passkeys.count == 1 }
        await fixture.model.loadMorePasskeys()
        XCTAssertEqual(fixture.model.passkeys.map(\.id), ["key-1", "key-2"])
        let url = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
        XCTAssertEqual(
            URLComponents(url: url, resolvingAgainstBaseURL: false)?.queryItems?.first { $0.name == "after" }?.value,
            "opaque"
        )
        fixture.model.cancel()
        do { _ = try await waiting.value
            XCTFail("Chooser cancellation must fail")
        } catch is CancellationError {}
    }

    func testCancelWhileBrokerStartIsInFlightNeverOpensProviderOrLeavesPendingAuthorization() async throws {
        let fixture = try makeFixture(brokerEnabled: true)
        let broker = try XCTUnwrap(fixture.broker)
        seedFreshBroker()
        let waiting = Task { try await fixture.model.authorize() }
        await waitForMethods(fixture.model)
        let path = "/api/v1/auth/re-auth/providers/github/authorizations"
        CannedFeedURLProtocol.suspendResponse(path: path)
        let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "POST")
        var opened: URL?
        let starting = Task { await fixture.model.beginBroker(provider: .github) { opened = $0 } }
        _ = try await barrier.wait()

        fixture.model.cancel()
        CannedFeedURLProtocol.releaseResponse(path: path)
        await starting.value

        XCTAssertNil(opened)
        XCTAssertNil(broker.pending)
        XCTAssertTrue(broker.canStartAuthorization)
        do { _ = try await waiting.value
            XCTFail("Canceled confirmation must fail")
        } catch is CancellationError {}
    }

    private func seedFreshBroker() {
        CannedFeedURLProtocol.handlers["/api/v1/auth/re-auth/methods"] = (
            Data(#"{"has_email":false,"has_passkey":false,"has_totp":false,"providers":["github"]}"#.utf8), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/auth/oauth/providers"] = (Data(#"""
        {"providers":["github"],"broker_capabilities":{
        "facebook":{"version":0,"modes":{"web":false,"native":false},"purposes":[]},
        "x":{"version":0,"modes":{"web":false,"native":false},"purposes":[]},
        "github":{"version":1,"modes":{"web":true,"native":true},"purposes":["connect"]}}}
        """#.utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/auth/re-auth/providers/github/authorizations"] = (
            Data(
                #"{"flow_id":"broker-flow","redirect_url":"https://github.com/authorize","expires_at":"2027-01-01T00:00:00Z"}"#
                    .utf8
            ), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/auth/oauth/authorizations/broker-flow/complete"] = (
            Data(#"{"oauth_account":{"id":"linked-user","name":null,"email_address":null}}"#.utf8), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/auth/re-auth/providers/github/proofs"] = (
            Data(#"{"re_auth_token":"broker-proof"}"#.utf8), 200
        )
    }
}
