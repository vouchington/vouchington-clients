import Foundation
@testable import VouchaAPI
import XCTest

final class LandingPageAnalyticsModelDecodingTests: XCTestCase {
    func testDecodesOwnerAndAdminLandingPageAnalyticsPayloads() throws {
        let decoder = makeVouchaDecoder()
        let analytics = try decoder.decode(
            LandingPageAnalyticsResponse.self,
            from: Data("""
            {
              "analytics": {
                "total_visits": 12,
                "total_clicks": 3,
                "unique_visitors": 7,
                "ctr": 0.25,
                "item_clicks": [
                  {
                    "item_id": "item-1",
                    "item_type": "link",
                    "click_count": 2
                  }
                ],
                "daily_stats": [
                  { "date": "2026-07-01", "visits": 12, "clicks": 3, "unique_visitors": 7 }
                ],
                "utm_sources": [
                  { "utm_source": "newsletter", "visits": 5 }
                ],
                "conversion_funnel": {
                  "total_visits": 12,
                  "total_clicks": 3,
                  "total_signups": 4,
                  "visit_to_click_rate": 0.25
                }
              }
            }
            """.utf8)
        )
        XCTAssertEqual(analytics.analytics.totalVisits, 12)
        XCTAssertEqual(analytics.analytics.itemClicks.first?.clickCount, 2)
        XCTAssertEqual(analytics.analytics.conversionFunnel.totalSignups, 4)

        let admin = try decoder.decode(
            AdminLandingPageAnalyticsResponse.self,
            from: Data("""
            {
              "landing_page": {
                "id": "page-1",
                "user_id": "user-1",
                "title": "Home",
                "subtitle": "Public profile",
                "slug": "home",
                "is_default": true,
                "created_at": "2026-07-01T00:00:00Z",
                "updated_at": "2026-07-01T00:00:00Z",
                "items": []
              },
              "analytics": {
                "total_visits": 12,
                "total_clicks": 3,
                "unique_visitors": 7,
                "ctr": 0.25,
                "item_clicks": [],
                "daily_stats": [],
                "utm_sources": [],
                "conversion_funnel": {
                  "total_visits": 12,
                  "total_clicks": 3,
                  "total_signups": 4,
                  "visit_to_click_rate": 0.25
                }
              }
            }
            """.utf8)
        )
        XCTAssertEqual(admin.landingPage.id, "page-1")
        XCTAssertEqual(admin.analytics.dailyStats.count, 0)
        XCTAssertEqual(admin.analytics.conversionFunnel.totalSignups, 4)
    }
}
