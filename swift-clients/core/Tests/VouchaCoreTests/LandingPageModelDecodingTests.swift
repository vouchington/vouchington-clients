import Foundation
@testable import VouchaAPI
import XCTest

final class LandingPageModelDecodingTests: XCTestCase {
    func testDecodesLandingPageListCandidateAndDetailFixtures() throws {
        let decoder = makeVouchaDecoder()

        let pages = try decoder.decode(
            MyLandingPageListResponse.self,
            from: ApiFixtureLoader.data("native.landing-pages.default")
        )
        XCTAssertEqual(pages.results.map(\.id), ["landing-page-1", "landing-page-2"])
        XCTAssertEqual(pages.results.first?.subtitle, "Cards and reviews I recommend")
        XCTAssertTrue(pages.results.first?.isDefault == true)
        XCTAssertNil(pages.results.last?.subtitle)

        let candidates = try decoder.decode(
            LandingPageCandidatesResponse.self,
            from: ApiFixtureLoader.data("native.landing-page-candidates.default")
        )
        XCTAssertTrue(candidates.candidates.canCreateLandingPages)
        XCTAssertEqual(candidates.candidates.profileLinks.first?.id, "profile-link-1")
        XCTAssertEqual(candidates.candidates.reviews.first?.reviewTopicRatings.first?.topicName, "Travel Cards")
        XCTAssertEqual(candidates.candidates.referralLinks.first?.label, "Apply")

        let detail = try decoder.decode(
            LandingPageDetailResponse.self,
            from: ApiFixtureLoader.data("native.landing-page-detail.default")
        )
        XCTAssertEqual(detail.landingPage.id, "landing-page-1")
        XCTAssertEqual(detail.landingPage.items.count, 5)
        XCTAssertEqual(detail.landingPage.items.compactMap { item -> String? in
            if case .profileLink = item {
                return "profile_link"
            }
            if case .review = item {
                return "review"
            }
            if case .referralLink = item {
                return "referral_link"
            }
            if case .topicGroup = item {
                return "topic_group"
            }
            if case .link = item {
                return "link"
            }
            return nil
        }, ["profile_link", "review", "referral_link", "topic_group", "link"])

        guard case let .topicGroup(_, topic, entries) = detail.landingPage.items[3] else {
            return XCTFail("Expected topic group fixture item")
        }
        XCTAssertEqual(topic.topicType, "referral_program")
        XCTAssertEqual(entries.map(\.id), ["group-entry-review-1", "group-entry-referral-1"])

        let mutation = try decoder.decode(
            LandingPageSummaryResponse.self,
            from: ApiFixtureLoader.data("native.landing-page-mutation.default")
        )
        XCTAssertEqual(mutation.landingPage.subtitle, nil)

        let ownerAnalytics = try decoder.decode(
            LandingPageAnalyticsResponse.self,
            from: ApiFixtureLoader.data("native.landing-page-analytics.default")
        )
        XCTAssertEqual(ownerAnalytics.analytics.totalVisits, 12)
        XCTAssertEqual(ownerAnalytics.analytics.itemClicks.first?.clickCount, 4)
        XCTAssertEqual(ownerAnalytics.analytics.dailyStats.first?.uniqueVisitors, 9)
        XCTAssertEqual(ownerAnalytics.analytics.utmSources.first?.utmSource, "direct")
        XCTAssertEqual(ownerAnalytics.analytics.conversionFunnel.totalSignups, 2)

        let adminAnalytics = try decoder.decode(
            AdminLandingPageAnalyticsResponse.self,
            from: ApiFixtureLoader.data("native.admin-landing-page-analytics.default")
        )
        XCTAssertEqual(adminAnalytics.landingPage.id, "landing-page-1")
        XCTAssertEqual(adminAnalytics.analytics.conversionFunnel.totalSignups, 2)
    }

    func testRejectsUnsupportedLandingPageItemAndGroupEntryTypes() throws {
        let decoder = makeVouchaDecoder()

        XCTAssertThrowsError(
            try decoder.decode(
                LandingPageItem.self,
                from: Data(#"{"id":"item-1","type":"unsupported"}"#.utf8)
            )
        ) { error in
            guard case let DecodingError.dataCorrupted(context) = error else {
                return XCTFail("Expected dataCorrupted error")
            }
            XCTAssertEqual(context.codingPath.map(\.stringValue), ["type"])
            XCTAssertEqual(context.debugDescription, "Unsupported landing page item type: unsupported")
        }

        XCTAssertThrowsError(
            try decoder.decode(
                LandingPageTopicGroupEntry.self,
                from: Data(#"{"id":"entry-1","type":"unsupported"}"#.utf8)
            )
        ) { error in
            guard case let DecodingError.dataCorrupted(context) = error else {
                return XCTFail("Expected dataCorrupted error")
            }
            XCTAssertEqual(context.codingPath.map(\.stringValue), ["type"])
            XCTAssertEqual(
                context.debugDescription,
                "Unsupported landing page topic group entry type: unsupported"
            )
        }
    }
}
