import Foundation
@testable import VouchaAPI
import XCTest

final class EndpointCommunityModerationEscalationCoverageTests: XCTestCase {
    func testCommunityModerationEscalationEndpointsUseExpectedRoutesAndBodies() {
        assertEndpoint(
            Endpoint.escalateCommunityModerationReport(idOrSlug: "test community", reportId: "report 1"),
            method: .POST,
            path: "/api/v1/communities/test%20community/reports/report%201/escalation",
            body: [:]
        )
        assertEndpoint(
            Endpoint.deEscalateCommunityModerationReport(idOrSlug: "test community", reportId: "report 1"),
            method: .DELETE,
            path: "/api/v1/communities/test%20community/reports/report%201/escalation"
        )
        assertEndpoint(
            Endpoint.escalateCommunityPendingPost(idOrSlug: "test community", postId: "post 1"),
            method: .POST,
            path: "/api/v1/communities/test%20community/posts/post%201/escalation",
            body: [:]
        )
        assertEndpoint(
            Endpoint.deEscalateCommunityPendingPost(idOrSlug: "test community", postId: "post 1"),
            method: .DELETE,
            path: "/api/v1/communities/test%20community/posts/post%201/escalation"
        )
    }
}
