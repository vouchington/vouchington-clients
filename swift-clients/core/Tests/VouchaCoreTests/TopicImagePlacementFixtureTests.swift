@testable import VouchaModels
import XCTest

final class TopicImagePlacementFixtureTests: XCTestCase {
    func testPublicTopicSearchAndDetailFixturesDecodePlacementIdentity() throws {
        let search = try makeVouchaDecoder().decode(
            TopicSearchResponse.self,
            from: ApiFixtureLoader.data("web.topics.search.default")
        )
        let searchedTopic = try XCTUnwrap(search.topics["topic-1"])
        XCTAssertEqual(searchedTopic.logoImagePlacement?.placementId, "00000000-0000-7000-8000-000000000306")
        XCTAssertEqual(searchedTopic.logoImagePlacement?.placementRevision, 3)
        XCTAssertEqual(searchedTopic.logoImagePlacement?.imageId, "00000000-0000-7000-8000-000000000205")

        let detail = try makeVouchaDecoder().decode(
            TopicEnvelope.self,
            from: ApiFixtureLoader.data("web.topics.show.default")
        )
        XCTAssertEqual(detail.topic.logoImagePlacement, searchedTopic.logoImagePlacement)
    }
}
