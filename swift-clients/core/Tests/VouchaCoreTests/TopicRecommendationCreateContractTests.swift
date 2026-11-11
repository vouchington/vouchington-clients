@testable import VouchaAPI
import VouchaModels
import XCTest

private let createFixtureId = "native.posts.topic-recommendation.create"

private struct TopicRecommendationCreateBody: Encodable {
    let postType = "topic_recommendation"
    let title = "Native recommendation"
    let markdown = "Recommendation body"
    let topicTitle = "Native Topic"
    let topicSlug = "native-topic"
    let topicMarkdown = "Recommendation body"
    let topicAliases = ["native topic"]
}

let topicRecommendationCreateFixtureEndpoints: [String: Endpoint] = [
    createFixtureId: Endpoint.createTopicRecommendation(
        body: TopicRecommendationCreateBody(),
        idempotencyKey: "70000000-0000-7000-8000-000000005598"
    )
]

let topicRecommendationCreateFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: createFixtureId) {
        try assertFixtureCoversDTO($0, as: TopicRecommendationPostEnvelope.self)
    }
]

final class TopicRecommendationCreateContractTests: XCTestCase {
    func testCreateResponseDecodesThePostShape() throws {
        let response = try makeVouchaDecoder().decode(
            TopicRecommendationPostEnvelope.self,
            from: ApiFixtureLoader.data(createFixtureId)
        )

        XCTAssertEqual(response.post.id, "70000000-0000-7000-8000-000000005597")
        XCTAssertEqual(response.post.postType, .topicRecommendation)
        XCTAssertNil(response.postElection)
        XCTAssertNil(response.electionVote)
        let recommendation = try XCTUnwrap(response.post.topicRecommendation)
        XCTAssertEqual(recommendation.postId, response.post.id)
        XCTAssertEqual(recommendation.status, "pending")
        XCTAssertEqual(recommendation.topicSlug, "native-topic")
        XCTAssertEqual(recommendation.aliases, ["native topic"])
        XCTAssertNil(recommendation.hostname)
        XCTAssertEqual(recommendation.hostnames?.count, 0)
    }

    func testCreateEndpointSendsTheIdempotencyKey() throws {
        let endpoint = try XCTUnwrap(topicRecommendationCreateFixtureEndpoints[createFixtureId])
        XCTAssertEqual(endpoint.headers["Idempotency-Key"], "70000000-0000-7000-8000-000000005598")
    }
}
