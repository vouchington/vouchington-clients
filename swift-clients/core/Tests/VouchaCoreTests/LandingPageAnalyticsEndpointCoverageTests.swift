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
            Endpoint.adminLandingPageAnalytics(pageId: "page 1"),
            path: "/api/v1/admin/landing-pages/page%201/analytics"
        )
    }
}
