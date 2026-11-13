import Foundation
import VouchaModels
import XCTest

final class TopicViewerMetricsTests: XCTestCase {
    private struct Response: Decodable { let topicMetrics: TopicViewerMetrics }

    func testCanonicalViewerBoundsRemainIndependent() throws {
        for (fixture, expected) in [
            ("web.topics.show.viewer-lower-bound", [true, false, false]),
            ("web.topics.show.viewer-review-data-lower-bound", [false, true, true])
        ] {
            let response = try makeVouchaDecoder().decode(Response.self, from: ApiFixtureLoader.data(fixture))
            let keys = ["discussions", "reviews", "data-points"]
            XCTAssertEqual(keys.map { response.topicMetrics.visibleCount($0) }, [0, 0, 0])
            XCTAssertEqual(keys.map { response.topicMetrics.isVisibleCountCapped($0) }, expected)
            XCTAssertEqual(response.topicMetrics.visiblePostsCount, 0)
            XCTAssertTrue(response.topicMetrics.isVisiblePostsCountCapped)
        }
    }

    func testPublicCountsUseIndependentPublicAndViewerBounds() throws {
        let metrics = try makeVouchaDecoder().decode(TopicViewerMetrics.self, from: Data(#"""
        {"count":{"discussions":999,"reviews":3},"count_capped":{"reviews":true},
        "viewer_count":{"discussions":2},"viewer_count_capped":{"discussions":true}}
        """#.utf8))
        XCTAssertEqual(metrics.visibleCount("discussions"), 999)
        XCTAssertEqual(metrics.visibleCount("reviews"), 3)
        XCTAssertEqual(metrics.visibleCount("data-points"), 0)
        XCTAssertTrue(metrics.isVisibleCountCapped("discussions"))
        XCTAssertTrue(metrics.isVisibleCountCapped("reviews"))
    }

    func testViewerExceedingPublicCountAddsLowerBoundOnlyToThatCategory() throws {
        let metrics = try makeVouchaDecoder().decode(TopicViewerMetrics.self, from: Data(#"""
        {"count":{"discussions":2,"reviews":5},"viewer_count":{"discussions":3,"reviews":1}}
        """#.utf8))
        XCTAssertEqual(metrics.visibleCount("discussions"), 2)
        XCTAssertTrue(metrics.isVisibleCountCapped("discussions"))
        XCTAssertEqual(metrics.visibleCount("reviews"), 5)
        XCTAssertFalse(metrics.isVisibleCountCapped("reviews"))
        XCTAssertFalse(metrics.isVisibleCountCapped("data-points"))
    }

    func testAggregatePostsUsesSumComparisonAndAllCategoryCaps() throws {
        for (viewer, capped) in [(4, false), (9, true)] {
            let metrics = try makeVouchaDecoder().decode(TopicViewerMetrics.self, from: Data("""
            {"count":{"discussions":2,"reviews":5,"data-points":1},
            "viewer_count":{"discussions":\(viewer),"reviews":0,"data-points":0}}
            """.utf8))
            XCTAssertEqual(metrics.visiblePostsCount, 8)
            XCTAssertEqual(metrics.isVisiblePostsCountCapped, capped)
        }
    }

    func testMissingAndNullCountsAreUncappedZero() throws {
        for json in ["{}", #"{"count":null,"viewer_count":null,"viewer_count_capped":null}"#] {
            let metrics = try makeVouchaDecoder().decode(TopicViewerMetrics.self, from: Data(json.utf8))
            XCTAssertEqual(metrics.visibleCount("discussions"), 0)
            XCTAssertEqual(metrics.visiblePostsCount, 0)
            XCTAssertFalse(metrics.isVisiblePostsCountCapped)
            XCTAssertFalse(metrics.isVisibleCountCapped("discussions"))
        }
    }
}
