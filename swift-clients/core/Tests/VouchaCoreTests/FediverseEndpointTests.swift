@testable import VouchaAPI
import XCTest

final class FediverseEndpointTests: XCTestCase {
    func testInstanceDirectoryEndpointForwardsOpaqueCursor() {
        let endpoint = Endpoint.fediverseInstances(after: "opaque+/=", limit: 25, sort: nil)
        XCTAssertEqual(endpoint.path, "/api/v1/fediverse/instances")
        XCTAssertEqual(endpoint.queryItems, [
            .init(name: "limit", value: "25"),
            .init(name: "after", value: "opaque+/=")
        ])
    }

    func testInstanceDetailEscapesSlug() {
        XCTAssertEqual(
            Endpoint.fediverseInstance(idOrSlug: "social/example").path,
            "/api/v1/fediverse/instances/social%2Fexample"
        )
    }

    func testNativeBlueskyEndpointsEncodeHandshake() {
        assertEndpoint(
            .beginNativeBlueskyAccountLink(
                handle: "alice.bsky.social",
                completionProofChallenge: "challenge"
            ),
            method: .POST,
            path: "/api/v1/auth/bluesky/link",
            body: [
                "callback_mode": "native",
                "completion_proof_challenge": "challenge",
                "handle": "alice.bsky.social"
            ]
        )
        assertEndpoint(
            .completeNativeBlueskyAccountLink(
                flowId: "flow",
                completionToken: "token",
                completionProofVerifier: "verifier"
            ),
            method: .POST,
            path: "/api/v1/auth/bluesky/link-completions",
            body: [
                "completion_proof_verifier": "verifier",
                "completion_token": "token",
                "flow_id": "flow"
            ]
        )
    }
}
