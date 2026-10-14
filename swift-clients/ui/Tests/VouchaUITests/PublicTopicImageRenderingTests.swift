import Foundation
import ViewInspector
import VouchaAPI
import VouchaCore
import VouchaDesignSystem
@testable import VouchaFeatures
import XCTest

@MainActor
final class PublicTopicImageRenderingTests: NativeRouteSurfaceViewModelTestCase {
    func testPublicTopicListAndDetailRenderClaimedPlacementFixtures() async throws {
        let config = try AppConfig(
            baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
            imageBaseURL: XCTUnwrap(URL(string: "https://topic-cdn.self-hosted.example"))
        )
        let client = APIClient(
            config: config,
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        CannedFeedURLProtocol.handlers["/api/v1/topics"] = (
            ApiFixtureLoader.data("web.topics.search.default"), 200
        )
        let listViewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .topicsBrowse), client: client
        )

        await listViewModel.load()

        let listRow = try XCTUnwrap(listViewModel.rows.first)
        XCTAssertEqual(listRow.title, "Test Topic")
        try assertTopicImageIsRendered(in: listRow)

        CannedFeedURLProtocol.handlers["/api/v1/topics/test-topic"] = (
            ApiFixtureLoader.data("web.topics.show.default"), 200
        )
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/test-topic/posts")?.match)
        let detailViewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .topicDetail), client: client, routeMatch: match
        )

        await detailViewModel.load()

        let detailRow = try XCTUnwrap(detailViewModel.rows.first)
        XCTAssertEqual(detailRow.title, "Test Topic")
        try assertTopicImageIsRendered(in: detailRow)
    }

    func testLegacyAndInvalidTopicLogoDataKeepTheAccessibleTitleFallback() async throws {
        let responses: [(String, String, String)] = [
            (
                #"{"id":"topic-legacy","name":"Legacy Topic","slug":"legacy-topic","logo_image_id":"image-without-placement","logo_image_placement":null}"#,
                "topic-legacy",
                "Legacy Topic"
            ),
            (
                #"{"id":"topic-invalid","name":"Invalid Topic","slug":"invalid-topic","logo_image_id":"legacy-image","logo_image_placement":{"image_id":"","placement_id":"","placement_revision":1}}"#,
                "topic-invalid",
                "Invalid Topic"
            )
        ]

        for (response, topicId, topicName) in responses {
            CannedFeedURLProtocol.handlers["/api/v1/topics"] = (
                Data("""
                {
                  "results": [{"id":"\(topicId)","name":"\(topicName)","slug":"\(topicName.lowercased()
                    .replacingOccurrences(
                        of: " ",
                        with: "-"
                    ))"}],
                  "page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null},
                  "topics":{"\(topicId)":\(response)},
                  "topic_elections":{},
                  "election_votes":{},
                  "topics_metrics":{}
                }
                """.utf8),
                200
            )
            let viewModel = try NativeRouteSurfaceViewModel(
                entry: entry(for: .topicsBrowse), client: makeClient()
            )

            await viewModel.load()

            let row = try XCTUnwrap(viewModel.rows.first)
            XCTAssertEqual(row.title, topicName)
            XCTAssertNil(row.imageURL)
            let rendered = try NativeSurfaceRow(row: row).inspect()
            XCTAssertTrue(try rendered.findAll(ViewType.View<AsyncImageView>.self).isEmpty)
            XCTAssertNoThrow(try rendered.find(text: row.title))
        }
    }

    private func assertTopicImageIsRendered(
        in row: NativeRouteDestinationRow,
        file: StaticString = #filePath,
        line: UInt = #line
    ) throws {
        XCTAssertEqual(
            row.imageURL,
            "https://topic-cdn.self-hosted.example/images/placements/00000000-0000-7000-8000-000000000306/3/00000000-0000-7000-8000-000000000205?w=96",
            file: file,
            line: line
        )
        let image = try NativeSurfaceRow(row: row)
            .inspect()
            .find(ViewType.View<AsyncImageView>.self)
        XCTAssertEqual(
            try image.actualView().resolvedURLString,
            row.imageURL,
            file: file,
            line: line
        )
    }
}
