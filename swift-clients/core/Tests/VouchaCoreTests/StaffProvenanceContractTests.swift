import Foundation
import VouchaModels
import XCTest

final class StaffProvenanceContractTests: XCTestCase {
    func testStaffFixturesDecodeRawChannelAndVerifiedClientAcrossEntityShapes() throws {
        let decoder = makeVouchaDecoder()
        let communities = try decoder.decode(
            CommunitiesSearchResponse.self,
            from: fixture("entity-provenance.communities.staff")
        )
        let topics = try decoder.decode(
            TopicSearchResponse.self,
            from: fixture("entity-provenance.topics.staff")
        )
        let lists = try decoder.decode(
            ListsSearchResponse.self,
            from: fixture("entity-provenance.lists.staff")
        )
        let feeds = try decoder.decode(
            RssFeedSourceListResponse.self,
            from: fixture("entity-provenance.rss-feeds.staff")
        )

        XCTAssertEqual(communities.communities["community-1-web"]?.staffProvenance?.createdVia, "web")
        XCTAssertEqual(topics.topics["topic-1-api"]?.staffProvenance?.createdVia, "api")
        XCTAssertEqual(lists.lists["list-1-api"]?.staffProvenance?.createdVia, "api")
        let client = communities.communities["community-1-mcp"]?.staffProvenance?.oauthClient
        XCTAssertEqual(client?.clientId, "voucha_fixture_agent")
        XCTAssertEqual(client?.clientName, "Fixture Agent")
        XCTAssertEqual(client?.verified, true)
        XCTAssertNil(client?.metadataUrl)
        XCTAssertTrue(feeds.results.contains { $0.staffProvenance?.oauthClient?.verified == true })
    }

    func testPublicFixtureHasNoStaffFacts() throws {
        let response = try makeVouchaDecoder().decode(
            CommunitiesSearchResponse.self,
            from: ApiFixtureLoader.data("entity-provenance.communities")
        )
        XCTAssertTrue(response.communities.values.allSatisfy { $0.staffProvenance == nil })
    }

    private func fixture(_ name: String) throws -> Data {
        try Data(contentsOf: ApiFixtureLoader.fixtureURL("responses/\(name).json"))
    }
}
