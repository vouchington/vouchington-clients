import Foundation
@testable import VouchaAPI
import XCTest

final class LandingPageAnalyticsEndpointCoverageTests: XCTestCase {
    func testLandingPageAnalyticsEndpointsUseExpectedRoutes() {
        assertEndpoint(
            Endpoint.myLandingPageAnalytics(pageId: "page 1"),
            path: "/api/v1/my/landing-pages/page%201/analytics"
        )
        assertEndpoint(
            Endpoint.adminUserLandingPages(userId: "user 1"),
            path: "/api/v1/admin/users/user%201/landing-pages"
        )
        assertEndpoint(
            Endpoint.adminLandingPageAnalytics(username: "user 1", slug: "links 1"),
            path: "/api/v1/admin/users/user%201/landing-pages/links%201/analytics"
        )
    }
}
