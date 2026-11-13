import Foundation
import VouchaAPI
@testable import VouchaFeatures
import XCTest

extension FreshAccountConfirmationTests {
    func testDelayedMethodsNeverPublishAcrossSessionOrDeviceChange() async throws {
        for name in ["st", "dt"] {
            let fixture = try makeFixture()
            let path = "/api/v1/auth/re-auth/methods"
            CannedFeedURLProtocol.suspendResponse(path: path)
            let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
            let waiting = Task { try await fixture.model.authorize() }
            _ = try await barrier.wait()
            try setCookie(name, value: "new-context", storage: fixture.cookies)
            CannedFeedURLProtocol.releaseResponse(path: path)
            await waitForAccountConfirmationState { !fixture.model.isPresented }
            XCTAssertFalse(fixture.model.isPresented)
            XCTAssertNil(fixture.model.methods)
            XCTAssertTrue(fixture.model.passkeys.isEmpty)
            if fixture.model.isPresented { fixture.model.cancel() }
            do { _ = try await waiting.value
                XCTFail("Changed discovery scope must cancel")
            } catch is CancellationError {}
            XCTAssertEqual(
                fixture.cookies.cookies(for: fixture.client.baseURL)?.first { $0.name == name }?.value,
                "new-context"
            )
            XCTAssertFalse(paths.contains("/api/v1/auth/bluesky/link"))
        }
    }

    func testDelayedOwnedPageNeverPublishesAnotherAccountsCredentials() async throws {
        let fixture = try makeFixture()
        CannedFeedURLProtocol.handlers["/api/v1/auth/re-auth/methods"] = (
            Data(#"{"has_email":false,"has_passkey":true,"has_totp":false,"providers":[]}"#.utf8), 200
        )
        let path = "/api/v1/auth/passkeys"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (
                Data(
                    #"{"results":[{"id":"owned","name":"Old account key"}],"page_info":{"has_next_page":true,"end_cursor":"cursor"}}"#
                        .utf8
                ),
                200,
                0
            ),
            (
                Data(
                    #"{"results":[{"id":"other","name":"New account private key"}],"page_info":{"has_next_page":false,"end_cursor":null}}"#
                        .utf8
                ),
                200,
                0
            )
        ]
        let waiting = Task { try await fixture.model.authorize() }
        await waitForAccountConfirmationState { fixture.model.passkeys.count == 1 }
        CannedFeedURLProtocol.suspendResponse(path: path)
        let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let loading = Task { await fixture.model.loadMorePasskeys() }
        _ = try await barrier.wait()
        try setCookie("st", value: "another-account-session", storage: fixture.cookies)
        CannedFeedURLProtocol.releaseResponse(path: path)
        await loading.value
        XCTAssertFalse(fixture.model.isPresented)
        XCTAssertNil(fixture.model.methods)
        XCTAssertTrue(fixture.model.passkeys.isEmpty)
        if fixture.model.isPresented { fixture.model.cancel() }
        do { _ = try await waiting.value
            XCTFail("Changed credential scope must cancel")
        } catch is CancellationError {}
        XCTAssertFalse(paths.contains("/api/v1/auth/re-auth/passkeys/verification"))
    }

    func testDelayedAppleChallengeDoesNotPublishAfterDeviceChange() async throws {
        let fixture = try makeFixture()
        CannedFeedURLProtocol.handlers["/api/v1/auth/re-auth/methods"] = (
            Data(#"{"has_email":false,"has_passkey":false,"has_totp":false,"providers":["apple"]}"#.utf8), 200
        )
        let path = "/api/v1/auth/re-auth/providers/apple/challenges"
        CannedFeedURLProtocol.handlers[path] = (
            Data(#"{"flow_id":"old-flow","nonce":"old-nonce","expires_at":"2027-01-01T00:00:00Z"}"#.utf8), 200
        )
        let waiting = Task { try await fixture.model.authorize() }
        await waitForMethods(fixture.model)
        CannedFeedURLProtocol.suspendResponse(path: path)
        let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "POST")
        let preparing = Task { await fixture.model.prepareApple() }
        _ = try await barrier.wait()
        try setCookie("dt", value: "another-device", storage: fixture.cookies)
        CannedFeedURLProtocol.releaseResponse(path: path)
        await preparing.value
        XCTAssertFalse(fixture.model.isPresented)
        XCTAssertNil(fixture.model.appleChallenge)
        if fixture.model.isPresented { fixture.model.cancel() }
        do { _ = try await waiting.value
            XCTFail("Changed provider scope must cancel")
        } catch is CancellationError {}
        XCTAssertFalse(paths.contains("/api/v1/auth/re-auth/providers/apple/verification"))
    }
}
