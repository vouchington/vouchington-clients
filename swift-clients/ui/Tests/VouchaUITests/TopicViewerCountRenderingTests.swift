import Foundation
import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class TopicViewerCountRenderingTests: NativeRouteSurfaceViewModelTestCase {
    func testCanonicalBoundsRenderIndependentNavigableCategoryCounts() async throws {
        for (fixture, expected) in [
            ("web.topics.show.viewer-lower-bound", ["0+", "0", "0"]),
            ("web.topics.show.viewer-review-data-lower-bound", ["0+", "0+", "0+"])
        ] {
            let model = try await loadTopic(ApiFixtureLoader.data(fixture))
            let rows = model.rows.filter { $0.targetPath != nil }
            XCTAssertEqual(rows.map(\.title), ["Posts", "Reviews", "Data points"])
            XCTAssertEqual(rows.map(\.detail), expected)
            var navigated: [String] = []
            let surface = NativeRowsSurface(rows: rows, state: model.state, retry: nil) { navigated.append($0) }
            let buttons = try surface.inspect().findAll(ViewType.Button.self)
            XCTAssertEqual(buttons.count, 3)
            for (index, button) in buttons.enumerated() {
                XCTAssertNoThrow(try button.find(text: expected[index]))
                XCTAssertFalse(button.isDisabled())
                try button.tap()
            }
            XCTAssertEqual(navigated, [
                "/topic/test-topic/posts",
                "/topic/test-topic/reviews",
                "/topic/test-topic/data-points"
            ])
        }
    }

    func testPublicCountsAndEachIndependentBoundReachRenderedRows() async throws {
        var payload = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("web.topics.show.viewer-lower-bound")
        ) as? [String: Any])
        payload["topic_metrics"] = [
            "count": ["discussions": 2, "reviews": 5, "data-points": 0],
            "viewer_count": ["discussions": 3, "reviews": 1, "data-points": 0],
            "count_capped": ["data-points": true]
        ]
        let model = try await loadTopic(JSONSerialization.data(withJSONObject: payload))
        let rows = model.rows.filter { $0.targetPath != nil }
        XCTAssertEqual(rows.map(\.detail), ["7+", "5", "0+"])
        for row in rows {
            XCTAssertNoThrow(try NativeSurfaceRow(row: row).inspect().find(text: row.detail))
        }
    }

    func testAbsentAndNullMetricsRenderUncappedCategoryCounts() async throws {
        for metrics in ["", #", "topic_metrics":null"#] {
            let model = try await loadTopic(Data("""
            {"topic":{"id":"topic-1","name":"Test Topic"}\(metrics)}
            """.utf8))
            let rows = model.rows.filter { $0.targetPath != nil }
            XCTAssertEqual(rows.map(\.detail), ["0", "0", "0"])
            XCTAssertEqual(rows.first?.targetPath, "/topic/topic-1/posts")
        }
    }

    private func loadTopic(_ data: Data) async throws -> NativeRouteSurfaceViewModel {
        CannedFeedURLProtocol.handlers["/api/v1/topics/test-topic"] = (data, 200)
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/test-topic/posts")?.match)
        let model = try NativeRouteSurfaceViewModel(
            entry: entry(for: .topicDetail), client: makeClient(), routeMatch: match
        )
        await model.load()
        return model
    }
}
