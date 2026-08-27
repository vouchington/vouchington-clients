import Foundation
@testable import VouchaAPI
import XCTest

final class SettingsEndpointManagementCoverageTests: XCTestCase {
    func testPushSubscriptionEndpointsUseExpectedRoutesAndBodies() {
        assertEndpoint(
            Endpoint.myPushSubscriptions(),
            path: "/api/v1/my/notifications/push-subscriptions"
        )
        assertEndpoint(
            Endpoint.createMyPushSubscription(
                endpoint: "https://push.example.com",
                p256dh: "abcdabcdabcdabcd",
                auth: "abcdefgh",
                expirationTimeMs: 1_234,
                userAgent: "Safari"
            ),
            method: .POST,
            path: "/api/v1/my/notifications/push-subscriptions",
            body: [
                "endpoint": "https://push.example.com",
                "p256dh": "abcdabcdabcdabcd",
                "auth": "abcdefgh",
                "expiration_time_ms": 1_234,
                "user_agent": "Safari"
            ]
        )
        assertEndpoint(
            Endpoint.revokeMyPushSubscription(id: "sub 1"),
            method: .DELETE,
            path: "/api/v1/my/notifications/push-subscriptions/sub%201"
        )
    }
}
